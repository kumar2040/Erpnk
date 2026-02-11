using Fido2NetLib;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Application.Interfaces;

public interface IWebAuthnService
{
    Task<CredentialCreateOptions> GetRegistrationOptionsAsync(string email, string deviceName);
    Task<AuthResponse> VerifyRegistrationAsync(string email, string deviceName, AuthenticatorAttestationRawResponse attestationResponse);
    Task<AssertionOptions> GetLoginOptionsAsync(string email);
    Task<AuthResponse> VerifyLoginAsync(string email, AuthenticatorAssertionRawResponse assertionResponse);
}
