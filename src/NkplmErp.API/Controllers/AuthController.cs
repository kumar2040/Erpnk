using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using Asp.Versioning;
using Fido2NetLib;
using Fido2NetLib.Objects;

namespace NkplmErp.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IIdentityService _identityService;
    private readonly IWebAuthnService _webAuthnService;

    public AuthController(IIdentityService identityService, IWebAuthnService webAuthnService)
    {
        _identityService = identityService;
        _webAuthnService = webAuthnService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var result = await _identityService.LoginAsync(request);
        if (result.IsSuccess && !result.RequiresMfa) SetAuthCookies(result);
        return Ok(result);
    }

    [HttpPost("mfa-verify")]
    public async Task<IActionResult> VerifyMfa([FromBody] MfaVerifyRequest request)
    {
        var result = await _identityService.VerifyMfaAsync(request.Email, request.Code);
        if (result.IsSuccess) SetAuthCookies(result);
        return Ok(result);
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] TokenRefreshRequest request)
    {
        var result = await _identityService.RefreshTokenAsync(request);
        if (result.IsSuccess) SetAuthCookies(result);
        return Ok(result);
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpGet("mfa-setup")]
    public async Task<IActionResult> GetMfaSetup()
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized("User email not found in claims");

        var result = await _identityService.GetMfaSetupAsync(email);
        return Ok(result);
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpPost("mfa-confirm")]
    public async Task<IActionResult> ConfirmMfa([FromBody] string code)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized("User email not found in claims");

        var result = await _identityService.ConfirmMfaRegistrationAsync(email, code);
        if (!result.IsSuccess) return BadRequest(result);

        return Ok(result);
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var result = await _identityService.RegisterAsync(request.Email, request.Password, request.FirstName, request.LastName);
        if (!result.IsSuccess) return BadRequest(result);

        return Ok(result);
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpGet("biometric-registration-options")]
    public async Task<IActionResult> GetBiometricRegistrationOptions([FromQuery] string deviceName)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized("User email not found in claims");

        var options = await _webAuthnService.GetRegistrationOptionsAsync(email, deviceName);
        return Ok(options);
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpPost("biometric-register")]
    public async Task<IActionResult> VerifyBiometricRegistration([FromBody] BiometricVerifyRegistrationRequest request)
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized("User email not found in claims");

        var result = await _webAuthnService.VerifyRegistrationAsync(email, request.DeviceName, request.AttestationResponse);
        if (!result.IsSuccess) return BadRequest(result);

        return Ok(result);
    }

    [HttpGet("biometric-login-options")]
    public async Task<IActionResult> GetBiometricLoginOptions([FromQuery] string email)
    {
        if (string.IsNullOrEmpty(email)) return BadRequest("Email is required");

        var options = await _webAuthnService.GetLoginOptionsAsync(email);
        return Ok(options);
    }

    [HttpPost("biometric-login")]
    public async Task<IActionResult> VerifyBiometricLogin([FromBody] BiometricVerifyLoginRequest request)
    {
        var result = await _webAuthnService.VerifyLoginAsync(request.Email, request.AssertionResponse);
        if (result.IsSuccess) SetAuthCookies(result);
        return Ok(result);
    }

    [Microsoft.AspNetCore.Authorization.Authorize]
    [HttpGet("userinfo")]
    public async Task<IActionResult> GetUserInfo()
    {
        var email = GetUserEmail();
        if (string.IsNullOrEmpty(email)) return Unauthorized();

        var user = await _identityService.GetUserByEmailAsync(email);
        if (user == null) return NotFound();

        return Ok(user);
    }

    [HttpPost("logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("X-Auth-Token");
        Response.Cookies.Delete("X-Refresh-Token");
        return Ok(new { Message = "Logged out" });
    }

    private void SetAuthCookies(AuthResponse result)
    {
        if (!result.IsSuccess || result.RequiresMfa) return;

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // Changed to false for local development
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddHours(1)
        };

        if (!string.IsNullOrEmpty(result.Token))
            Response.Cookies.Append("X-Auth-Token", result.Token, cookieOptions);

        if (!string.IsNullOrEmpty(result.RefreshToken))
        {
            var refreshCookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = false, // Changed to false for local development
                SameSite = SameSiteMode.Lax,
                Expires = DateTime.UtcNow.AddDays(7)
            };
            Response.Cookies.Append("X-Refresh-Token", result.RefreshToken, refreshCookieOptions);
        }
    }

    private string? GetUserEmail()
    {
        return User.Identity?.Name ?? 
               User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? 
               User.FindFirst("email")?.Value;
    }
}
