namespace NkplmErp.Blazor.Services.Toast;

public class ToastService
{
    public event Action<ToastMessage>? OnShow;

    public void ShowSuccess(string message, int durationSeconds = 5)
    {
        OnShow?.Invoke(new ToastMessage { Message = message, Type = ToastType.Success, DurationSeconds = durationSeconds });
    }

    public void ShowError(string message, int durationSeconds = 5)
    {
        OnShow?.Invoke(new ToastMessage { Message = message, Type = ToastType.Error, DurationSeconds = durationSeconds });
    }

    public void ShowInfo(string message, int durationSeconds = 5)
    {
        OnShow?.Invoke(new ToastMessage { Message = message, Type = ToastType.Info, DurationSeconds = durationSeconds });
    }

    public void ShowWarning(string message, int durationSeconds = 5)
    {
        OnShow?.Invoke(new ToastMessage { Message = message, Type = ToastType.Warning, DurationSeconds = durationSeconds });
    }
}
