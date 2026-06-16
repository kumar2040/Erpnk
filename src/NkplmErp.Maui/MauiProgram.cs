using Microsoft.Extensions.Logging;
using NkplmErp.Maui.Services;
using NkplmErp.Maui.ViewModels;
using NkplmErp.Maui.Views;

namespace NkplmErp.Maui;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>()
            .ConfigureFonts(fonts =>
            {
                fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
            });

        // Register API HttpClient
        builder.Services.AddHttpClient<AuthService>(client =>
        {
            client.BaseAddress = new Uri(ApiConfig.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Register ViewModels
        builder.Services.AddTransient<LoginPageViewModel>();

        // Register Views
        builder.Services.AddTransient<LoginPage>();

#if DEBUG
		builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}
