using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Services.Lookup;

public interface ILookupClient
{
    Task<IEnumerable<string>> GetDistinctValuesAsync(string tableName, string columnName);
    Task<IEnumerable<LookupItemDto>> GetLookupItemsAsync(string tableName, string keyColumn, string valueColumn);
}
