using Fido2NetLib;
using Fido2NetLib.Objects;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using NkplmErp.Application.Interfaces;
using NkplmErp.Domain.Entities;
using NkplmErp.Infrastructure.Persistence;
using NkplmErp.Shared.DTOs;
using System.Text;
using User = NkplmErp.Domain.Entities.User;

namespace NkplmErp.Security.Authentication;

public class WebAuthnService(
    IFido2 fido2,
    SecurityDbContext context,
    UserManager<User> userManager,
    IMemoryCache cache,
    IJwtTokenService jwtTokenService,
    IAuditService auditService) : IWebAuthnService
{
    private readonly IFido2 _fido2 = fido2;
    private readonly SecurityDbContext _context = context;
    private readonly UserManager<User> _userManager = userManager;
    private readonly IMemoryCache _cache = cache;
    private readonly IJwtTokenService _jwtTokenService = jwtTokenService;
    private readonly IAuditService _auditService = auditService;

    public async Task<CredentialCreateOptions> GetRegistrationOptionsAsync(string email, string deviceName)
    {
        var user = await _userManager.FindByEmailAsync(email) ?? throw new Exception("User not found");

        var fidoUser = new Fido2User
        {
            DisplayName = user.UserName,
            Name = user.Email,
            Id = Encoding.UTF8.GetBytes(user.Id)
        };

        var existingCredentials = await _context.BiometricCredentials
            .Where(c => c.UserId == user.Id)
            .Select(c => new PublicKeyCredentialDescriptor(c.DescriptorId))
            .ToListAsync();

        var options = _fido2.RequestNewCredential(new RequestNewCredentialParams
        {
            User = fidoUser,
            ExcludeCredentials = existingCredentials,
            AuthenticatorSelection = new AuthenticatorSelection
            {
                UserVerification = UserVerificationRequirement.Required,
                ResidentKey = ResidentKeyRequirement.Discouraged
            },
            AttestationPreference = AttestationConveyancePreference.None
        });

        _cache.Set($"webauthn_reg_{user.Id}", options, TimeSpan.FromMinutes(5));

        return options;
    }

    public async Task<AuthResponse> VerifyRegistrationAsync(string email, string deviceName, AuthenticatorAttestationRawResponse attestationResponse)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null) return new AuthResponse { IsSuccess = false, Message = "User not found" };

        if (!_cache.TryGetValue($"webauthn_reg_{user.Id}", out CredentialCreateOptions? options) || options is null)
        {
            return new AuthResponse { IsSuccess = false, Message = "Registration session expired" };
        }

        try
        {
            var success = await _fido2.MakeNewCredentialAsync(new MakeNewCredentialParams
            {
                AttestationResponse = attestationResponse,
                OriginalOptions = options,
                IsCredentialIdUniqueToUserCallback = async (args, cancellationToken) =>
                {
                    var exists = await _context.BiometricCredentials.AnyAsync(c => c.DescriptorId == args.CredentialId, cancellationToken);
                    return !exists;
                }
            });

            var credential = new BiometricCredential
            {
                UserId = user.Id,
                DescriptorId = success.Id,
                PublicKey = success.PublicKey,
                UserHandle = success.User.Id,
                SignatureCounter = success.SignCount,
                CredType = success.Type.ToString(),
                RegDate = DateTime.UtcNow,
                DeviceFriendlyName = deviceName
            };

            _context.BiometricCredentials.Add(credential);
            await _context.SaveChangesAsync();

            await _auditService.LogAsync(user.Id, "BiometricEnrolled", "Device", credential.Id.ToString(), "", $"Device: {deviceName}");

            return new AuthResponse { IsSuccess = true, Message = "Biometric device enrolled successfully" };
        }
        catch (Fido2VerificationException ex)
        {
            return new AuthResponse { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<AssertionOptions> GetLoginOptionsAsync(string email)
    {
        var user = await _userManager.FindByEmailAsync(email) ?? throw new Exception("User not found");

        var existingCredentials = await _context.BiometricCredentials
            .Where(c => c.UserId == user.Id)
            .Select(c => new PublicKeyCredentialDescriptor(c.DescriptorId))
            .ToListAsync();

        var options = _fido2.GetAssertionOptions(new GetAssertionOptionsParams
        {
            AllowedCredentials = existingCredentials,
            UserVerification = UserVerificationRequirement.Required
        });

        _cache.Set($"webauthn_login_{user.Id}", options, TimeSpan.FromMinutes(5));

        return options;
    }

    public async Task<AuthResponse> VerifyLoginAsync(string email, AuthenticatorAssertionRawResponse assertionResponse)
    {
        var user = await _userManager.FindByEmailAsync(email);
        if (user is null) return new AuthResponse { IsSuccess = false, Message = "User not found" };

        if (!_cache.TryGetValue($"webauthn_login_{user.Id}", out AssertionOptions? options) || options is null)
        {
            return new AuthResponse { IsSuccess = false, Message = "Login session expired" };
        }

        var cred = await _context.BiometricCredentials.FirstOrDefaultAsync(c => c.UserId == user.Id);
        if (cred is null) return new AuthResponse { IsSuccess = false, Message = "No biometric device found" };

        VerifyAssertionResult success;
        try
        {
            success = await _fido2.MakeAssertionAsync(new MakeAssertionParams
            {
                AssertionResponse = assertionResponse,
                OriginalOptions = options,
                StoredPublicKey = cred.PublicKey,
                StoredSignatureCounter = cred.SignatureCounter,
                IsUserHandleOwnerOfCredentialIdCallback = async (args, cancellationToken) =>
                {
                    var c = await _context.BiometricCredentials.FirstOrDefaultAsync(x => x.DescriptorId == args.CredentialId, cancellationToken);
                    return c != null && c.UserHandle.SequenceEqual(args.UserHandle);
                }
            });

            cred.SignatureCounter = success.SignCount;
            await _context.SaveChangesAsync();
        }
        catch (Fido2VerificationException ex)
        {
            await _auditService.LogAsync(user.Id, "BiometricLoginFailed", "Device", "", "", ex.Message);
            return new AuthResponse { IsSuccess = false, Message = ex.Message };
        }

        var roles = await _userManager.GetRolesAsync(user);
        var token = await _jwtTokenService.GenerateTokenAsync(user, roles);
        var refreshToken = _jwtTokenService.GenerateRefreshToken();

        // Save refresh token logic (reusing if possible or adding here)
        var rt = new RefreshToken
        {
            Token = refreshToken,
            UserId = user.Id,
            Expires = DateTime.UtcNow.AddDays(7),
            CreatedByIp = "biometric"
        };
        _context.RefreshTokens.Add(rt);
        await _context.SaveChangesAsync();

        await _auditService.LogAsync(user.Id, "BiometricLoginSuccess", "Device", user.Id, "", "Authenticated via Biometrics");

        return new AuthResponse
        {
            IsSuccess = true,
            Token = token,
            RefreshToken = refreshToken,
            Message = "Login successful"
        };
    }
}
