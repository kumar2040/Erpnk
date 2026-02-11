using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;

namespace NkplmErp.Blazor.Services.Auth;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly ILocalStorageService _localStorage;
    private readonly ILogger<CustomAuthStateProvider> _logger;
    private readonly AuthenticationState _anonymous;
    public string? AuthToken { get; private set; }
    public Guid InstanceId { get; } = Guid.NewGuid();

    public CustomAuthStateProvider(ILocalStorageService localStorage, ILogger<CustomAuthStateProvider> logger)
    {
        _localStorage = localStorage;
        _logger = logger;
        _anonymous = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        _logger.LogInformation("DEBUG: AuthStateProvider Created: InstanceId={InstanceId}", InstanceId);
    }

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        try
        {
            _logger.LogInformation("DEBUG: CustomAuthStateProvider[{InstanceId}].GetAuthenticationStateAsync - Checking localStorage for 'authToken'", InstanceId);
            var token = await _localStorage.GetItemAsync<string>("authToken");

            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.LogInformation("DEBUG: CustomAuthStateProvider[{InstanceId}].GetAuthenticationStateAsync - No token found in localStorage.", InstanceId);
                AuthToken = null;
                return _anonymous;
            }

            _logger.LogInformation("DEBUG: CustomAuthStateProvider[{InstanceId}].GetAuthenticationStateAsync - Token FOUND (Length: {Length}). Setting AuthToken property.", InstanceId, token.Length);
            AuthToken = token;
            return BuildAuthenticationState(token);
        }
        catch (InvalidOperationException ex) 
        {
            // This happens during static rendering (Prerendering) because JS Interop is not available.
            // We return anonymous state here to let the page render initially.
            // The client-side takeover will re-run this and succeed.
            _logger.LogInformation("DEBUG: CustomAuthStateProvider[{InstanceId}].GetAuthenticationStateAsync - JS Interop unavailable (expected during prerender). Error: {Message}", InstanceId, ex.Message);
            return _anonymous;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: CustomAuthStateProvider[{InstanceId}].GetAuthenticationStateAsync - EXCEPTION: {Type}: {Message}", InstanceId, ex.GetType().Name, ex.Message);
            return _anonymous;
        }
    }

    public void NotifyUserAuthentication(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            NotifyUserLogout();
            return;
        }

        Console.WriteLine($"AuthStateProvider.NotifyUserAuthentication [{InstanceId}]: Setting token (length: {token.Length})");
        AuthToken = token;
        var authState = BuildAuthenticationState(token);
        NotifyAuthenticationStateChanged(Task.FromResult(authState));
    }

    public void NotifyUserLogout()
    {
        Console.WriteLine("AuthStateProvider.NotifyUserLogout: Clearing token");
        AuthToken = null;
        var authState = Task.FromResult(_anonymous);
        NotifyAuthenticationStateChanged(authState);
    }

    private static AuthenticationState BuildAuthenticationState(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        
        // Map JWT claims to standard ClaimsIdentity properties
        var identity = new ClaimsIdentity(jwtToken.Claims, "jwt", "name", "role");
        var user = new ClaimsPrincipal(identity);

        return new AuthenticationState(user);
    }
}
