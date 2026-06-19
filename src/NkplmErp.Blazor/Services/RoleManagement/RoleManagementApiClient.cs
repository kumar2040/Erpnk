using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Services.RoleManagement;

/// <summary>
/// Blazor HTTP client proxy for the RoleManagement API.
/// All calls include the JWT token via AuthenticationDelegatingHandler.
/// </summary>
public class RoleManagementApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RoleManagementApiClient> _logger;
    private const string Base = "api/v1/RoleManagement";

    public RoleManagementApiClient(HttpClient httpClient, ILogger<RoleManagementApiClient> logger)
    {
        _httpClient = httpClient;
        _logger     = logger;
    }

    // ===== Permissions =====

    public async Task<UserPermissionsResponse?> GetMyPermissionsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/my-permissions");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<UserPermissionsResponse>();
            _logger.LogWarning("GetMyPermissions returned {StatusCode}", response.StatusCode);
            return null;
        }
        catch (Exception ex) { _logger.LogError(ex, "GetMyPermissionsAsync failed"); return null; }
    }

    public async Task<string?> GetMyLandingAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/my-landing");
            if (response.IsSuccessStatusCode)
            {
                var doc = await response.Content.ReadFromJsonAsync<LandingResponse>();
                return doc?.Url;
            }
            return null;
        }
        catch (Exception ex) { _logger.LogError(ex, "GetMyLandingAsync failed"); return null; }
    }

    private sealed class LandingResponse { public string? Url { get; set; } }

    // ===== Roles =====

    public async Task<List<AppRoleDto>> GetAllRolesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/roles");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<AppRoleDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAllRolesAsync failed"); return new(); }
    }

    public async Task<RoleOperationResult?> SaveRoleAsync(SaveRoleRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/roles", request);
            return await response.Content.ReadFromJsonAsync<RoleOperationResult>();
        }
        catch (Exception ex) { _logger.LogError(ex, "SaveRoleAsync failed"); return null; }
    }

    public async Task<RoleOperationResult?> DeleteRoleAsync(string roleId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{Base}/roles/{roleId}");
            return await response.Content.ReadFromJsonAsync<RoleOperationResult>();
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteRoleAsync failed"); return null; }
    }

    // ===== Pages =====

    public async Task<List<AppPageDto>> GetAllPagesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/pages");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<AppPageDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAllPagesAsync failed"); return new(); }
    }

    public async Task<RoleOperationResult?> SavePageAsync(SavePageRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/pages", request);
            return await response.Content.ReadFromJsonAsync<RoleOperationResult>();
        }
        catch (Exception ex) { _logger.LogError(ex, "SavePageAsync failed"); return null; }
    }

    public async Task<RoleOperationResult?> DeletePageAsync(int appPageId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{Base}/pages/{appPageId}");
            return await response.Content.ReadFromJsonAsync<RoleOperationResult>();
        }
        catch (Exception ex) { _logger.LogError(ex, "DeletePageAsync failed"); return null; }
    }

    // ===== Role Permissions =====

    public async Task<List<RolePagePermissionDto>> GetPermissionsByRoleAsync(string roleId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/roles/{roleId}/permissions");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<RolePagePermissionDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetPermissionsByRoleAsync failed"); return new(); }
    }

    public async Task<bool> SaveAllPermissionsAsync(List<SavePermissionRequest> permissions)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/permissions/save-all", permissions);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { _logger.LogError(ex, "SaveAllPermissionsAsync failed"); return false; }
    }

    // ===== Users with Roles =====

    public async Task<List<UserWithRolesDto>> GetAllUsersWithRolesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/users-with-roles");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<UserWithRolesDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAllUsersWithRolesAsync failed"); return new(); }
    }

    public async Task<List<UserRoleDto>> GetRolesByUserAsync(string userId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/users/{userId}/roles");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<UserRoleDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetRolesByUserAsync failed"); return new(); }
    }

    public async Task<RoleOperationResult?> AssignUserRoleAsync(AssignUserRoleRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/users/assign-role", request);
            return await response.Content.ReadFromJsonAsync<RoleOperationResult>();
        }
        catch (Exception ex) { _logger.LogError(ex, "AssignUserRoleAsync failed"); return null; }
    }
}
