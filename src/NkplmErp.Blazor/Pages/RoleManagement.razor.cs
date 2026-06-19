using Microsoft.AspNetCore.Components;
using NkplmErp.Blazor.Services.RoleManagement;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Pages;

public partial class RoleManagement
{
    [Inject] private RoleManagementApiClient RoleApi { get; set; } = default!;
    [Inject] private PermissionService PermSvc { get; set; } = default!;

    private bool CanDeleteRoleMgmt => PermSvc.CanDelete("RoleManagement");
    private bool CanEditRoleMgmt   => PermSvc.CanEdit("RoleManagement");

    // ===== State =====
    private List<AppRoleDto>        Roles       = new();
    private List<RolePagePermissionDto> Permissions = new();
    private List<UserWithRolesDto>  AllUsers    = new();
    private List<UserWithRolesDto>  UsersInRole = new();

    private AppRoleDto?  SelectedRole = null;
    private SaveRoleRequest EditRole  = new();
    private bool ShowRoleForm         = false;
    private string SelectedUserIdToAssign = "";

    // Scope builder for the assignment being created.
    private static readonly string[] DepartmentOptions = { "knit", "weave", "silk", "linen", "other" };
    private List<ScopeEntry> PendingScopes = new();
    private string NewScopeDept  = "knit";
    private string NewScopeValue = "";

    // Users not already assigned to the selected role (distinct, one entry per user).
    private IEnumerable<UserWithRolesDto> AssignableUsers
    {
        get
        {
            if (SelectedRole == null) return Enumerable.Empty<UserWithRolesDto>();
            var assignedIds = UsersInRole.Select(u => u.UserId).ToHashSet();
            return AllUsers
                .Where(u => !assignedIds.Contains(u.UserId))
                .GroupBy(u => u.UserId)
                .Select(g => g.First());
        }
    }

    private bool IsLoadingRoles       = false;
    private bool IsLoadingPermissions = false;
    private bool IsLoadingUsers       = false;

    private string StatusMessage      = "";
    private bool   IsError            = false;
    private System.Timers.Timer? _statusTimer;

    // ===== Lifecycle =====

    private bool AccessDenied = false;

    protected override async Task OnInitializedAsync()
    {
        // Ensure cached permissions exist (direct navigation / refresh), then gate view.
        if (!PermSvc.IsLoaded)
            await PermSvc.LoadPermissionsAsync();

        if (!PermSvc.CanView("RoleManagement"))
        {
            AccessDenied = true;
            return;
        }

        await LoadRolesAsync();
        await LoadAllUsersAsync();
    }

    // ===== Role Actions =====

    private async Task LoadRolesAsync()
    {
        IsLoadingRoles = true;
        StateHasChanged();
        Roles = await RoleApi.GetAllRolesAsync();
        IsLoadingRoles = false;
    }

    private void ShowAddRoleForm()
    {
        EditRole = new SaveRoleRequest { Flag = 1 };
        ShowRoleForm = true;
    }

    private void EditRoleAction(AppRoleDto role)
    {
        EditRole = new SaveRoleRequest
        {
            RoleId        = role.RoleId,
            RoleName      = role.RoleName,
            Description   = role.Description,
            AssignedGauge = role.AssignedGauge,
            IsActive      = role.IsActive,
            Flag          = 2  // Update
        };
        ShowRoleForm = true;
    }

    private void CancelRoleForm()
    {
        ShowRoleForm = false;
        EditRole = new();
    }

    private async Task SaveRole()
    {
        if (string.IsNullOrWhiteSpace(EditRole.RoleName))
        {
            ShowStatus("Role name is required.", isError: true);
            return;
        }

        var result = await RoleApi.SaveRoleAsync(EditRole);
        if (result?.IsSuccess == true)
        {
            ShowStatus(result.Message);
            ShowRoleForm = false;
            await LoadRolesAsync();
        }
        else
        {
            ShowStatus(result?.Message ?? "Failed to save role.", isError: true);
        }
    }

    private async Task DeleteRole(string roleId)
    {
        // Block deleting a role that still has members - it would silently strip their access.
        var role = Roles.FirstOrDefault(r => r.RoleId == roleId);
        if (role != null && role.UserCount > 0)
        {
            ShowStatus($"Cannot delete '{role.RoleName}': {role.UserCount} user(s) still assigned. Remove them first.", isError: true);
            return;
        }

        var result = await RoleApi.DeleteRoleAsync(roleId);
        if (result?.IsSuccess == true)
        {
            ShowStatus("Role deleted.");
            if (SelectedRole?.RoleId == roleId)
            {
                SelectedRole  = null;
                Permissions   = new();
                UsersInRole   = new();
            }
            await LoadRolesAsync();
        }
        else
        {
            ShowStatus(result?.Message ?? "Failed to delete role.", isError: true);
        }
    }

    // ===== Role Selection (loads panels 2 & 3) =====

    private async Task SelectRole(AppRoleDto role)
    {
        SelectedRole = role;
        await LoadPermissionsAsync(role.RoleId);
        await LoadUsersInRoleAsync(role.RoleId);
    }

    // ===== Permissions =====

