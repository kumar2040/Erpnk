using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NkplmErp.Application.Interfaces;
using NkplmErp.Infrastructure.Persistence;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Infrastructure.Services;

public class LookupService(ApplicationDbContext context) : ILookupService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<IEnumerable<string>> GetDistinctValuesAsync(string tableName, string columnName)
    {
        // 1. Strict validation against system tables to prevent SQL Injection
        // We only allow alphanumeric characters and underscores for table and column names
        if (!IsValidIdentifier(tableName) || !IsValidIdentifier(columnName))
        {
            throw new ArgumentException("Invalid table or column name.");
        }

        // 2. Further validation: Check if table and column actually exist in the database
        var exists = await CheckExistsAsync(tableName, columnName);
        if (!exists)
        {
            return Enumerable.Empty<string>();
        }

        // 3. Since we cannot parameterize table/column names in T-SQL directly for SELECT DISTINCT,
        // and we have validated the identifiers against sys.tables/sys.columns, it is safe to construct the query.
        // We use Interpolated string but because table/column names are not values, we must be careful.
        // However, SqlQueryRaw is needed for dynamic schema elements.
        
        var query = $"SELECT DISTINCT CAST([{columnName}] AS NVARCHAR(MAX)) AS [Value] FROM [{tableName}] WHERE [{columnName}] IS NOT NULL ORDER BY [Value]";
        
        // Use a simple DTO or just fetch as strings if possible. 
        // EF Core 8+ SqlQueryRaw<string> works for single column results.
        var result = await _context.Database
            .SqlQueryRaw<string>(query)
            .ToListAsync();

        return result;
    }

    public async Task<IEnumerable<LookupItemDto>> GetLookupItemsAsync(string tableName, string keyColumn, string valueColumn)
    {
        if (!IsValidIdentifier(tableName) || !IsValidIdentifier(keyColumn) || !IsValidIdentifier(valueColumn))
        {
            throw new ArgumentException("Invalid table or column names.");
        }

        var query = $"SELECT DISTINCT CAST([{keyColumn}] AS NVARCHAR(MAX)) as [Key], CAST([{valueColumn}] AS NVARCHAR(MAX)) as [Value] FROM [{tableName}] WHERE [{keyColumn}] IS NOT NULL ORDER BY [Value]";
        
        var result = await _context.Database
            .SqlQueryRaw<LookupItemDto>(query)
            .ToListAsync();

        return result;
    }

    private static bool IsValidIdentifier(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        // Basic Regex-like check: only alphanumeric and underscore, doesn't start with number
        if (!char.IsLetter(name[0]) && name[0] != '_') return false;
        foreach (var c in name)
        {
            if (!char.IsLetterOrDigit(c) && c != '_') return false;
        }
        return true;
    }

    private async Task<bool> CheckExistsAsync(string tableName, string columnName)
    {
        var tableParam = new SqlParameter("@TableName", tableName);
        var columnParam = new SqlParameter("@ColumnName", columnName);

        var exists = await _context.Database
            .SqlQueryRaw<int>("SELECT CAST(COUNT(*) AS INT) AS [Value] FROM sys.columns c JOIN sys.tables t ON c.object_id = t.object_id WHERE t.name = @TableName AND c.name = @ColumnName", 
                tableParam, columnParam)
            .FirstOrDefaultAsync();

        return exists > 0;
    }
}
