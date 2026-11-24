using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NkplmErp.ThirdPartyApi.Tts.Outbound.Manager.Interface;
using NkplmErp.ThirdPartyApi.Tts.Outbound.Model;
using NkplmErp.Shared.DTOs.Tts;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.ThirdPartyApi.Tts.Outbound.Manager.Implementation
{
    /// <summary>
    /// Talks to the external TTS server: GET /api/v1/voices, POST /api/v1/tts (returns WAV), GET /api/health.
    /// Every call carries the username/password as an HTTP Basic Authorization header. The server owns the
    /// rules (word limit, language detection, voice choice); this class only forwards and reports.
    /// </summary>
    public class TtsService(HttpClient http, IOptions<TtsSettings> options, ILogger<TtsService> logger) : ITtsService
    {
        private readonly TtsSettings _settings = options.Value;

        public Task<IResponse<TtsVoicesResponseModel>> GetVoicesAsync(CancellationToken ct = default) =>
            GetJsonAsync<TtsVoicesResponseModel>("/api/v1/voices", ct);

        public Task<IResponse<TtsHealthResponseModel>> GetHealthAsync(CancellationToken ct = default) =>
            GetJsonAsync<TtsHealthResponseModel>("/api/health", ct);

        public async Task<IResponse<TtsAudioResponseModel>> SpeakAsync(TtsRequestModel request, CancellationToken ct = default)
        {
            try
            {
                if (!TryBuildUri("/api/v1/tts", out var uri, out var configError))
                    return Response<TtsAudioResponseModel>.Fail(configError);

                using var message = new HttpRequestMessage(HttpMethod.Post, uri) { Content = JsonContent.Create(request) };
                message.Headers.Authorization = BasicAuth();

                using var resp = await http.SendAsync(message, ct);
                if (!resp.IsSuccessStatusCode)
                    return Response<TtsAudioResponseModel>.Fail(await DescribeFailureAsync(resp, ct));

                return Response<TtsAudioResponseModel>.Success(new TtsAudioResponseModel
                {
                    Audio = await resp.Content.ReadAsByteArrayAsync(ct),
                    ContentType = resp.Content.Headers.ContentType?.MediaType ?? "audio/wav"
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "TTS speak request failed");
                return Response<TtsAudioResponseModel>.Fail("The text-to-speech server could not be reached.");
            }
        }

        private async Task<IResponse<T>> GetJsonAsync<T>(string path, CancellationToken ct)
        {
            try
            {
                if (!TryBuildUri(path, out var uri, out var configError))
                    return Response<T>.Fail(configError);

                using var message = new HttpRequestMessage(HttpMethod.Get, uri);
                message.Headers.Authorization = BasicAuth();

                using var resp = await http.SendAsync(message, ct);
                if (!resp.IsSuccessStatusCode)
                    return Response<T>.Fail(await DescribeFailureAsync(resp, ct));

                var data = await resp.Content.ReadFromJsonAsync<T>(cancellationToken: ct);
                return data is null
                    ? Response<T>.Fail("The text-to-speech server returned an empty answer.")
                    : Response<T>.Success(data);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "TTS request {Path} failed", path);
                return Response<T>.Fail("The text-to-speech server could not be reached.");
            }
        }

        // "172.25.10.46:8091" has no scheme; the server is plain http on the internal network.
        private bool TryBuildUri(string path, out Uri uri, out string error)
        {
            uri = null!;
            error = string.Empty;

            if (string.IsNullOrWhiteSpace(_settings.BaseUrl)
                || string.IsNullOrWhiteSpace(_settings.Username)
                || string.IsNullOrWhiteSpace(_settings.Password))
            {
                error = "Text-to-speech is not configured.";
                return false;
            }

            var baseUrl = _settings.BaseUrl.Trim().TrimEnd('/');
            if (!baseUrl.Contains("://")) baseUrl = "http://" + baseUrl;

            if (!Uri.TryCreate(baseUrl + path, UriKind.Absolute, out var built))
            {
                error = "Text-to-speech address is invalid.";
                return false;
            }

            uri = built;
            return true;
        }

        private AuthenticationHeaderValue BasicAuth() =>
            new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_settings.Username}:{_settings.Password}")));

        // The server answers errors as { "detail": "..." }; 401 means the configured login was rejected.
        private async Task<string> DescribeFailureAsync(HttpResponseMessage resp, CancellationToken ct)
        {
            logger.LogWarning("TTS server answered {Status} for {Url}", (int)resp.StatusCode, resp.RequestMessage?.RequestUri);

            if ((int)resp.StatusCode == 401)
                return "Text-to-speech login was rejected. Check the configured username and password.";

            try
            {
                using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
                if (doc.RootElement.TryGetProperty("detail", out var detail) && detail.ValueKind == JsonValueKind.String)
                    return detail.GetString() ?? "Text-to-speech request failed.";
            }
            catch { /* not JSON: fall through */ }

            return $"Text-to-speech request failed ({(int)resp.StatusCode}).";
        }
    }
}
