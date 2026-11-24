namespace NkplmErp.ThirdPartyApi.Tts.Outbound.Model
{
    /// <summary>
    /// "Tts" configuration section. BaseUrl is plain config (appsettings.json); Username and Password are
    /// secrets: set them with user-secrets (dev) or the Tts__Username / Tts__Password env vars (prod).
    /// </summary>
    public class TtsSettings
    {
        public const string SectionName = "Tts";

        /// <summary>Host of the TTS server, e.g. "172.25.10.46:8091" (http:// is assumed when no scheme is given).</summary>
        public string BaseUrl { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;

        /// <summary>Generating long text takes a while, so this is generous.</summary>
        public int TimeoutSeconds { get; set; } = 120;
    }
}
