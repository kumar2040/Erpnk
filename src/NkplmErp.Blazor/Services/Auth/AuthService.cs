using System;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Authorization;
using NkplmErp.Shared.DTOs;
using Fido2NetLib;

namespace NkplmErp.Blazor.Services.Auth;

public interface IAuthService
{
    Task<AuthResponse> Login(LoginRequest loginRequest);
    Task Logout();
    Task<AuthResponse> VerifyMfa(MfaVerifyRequest mfaRequest);
    Task<AuthResponse> ConfirmMfa(string code);
    Task<AuthResponse> DisableMfa();
    Task<AuthResponse> RegisterBiometric(string deviceName);
    Task<AuthResponse> LoginBiometric(string? email = null);
    Task<AuthResponse> RemoveBiometric(Guid deviceId);
    Task<MfaSetupResponse> GetMfaSetup();
    Task<UserInfoDto?> GetUserInfo(CancellationToken cancellationToken = default);
    Task<AuthResponse> ChangePassword(ChangePasswordRequest request);
}

public class AuthService : IAuthService, IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly TokenProvider _tokenProvider;
    private readonly Microsoft.JSInterop.IJSRuntime _jsRuntime;
    private System.Timers.Timer? _refreshTimer;

    public AuthService(HttpClient httpClient,
                       TokenProvider tokenProvider,
                       Microsoft.JSInterop.IJSRuntime jsRuntime)
    {
        _httpClient = httpClient;
        _tokenProvider = tokenProvider;
        _jsRuntime = jsRuntime;
    }

    private void InitializeRefreshTimer()
    {
        if (_refreshTimer != null) return;
        _refreshTimer = new System.Timers.Timer(5 * 60 * 1000); // 5 minutes
        _refreshTimer.Elapsed += async (s, e) => 
        {
            try
            {
                await TryRefreshToken();
            }
            catch { }
        };
        _refreshTimer.AutoReset = true;
        _refreshTimer.Start();
    }

    public async Task<AuthResponse> Login(LoginRequest loginRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/auth/login", loginRequest);
        
        if (response.IsSuccessStatusCode)
        {
            try 
            {
                var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (result!.IsSuccess && !result.RequiresMfa)
                {
                    _tokenProvider.Token = result.Token;
                    InitializeRefreshTimer();
                }
                return result;
            }
            catch (Exception ex)
            {
                var rawContent = await response.Content.ReadAsStringAsync();
                return new AuthResponse { IsSuccess = false, Message = $"JSON Parsing Error: {ex.Message}. Raw Response: {rawContent}" };
            }
        }
        else
        {
            var rawContent = await response.Content.ReadAsStringAsync();
            return new AuthResponse { IsSuccess = false, Message = $"Server Error ({response.StatusCode}): {rawContent}" };
        }
    }

    public async Task<AuthResponse> VerifyMfa(MfaVerifyRequest mfaRequest)
    {
        var response = await _httpClient.PostAsJsonAsync("api/v1/auth/mfa-verify", mfaRequest);
        var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

        if (result!.IsSuccess)
        {
            _tokenProvider.Token = result.Token;
            InitializeRefreshTimer();
        }

        return result;
    }

    public async Task<string> TryRefreshToken()
    {
        try 
        {
            var refreshRequest = new TokenRefreshRequest { Token = "", RefreshToken = "" }; // Token is handled by cookies
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

            _tokenProvider.Token = result.Token;
            return result.Token;
        }
        catch { return string.Empty; }
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

    public async Task<AuthResponse> DisableMfa()
    {
        var response = await _httpClient.PostAsync("api/v1/auth/mfa-disable", null);
        return await response.Content.ReadFromJsonAsync<AuthResponse>() ?? new AuthResponse { IsSuccess = false, Message = "Failed to disable MFA." };
    }

    public async Task<AuthResponse> ChangePassword(ChangePasswordRequest request)
    {
        try 
        {
            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/change-password", request);
            
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<AuthResponse>() 
                    ?? new AuthResponse { IsSuccess = false, Message = "Empty response from server." };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                Console.WriteLine($">>>> [DEBUG] Password Change API Error ({response.StatusCode}): {errorContent}");
                
                try 
                {
                    // Try to parse as AuthResponse anyway
                    return System.Text.Json.JsonSerializer.Deserialize<AuthResponse>(errorContent, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
                        ?? new AuthResponse { IsSuccess = false, Message = $"Error {response.StatusCode}" };
                }
                catch 
                {
                    return new AuthResponse { IsSuccess = false, Message = $"Server Error ({response.StatusCode}): {errorContent}" };
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>>> [DEBUG] Exception in AuthService.ChangePassword: {ex.Message}");
            throw;
        }
    }

    public async Task Logout()
    {
        _refreshTimer?.Stop();
        _refreshTimer = null;
        await _httpClient.PostAsync("api/v1/auth/logout", null);
        _tokenProvider.Token = null;
    }

    public async Task<AuthResponse> RemoveBiometric(Guid deviceId)
    {
        var response = await _httpClient.DeleteAsync($"api/v1/auth/biometric-device/{deviceId}");
        return await response.Content.ReadFromJsonAsync<AuthResponse>() ?? new AuthResponse { IsSuccess = false };
    }

    public async Task<AuthResponse> RegisterBiometric(string deviceName)
    {
        try
        {
            var request = new HttpRequestMessage(HttpMethod.Get, $"api/v1/auth/biometric-registration-options?deviceName={deviceName}");

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

    public async Task<AuthResponse> LoginBiometric(string? email = null)
    {
        try
        {
            var url = string.IsNullOrEmpty(email) ? "api/v1/auth/biometric-login-options" : $"api/v1/auth/biometric-login-options?email={email}";
            var optionsResponse = await _httpClient.GetAsync(url);
            if (!optionsResponse.IsSuccessStatusCode) return new AuthResponse { IsSuccess = false, Message = "Failed to get login options" };

            var optionsJson = await optionsResponse.Content.ReadAsStringAsync();
            var optionsDoc = System.Text.Json.JsonDocument.Parse(optionsJson);
            var challengeBase64Url = optionsDoc.RootElement.GetProperty("challenge").GetString() ?? "";
            
            // Fido2NetLib represents challenge as a Base64Url string in its JSON serialization
            var challengeBytes = DecodeBase64Url(challengeBase64Url);
            var sessionId = Convert.ToBase64String(challengeBytes);

            var assertionResponse = await _jsRuntime.InvokeAsync<AuthenticatorAssertionRawResponse>("webAuthnInterop.login", new object[] { optionsJson });

            var verifyRequest = new BiometricVerifyLoginRequest
            {
                Email = email ?? "",
                SessionId = sessionId,
                AssertionResponse = assertionResponse
            };

            var response = await _httpClient.PostAsJsonAsync("api/v1/auth/biometric-login", verifyRequest);
            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();

            if (result!.IsSuccess)
            {
                _tokenProvider.Token = result.Token;
                InitializeRefreshTimer();
            }

            return result;
        }
        catch (Exception ex)
        {
            return new AuthResponse { IsSuccess = false, Message = ex.Message };
        }
    }

    public async Task<UserInfoDto?> GetUserInfo(CancellationToken cancellationToken = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            Console.WriteLine($"DEBUG: AuthService[{_httpClient.GetHashCode()}] - GetUserInfo START");
            var response = await _httpClient.GetAsync("api/v1/auth/userinfo", cancellationToken);
            Console.WriteLine($"DEBUG: AuthService - GetUserInfo response: {response.StatusCode} (Elapsed: {sw.ElapsedMilliseconds}ms)");
            
            if (!response.IsSuccessStatusCode) return null;
            
            Console.WriteLine("DEBUG: AuthService - Reading JSON content...");
            var user = await response.Content.ReadFromJsonAsync<UserInfoDto>(cancellationToken: cancellationToken);
            Console.WriteLine($"DEBUG: AuthService - GetUserInfo SUCCESS for: {user?.Email} (Total: {sw.ElapsedMilliseconds}ms)");
            return user;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine($"DEBUG: AuthService - GetUserInfo TIMEOUT/CANCEL after {sw.ElapsedMilliseconds}ms");
            return null;
        }
        catch (Exception ex)
        { 
            Console.WriteLine($"DEBUG: AuthService - GetUserInfo EXCEPTION after {sw.ElapsedMilliseconds}ms: {ex.Message}");
            return null; 
        }
    }

    public void Dispose()
    {
        _refreshTimer?.Dispose();
    }

    private static byte[] DecodeBase64Url(string input)
    {
        string s = input.Replace('-', '+').Replace('_', '/');
        switch (s.Length % 4)
        {
            case 2: s += "=="; break;
            case 3: s += "="; break;
        }
        return Convert.FromBase64String(s);
    }
}
