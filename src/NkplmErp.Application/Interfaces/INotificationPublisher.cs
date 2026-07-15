using NkplmErp.Shared.DTOs;

namespace NkplmErp.Application.Interfaces;

/// <summary>
/// Pushes an in-app notification to a connected user in real time. The concrete
/// implementation lives in the API (SignalR hub); the service layer depends only
/// on this abstraction. A no-op implementation is fine when push isn't wired —
/// notifications still persist and show on the bell via the normal reads.
/// </summary>
public interface INotificationPublisher
{
    Task PushAsync(string userId, PoTaskNotificationDto payload);
}
