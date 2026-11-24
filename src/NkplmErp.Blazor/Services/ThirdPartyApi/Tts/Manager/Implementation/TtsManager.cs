using System.Net.Http.Json;
using NkplmErp.Blazor.Services.ThirdPartyApi.Tts.Manager.Interface;
using NkplmErp.Blazor.Services.ThirdPartyApi.Tts.Manager.Route;
using NkplmErp.Shared.DTOs.Tts;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Services.ThirdPartyApi.Tts.Manager.Implementation
{
    /// <summary>
    /// Typed client for TtsController. Speak answers with raw audio on success (not the usual IResponse
    /// envelope), so it is read as bytes here and only wrapped afterwards. Failures are quiet on purpose:
    /// the caller decides whether a failed read-aloud deserves a message.
    /// </summary>
    public class TtsManager(HttpClient http) : ITtsManager
    {
        public async Task<IResponse<TtsAudioResponseModel>> SpeakAsync(TtsRequestModel request)
        {
            try
            {
                using var resp = await http.PostAsJsonAsync(TtsEndpoint.Speak, request);
                if (!resp.IsSuccessStatusCode)
                    return Response<TtsAudioResponseModel>.Fail(await ReadFailureAsync(resp));

                return Response<TtsAudioResponseModel>.Success(new TtsAudioResponseModel
                {
                    Audio = await resp.Content.ReadAsByteArrayAsync(),
                    ContentType = resp.Content.Headers.ContentType?.MediaType ?? "audio/wav"
                });
            }
            catch (Exception ex)
            {
                return Response<TtsAudioResponseModel>.Fail(ex.Message);
            }
        }

        public async Task<IResponse<TtsVoicesResponseModel>> GetVoicesAsync()
        {
            try
            {
                var body = await http.GetFromJsonAsync<Response<TtsVoicesResponseModel>>(TtsEndpoint.Voices);
                return body is { Succeeded: true, Data: not null }
                    ? Response<TtsVoicesResponseModel>.Success(body.Data)
                    : Response<TtsVoicesResponseModel>.Fail(body?.Messages ?? "Voices could not be loaded.");
            }
            catch (Exception ex)
            {
                return Response<TtsVoicesResponseModel>.Fail(ex.Message);
            }
        }

        // Errors come back as the standard envelope ({ succeeded:false, messages }) or a plain string.
        private static async Task<string> ReadFailureAsync(HttpResponseMessage resp)
        {
            try
            {
                var env = await resp.Content.ReadFromJsonAsync<Response<object>>();
                if (!string.IsNullOrWhiteSpace(env?.Messages)) return env.Messages!;
            }
            catch { /* not an envelope */ }
            return $"Text-to-speech failed ({(int)resp.StatusCode}).";
        }
    }
}
