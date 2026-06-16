namespace NkplmErp.Maui.WinUI;

public partial class App : Microsoft.Maui.MauiWinUIApplication
{
    public App()
    {
        this.InitializeComponent();
    }

    protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();
}
