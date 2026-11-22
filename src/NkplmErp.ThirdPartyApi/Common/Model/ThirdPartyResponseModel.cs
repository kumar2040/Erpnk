namespace NkplmErp.ThirdPartyApi.Common.Model;

/// <summary>Success envelope returned by the third-party API: { success, route, data }. Data is the payload itself.</summary>
public class ThirdPartyResponseModel<T>
{
    public bool Success { get; set; }
    public string Route { get; set; } = string.Empty;
    public T? Data { get; set; }
}
