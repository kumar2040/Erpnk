using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Components.Authorization;

namespace NkplmErp.Blazor.Services.Auth;

public class CustomAuthStateProvider : AuthenticationStateProvider
{
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<CustomAuthStateProvider> _logger;
    private readonly AuthenticationState _anonymous;
    public string? AuthToken => _tokenProvider.Token;
    public Guid InstanceId { get; } = Guid.NewGuid();

    private Task<AuthenticationState>? _cachedStateTask;

    public CustomAuthStateProvider(
        TokenProvider tokenProvider,
        ILogger<CustomAuthStateProvider> logger)
    {
        _tokenProvider = tokenProvider;
        _logger = logger;
        _anonymous = new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));

        // Subscribe to token changes to trigger UI updates
        _tokenProvider.OnTokenChanged += () => 
        {
            try
            {
                Console.WriteLine(">>>> [DEBUG] TokenProvider.OnTokenChanged triggered. Notifying state change.");
                var authState = BuildAuthenticationState(_tokenProvider.Token ?? string.Empty);
                NotifyAuthenticationStateChanged(Task.FromResult(authState));
            }
            catch (Exception ex)
            {
                Console.WriteLine($">>>> [DEBUG] ERROR in OnTokenChanged: {ex.Message}");
            }
        };
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        Console.WriteLine($">>>> [DEBUG] GetAuthenticationStateAsync (Mini) Called. Token Present: {!string.IsNullOrEmpty(_tokenProvider.Token)}");
        
        if (string.IsNullOrEmpty(_tokenProvider.Token))
        {
            return Task.FromResult(_anonymous);
        }

        try
        {
            var state = BuildAuthenticationState(_tokenProvider.Token);
            return Task.FromResult(state);
        }
        catch (Exception ex)
        {
             Console.WriteLine($">>>> [DEBUG] Error in GetAuthenticationStateAsync: {ex.Message}");
             return Task.FromResult(_anonymous);
        }
    }

    private AuthenticationState BuildAuthenticationState(string token)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(token))
                return _anonymous;

            // Optional: Basic JWT parsing without heavy libraries if needed, 
            // but we'll try the standard one first with a guard
            var handler = new JwtSecurityTokenHandler();
            if (!handler.CanReadToken(token))
            {
                Console.WriteLine(">>>> [DEBUG] Cannot read token. Returning anonymous.");
                return _anonymous;
            }

            var jwtToken = handler.ReadJwtToken(token);
            var identity = new ClaimsIdentity(jwtToken.Claims, "jwt", "name", "role");
            return new AuthenticationState(new ClaimsPrincipal(identity));
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>>> [DEBUG] Error parsing JWT: {ex.Message}. Returning fallback identity.");
            // Fallback to a simple identity so the app doesn't hang
            var fallbackIdentity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.Name, "User") }, "jwt");
            return new AuthenticationState(new ClaimsPrincipal(fallbackIdentity));
        }
    }
}
