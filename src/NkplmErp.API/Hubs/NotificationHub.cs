using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace NkplmErp.API.Hubs;

/// <summary>
/// Real-time notification hub. Each connection joins a group named by the user's
/// id, so the publisher can push to a single user across all their tabs/devices
/// with Clients.Group(userId). Requires a valid JWT (sent as the access_token
/// query param for the WebSocket — see the JwtBearer OnMessageReceived).
/// </summary>
[Authorize]
public class NotificationHub : Hub
{
    public const string ReceiveMethod = "ReceiveNotification";

    private string? UserId =>
        Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? Context.User?.FindFirstValue("sub");

    public override async Task OnConnectedAsync()
    {
        var userId = UserId;
        if (!string.IsNullOrEmpty(userId))
            await Groups.AddToGroupAsync(Context.ConnectionId, userId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = UserId;
        if (!string.IsNullOrEmpty(userId))
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, userId);
        await base.OnDisconnectedAsync(exception);
    }
}
