namespace NkplmErp.Blazor.Services.Users;

public class UsersApiClient
{
    public HttpClient Client { get; }
    
    public UsersApiClient(HttpClient client)
    {
        Client = client;
    }
}
