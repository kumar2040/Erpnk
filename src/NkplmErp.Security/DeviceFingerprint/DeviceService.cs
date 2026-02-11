using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;

namespace NkplmErp.Security.DeviceFingerprint;

public interface IDeviceService
{
    string GetDeviceFingerprint();
}

public class DeviceService(IHttpContextAccessor httpContextAccessor) : IDeviceService
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public string GetDeviceFingerprint()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context is null) return "unknown";

        var userAgent = context.Request.Headers.UserAgent.ToString();
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var acceptLanguage = context.Request.Headers.AcceptLanguage.ToString();

        var rawData = $"{userAgent}|{ipAddress}|{acceptLanguage}";
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawData));
        return Convert.ToBase64String(bytes);
    }
}
