using System.Net.Http.Json;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Services.Lookup;

public class LookupClient(HttpClient httpClient) : ILookupClient
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<IEnumerable<string>> GetDistinctValuesAsync(string tableName, string columnName)
    {
        var response = await _httpClient.GetAsync($"api/v1/Lookup/distinct?tableName={tableName}&columnName={columnName}");
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<IEnumerable<string>>() ?? Enumerable.Empty<string>();
        }
        
        return Enumerable.Empty<string>();
    }

    public async Task<IEnumerable<LookupItemDto>> GetLookupItemsAsync(string tableName, string keyColumn, string valueColumn)
    {
        var response = await _httpClient.GetAsync($"api/v1/Lookup/items?tableName={tableName}&keyColumn={keyColumn}&valueColumn={valueColumn}");
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<IEnumerable<LookupItemDto>>() ?? Enumerable.Empty<LookupItemDto>();
        }
        
        return Enumerable.Empty<LookupItemDto>();
    }
}
