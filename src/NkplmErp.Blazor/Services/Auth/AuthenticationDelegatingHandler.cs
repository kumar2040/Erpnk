using Microsoft.AspNetCore.Components.Authorization;
using System.Net.Http.Headers;

using System.Net.Http;
using Microsoft.Extensions.Logging;

namespace NkplmErp.Blazor.Services.Auth;

public class AuthenticationDelegatingHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly TokenProvider _tokenProvider;
    private readonly ILogger<AuthenticationDelegatingHandler> _logger;

    public Guid InstanceId { get; } = Guid.NewGuid();

    public AuthenticationDelegatingHandler(
        IHttpContextAccessor httpContextAccessor,
        TokenProvider tokenProvider,
        ILogger<AuthenticationDelegatingHandler> _logger)
    {
        _httpContextAccessor = httpContextAccessor;
        _tokenProvider = tokenProvider;
        this._logger = _logger;
        _logger.LogInformation("DEBUG: AuthHandler Created: InstanceId={InstanceId}", InstanceId);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var requestUrl = request.RequestUri?.ToString() ?? "unknown";
        
        try
        {
            _logger.LogInformation("DEBUG: AuthHandler[{InstanceId}] - Sending {Method} request to {Url}", 
                InstanceId, request.Method, requestUrl);
            
            // Priority 1: TokenProvider (reliable in SignalR circuit)
            // Priority 2: HttpContext (reliable during initial Prerender/Render)
            string? token = _tokenProvider.Token;
            
            if (string.IsNullOrEmpty(token))
            {
                var context = _httpContextAccessor.HttpContext;
                if (context != null)
                {
                    token = context.Request.Cookies["X-Auth-Token"];
                }
            }

            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                _logger.LogInformation("DEBUG: AuthHandler[{InstanceId}] - Token attached from {Source}", 
                    InstanceId, !string.IsNullOrEmpty(_tokenProvider.Token) ? "TokenProvider" : "Cookie");
            }
            else
            {
                _logger.LogWarning("DEBUG: AuthHandler[{InstanceId}] - No authentication token found", InstanceId);
            }
            
            _logger.LogInformation("DEBUG: AuthHandler[{InstanceId}] - Calling base.SendAsync...", InstanceId);
            var response = await base.SendAsync(request, cancellationToken);
            _logger.LogInformation("DEBUG: AuthHandler[{InstanceId}] - base.SendAsync returned with status: {StatusCode}", InstanceId, response.StatusCode);

            if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                _logger.LogWarning(">>>> [DEBUG] 401 Unauthorized detected! Clearing token and notifying UI.");
                _tokenProvider.Token = null;
                _tokenProvider.NotifySessionExpired();
            }

            return response;
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "DEBUG: AuthHandler[{InstanceId}] - HttpRequestException on {Url}", InstanceId, requestUrl);
            return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError);
        }
        catch (OperationCanceledException opEx)
        {
            _logger.LogError(opEx, "DEBUG: AuthHandler[{InstanceId}] - Request timeout on {Url}", InstanceId, requestUrl);
            return new HttpResponseMessage(System.Net.HttpStatusCode.RequestTimeout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: AuthHandler[{InstanceId}] - Unexpected CRITICAL error: {Message}", InstanceId, ex.Message);
            
            // Return a safe 500 error instead of throwing to avoid crashing the Blazor circuit
            return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
            {
                Content = new StringContent($"Critical Auth Handler Error: {ex.Message}")
            };
        }
    }
}
