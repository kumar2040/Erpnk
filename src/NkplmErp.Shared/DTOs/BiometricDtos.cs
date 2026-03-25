using Fido2NetLib;
using Fido2NetLib.Objects;

namespace NkplmErp.Shared.DTOs;

public class BiometricRegistrationRequest
{
    public string DeviceName { get; set; } = string.Empty;
}

public class BiometricLoginRequest
{
    public string Email { get; set; } = string.Empty;
}

public class BiometricVerifyRegistrationRequest
{
    public string DeviceName { get; set; } = string.Empty;
    public AuthenticatorAttestationRawResponse AttestationResponse { get; set; } = null!;
}

public class BiometricVerifyLoginRequest
{
    public string? Email { get; set; } = string.Empty;
    public string SessionId { get; set; } = string.Empty;
    public AuthenticatorAssertionRawResponse AssertionResponse { get; set; } = null!;
}
