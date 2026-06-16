namespace NkplmErp.Maui.Services;

public static class ApiConfig
{
#if ANDROID
    // Android emulator maps 10.0.2.2 to the host machine's localhost
    public const string BaseUrl = "http://10.0.2.2:5272";
#else
    // Windows / iOS simulator
    public const string BaseUrl = "http://localhost:5272";
#endif

    public const string LoginEndpoint       = "/api/v1/auth/login";
    public const string RefreshEndpoint     = "/api/v1/auth/refresh";
    public const string LogoutEndpoint      = "/api/v1/auth/logout";
    public const string UserInfoEndpoint    = "/api/v1/auth/userinfo";
}
