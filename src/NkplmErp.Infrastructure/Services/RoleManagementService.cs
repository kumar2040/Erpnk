using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Infrastructure.Services;

/// <summary>
/// Zero Trust Role Management Service.
/// Executes all operations via stored procedures.
/// No raw SQL — all data access is through parameterized stored procedures.
/// </summary>
public class RoleManagementService : IRoleManagementService
{
    private readonly string _connectionString;

    public RoleManagementService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    // =========================================================
    // ROLE CRUD
    // =========================================================

    public async Task<IEnumerable<AppRoleDto>> GetAllRolesAsync()
    {
        var result = new List<AppRoleDto>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageRole", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag", 4);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var role = new AppRoleDto
            {
                RoleId        = reader["RoleId"]?.ToString() ?? "",
                RoleName      = reader["RoleName"]?.ToString() ?? "",
                Description   = reader["Description"]?.ToString(),
                IsActive      = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"]),
                CreatedDate   = reader["CreatedDate"] != DBNull.Value ? Convert.ToDateTime(reader["CreatedDate"]) : DateTime.MinValue,
                UserCount     = reader["UserCount"] != DBNull.Value ? Convert.ToInt32(reader["UserCount"]) : 0,
                PageCount     = reader["PageCount"] != DBNull.Value ? Convert.ToInt32(reader["PageCount"]) : 0,
            };
            try { role.AssignedGauge = reader["AssignedGauge"]?.ToString(); } catch { /* column absent in older proc */ }
            result.Add(role);
        }
        return result;
    }

    public async Task<AppRoleDto?> GetRoleByIdAsync(string roleId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageRole", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag",   5);
        cmd.Parameters.AddWithValue("@roleId", roleId);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AppRoleDto
            {
                RoleId        = reader["RoleId"]?.ToString() ?? "",
                RoleName      = reader["RoleName"]?.ToString() ?? "",
                Description   = reader["Description"]?.ToString(),
                IsActive      = Convert.ToBoolean(reader["IsActive"]),
                CreatedDate   = Convert.ToDateTime(reader["CreatedDate"]),
            };
        }
        return null;
    }

    public async Task<RoleOperationResult> SaveRoleAsync(SaveRoleRequest request)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageRole", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag",         request.Flag);
        cmd.Parameters.AddWithValue("@roleId",       string.IsNullOrEmpty(request.RoleId) ? (object)DBNull.Value : request.RoleId);
        cmd.Parameters.AddWithValue("@roleName",     request.RoleName);
        cmd.Parameters.AddWithValue("@description",  (object?)request.Description ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@assignedGauge", string.IsNullOrWhiteSpace(request.AssignedGauge) ? (object)DBNull.Value : request.AssignedGauge.Trim());
        cmd.Parameters.AddWithValue("@isActive",     request.IsActive);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new RoleOperationResult
            {
                Result  = Convert.ToInt32(reader["Result"]),
                Message = reader["Message"]?.ToString() ?? ""
            };
        }
        return new RoleOperationResult { Result = -1, Message = "No response from procedure." };
    }

    // =========================================================
    // PAGE REGISTRY
    // =========================================================

    public async Task<IEnumerable<AppPageDto>> GetAllPagesAsync()
    {
        var result = new List<AppPageDto>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManagePage", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag", 3);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new AppPageDto
            {
                AppPageId    = Convert.ToInt32(reader["AppPageId"]),
                PageKey      = reader["PageKey"]?.ToString() ?? "",
                PageName     = reader["PageName"]?.ToString() ?? "",
                PageUrl      = reader["PageUrl"]?.ToString(),
                IsActive     = Convert.ToBoolean(reader["IsActive"]),
                DisplayOrder = reader["DisplayOrder"] != DBNull.Value ? Convert.ToInt32(reader["DisplayOrder"]) : 0,
            });
        }
        return result;
    }

    public async Task<string?> GetUserLandingPageAsync(string userId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_GetUserLandingPage", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@userId", userId);
        var result = await cmd.ExecuteScalarAsync();
        return result == null || result == DBNull.Value ? null : result.ToString();
    }

    public async Task<AppPageDto?> GetPageByIdAsync(int appPageId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManagePage", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag", 5);
        cmd.Parameters.AddWithValue("@appPageId", appPageId);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new AppPageDto
            {
                AppPageId    = Convert.ToInt32(reader["AppPageId"]),
                PageKey      = reader["PageKey"]?.ToString() ?? "",
                PageName     = reader["PageName"]?.ToString() ?? "",
                PageUrl      = reader["PageUrl"]?.ToString(),
                IsActive     = Convert.ToBoolean(reader["IsActive"]),
                DisplayOrder = reader["DisplayOrder"] != DBNull.Value ? Convert.ToInt32(reader["DisplayOrder"]) : 0,
            };
        }
        return null;
    }

    public async Task<RoleOperationResult> SavePageAsync(SavePageRequest request)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManagePage", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag",         request.Flag);
        cmd.Parameters.AddWithValue("@appPageId",    request.AppPageId);
        cmd.Parameters.AddWithValue("@pageKey",      (object?)request.PageKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pageName",     (object?)request.PageName ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@pageUrl",      string.IsNullOrWhiteSpace(request.PageUrl) ? (object)DBNull.Value : request.PageUrl.Trim());
        cmd.Parameters.AddWithValue("@isActive",     request.IsActive);
        cmd.Parameters.AddWithValue("@displayOrder", request.DisplayOrder);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return new RoleOperationResult { Result = Convert.ToInt32(reader["Result"]), Message = reader["Message"]?.ToString() ?? "" };
        return new RoleOperationResult { Result = -1, Message = "No response from procedure." };
    }

    public async Task<RoleOperationResult> DeletePageAsync(int appPageId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManagePage", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag", 4);
        cmd.Parameters.AddWithValue("@appPageId", appPageId);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return new RoleOperationResult { Result = Convert.ToInt32(reader["Result"]), Message = reader["Message"]?.ToString() ?? "" };
        return new RoleOperationResult { Result = -1, Message = "No response." };
    }

    // =========================================================
    // PERMISSIONS
    // =========================================================

    public async Task<IEnumerable<RolePagePermissionDto>> GetPermissionsByRoleAsync(string roleId)
    {
        var result = new List<RolePagePermissionDto>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageRolePermission", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag",   2);
        cmd.Parameters.AddWithValue("@roleId", roleId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new RolePagePermissionDto
            {
                AppPageId = Convert.ToInt32(reader["AppPageId"]),
                PageKey   = reader["PageKey"]?.ToString() ?? "",
                PageName  = reader["PageName"]?.ToString() ?? "",
                CanView   = reader["CanView"]   != DBNull.Value && Convert.ToBoolean(reader["CanView"]),
                CanEdit   = reader["CanEdit"]   != DBNull.Value && Convert.ToBoolean(reader["CanEdit"]),
                CanDelete = reader["CanDelete"] != DBNull.Value && Convert.ToBoolean(reader["CanDelete"]),
            });
        }
        return result;
    }

    public async Task<RoleOperationResult> SavePermissionAsync(SavePermissionRequest request)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageRolePermission", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag",      1);
        cmd.Parameters.AddWithValue("@roleId",    request.RoleId);
        cmd.Parameters.AddWithValue("@appPageId", request.AppPageId);
        cmd.Parameters.AddWithValue("@canView",   request.CanView);
        cmd.Parameters.AddWithValue("@canEdit",   request.CanEdit);
        cmd.Parameters.AddWithValue("@canDelete", request.CanDelete);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return new RoleOperationResult { Result = Convert.ToInt32(reader["Result"]), Message = reader["Message"]?.ToString() ?? "" };
        return new RoleOperationResult { Result = -1, Message = "No response." };
    }

    public async Task<RoleOperationResult> ClearPermissionsByRoleAsync(string roleId)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageRolePermission", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag",   3);
        cmd.Parameters.AddWithValue("@roleId", roleId);
        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return new RoleOperationResult { Result = Convert.ToInt32(reader["Result"]), Message = reader["Message"]?.ToString() ?? "" };
        return new RoleOperationResult { Result = -1, Message = "No response." };
    }

    // =========================================================
    // USER-ROLE ASSIGNMENT
    // =========================================================

    public async Task<IEnumerable<UserWithRolesDto>> GetAllUsersWithRolesAsync()
    {
        var result = new List<UserWithRolesDto>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_AssignUserRole", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag", 4);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new UserWithRolesDto
            {
                UserId        = reader["UserId"]?.ToString() ?? "",
                Email         = reader["Email"]?.ToString() ?? "",
                FullName      = reader["FullName"]?.ToString() ?? "",
                RoleId        = reader["RoleId"] != DBNull.Value ? reader["RoleId"].ToString() : null,
                RoleName      = reader["RoleName"]?.ToString(),
                AssignedGauge = reader["AssignedGauge"]?.ToString(),
                AssignedDate  = reader["AssignedDate"] != DBNull.Value ? Convert.ToDateTime(reader["AssignedDate"]) : null,
            });
        }
        return result;
    }

    public async Task<IEnumerable<UserRoleDto>> GetRolesByUserAsync(string userId)
    {
        var result = new List<UserRoleDto>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_AssignUserRole", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@flag",   3);
        cmd.Parameters.AddWithValue("@userId", userId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new UserRoleDto
            {
                UserRoleId    = Convert.ToInt32(reader["UserRoleId"]),
                UserId        = reader["UserId"]?.ToString() ?? "",
                RoleId        = reader["RoleId"]?.ToString() ?? "",
                RoleName      = reader["RoleName"]?.ToString() ?? "",
                Description   = reader["Description"]?.ToString(),
                AssignedGauge = reader["AssignedGauge"]?.ToString(),
                AssignedDate  = Convert.ToDateTime(reader["AssignedDate"]),
            });
        }
        return result;
    }

    public async Task<RoleOperationResult> AssignUserRoleAsync(AssignUserRoleRequest request, string assignedByUserId)
    {
        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var cmd = new SqlCommand("sp_AssignUserRole", connection) { CommandType = CommandType.StoredProcedure };
            cmd.Parameters.AddWithValue("@flag",       request.Flag);
            cmd.Parameters.AddWithValue("@userId",     request.UserId);
            cmd.Parameters.AddWithValue("@roleId",     request.RoleId);
            cmd.Parameters.AddWithValue("@assignedBy", assignedByUserId);

            // Scope set as a table-valued parameter (dbo.UserScopeList).
            var scopeTable = new DataTable();
            scopeTable.Columns.Add("KnitType",   typeof(string));
            scopeTable.Columns.Add("GaugeValue", typeof(string));
            foreach (var s in request.Scopes ?? new List<ScopeEntry>())
            {
                if (string.IsNullOrWhiteSpace(s.KnitType)) continue;
                scopeTable.Rows.Add(s.KnitType.Trim(), (s.GaugeValue ?? string.Empty).Trim());
            }
            var scopeParam = cmd.Parameters.AddWithValue("@scopes", scopeTable);
            scopeParam.SqlDbType = SqlDbType.Structured;
            scopeParam.TypeName  = "dbo.UserScopeList";
            using var reader = await cmd.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return new RoleOperationResult { Result = Convert.ToInt32(reader["Result"]), Message = reader["Message"]?.ToString() ?? "" };
            return new RoleOperationResult { Result = -1, Message = "No response from sp_AssignUserRole." };
        }
        catch (Exception ex)
        {
            // Surface the real DB error (e.g. missing dbo.UserScopeList type / old proc signature)
            // instead of failing silently.
            return new RoleOperationResult { Result = -1, Message = "DB error: " + ex.Message };
        }
    }

    // =========================================================
    // ZERO TRUST: Get all permissions for a user (called by API)
    // =========================================================

    public async Task<UserPermissionsResponse> GetUserPermissionsAsync(string userId)
    {
        var response = new UserPermissionsResponse { UserId = userId };
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_GetUserPermissions", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@userId", userId);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var perm = new UserPermissionDto
            {
                PageKey       = reader["PageKey"]?.ToString() ?? "",
                PageName      = reader["PageName"]?.ToString() ?? "",
                CanView       = reader["CanView"]   != DBNull.Value && Convert.ToBoolean(reader["CanView"]),
                CanEdit       = reader["CanEdit"]   != DBNull.Value && Convert.ToBoolean(reader["CanEdit"]),
                CanDelete     = reader["CanDelete"] != DBNull.Value && Convert.ToBoolean(reader["CanDelete"]),
                AssignedGauge = reader["AssignedGauge"]?.ToString(),
            };
            response.Permissions.Add(perm);

            // Set gauge restriction once (same for all rows)
            if (response.AssignedGauge == null && perm.AssignedGauge != null)
                response.AssignedGauge = perm.AssignedGauge;
        }
        return response;
    }
}
