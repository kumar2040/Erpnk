namespace NkplmErp.Blazor.Services.ThirdPartyApi.Tts.Manager.Route
{
    // Central place for the TtsController routes (UI -> NkplmErp.API).
    public static class TtsEndpoint
    {
        public const string Base = "api/v1/Tts";
        public const string Speak = Base + "/speak";
        public const string Voices = Base + "/voices";
    }
}
