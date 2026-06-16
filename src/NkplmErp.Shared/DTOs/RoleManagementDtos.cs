namespace NkplmErp.Shared.DTOs;

// ===== Role DTOs =====

public class AppRoleDto
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AssignedGauge { get; set; }   // NULL = unrestricted; value = gauge-scoped role
    public bool IsActive { get; set; } = true;
    public DateTime CreatedDate { get; set; }
    public int UserCount { get; set; }
    public int PageCount { get; set; }
}

public class SaveRoleRequest
{
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AssignedGauge { get; set; }   // optional gauge restriction for the role
    public bool IsActive { get; set; } = true;
    public int Flag { get; set; }  // 1=Insert, 2=Update, 3=Delete
}

// ===== Page DTOs =====

public class AppPageDto
{
    public int AppPageId { get; set; }
    public string PageKey { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public string? PageUrl { get; set; }
    public bool IsActive { get; set; } = true;
}

// ===== Permission DTOs =====

public class RolePagePermissionDto
{
    public int AppPageId { get; set; }
    public string PageKey { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public class SavePermissionRequest
{
    public string RoleId { get; set; } = string.Empty;
    public int AppPageId { get; set; }
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

// ===== User-Role DTOs =====

public class UserRoleDto
{
    public int UserRoleId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? AssignedGauge { get; set; }
    public DateTime AssignedDate { get; set; }
}

public class UserWithRolesDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? RoleId { get; set; }
    public string? RoleName { get; set; }
    public string? AssignedGauge { get; set; }
    public DateTime? AssignedDate { get; set; }
}

public class AssignUserRoleRequest
{
    public string UserId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public int Flag { get; set; }  // 1=Assign, 2=Remove
}

// ===== User Permissions (Zero Trust cache) =====

/// <summary>
/// Returned by sp_GetUserPermissions — cached client-side on login.
/// Enforced server-side on every API call.
/// </summary>
public class UserPermissionDto
{
    public string PageKey { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public bool CanView { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public string? AssignedGauge { get; set; }  // data-level filter
}

public class UserPermissionsResponse
{
    public string UserId { get; set; } = string.Empty;
    public string? AssignedGauge { get; set; }
    public List<UserPermissionDto> Permissions { get; set; } = new();

    public bool CanView(string pageKey) =>
        Permissions.Any(p => string.Equals(p.PageKey, pageKey, StringComparison.OrdinalIgnoreCase) && p.CanView);

    public bool CanEdit(string pageKey) =>
        Permissions.Any(p => string.Equals(p.PageKey, pageKey, StringComparison.OrdinalIgnoreCase) && p.CanEdit);

    public bool CanDelete(string pageKey) =>
        Permissions.Any(p => string.Equals(p.PageKey, pageKey, StringComparison.OrdinalIgnoreCase) && p.CanDelete);
}

public class RoleOperationResult
{
    public int Result { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsSuccess => Result > 0;
}
