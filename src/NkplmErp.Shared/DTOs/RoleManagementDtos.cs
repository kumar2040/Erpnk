namespace NkplmErp.Shared.DTOs;

/// <summary>
/// One scope entry: a department (KnitType: knit/weave/silk/linen/other) plus an
/// optional specific value within it (GaugeValue). GaugeValue null/blank = the whole
/// department. Examples: (weave, "Gyatri Pashmina"), (silk, "t1"), (knit, null).
/// </summary>
public class ScopeEntry
{
    public string KnitType { get; set; } = string.Empty;
    public string? GaugeValue { get; set; }

    public string DisplayLabel =>
        string.IsNullOrWhiteSpace(GaugeValue) ? $"{KnitType} (all)" : $"{KnitType}: {GaugeValue}";
}

/// <summary>
/// Wire codec for a scope set. Encoded as ';'-separated entries, each "KnitType|GaugeValue"
/// (GaugeValue empty = whole department). A null/empty encoded string = unrestricted.
/// </summary>
public static class ScopeSet
{
    public static List<ScopeEntry> Parse(string? encoded)
    {
        var result = new List<ScopeEntry>();
        if (string.IsNullOrWhiteSpace(encoded)) return result;

        foreach (var raw in encoded.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = raw.Split('|', 2);
            var knitType = parts[0].Trim();
            if (knitType.Length == 0) continue;
            var value = parts.Length > 1 ? parts[1].Trim() : "";
            result.Add(new ScopeEntry { KnitType = knitType, GaugeValue = value.Length == 0 ? null : value });
        }
        return result;
    }

    public static string? Encode(IEnumerable<ScopeEntry>? scopes)
    {
        if (scopes == null) return null;
        var parts = scopes
            .Where(s => !string.IsNullOrWhiteSpace(s.KnitType))
            .Select(s => $"{s.KnitType.Trim()}|{(s.GaugeValue ?? string.Empty).Trim()}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        return parts.Count == 0 ? null : string.Join(";", parts);
    }
}

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
    public int DisplayOrder { get; set; }
    public string? Icon { get; set; }          // Font Awesome class, e.g. "fa-solid fa-box"
    public int? MenuId { get; set; }           // identity.Menu.Id this page nests under; null = ungrouped
    public string? MenuTitle { get; set; }
}

public class SavePageRequest
{
    public int AppPageId { get; set; }
    public string PageKey { get; set; } = string.Empty;
    public string PageName { get; set; } = string.Empty;
    public string? PageUrl { get; set; }
    public bool IsActive { get; set; } = true;
    public int DisplayOrder { get; set; }
    public string? Icon { get; set; }
    public int? MenuId { get; set; }
    public int Flag { get; set; }  // 1=Insert, 2=Update
}

// ===== Menu DTOs =====

public class MenuDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
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
    public string? AssignedGauge { get; set; }   // encoded scope set for this (user, role) assignment
    public DateTime AssignedDate { get; set; }

    /// <summary>Scope entries for this assignment, parsed from the encoded string.</summary>
    public List<ScopeEntry> Scopes => ScopeSet.Parse(AssignedGauge);
}

public class UserWithRolesDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? RoleId { get; set; }
    public string? RoleName { get; set; }
    public string? AssignedGauge { get; set; }   // encoded scope set for this (user, role) assignment
    public DateTime? AssignedDate { get; set; }

    /// <summary>Scope entries for this assignment, parsed from the encoded string.</summary>
    public List<ScopeEntry> Scopes => ScopeSet.Parse(AssignedGauge);
}

public class AssignUserRoleRequest
{
    public string UserId { get; set; } = string.Empty;
    public string RoleId { get; set; } = string.Empty;
    public int Flag { get; set; }  // 1=Assign, 2=Remove

    /// <summary>Scope entries this assignment grants. Empty = unrestricted for this assignment.</summary>
    public List<ScopeEntry> Scopes { get; set; } = new();
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

    /// <summary>Encoded scope set (union across all the user's roles). Null/empty = unrestricted.</summary>
    public string? AssignedGauge { get; set; }

    public List<UserPermissionDto> Permissions { get; set; } = new();

    /// <summary>The user's effective scope entries, parsed from <see cref="AssignedGauge"/>.</summary>
    public List<ScopeEntry> Scopes => ScopeSet.Parse(AssignedGauge);

    /// <summary>True if the user is unrestricted (no scope) — typically an admin.</summary>
    public bool IsUnrestricted => Scopes.Count == 0;

    private static string NormDept(string? knitType) =>
        string.IsNullOrWhiteSpace(knitType) ? "knit" : knitType.Trim();

    /// <summary>
    /// Two-level check: may the user act on a plan row tagged (knitType, gauge)?
    /// A null/blank department is treated as "knit" (the storage default).
    /// A scope entry matches when its department equals the row's, and its specific
    /// value is blank (whole department) or equals the row's gauge value.
    /// </summary>
    public bool IsRowAllowed(string? knitType, string? gauge)
    {
        if (IsUnrestricted) return true;
        var dept = NormDept(knitType);
        var g = gauge?.Trim() ?? string.Empty;
        return Scopes.Any(s =>
            string.Equals(s.KnitType.Trim(), dept, StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrWhiteSpace(s.GaugeValue) ||
             string.Equals(s.GaugeValue.Trim(), g, StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// True if the user has ANY access (whole-department or specific) within any of the
    /// given departments — used to decide whether a department's view is reachable at all.
    /// </summary>
    public bool CanAccessDept(params string[] departments)
    {
        if (IsUnrestricted) return true;
        return Scopes.Any(s => departments.Any(d =>
            string.Equals(s.KnitType.Trim(), NormDept(d), StringComparison.OrdinalIgnoreCase)));
    }

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
