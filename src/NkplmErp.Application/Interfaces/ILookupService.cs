using NkplmErp.Shared.DTOs;

namespace NkplmErp.Application.Interfaces;

public interface ILookupService
{
    Task<IEnumerable<string>> GetDistinctValuesAsync(string tableName, string columnName);
    Task<IEnumerable<LookupItemDto>> GetLookupItemsAsync(string tableName, string keyColumn, string valueColumn);
}
