using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NkplmErp.Shared.Wrapper;
using NkplmErp.ThirdPartyApi.Outbound.Manager.Interface;
using NkplmErp.ThirdPartyApi.Common.Model;
using NkplmErp.ThirdPartyApi.Outbound.Route;

namespace NkplmErp.ThirdPartyApi.Outbound.Manager.Implementation;

/// <summary>
/// The one generic caller for the third-party (PLM) API. Reads ThirdPartyApi:BaseUrl / Username /
/// Password from configuration and sends them with every call.
/// </summary>
public class ThirdPartyManager(HttpClient http, IConfiguration config, ILogger<ThirdPartyManager> logger) : IThirdPartyManager
{
    private const string GenericFailure = "Third-party API request failed.";
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public async Task<IResponse<T>> CallAsync<T>(ThirdPartyRoute route, object? parameters = null, CancellationToken ct = default)
    {
        var request = new ThirdPartyRequestModel
        {
            Username = config["ThirdPartyApi:Username"] ?? string.Empty,
            Password = config["ThirdPartyApi:Password"] ?? string.Empty,
            Route = route.Path,
            Parameters = parameters ?? new { }
        };

        var result = await SendAsync<T>(request, ct);
        if (!result.Succeeded && !string.IsNullOrWhiteSpace(route.Fallback))
        {
            request.Route = route.Fallback;
            result = await SendAsync<T>(request, ct);
        }
        return result;
    }

    private async Task<IResponse<T>> SendAsync<T>(ThirdPartyRequestModel request, CancellationToken ct)
    {
        try
        {
            var baseUrl = config["ThirdPartyApi:BaseUrl"];
            if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
                return Response<T>.Fail("Third-party API is not configured.");

            // Fixed-length body on purpose: PostAsJsonAsync streams chunked, which IIS + php-cgi never finishes reading.
            using var body = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json");
            using var resp = await http.PostAsync(baseUrl.TrimEnd('/') + "/" + request.Route.TrimStart('/'), body, ct);
            if (!resp.IsSuccessStatusCode)
            {
                // The API answers every failure with the same generic 400; don't pretend to know more.
                logger.LogWarning("Third-party route {Route} returned {Status}", request.Route, (int)resp.StatusCode);
                return Response<T>.Fail(GenericFailure);
            }

            var model = await resp.Content.ReadFromJsonAsync<ThirdPartyResponseModel<T>>(Json, ct);
            return model is { Success: true, Data: not null }
                ? Response<T>.Success(model.Data)
                : Response<T>.Fail(GenericFailure);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Third-party call {Route} failed", request.Route);
            return Response<T>.Fail(GenericFailure);
        }
    }
}