    private async Task LoadPermissionsAsync(string roleId)
    {
        IsLoadingPermissions = true;
        StateHasChanged();
        Permissions = await RoleApi.GetPermissionsByRoleAsync(roleId);
        IsLoadingPermissions = false;
    }

    // Permission coherence: Edit/Delete imply View; removing View removes Edit/Delete.
    private void SetViewPermission(RolePagePermissionDto perm, bool value)
    {
        perm.CanView = value;
        if (!value)
        {
            perm.CanEdit = false;
            perm.CanDelete = false;
        }
    }

    private void SetEditPermission(RolePagePermissionDto perm, bool value)
    {
        perm.CanEdit = value;
        if (value) perm.CanView = true;
    }

    private void SetDeletePermission(RolePagePermissionDto perm, bool value)
    {
        perm.CanDelete = value;
        if (value) perm.CanView = true;
    }

    private async Task SaveAllPermissions()
    {
        if (SelectedRole == null) return;

        var requests = Permissions.Select(p => new SavePermissionRequest
        {
            RoleId    = SelectedRole.RoleId,
            AppPageId = p.AppPageId,
            CanView   = p.CanView,
            CanEdit   = p.CanEdit,
            CanDelete = p.CanDelete
        }).ToList();

        var success = await RoleApi.SaveAllPermissionsAsync(requests);
        ShowStatus(success
            ? "Permissions saved. Changes take effect at each user's next login."
            : "Failed to save permissions.", isError: !success);
        if (success) await LoadRolesAsync();
    }

    // ===== Users =====

    private async Task LoadAllUsersAsync()
    {
        AllUsers = await RoleApi.GetAllUsersWithRolesAsync();
    }

    private async Task LoadUsersInRoleAsync(string roleId)
    {
        IsLoadingUsers = true;
        StateHasChanged();
        UsersInRole = AllUsers
            .Where(u => u.RoleId == roleId)
            .ToList();
        IsLoadingUsers = false;
    }

    private async Task AssignUser()
    {
        if (SelectedRole == null)
            return;
        if (string.IsNullOrEmpty(SelectedUserIdToAssign))
        {
            ShowStatus("Select a user first.", isError: true);
            return;
        }

        // Auto-commit a scope value typed in the builder but not yet added with "+ Add"
        // (the common reason a scope "didn't save").
        if (!string.IsNullOrWhiteSpace(NewScopeValue))
            AddScope();

        var result = await RoleApi.AssignUserRoleAsync(new AssignUserRoleRequest
        {
            UserId = SelectedUserIdToAssign,
            RoleId = SelectedRole.RoleId,
            Flag   = 1,  // Assign
            Scopes = new List<ScopeEntry>(PendingScopes)
        });

        if (result?.IsSuccess == true)
        {
            ShowStatus("User assigned to role.");
            SelectedUserIdToAssign = "";
            PendingScopes = new();
            NewScopeDept  = "knit";
            NewScopeValue = "";
            await LoadAllUsersAsync();
            await LoadUsersInRoleAsync(SelectedRole.RoleId);
        }
        else
        {
            ShowStatus(result?.Message ?? "Failed to assign user.", isError: true);
        }
    }

    private void AddScope()
    {
        if (string.IsNullOrWhiteSpace(NewScopeDept)) return;
        var value = string.IsNullOrWhiteSpace(NewScopeValue) ? null : NewScopeValue.Trim();
        // Avoid duplicates (same dept + value).
        if (PendingScopes.Any(s =>
                string.Equals(s.KnitType, NewScopeDept, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(s.GaugeValue ?? "", value ?? "", StringComparison.OrdinalIgnoreCase)))
        {
            NewScopeValue = "";
            return;
        }
        PendingScopes.Add(new ScopeEntry { KnitType = NewScopeDept, GaugeValue = value });
        NewScopeValue = "";
    }

    private void RemoveScope(ScopeEntry scope) => PendingScopes.Remove(scope);

    // Press Enter in the value box to add the scope.
    private void OnScopeValueKeyDown(Microsoft.AspNetCore.Components.Web.KeyboardEventArgs e)
    {
        if (e.Key == "Enter") AddScope();
    }

    private async Task RemoveUserFromRole(string userId)
    {
        if (SelectedRole == null) return;

        var result = await RoleApi.AssignUserRoleAsync(new AssignUserRoleRequest
        {
            UserId = userId,
            RoleId = SelectedRole.RoleId,
            Flag   = 2  // Remove
        });

        if (result?.IsSuccess == true)
        {
            ShowStatus("User removed from role.");
            await LoadAllUsersAsync();
            await LoadUsersInRoleAsync(SelectedRole.RoleId);
        }
        else
        {
            ShowStatus(result?.Message ?? "Failed to remove user.", isError: true);
        }
    }

    // ===== Status Toast =====

    private void ShowStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        IsError       = isError;
        StateHasChanged();

        _statusTimer?.Dispose();
        _statusTimer = new System.Timers.Timer(3500);
        _statusTimer.Elapsed += (_, _) =>
        {
            StatusMessage = "";
            InvokeAsync(StateHasChanged);
            _statusTimer?.Dispose();
        };
        _statusTimer.AutoReset = false;
        _statusTimer.Start();
    }

    public void Dispose()
    {
        _statusTimer?.Dispose();
    }
}
