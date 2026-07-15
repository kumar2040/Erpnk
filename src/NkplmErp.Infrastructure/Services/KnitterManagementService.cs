using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Infrastructure.Services;

/// <summary>
/// Knitter CRUD service. All data access goes through the parameterized
/// stored procedure sp_ManageKnitter (flags 1-6).
/// </summary>
public class KnitterManagementService : IKnitterManagementService
{
    private readonly string _connectionString;

    public KnitterManagementService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public async Task<List<KnitterManagementDto>> GetAllKnittersAsync()
    {
        var result = new List<KnitterManagementDto>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageKnitter", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag", 4);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var dto = new KnitterManagementDto();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                if (reader.IsDBNull(i)) continue;

                switch (col)
                {
                    case "cardno": dto.CardNo = ToIntSafe(reader[i]); break;
                    case "knittername": dto.KnitterName = reader[i].ToString() ?? string.Empty; break;
                    // PRSalary is free-text in some rows (e.g. 'S'), so parse leniently.
                    case "prsalary":
                        dto.PRSalary = decimal.TryParse(reader[i].ToString(), out var sal) ? sal : (decimal?)null;
                        break;
                    case "isactive": dto.IsActive = ToBoolSafe(reader[i]); break;
                    case "gauges":
                        dto.Gauges = (reader[i].ToString() ?? string.Empty)
                            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                            .ToList();
                        break;
                    case "activeassignments": dto.ActiveAssignments = ToIntSafe(reader[i]); break;
                }
            }
            result.Add(dto);
        }
        return result;
    }

    public async Task<List<string>> GetGaugeOptionsAsync()
    {
        var result = new List<string>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageKnitter", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag", 6);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
            {
                var g = reader[0].ToString();
                if (!string.IsNullOrWhiteSpace(g)) result.Add(g.Trim());
            }
        }
        return result;
    }

    public async Task<KnitterOperationResult> SaveKnitterAsync(SaveKnitterRequest request)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageKnitter", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag", request.Flag);
        cmd.Parameters.AddWithValue("@cardNo", request.CardNo > 0 ? request.CardNo : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@knitterName", string.IsNullOrWhiteSpace(request.KnitterName) ? (object)DBNull.Value : request.KnitterName.Trim());
        cmd.Parameters.AddWithValue("@prSalary", request.PRSalary.HasValue ? request.PRSalary.Value : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@isActive", request.IsActive);
        cmd.Parameters.AddWithValue("@gauges", string.Join(",", request.Gauges
            .Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim())));

        return await ReadResultAsync(cmd);
    }

    public async Task<KnitterOperationResult> DeleteKnitterAsync(int cardNo)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageKnitter", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag", 3);
        cmd.Parameters.AddWithValue("@cardNo", cardNo);
        return await ReadResultAsync(cmd);
    }

    public async Task<KnitterOperationResult> SetActiveAsync(int cardNo, bool isActive)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageKnitter", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag", 5);
        cmd.Parameters.AddWithValue("@cardNo", cardNo);
        cmd.Parameters.AddWithValue("@isActive", isActive);
        return await ReadResultAsync(cmd);
    }

    private static int ToIntSafe(object value)
    {
        if (value == null || value == DBNull.Value) return 0;
        if (value is int i) return i;
        return int.TryParse(value.ToString(), out var n) ? n : 0;
    }

    // Tolerates BIT, 0/1, and string forms ("true"/"false", "Y"/"N", "1"/"0").
    private static bool ToBoolSafe(object value)
    {
        if (value == null || value == DBNull.Value) return true;
        if (value is bool b) return b;
        var s = value.ToString()?.Trim();
        if (string.IsNullOrEmpty(s)) return true;
        if (bool.TryParse(s, out var parsed)) return parsed;
        if (int.TryParse(s, out var num)) return num != 0;
        return s.Equals("y", StringComparison.OrdinalIgnoreCase)
            || s.Equals("yes", StringComparison.OrdinalIgnoreCase)
            || s.Equals("active", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<KnitterOperationResult> ReadResultAsync(SqlCommand cmd)
    {
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new KnitterOperationResult
            {
                Result = reader["Result"] != DBNull.Value ? Convert.ToInt32(reader["Result"]) : -1,
                Message = reader["Message"]?.ToString() ?? string.Empty,
                CardNo = reader["CardNo"] != DBNull.Value ? Convert.ToInt32(reader["CardNo"]) : null
            };
        }
        return new KnitterOperationResult { Result = -1, Message = "No response from procedure." };
    }
}
