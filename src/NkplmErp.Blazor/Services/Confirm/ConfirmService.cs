namespace NkplmErp.Blazor.Services.Confirm;

/// <summary>
/// App-wide confirmation dialog, a drop-in for the browser's confirm(): <c>if (!await Confirm.ConfirmAsync("…")) return;</c>.
/// The single <c>ConfirmHost</c> in MainLayout renders it with the shared <c>ConfirmModal</c>.
///
/// Registered Scoped (one per user circuit), like LoadingService: a Singleton would pop one user's dialog on
/// everyone's screen.
/// </summary>
public class ConfirmService
{
    public event Action? OnChange;

    public bool IsVisible { get; private set; }
    public string Title { get; private set; } = "Confirm";
    public string Message { get; private set; } = string.Empty;
    public string ConfirmText { get; private set; } = "Yes";
    public string CancelText { get; private set; } = "Cancel";
    public bool Danger { get; private set; }

    private TaskCompletionSource<bool>? _pending;

    /// <summary>Asks the user; completes with true when confirmed, false when cancelled or dismissed.</summary>
    public Task<bool> ConfirmAsync(string message, string title = "Confirm", string confirmText = "Yes",
                                   bool danger = false, string cancelText = "Cancel")
    {
        // A second question while one is open replaces it (the first counts as cancelled).
        _pending?.TrySetResult(false);

        Message = message;
        Title = title;
        ConfirmText = confirmText;
        CancelText = cancelText;
        Danger = danger;
        IsVisible = true;
        _pending = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        OnChange?.Invoke();
        return _pending.Task;
    }

    public void Resolve(bool result)
    {
        IsVisible = false;
        var pending = _pending;
        _pending = null;
        OnChange?.Invoke();
        pending?.TrySetResult(result);
    }
}
