namespace NkplmErp.Shared.DTOs;

public class AuthResponse
{
    public bool IsSuccess { get; set; }
    public bool RequiresMfa { get; set; }
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class LoginRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class TokenRefreshRequest
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public class BiometricDeviceDto
{
    public Guid Id { get; set; }
    public string DeviceFriendlyName { get; set; } = string.Empty;
    public DateTime RegDate { get; set; }
}

public class MfaSetupResponse
{
    public bool IsMfaEnabled { get; set; }
    public string? SharedKey { get; set; }
    public string? AuthenticatorUri { get; set; }
    public List<BiometricDeviceDto> BiometricDevices { get; set; } = new();
}

public class MfaVerifyRequest
{
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}

public class UserInfoDto
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public List<string> Roles { get; set; } = new();
}

public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
