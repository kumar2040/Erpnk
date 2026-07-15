using Microsoft.AspNetCore.SignalR;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.API.Hubs;

/// <summary>SignalR-backed <see cref="INotificationPublisher"/> — pushes to the user's group.</summary>
public class SignalRNotificationPublisher : INotificationPublisher
{
    private readonly IHubContext<NotificationHub> _hub;
    private readonly ILogger<SignalRNotificationPublisher> _logger;

    public SignalRNotificationPublisher(IHubContext<NotificationHub> hub, ILogger<SignalRNotificationPublisher> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public async Task PushAsync(string userId, PoTaskNotificationDto payload)
    {
        try
        {
            await _hub.Clients.Group(userId).SendAsync(NotificationHub.ReceiveMethod, payload);
        }
        catch (Exception ex)
        {
            // Best-effort: the notification is already persisted; a failed push just means
            // the user sees it on next poll / page load instead of instantly.
            _logger.LogWarning(ex, "SignalR push failed for user {UserId}", userId);
        }
    }
}
