using NkplmErp.Shared.DTOs.Tts;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Services.ThirdPartyApi.Tts.Manager.Interface
{
    public interface ITtsManager
    {
        /// <summary>Turns the text into speech (English / Nepali, language auto-detected) and returns the WAV audio.</summary>
        Task<IResponse<TtsAudioResponseModel>> SpeakAsync(TtsRequestModel request);

        /// <summary>The voices the TTS server offers and its word limit.</summary>
        Task<IResponse<TtsVoicesResponseModel>> GetVoicesAsync();
    }
}
