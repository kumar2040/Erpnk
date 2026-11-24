using NkplmErp.Shared.DTOs.Tts;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.ThirdPartyApi.Tts.Outbound.Manager.Interface
{
    /// <summary>Text-to-speech through the external TTS server (English + Nepali).</summary>
    public interface ITtsService
    {
        /// <summary>Voices on offer and the per-request word limit.</summary>
        Task<IResponse<TtsVoicesResponseModel>> GetVoicesAsync(CancellationToken ct = default);

        /// <summary>Turns the text into speech and returns the audio.</summary>
        Task<IResponse<TtsAudioResponseModel>> SpeakAsync(TtsRequestModel request, CancellationToken ct = default);

        /// <summary>Whether the TTS server is reachable.</summary>
        Task<IResponse<TtsHealthResponseModel>> GetHealthAsync(CancellationToken ct = default);
    }
}
