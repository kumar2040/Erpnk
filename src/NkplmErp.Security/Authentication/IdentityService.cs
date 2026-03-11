using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NkplmErp.Application.Interfaces;
using NkplmErp.Domain.Entities;
using NkplmErp.Infrastructure.Persistence;
using NkplmErp.Security.DeviceFingerprint;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Security.Authentication;

public class IdentityService(
    UserManager<User> userManager,
    IJwtTokenService jwtTokenService,
    IAuditService auditService,
    IMfaService mfaService,
    IDeviceService deviceService,
    ICurrentUserService currentUserService,
    SecurityDbContext context) : IIdentityService
{
    private readonly UserManager<User> _userManager = userManager;
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;
    private readonly IAuditService _auditService = auditService;
    private readonly IMfaService _mfaService = mfaService;
    private readonly IDeviceService _deviceService = deviceService;
    private readonly ICurrentUserService _currentUserService = currentUserService;
    private readonly SecurityDbContext _context = context;

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        try
        {
            Console.WriteLine($"DEBUG: Security - LoginAsync attempt for email: {request.Email}");
            var user = await _userManager.FindByEmailAsync(request.Email);
            if (user == null)
            {
                Console.WriteLine($"DEBUG: Security - User not found: {request.Email}");
                await _auditService.LogAsync("system", "LoginFailed", "User", request.Email, "", "Invalid credentials");
                return new AuthResponse { IsSuccess = false, Message = "Invalid credentials." };
            }

            if (!await _userManager.CheckPasswordAsync(user, request.Password))
            {
                Console.WriteLine($"DEBUG: Security - Invalid password for user: {user.Email}");
                await _auditService.LogAsync("system", "LoginFailed", "User", request.Email, "", "Invalid credentials");
                return new AuthResponse { IsSuccess = false, Message = "Invalid credentials." };
            }

            var fingerprint = _deviceService.GetDeviceFingerprint();
            await _auditService.LogAsync(user.Id, "LoginAttempt", "User", user.Id, "", $"Device: {fingerprint}");

            if (user.MfaEnabled)
            {
                return new AuthResponse
                {
                    IsSuccess = true,
                    RequiresMfa = true,
                    Message = "MFA Required."
                };
            }

            var roles = await _userManager.GetRolesAsync(user);
            var token = await _jwtTokenService.GenerateTokenAsync(user, roles);
            var refreshToken = _jwtTokenService.GenerateRefreshToken();

            await SaveRefreshToken(user.Id, refreshToken);

            await _auditService.LogAsync(user.Id, "LoginSuccess", "User", user.Id, "", "JWT & Refresh Token Generated");

            return new AuthResponse
            {
                IsSuccess = true,
                Token = token,
                RefreshToken = refreshToken,
                Message = "Login successful."
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Security - Exception in LoginAsync: {ex.Message}");
            return new AuthResponse { IsSuccess = false, Message = $"Login error: {ex.Message}" };
        }
    }

    public async Task<AuthResponse> VerifyMfaAsync(string email, string code)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || user.MfaSecret == null)
        {
            return new AuthResponse { IsSuccess = false, Message = "User not found or MFA not enabled." };
        }

        if (!_mfaService.VerifyCode(user.MfaSecret, code))
        {
            await _auditService.LogAsync(user.Id, "MfaFailed", "User", user.Id, "", "Invalid TOTP code");
            return new AuthResponse { IsSuccess = false, Message = "Invalid MFA code." };
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = await _jwtTokenService.GenerateTokenAsync(user, roles);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        await SaveRefreshToken(user.Id, refreshToken);

        await _auditService.LogAsync(user.Id, "MfaSuccess", "User", user.Id, "", "Authenticated via MFA");

        return new AuthResponse
        {
            IsSuccess = true,
            Token = token,
            RefreshToken = refreshToken,
            Message = "Login successful."
        };
    }

    public async Task<AuthResponse> RefreshTokenAsync(TokenRefreshRequest request)
    {
        var principal = _jwtTokenService.GetClaimsPrincipalFromExpiredToken(request.Token);
        var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub");

        if (string.IsNullOrEmpty(userId))
        {
            return new AuthResponse { IsSuccess = false, Message = "Invalid token principal." };
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new AuthResponse { IsSuccess = false, Message = "User not found." };
        }

        var storedToken = await _context.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken && t.UserId == userId);

        if (storedToken == null || !storedToken.IsActive)
        {
            await _auditService.LogAsync(userId, "TokenRefreshFailed", "RefreshToken", request.RefreshToken, "", "Invalid or inactive refresh token");
            return new AuthResponse { IsSuccess = false, Message = "Invalid refresh token." };
        }

        // Revoke old token
        storedToken.Revoked = DateTime.UtcNow;
        storedToken.RevokedByIp = "system"; // In real scenario, get from request

        // Generate new tokens
        var roles = await _userManager.GetRolesAsync(user);
        var newToken = await _jwtTokenService.GenerateTokenAsync(user, roles);
        var newRefreshToken = _jwtTokenService.GenerateRefreshToken();

        storedToken.ReplacedByToken = newRefreshToken;

        await SaveRefreshToken(user.Id, newRefreshToken);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(userId, "TokenRefreshed", "RefreshToken", newRefreshToken, request.RefreshToken, "Token rotated");

        return new AuthResponse
        {
            IsSuccess = true,
            Token = newToken,
            RefreshToken = newRefreshToken,
            Message = "Token refreshed successfully."
        };
    }

    private async Task SaveRefreshToken(string userId, string token)
    {
        var refreshToken = new RefreshToken
        {
            Token = token,
            UserId = userId,
            Expires = DateTime.UtcNow.AddDays(7),
            CreatedByIp = "system" // In real scenario, get from request
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();
    }

    public async Task<MfaSetupResponse> GetMfaSetupAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null) return new MfaSetupResponse { IsMfaEnabled = false };

        if (user.MfaEnabled)
            return new MfaSetupResponse { IsMfaEnabled = true };

        var secret = _mfaService.GenerateSecret();
        user.MfaSecret = secret;
        await _userManager.UpdateAsync(user);

        return new MfaSetupResponse
        {
            IsMfaEnabled = false,
            SharedKey = secret,
            AuthenticatorUri = _mfaService.GetQrCodeUri(email, secret)
        };
    }

    public async Task<AuthResponse> ConfirmMfaRegistrationAsync(string email, string code)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null || string.IsNullOrEmpty(user.MfaSecret))
        {
            return new AuthResponse { IsSuccess = false, Message = "MFA setup not initiated." };
        }

        if (!_mfaService.VerifyCode(user.MfaSecret, code))
        {
            return new AuthResponse { IsSuccess = false, Message = "Invalid verification code." };
        }

        user.MfaEnabled = true;
        await _userManager.UpdateAsync(user);

        await _auditService.LogAsync(user.Id, "MfaEnabled", "User", user.Id, "", "MFA Registration Confirmed");

        return new AuthResponse { IsSuccess = true, Message = "MFA enabled successfully." };
    }

    public async Task<string> EnableMfaAsync(string email)
    {
        // Legacy method, keeping for compatibility if needed, but redirects to setup
        var setup = await GetMfaSetupAsync(email);
        return setup.AuthenticatorUri ?? "";
    }

    public async Task<AuthResponse> RegisterAsync(string email, string password, string firstName, string lastName)
    {
        var user = new User
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            return new AuthResponse
            {
                IsSuccess = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            };
        }

        await _auditService.LogAsync("system", "UserRegistered", "User", user.Id, "", "Manual registration");

        return new AuthResponse { IsSuccess = true, Message = "User registered successfully." };
    }

    public async Task<UserInfoDto?> GetUserByEmailAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user == null) return null;

        var roles = await _userManager.GetRolesAsync(user);

        return new UserInfoDto
        {
            Id = user.Id,
            Email = user.Email!,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Roles = roles.ToList()
        };
    }
}
