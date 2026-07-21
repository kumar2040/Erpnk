using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Shared.Http
{
    // One place for every UI -> API call. Managers use this instead of holding
    // their own HttpClient, so envelope handling, auth failures and error toasts
    // are written once.
    public interface IHttpServices
    {
        Task<IResponse<T>> GetAsync<T>(string url);
        Task<IResponse<T>> PostAsJsonAsync<T>(string url, object data);
        Task<IResponse<T>> DeleteAsync<T>(string url);
    }
}
