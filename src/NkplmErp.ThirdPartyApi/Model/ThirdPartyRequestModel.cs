using System.Text.Json.Serialization;

namespace NkplmErp.ThirdPartyApi.Model;

/// <summary>
/// What every third-party call sends. The manager fills Username/Password from configuration, so a
/// caller only supplies the route (see ThirdPartyRouter) and the parameters.
/// </summary>
public class ThirdPartyRequestModel
{
    [JsonPropertyName("username")] public string Username { get; set; } = string.Empty;
    [JsonPropertyName("password")] public string Password { get; set; } = string.Empty;

    /// <summary>Goes in the URL, not the body.</summary>
    [JsonIgnore] public string Route { get; set; } = string.Empty;

    [JsonPropertyName("parameters")] public object Parameters { get; set; } = new { };
}
