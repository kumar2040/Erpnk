using System.Diagnostics;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NkplmErp.ThirdPartyApi.Common.Model;

namespace NkplmErp.ThirdPartyApi.Plm.Inbound.Base;

/// <summary>
/// Reply helpers for the inbound gateway (same shape as PLM's ThirdPartyResponse): success is
/// 200 {success, route, data}; every failure is 400 {"message":"Bad Request"} padded to the same
/// fixed delay, so nothing leaks about which check failed. The real reason only goes to our log.
/// </summary>
public static class ThirdPartyInboundResponse
{
    internal const string StartedKey = "ThirdPartyInbound.Started";
    internal const string FailureSecondsKey = "ThirdPartyInbound.FailureSeconds";

    /// <summary>Runs <paramref name="logic"/> with the body's "parameters"; any exception becomes the generic failure.</summary>
    public static async Task<IActionResult> RespondAsync(HttpContext http, string route, object? parameters, Func<JsonElement, Task<object?>> logic)
    {
        try
        {
            var p = parameters is JsonElement e && e.ValueKind == JsonValueKind.Object ? e : JsonDocument.Parse("{}").RootElement;
            var data = await logic(p);
            return new OkObjectResult(new ThirdPartyResponseModel<object?> { Success = true, Route = route, Data = data });
        }
        catch (Exception ex)
        {
            Logger(http).LogError(ex, "ThirdPartyInbound {Route} failed", route);
            return await FailAsync(http);
        }
    }

    /// <summary>Pads to FailureSeconds (measured from when the gateway first saw the request), then answers the generic 400.</summary>
    public static async Task<IActionResult> FailAsync(HttpContext http)
    {
        var started = http.Items[StartedKey] as long? ?? Stopwatch.GetTimestamp();
        var seconds = http.Items[FailureSecondsKey] as int? ?? 3;
        var remaining = TimeSpan.FromSeconds(seconds) - Stopwatch.GetElapsedTime(started);
        if (remaining > TimeSpan.Zero)
        {
            try { await Task.Delay(remaining, http.RequestAborted); }
            catch (OperationCanceledException) { }
        }
        return new BadRequestObjectResult(new { message = "Bad Request" });
    }

    private static ILogger Logger(HttpContext http) =>
        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("ThirdPartyInbound");
}
