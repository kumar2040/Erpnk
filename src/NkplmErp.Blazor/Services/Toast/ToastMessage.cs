namespace NkplmErp.Blazor.Services.Toast;

public class ToastMessage
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Message { get; set; } = string.Empty;
    public ToastType Type { get; set; } = ToastType.Info;
    public DateTime Timestamp { get; } = DateTime.Now;
    public int DurationSeconds { get; set; } = 5;
}
