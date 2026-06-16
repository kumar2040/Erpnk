using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.API.Controllers;

/// <summary>
/// Zero Trust Role Management Controller.
/// ALL endpoints require [Authorize].
/// Admin-only operations additionally verify the caller has RoleManagement permissions.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class RoleManagementController(IRoleManagementService roleManagementService) : ControllerBase
{
    private readonly IRoleManagementService _roleService = roleManagementService;

    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("User identity not found in token.");

    // =========================================================
    // PERMISSION CHECK — Zero Trust gate
    // Called by Blazor on every page load after login
    // =========================================================

    /// <summary>
    /// Returns all page permissions for the currently authenticated user.
    /// The client caches this on login. The API re-checks on every sensitive operation.
    /// </summary>
    [HttpGet("my-permissions")]
    public async Task<IActionResult> GetMyPermissions()
    {
        var userId = GetCurrentUserId();
        var permissions = await _roleService.GetUserPermissionsAsync(userId);
        return Ok(permissions);
    }

    // =========================================================
    // ROLES
    // =========================================================

    [HttpGet("roles")]
    public async Task<IActionResult> GetAllRoles()
    {
        var result = await _roleService.GetAllRolesAsync();
        return Ok(result);
    }

    [HttpGet("roles/{id}")]
    public async Task<IActionResult> GetRoleById(string id)
    {
        var result = await _roleService.GetRoleByIdAsync(id);
        return result == null ? NotFound() : Ok(result);
    }

    [HttpPost("roles")]
    public async Task<IActionResult> SaveRole([FromBody] SaveRoleRequest request)
    {
        // Zero Trust: verify caller has permission to manage roles
        var userId = GetCurrentUserId();
        var callerPerms = await _roleService.GetUserPermissionsAsync(userId);
        if (!callerPerms.CanEdit("RoleManagement"))
            return Forbid();

        var result = await _roleService.SaveRoleAsync(request);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("roles/{id}")]
    public async Task<IActionResult> DeleteRole(string id)
    {
        var userId = GetCurrentUserId();
        var callerPerms = await _roleService.GetUserPermissionsAsync(userId);
        if (!callerPerms.CanDelete("RoleManagement"))
            return Forbid();

        var result = await _roleService.SaveRoleAsync(new SaveRoleRequest { RoleId = id, Flag = 3, RoleName = "" });
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // =========================================================
    // PAGES
    // =========================================================

    [HttpGet("pages")]
    public async Task<IActionResult> GetAllPages()
    {
        var result = await _roleService.GetAllPagesAsync();
        return Ok(result);
    }

    // =========================================================
    // ROLE PERMISSIONS
    // =========================================================

    [HttpGet("roles/{roleId}/permissions")]
    public async Task<IActionResult> GetPermissionsByRole(string roleId)
    {
        var result = await _roleService.GetPermissionsByRoleAsync(roleId);
        return Ok(result);
    }

    [HttpPost("permissions")]
    public async Task<IActionResult> SavePermission([FromBody] SavePermissionRequest request)
    {
        var userId = GetCurrentUserId();
        var callerPerms = await _roleService.GetUserPermissionsAsync(userId);
        if (!callerPerms.CanEdit("RoleManagement"))
            return Forbid();

        var result = await _roleService.SavePermissionAsync(request);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    [HttpPost("permissions/save-all")]
    public async Task<IActionResult> SaveAllPermissions([FromBody] List<SavePermissionRequest> permissions)
    {
        var userId = GetCurrentUserId();
        var callerPerms = await _roleService.GetUserPermissionsAsync(userId);
        if (!callerPerms.CanEdit("RoleManagement"))
            return Forbid();

        foreach (var perm in permissions)
        {
            var result = await _roleService.SavePermissionAsync(perm);
            if (!result.IsSuccess)
                return BadRequest(new { Message = $"Failed saving permission for page {perm.AppPageId}: {result.Message}" });
        }
        return Ok(new { Message = "All permissions saved successfully." });
    }

    // =========================================================
    // USER-ROLE ASSIGNMENT
    // =========================================================

    [HttpGet("users-with-roles")]
    public async Task<IActionResult> GetAllUsersWithRoles()
    {
        var result = await _roleService.GetAllUsersWithRolesAsync();
        return Ok(result);
    }

    [HttpGet("users/{userId}/roles")]
    public async Task<IActionResult> GetRolesByUser(string userId)
    {
        var result = await _roleService.GetRolesByUserAsync(userId);
        return Ok(result);
    }

    [HttpPost("users/assign-role")]
    public async Task<IActionResult> AssignUserRole([FromBody] AssignUserRoleRequest request)
    {
        var assignedByUserId = GetCurrentUserId();
        var callerPerms = await _roleService.GetUserPermissionsAsync(assignedByUserId);
        if (!callerPerms.CanEdit("RoleManagement"))
            return Forbid();

        var result = await _roleService.AssignUserRoleAsync(request, assignedByUserId);
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }
}
