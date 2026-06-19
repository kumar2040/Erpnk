using NkplmErp.Shared.DTOs;

namespace NkplmErp.Application.Interfaces;

public interface IIdentityService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<AuthResponse> RefreshTokenAsync(TokenRefreshRequest request);
    Task<MfaSetupResponse> GetMfaSetupAsync(string email);
    Task<AuthResponse> ConfirmMfaRegistrationAsync(string email, string code);
    Task<AuthResponse> VerifyMfaAsync(string email, string code);
    Task<string> EnableMfaAsync(string email);
    Task<AuthResponse> DisableMfaAsync(string email);
    Task<AuthResponse> RegisterAsync(string email, string password, string firstName, string lastName);
    Task<UserInfoDto?> GetUserByEmailAsync(string email);
    Task<AuthResponse> RemoveBiometricAsync(string email, Guid deviceId);
    Task<AuthResponse> ChangePasswordAsync(string email, ChangePasswordRequest request);

    /// <summary>
    /// Immediately ends all of a user's active sessions: rotates their security
    /// stamp (invalidating every live access token at the next request) and revokes
    /// their refresh tokens. Used by admins to force-logout a user.
    /// </summary>
    Task<AuthResponse> ForceLogoutAsync(string userId, string performedByUserId);
}
