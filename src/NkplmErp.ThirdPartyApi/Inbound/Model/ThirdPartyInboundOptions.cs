using Microsoft.Extensions.Configuration;

namespace NkplmErp.ThirdPartyApi.Inbound.Model;

/// <summary>
/// Settings for the inbound gateway (PLM / PHP calling NkplmErp), section "ThirdPartyInbound".
/// Mirrors PLM's ThirdPartyGateway: the caller sends Username + PasswordHash (the hash string itself)
/// in the JSON body. Keep the real values in user-secrets / environment variables, not appsettings.json.
/// </summary>
public class ThirdPartyInboundOptions
{
    public const string Section = "ThirdPartyInbound";

    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Base URLs allowed to call, compared against the caller's Origin, X-Client-Base-Url or Referer header.
    /// "http://plm.local" = exact scheme+host+port; a bare host or IP ("172.25.10.89") = that host on any
    /// scheme/port. Empty list = nobody gets in.
    /// </summary>
    public List<string> AllowedBaseUrls { get; set; } = new();

    /// <summary>Max requests per client IP per window, checked first. 0 = unlimited.</summary>
    public int RateLimit { get; set; } = 15;
    public int RateWindowSeconds { get; set; } = 60;

    /// <summary>Every failure takes exactly this long, so the caller can't tell which check failed.</summary>
    public int FailureSeconds { get; set; } = 3;

    public static ThirdPartyInboundOptions From(IConfiguration config) =>
        config.GetSection(Section).Get<ThirdPartyInboundOptions>() ?? new ThirdPartyInboundOptions();
}
