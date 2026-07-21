using NkplmErp.Blazor.Services.Toast;
using NkplmErp.Shared.Wrapper;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NkplmErp.Blazor.Shared.Http
{
    /// <summary>
    /// Single entry point for UI -> API traffic. Every call returns the
    /// <see cref="IResponse{T}"/> envelope — it never throws for a failed request —
    /// so callers keep branching on <c>result.Succeeded</c>.
    /// </summary>
    /// <remarks>
    /// Auth is deliberately absent here. The "ApiGateway" client is registered with
    /// <c>AuthenticationDelegatingHandler</c>, which attaches the bearer token to every
    /// request and, on a 401, clears the token and raises <c>OnSessionExpired</c> for
    /// MainLayout to redirect. Do not re-add header or redirect code to this class.
    /// </remarks>
    public class HttpServices : IHttpServices
    {
        private readonly HttpClient _httpClient;
        private readonly ToastService _toastService;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            ReferenceHandler = ReferenceHandler.Preserve
        };

        public HttpServices(IHttpClientFactory httpClientFactory, ToastService toastService)
        {
            _httpClient = httpClientFactory.CreateClient("ApiGateway");
            _toastService = toastService;
        }

        public Task<IResponse<T>> GetAsync<T>(string url)
            => SendAsync<T>(() => _httpClient.GetAsync(url));

        public Task<IResponse<T>> PostAsJsonAsync<T>(string url, object data)
            => SendAsync<T>(() => _httpClient.PostAsJsonAsync(url, data));

        public Task<IResponse<T>> DeleteAsync<T>(string url)
            => SendAsync<T>(() => _httpClient.DeleteAsync(url));

        /// <summary>
        /// The one place a request is actually issued: send, read, unwrap the envelope,
        /// fall back to a failed envelope. Verb methods above only supply the call.
        /// </summary>
        private async Task<IResponse<T>> SendAsync<T>(Func<Task<HttpResponseMessage>> send)
        {
            try
            {
                var response = await send();
                var body = await response.Content.ReadAsStringAsync();

                // The API returns the Response<T> envelope for handled failures too,
                // so prefer the server's own message over our generic status text.
                var envelope = TryDeserialize<T>(body);
                if (envelope is not null)
                {
                    if (!envelope.Succeeded && !string.IsNullOrWhiteSpace(envelope.Messages))
                    {
                        _toastService.ShowError(envelope.Messages);
                    }
                    return envelope;
                }

                // No readable envelope: 401 is already being handled by the auth
                // handler, so stay quiet and let MainLayout drive the redirect.
                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    return Response<T>.Fail("Your session has expired.");
                }

                var message = Describe(response.StatusCode);
                _toastService.ShowError(message);
                return Response<T>.Fail(message);
            }
            catch (Exception ex)
            {
                _toastService.ShowError(ex.Message);
                return Response<T>.Fail(ex.Message);
            }
        }

        private static Response<T>? TryDeserialize<T>(string body)
        {
            if (string.IsNullOrWhiteSpace(body)) return null;

            try
            {
                return JsonSerializer.Deserialize<Response<T>>(body, JsonOptions);
            }
            catch (JsonException)
            {
                // Non-envelope payload (HTML error page, plain text, truncated body).
                return null;
            }
        }

        private static string Describe(HttpStatusCode status) => status switch
        {
            HttpStatusCode.BadRequest => "Bad request. Please check your input and try again.",
            HttpStatusCode.Unauthorized => "Unauthorized. Please check your credentials.",
            HttpStatusCode.Forbidden => "Forbidden. You do not have permission to access this resource.",
            HttpStatusCode.NotFound => "Resource not found. Please check the URL and try again.",
            HttpStatusCode.InternalServerError => "Internal server error. Please try again later.",
            _ => "An error occurred. Please try again."
        };
    }
}
