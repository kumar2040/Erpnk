using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NkplmErp.Shared.Wrapper;
using NkplmErp.ThirdPartyApi.Plm.Outbound.Manager.Interface;
using NkplmErp.ThirdPartyApi.Common.Model;
using NkplmErp.ThirdPartyApi.Plm.Outbound.Route;

namespace NkplmErp.ThirdPartyApi.Plm.Outbound.Manager.Implementation;

/// <summary>
/// The one generic caller for the third-party (PLM) API. Reads ThirdPartyApi:BaseUrl / Username /
/// Password from configuration and sends them with every call, plus ThirdPartyApi:ClientBaseUrl as the
/// X-Client-Base-Url header (PLM checks it against its allowed base URLs).
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
            using var message = new HttpRequestMessage(HttpMethod.Post, baseUrl.TrimEnd('/') + "/" + request.Route.TrimStart('/'))
            {
                Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
            };
            // PLM only accepts callers whose base URL is in its thirdParty allowedBaseUrls list.
            var clientBaseUrl = config["ThirdPartyApi:ClientBaseUrl"];
            if (!string.IsNullOrWhiteSpace(clientBaseUrl))
                message.Headers.TryAddWithoutValidation("X-Client-Base-Url", clientBaseUrl.Trim());
            using var resp = await http.SendAsync(message, ct);
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
