using NkplmErp.Shared.DTOs;

namespace NkplmErp.Application.Interfaces;

/// <summary>
/// Zero Trust Role Management Service Interface.
/// All methods enforce that the calling user has appropriate access.
/// </summary>
public interface IRoleManagementService
{
    // Role CRUD
    Task<IEnumerable<AppRoleDto>> GetAllRolesAsync();
    Task<AppRoleDto?> GetRoleByIdAsync(string roleId);
    Task<RoleOperationResult> SaveRoleAsync(SaveRoleRequest request);

    // Page registry
    Task<IEnumerable<AppPageDto>> GetAllPagesAsync();

    // Role permissions (per-page View/Edit/Delete flags)
    Task<IEnumerable<RolePagePermissionDto>> GetPermissionsByRoleAsync(string roleId);
    Task<RoleOperationResult> SavePermissionAsync(SavePermissionRequest request);
    Task<RoleOperationResult> ClearPermissionsByRoleAsync(string roleId);

    // User-role assignment
    Task<IEnumerable<UserWithRolesDto>> GetAllUsersWithRolesAsync();
    Task<IEnumerable<UserRoleDto>> GetRolesByUserAsync(string userId);
    Task<RoleOperationResult> AssignUserRoleAsync(AssignUserRoleRequest request, string assignedByUserId);

    // Zero Trust: called by API middleware to verify access
    Task<UserPermissionsResponse> GetUserPermissionsAsync(string userId);
}
