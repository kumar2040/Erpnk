using System.Text.Json.Serialization;

namespace NkplmErp.Shared.DTOs.Tts;

/// <summary>
/// Text-to-speech request. The property names are the TTS server's own (lowercase), so this model is sent
/// to it as-is: { "text": "...", "lang": "auto", "speed": 1, "voices": { "en": "...", "ne": "..." } }.
/// </summary>
public class TtsRequestModel
{
    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;

    /// <summary>"auto" (detect English / Nepali from the script), "en" or "ne".</summary>
    [JsonPropertyName("lang")]
    public string Lang { get; set; } = "auto";

    /// <summary>Speaking speed, 0.5 – 2.0 (1 = normal).</summary>
    [JsonPropertyName("speed")]
    public double Speed { get; set; } = 1;

    /// <summary>Optional voice ids per language; omitted = the server's default voice.</summary>
    [JsonPropertyName("voices")]
    public TtsVoiceChoiceModel? Voices { get; set; }
}

public class TtsVoiceChoiceModel
{
    [JsonPropertyName("en")]
    public string? En { get; set; }

    [JsonPropertyName("ne")]
    public string? Ne { get; set; }
}
