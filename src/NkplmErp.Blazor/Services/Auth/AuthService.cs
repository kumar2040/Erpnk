using System.Net.Http.Json;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using NkplmErp.Shared.DTOs;
using Fido2NetLib;

namespace NkplmErp.Blazor.Services.Auth;

public interface IAuthService
{
    Task<AuthResponse> Login(LoginRequest loginRequest);
    Task Logout();
    Task<AuthResponse> VerifyMfa(MfaVerifyRequest mfaRequest);
    Task<string> TryRefreshToken();
    Task<AuthResponse> ConfirmMfa(string code);
    Task<AuthResponse> RegisterBiometric(string deviceName);
    Task<AuthResponse> LoginBiometric(string email);
    Task<MfaSetupResponse> GetMfaSetup();
}

public class AuthService : IAuthService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly AuthenticationStateProvider _authStateProvider;
    private readonly ILocalStorageService _localStorage;
    private readonly Microsoft.JSInterop.IJSRuntime _jsRuntime;
    private System.Timers.Timer? _refreshTimer;

    public AuthService(HttpClient httpClient,
                       AuthenticationStateProvider authStateProvider,
                       ILocalStorageService localStorage,
                       Microsoft.JSInterop.IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _authStateProvider = authStateProvider;
        _localStorage = localStorage;
        _jsRuntime = jsRuntime;
        // InitializeRefreshTimer(); // Move to Login/Verify actions only to avoid startup crash
    }

    private void InitializeRefreshTimer()
    {
        _refreshTimer = new System.Timers.Timer(5 * 60 * 1000); // 5 minutes
        _refreshTimer.Elapsed += async (s, e) => 
        {
            try
            {
                // Ensure we run on the main thread if needed, or just suppress context issues if possible. 
                // However, Blazor Server timers generally run on a thread pool thread.
                // Since ILocalStorageService requires JS interaction, we might need to be careful.
                // Note: In Blazor Server, accessing localStorage from a background timer is tricky because the circuit might be idle.
                // Ideally, we should check if the circuit is alive.
                await TryRefreshToken();
            }
            catch
            {
                // Ignore errors during background refresh to prevent crash
            }
        };
        _refreshTimer.AutoReset = true;
        _refreshTimer.Start();
    }

    public async Task<AuthResponse> Login(LoginRequest loginRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", loginRequest);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

        if (result!.IsSuccess && !result.RequiresMfa)
        {
            await _localStorage.SetItemAsync("authToken", result.Token);
            await _localStorage.SetItemAsync("refreshToken", result.RefreshToken);
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);
            InitializeRefreshTimer(); // Restart timer on login
        }

        return result;
    }

    public async Task<AuthResponse> VerifyMfa(MfaVerifyRequest mfaRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/auth/mfa-verify", mfaRequest);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

        if (result!.IsSuccess)
        {
            await _localStorage.SetItemAsync("authToken", result.Token);
            await _localStorage.SetItemAsync("refreshToken", result.RefreshToken);
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);
            InitializeRefreshTimer(); // Restart timer on login
        }

        return result;
    }

    public async Task<string> TryRefreshToken()
    {
        try 
        {
            var token = await _localStorage.GetItemAsync<string>("authToken");
            var refreshToken = await _localStorage.GetItemAsync<string>("refreshToken");

            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(refreshToken))
                return string.Empty;

            var refreshRequest = new TokenRefreshRequest { Token = token, RefreshToken = refreshToken };
            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/refresh", refreshRequest);

            if (!response.IsSuccessStatusCode)
            {
                await Logout();
                return string.Empty;
            }

            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (result == null || !result.IsSuccess)
            {
                await Logout();
                return string.Empty;
            }

            await _localStorage.SetItemAsync("authToken", result.Token);
            await _localStorage.SetItemAsync("refreshToken", result.RefreshToken);
            ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);

            return result.Token;
        }
        catch
        {
            return string.Empty;
        }
    }

    public async Task<MfaSetupResponse> GetMfaSetup()
    {
        var response = await _httpClient.GetAsync("api/v1/auth/mfa-setup");
        if (!response.IsSuccessStatusCode) return new MfaSetupResponse { IsMfaEnabled = false };

        return await response.Content.ReadFromJsonAsync<MfaSetupResponse>() ?? new MfaSetupResponse { IsMfaEnabled = false };
    }

    public async Task<AuthResponse> ConfirmMfa(string code)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/auth/mfa-confirm", code);
        return await response.Content.ReadFromJsonAsync<AuthResponse>() ?? new AuthResponse { IsSuccess = false, Message = "Failed to confirm MFA." };
    }

    public async Task Logout()
    {
        _refreshTimer?.Stop();
        await _localStorage.RemoveItemAsync("authToken");
        await _localStorage.RemoveItemAsync("refreshToken");
        ((CustomAuthStateProvider)_authStateProvider).NotifyUserLogout();
    }

    public async Task<AuthResponse> RegisterBiometric(string deviceName)
    {
        try
        {
            var token = await _localStorage.GetItemAsync<string>("authToken");
            var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/auth/biometric-registration-options?deviceName={deviceName}");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }

            var optionsResponse = await _httpClient.SendAsync(request);

            if (!optionsResponse.IsSuccessStatusCode)
            {
                var errorContent = await optionsResponse.Content.ReadAsStringAsync();
                // Removed debug token status
                return new AuthResponse { IsSuccess = false, Message = $"Failed: {optionsResponse.StatusCode} - {errorContent}" };
            }

            var optionsJson = await optionsResponse.Content.ReadAsStringAsync();
            var attestationResponse = await _jsRuntime.InvokeAsync<AuthenticatorAttestationRawResponse>("webAuthnInterop.register", new object[] { optionsJson });

            var verifyRequest = new BiometricVerifyRegistrationRequest
            {
                DeviceName = deviceName,
                AttestationResponse = attestationResponse
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/biometric-register", verifyRequest);
            return await response.Content.ReadFromJsonAsync<AuthResponse>() ?? new AuthResponse { IsSuccess = false };
        }
        catch (Exception ex)
        {
            return new AuthResponse { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<AuthResponse> LoginBiometric(string email)
    {
        try
        {
            var optionsResponse = await _httpClient.GetAsync($"api/v1/auth/biometric-login-options?email={email}");
            if (!optionsResponse.IsSuccessStatusCode) return new AuthResponse { IsSuccess = false, Message = "Failed to get login options" };

            var optionsJson = await optionsResponse.Content.ReadAsStringAsync();
            var assertionResponse = await _jsRuntime.InvokeAsync<AuthenticatorAssertionRawResponse>("webAuthnInterop.login", new object[] { optionsJson });

            var verifyRequest = new BiometricVerifyLoginRequest
            {
                Email = email,
                AssertionResponse = assertionResponse
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/biometric-login", verifyRequest);
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

            if (result!.IsSuccess)
            {
                await _localStorage.SetItemAsync("authToken", result.Token);
                await _localStorage.SetItemAsync("refreshToken", result.RefreshToken);
                ((CustomAuthStateProvider)_authStateProvider).NotifyUserAuthentication(result.Token);
                InitializeRefreshTimer(); // Restart timer on login
            }

            return result;
        }
        catch (Exception ex)
        {
            return new AuthResponse { IsSuccess = false, Message = ex.Message };
        }
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }
}
