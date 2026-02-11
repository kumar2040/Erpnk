using OtpNet;

namespace NkplmErp.Security.Authentication;

public class MfaService : IMfaService
{
    private const string Issuer = "NkplmErp";

    public string GenerateSecret()
    {
        var key = KeyGeneration.GenerateRandomKey(20);
        return Base32Encoding.ToString(key);
    }

    public string GetQrCodeUri(string email, string secret)
    {
        return $"otpauth://totp/{Issuer}:{email}?secret={secret}&issuer={Issuer}";
    }

    public bool VerifyCode(string secret, string code)
    {
        try
        {
            var key = Base32Encoding.ToBytes(secret);
            var totp = new Totp(key);
            return totp.VerifyTotp(code, out _, new VerificationWindow(1, 1));
        }
        catch
        {
            return false;
        }
    }
}
