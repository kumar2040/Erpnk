public class TokenProvider
{
    private string? _token;
    public string? Token 
    { 
        get => _token; 
        set 
        {
            if (_token != value)
            {
                _token = value;
                OnTokenChanged?.Invoke();
            }
        }
    }

    public event Action? OnTokenChanged;
    public event Action? OnSessionExpired;

    public void NotifySessionExpired()
    {
        OnSessionExpired?.Invoke();
    }
}
