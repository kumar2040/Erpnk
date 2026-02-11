using Blazored.LocalStorage;
using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;

using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace NkplmErp.Blazor.Services.Auth;

public class AuthenticationDelegatingHandler : DelegatingHandler
{
    private readonly CustomAuthStateProvider _authStateProvider;
    private readonly ILogger<AuthenticationDelegatingHandler> _logger;

    public Guid InstanceId { get; } = Guid.NewGuid();

    public AuthenticationDelegatingHandler(
        CustomAuthStateProvider authStateProvider, 
        ILogger<AuthenticationDelegatingHandler> logger)
    {
        _authStateProvider = authStateProvider;
        _logger = logger;
        _logger.LogInformation("DEBUG: AuthHandler Created: InstanceId={InstanceId}", InstanceId);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestUrl = request.RequestUri?.ToString() ?? "unknown";
        
        try
        {
            // Use the cached token from CustomAuthStateProvider
            // This is populated by the UI (MainDashboard) calling GetAuthenticationStateAsync()
            // avoiding JS interop calls inside the HttpClient pipeline.
            var token = _authStateProvider.AuthToken;

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                _logger.LogInformation("DEBUG: AuthHandler[{InstanceId}] - Attached cached token for {requestUrl}. Provider[{ProviderId}]", InstanceId, requestUrl, _authStateProvider.InstanceId);
            }
            else
            {
                // Fallback: If token is missing from provider, try one last time to get it 
                // but only if we are not in a context that forbids it.
                // However, for Blazor Server, relying on the provider is safest.
                _logger.LogWarning("DEBUG: AuthHandler[{InstanceId}] - No cached token found in Provider[{ProviderId}] for {requestUrl}", InstanceId, _authStateProvider.InstanceId, requestUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: AuthHandler[{InstanceId}] - Error reading token from Provider: {Message}", InstanceId, ex.Message);
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
