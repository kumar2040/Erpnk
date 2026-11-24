namespace NkplmErp.ThirdPartyApi.Tts.Outbound.Route;

/// <summary>Every route on the external TTS server, relative to Tts:BaseUrl.</summary>
public static class TtsRouter
{
    public const string Voices = "/api/v1/voices";
    public const string Speak = "/api/v1/tts";
    public const string Health = "/api/health";
}
