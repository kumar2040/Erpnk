using System.Text.Json.Serialization;

namespace NkplmErp.Shared.DTOs.Tts;

/// <summary>The voices the TTS server offers, plus its per-request word limit.</summary>
public class TtsVoicesResponseModel
{
    [JsonPropertyName("voices")]
    public List<TtsVoiceModel> Voices { get; set; } = new();

    [JsonPropertyName("max_words")]
    public int MaxWords { get; set; }
}

public class TtsVoiceModel
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>"en" or "ne".</summary>
    [JsonPropertyName("lang")]
    public string Lang { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("default")]
    public bool Default { get; set; }
}

/// <summary>Generated speech: the audio bytes (WAV) and the content type the TTS server sent.</summary>
public class TtsAudioResponseModel
{
    public byte[] Audio { get; set; } = Array.Empty<byte>();
    public string ContentType { get; set; } = "audio/wav";
}

/// <summary>TTS server health (GET /api/health).</summary>
public class TtsHealthResponseModel
{
    [JsonPropertyName("voices_available")]
    public int VoicesAvailable { get; set; }
}
