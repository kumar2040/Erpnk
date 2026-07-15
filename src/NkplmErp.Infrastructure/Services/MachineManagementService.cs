using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Infrastructure.Services;

/// <summary>
/// Knit machine CRUD service. All data access goes through the parameterized
/// stored procedure sp_ManageMachine (flags 1-6).
/// </summary>
public class MachineManagementService : IMachineManagementService
{
    private readonly string _connectionString;

    public MachineManagementService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public async Task<List<MachineManagementDto>> GetAllMachinesAsync()
    {
        var result = new List<MachineManagementDto>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageMachine", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag", 4);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var dto = new MachineManagementDto();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                if (reader.IsDBNull(i)) continue;

                switch (col)
                {
                    case "machineid": dto.MachineId = ToIntSafe(reader[i]); break;
                    case "machineno": dto.MachineNo = reader[i].ToString() ?? string.Empty; break;
                    case "gauge": dto.Gauge = reader[i].ToString(); break;
                    case "size": dto.Size = reader[i].ToString(); break;
                    case "isactive": dto.IsActive = ToBoolSafe(reader[i]); break;
                    case "activeplans": dto.ActivePlans = ToIntSafe(reader[i]); break;
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
        using var cmd = new SqlCommand("sp_ManageMachine", connection) { CommandType = CommandType.StoredProcedure };
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

    public async Task<MachineOperationResult> SaveMachineAsync(SaveMachineRequest request)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageMachine", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag", request.Flag);
        cmd.Parameters.AddWithValue("@machineId", request.MachineId > 0 ? request.MachineId : (object)DBNull.Value);
        cmd.Parameters.AddWithValue("@machineNo", string.IsNullOrWhiteSpace(request.MachineNo) ? (object)DBNull.Value : request.MachineNo.Trim());
        cmd.Parameters.AddWithValue("@gauge", string.IsNullOrWhiteSpace(request.Gauge) ? (object)DBNull.Value : request.Gauge.Trim());
        cmd.Parameters.AddWithValue("@size", string.IsNullOrWhiteSpace(request.Size) ? (object)DBNull.Value : request.Size.Trim());
        cmd.Parameters.AddWithValue("@isActive", request.IsActive);

        return await ReadResultAsync(cmd);
    }

    public async Task<MachineOperationResult> DeleteMachineAsync(int machineId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageMachine", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag", 3);
        cmd.Parameters.AddWithValue("@machineId", machineId);
        return await ReadResultAsync(cmd);
    }

    public async Task<MachineOperationResult> SetActiveAsync(int machineId, bool isActive)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageMachine", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag", 5);
        cmd.Parameters.AddWithValue("@machineId", machineId);
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

    private static async Task<MachineOperationResult> ReadResultAsync(SqlCommand cmd)
    {
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new MachineOperationResult
            {
                Result = reader["Result"] != DBNull.Value ? Convert.ToInt32(reader["Result"]) : -1,
                Message = reader["Message"]?.ToString() ?? string.Empty,
                MachineId = reader["MachineId"] != DBNull.Value ? Convert.ToInt32(reader["MachineId"]) : null
            };
        }
        return new MachineOperationResult { Result = -1, Message = "No response from procedure." };
    }
}
