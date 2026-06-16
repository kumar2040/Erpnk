using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NkplmErp.Maui.Services;

namespace NkplmErp.Maui.ViewModels;

public partial class LoginPageViewModel : ObservableObject
{
    private readonly AuthService _authService;

    public LoginPageViewModel(AuthService authService)
    {
        _authService = authService;
    }

    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private bool _isPasswordHidden = true;

    [ObservableProperty]
    private string _eyeIconSource = "eye_hidden.png";

    [ObservableProperty]
    private bool _isBusy = false;

    private Page CurrentPage => Application.Current!.Windows[0].Page!;

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        IsPasswordHidden = !IsPasswordHidden;
        EyeIconSource = IsPasswordHidden ? "eye_hidden.png" : "eye_visible.png";
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            await CurrentPage.DisplayAlert("Validation Error", "Please enter email and password.", "OK");
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _authService.LoginAsync(Username, Password);

            if (result.IsSuccess && !result.RequiresMfa)
            {
                await CurrentPage.DisplayAlert("Success", "Login successful!", "OK");
                // TODO: Navigate to main shell/dashboard
            }
            else if (result.RequiresMfa)
            {
                await CurrentPage.DisplayAlert("MFA Required", "Please enter your MFA code.", "OK");
                // TODO: Navigate to MFA page
            }
            else
            {
                await CurrentPage.DisplayAlert("Login Failed", result.Message, "OK");
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ForgotPasswordAsync()
    {
        await CurrentPage.DisplayAlert("Forgot Password", "Please contact your administrator.", "OK");
    }

    [RelayCommand]
    private async Task SocialLoginAsync(string platform)
    {
        await CurrentPage.DisplayAlert("Social Sign-In", $"Connecting with {platform}...", "OK");
    }
}
