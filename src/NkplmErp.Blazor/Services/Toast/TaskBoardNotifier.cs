namespace NkplmErp.Blazor.Services.Toast;

/// <summary>
/// Lightweight relay: NotificationBell fires OnNewTaskAsync when a task notification
/// arrives via SignalR; the /tasks page subscribes and refreshes + highlights.
/// Scoped (one per circuit), so the bell and the page share the same instance.
/// </summary>
public class TaskBoardNotifier
{
    /// <summary>Fires with the PoTaskId of the newly created/assigned task.</summary>
    public event Func<int, Task>? OnNewTaskAsync;

    public async Task NotifyNewTaskAsync(int poTaskId)
    {
        var handlers = OnNewTaskAsync;
        if (handlers is null) return;

        foreach (var handler in handlers.GetInvocationList().Cast<Func<int, Task>>())
        {
            try
            {
                await handler(poTaskId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[TaskBoardNotifier] Error in handler: {ex.Message}");
            }
        }
    }
}
