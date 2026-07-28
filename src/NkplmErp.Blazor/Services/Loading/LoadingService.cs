namespace NkplmErp.Blazor.Services.Loading;

/// <summary>
/// App-wide loading veil. Inject it anywhere and call <see cref="Show"/> / <see cref="Hide"/>;
/// the single <c>LoadingOverlay</c> in MainLayout renders whatever this reports.
///
/// Registered Scoped, which in Blazor Server is one instance per user circuit. A Singleton
/// here would put one user's spinner on every other user's screen.
/// </summary>
public class LoadingService
{
    public event Action? OnChange;

    public bool IsVisible { get; private set; }
    public string? Message { get; private set; }

    // Counted rather than a plain bool: with two overlapping loads, the inner Hide() would
    // otherwise lift the veil while the outer one is still running.
    private int _depth;

    public void Show(string? message = null)
    {
        _depth++;
        Message = message;
        IsVisible = true;
        OnChange?.Invoke();
    }

    public void Hide()
    {
        if (_depth > 0) _depth--;
        if (_depth > 0) return;          // an outer Show() is still open
        IsVisible = false;
        Message = null;
        OnChange?.Invoke();
    }

    /// <summary>
    /// Drops the veil regardless of depth. For navigation or error paths where a matching
    /// Hide() may never run and the user would otherwise be stuck behind a spinner.
    /// </summary>
    public void Reset()
    {
        _depth = 0;
        IsVisible = false;
        Message = null;
        OnChange?.Invoke();
    }
}
