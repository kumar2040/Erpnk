using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NkplmErp.ThirdPartyApi.Inbound.Base;
using NkplmErp.ThirdPartyApi.Inbound.Model;

namespace NkplmErp.ThirdPartyApi.Inbound.Filter;

/// <summary>
/// Guards every inbound route PLM (PHP) calls on NkplmErp. Same checks as PLM's ApiAuthFilter, plus an
/// allowed-base-URL check, in this order:
///   1) rate limit per client IP
///   2) caller base URL (Origin, X-Client-Base-Url or Referer) must be in ThirdPartyInbound:AllowedBaseUrls
///   3) body must be a JSON object
///   4) username / password (the configured hash string) compared in constant time
/// Any failure answers the generic 400 after the fixed delay; the reason is logged here only.
/// Note: the base-URL check is a filter, not authentication — a server-side caller can set any header,
/// so the credentials stay the real guard.
/// </summary>
public class ThirdPartyInboundAuthFilter(IConfiguration config, IMemoryCache cache, ILogger<ThirdPartyInboundAuthFilter> logger) : IAsyncResourceFilter
{
    public const string ClientBaseUrlHeader = "X-Client-Base-Url";

    public async Task OnResourceExecutionAsync(ResourceExecutingContext context, ResourceExecutionDelegate next)
    {
        var http = context.HttpContext;
        var opt = ThirdPartyInboundOptions.From(config);
        http.Items[ThirdPartyInboundResponse.StartedKey] = Stopwatch.GetTimestamp();
        http.Items[ThirdPartyInboundResponse.FailureSecondsKey] = opt.FailureSeconds;

        var reason = await ValidateAsync(http, opt);
        if (reason is not null)
        {
            logger.LogWarning("ThirdPartyInbound rejected {Method} {Path} from {Ip}: {Reason}",
                http.Request.Method, http.Request.Path, http.Connection.RemoteIpAddress, reason);
            context.Result = await ThirdPartyInboundResponse.FailAsync(http);
            return;
        }

        await next();
    }

    private async Task<string?> ValidateAsync(HttpContext http, ThirdPartyInboundOptions opt)
    {
        // 1) Rate limit per client IP, before any other work.
        if (opt.RateLimit > 0)
        {
            var key = "tp_inbound_" + http.Connection.RemoteIpAddress;
            var counter = cache.GetOrCreate(key, e =>
            {
                e.AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(Math.Max(1, opt.RateWindowSeconds));
                return new StrongBox<int>(0);
            })!;
            if (Interlocked.Increment(ref counter.Value) > opt.RateLimit)
                return $"rate limit exceeded (limit={opt.RateLimit}/{opt.RateWindowSeconds}s)";
        }

        // 2) Allowed base URL.
        var allowed = opt.AllowedBaseUrls.Select(NormalizeBaseUrl).OfType<string>().ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (allowed.Count == 0)
            return "no AllowedBaseUrls configured";
        var caller = CallerBaseUrl(http.Request);
        if (caller is null)
            return $"no caller base URL (send Origin or {ClientBaseUrlHeader})";
        if (!allowed.Contains(caller))
            return $"base URL not allowed: {caller}";

        // 3) Body must be a JSON object; read it buffered so model binding can read it again.
        http.Request.EnableBuffering();
        string username, password;
        try
        {
            using var doc = await JsonDocument.ParseAsync(http.Request.Body, cancellationToken: http.RequestAborted);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
                return "body is not a JSON object";
            username = ReadString(doc.RootElement, "username");
            password = ReadString(doc.RootElement, "password");
        }
        catch (JsonException)
        {
            return $"body is not valid JSON (content-type={http.Request.ContentType})";
        }
        finally
        {
            http.Request.Body.Position = 0;
        }

        // 4) Credentials, constant time.
        if (opt.Username.Length == 0 || opt.PasswordHash.Length == 0)
            return "gateway not configured (ThirdPartyInbound:Username / PasswordHash missing)";
        var userOk = SecretEquals(opt.Username, username);
        var passOk = SecretEquals(opt.PasswordHash, password);
        if (!(userOk && passOk))
            return $"bad credentials (username {(userOk ? "ok" : "WRONG")}, password {(passOk ? "ok" : "WRONG")})";

        return null;
    }

    /// <summary>First of Origin / X-Client-Base-Url / Referer that parses, reduced to scheme://host[:port].</summary>
    private static string? CallerBaseUrl(HttpRequest r) =>
        new[] { r.Headers.Origin.ToString(), r.Headers[ClientBaseUrlHeader].ToString(), r.Headers.Referer.ToString() }
            .Select(NormalizeBaseUrl)
            .FirstOrDefault(u => u is not null);

    /// <summary>"http://PLM.local:80/api/x" -> "http://plm.local"; anything that isn't an absolute http(s) URL -> null.</summary>
    public static string? NormalizeBaseUrl(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps)
            ? u.GetLeftPart(UriPartial.Authority).ToLowerInvariant()
            : null;

    private static string ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? string.Empty : string.Empty;

    private static bool SecretEquals(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
