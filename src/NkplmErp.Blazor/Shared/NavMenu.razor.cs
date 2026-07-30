using Microsoft.AspNetCore.Components;
using NkplmErp.Blazor.Services.RoleManagement;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Shared;

public partial class NavMenu
{
    [Inject] private RoleManagementApiClient RoleApi { get; set; } = default!;
    [Inject] private PermissionService PermSvc { get; set; } = default!;

    // Pages the current user can actually view, in registry order.
    private List<AppPageDto> VisiblePages = new();

    // Groups collapse by default (mega-menu style) — expanded ones are tracked by MenuId.
    private readonly HashSet<int> _expandedGroups = new();
    private bool IsExpanded(int menuId) => _expandedGroups.Contains(menuId);
    private void ToggleGroup(int menuId)
    {
        if (!_expandedGroups.Remove(menuId))
            _expandedGroups.Add(menuId);
    }

    // Ungrouped pages first (MenuId == null), then each menu group in the order
    // its first page appears — sp_ManagePage's flag=3 already orders by
    // ISNULL(MenuId, 0), DisplayOrder, so this just reads that order as-is.
    private IEnumerable<AppPageDto> UngroupedPages => VisiblePages.Where(p => p.MenuId is null);

    private IEnumerable<(int MenuId, string MenuTitle, List<AppPageDto> Pages)> GroupedPages =>
        VisiblePages
            .Where(p => p.MenuId is not null)
            .GroupBy(p => p.MenuId!.Value)
            .Select(g => (MenuId: g.Key, MenuTitle: g.First().MenuTitle ?? "Other", Pages: g.ToList()));

    // identity.Menu has no icon of its own — the group row borrows its first
    // child page's icon so it doesn't need a schema change.
    private static string GroupIcon(List<AppPageDto> pages) =>
        pages.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.Icon))?.Icon ?? "fa-solid fa-folder";

    protected override async Task OnInitializedAsync()
    {
        if (!PermSvc.IsLoaded)
            await PermSvc.LoadPermissionsAsync();

        var pages = await RoleApi.GetAllPagesAsync();
        VisiblePages = pages
            .Where(p => p.IsActive && !string.IsNullOrWhiteSpace(p.PageUrl) && PermSvc.CanView(p.PageKey))
            .ToList();
    }
}
