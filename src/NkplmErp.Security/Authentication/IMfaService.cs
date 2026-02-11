namespace NkplmErp.Security.Authentication;

public interface IMfaService
{
    string GenerateSecret();
    string GetQrCodeUri(string email, string secret);
    bool VerifyCode(string secret, string code);
}
