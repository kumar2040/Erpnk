using NkplmErp.Maui.ViewModels;

namespace NkplmErp.Maui.Views;

public partial class LoginPage : ContentPage
{
    public LoginPage(LoginPageViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
