using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Services.RoleManagement;

/// <summary>
/// Zero Trust PermissionService.
/// Fetches user permissions from the API on login and caches them for UI rendering.
/// 
/// IMPORTANT: This is a UX helper only. True security enforcement happens on the API side.
/// Never trust the client — always verify on the server.
/// </summary>
public class PermissionService
{
    private readonly RoleManagementApiClient _apiClient;
    private UserPermissionsResponse? _cachedPermissions;
    private bool _isLoaded = false;

    public PermissionService(RoleManagementApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    /// <summary>
    /// Load and cache permissions. Call this immediately after successful login.
    /// </summary>
    public async Task LoadPermissionsAsync()
    {
        _cachedPermissions = await _apiClient.GetMyPermissionsAsync();
        _isLoaded = true;
    }

    /// <summary>
    /// Clear cache on logout.
    /// </summary>
    public void ClearPermissions()
    {
        _cachedPermissions = null;
        _isLoaded = false;
        LandingApplied = false;
    }

    /// <summary>
    /// Returns the gauge/factory restriction for the current user.
    /// NULL = admin, no restriction. Non-null = can only see data for that gauge.
    /// </summary>
    public string? AssignedGauge => _cachedPermissions?.AssignedGauge;

    public bool IsLoaded => _isLoaded;

    /// <summary>
    /// One-shot guard so the per-user landing redirect runs at most once per circuit
    /// (prevents dashboard↔landing redirect loops/flicker).
    /// </summary>
    public bool LandingApplied { get; set; }

    // ===== Permission Check Helpers =====
    // Use these in .razor files to show/hide UI elements.
    // Server will independently re-verify before processing any operation.

    public bool CanView(string pageKey) => _cachedPermissions?.CanView(pageKey) == true;

    public bool CanEdit(string pageKey) => _cachedPermissions?.CanEdit(pageKey) == true;

    public bool CanDelete(string pageKey) => _cachedPermissions?.CanDelete(pageKey) == true;

    /// <summary>
    /// Returns true if the user has no role assigned (will be denied by API anyway).
    /// </summary>
    public bool HasNoPermissions => _isLoaded && (_cachedPermissions == null || !_cachedPermissions.Permissions.Any());

    /// <summary>True if the user has no scope restriction (admin / blank scope) — sees everything.</summary>
    public bool IsUnrestricted => _cachedPermissions?.IsUnrestricted ?? true;

    /// <summary>
    /// Two-level row check: may the user see a row tagged (knitType, gauge)?
    /// Null/blank scope or admin => true. Fails open if permissions aren't loaded yet
    /// (the server still enforces; this is only for display).
    /// </summary>
    public bool IsRowAllowed(string? knitType, string? gauge) =>
        _cachedPermissions?.IsRowAllowed(knitType, gauge) ?? true;
}
