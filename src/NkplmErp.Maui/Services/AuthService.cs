using System.Net.Http.Json;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Maui.Services;

public class AuthService
{
    private readonly HttpClient _http;

    public AuthService(HttpClient http)
    {
        _http = http;
    }

    public string? Token { get; private set; }

    public async Task<AuthResponse> LoginAsync(string email, string password)
    {
        try
        {
            var response = await _http.PostAsJsonAsync(
                ApiConfig.LoginEndpoint,
                new LoginRequest { Email = email, Password = password });

            var result = await response.Content.ReadFromJsonAsync<AuthResponse>();
            if (result?.IsSuccess == true && !string.IsNullOrEmpty(result.Token))
                Token = result.Token;

            return result ?? new AuthResponse { IsSuccess = false, Message = "No response from server." };
        }
        catch (HttpRequestException ex)
        {
            return new AuthResponse { IsSuccess = false, Message = $"Cannot reach server: {ex.Message}" };
        }
        catch (Exception ex)
        {
            return new AuthResponse { IsSuccess = false, Message = $"Unexpected error: {ex.Message}" };
        }
    }

    public void Logout() => Token = null;
}
