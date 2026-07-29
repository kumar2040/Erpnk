using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NkplmErp.Blazor.Services.RoleManagement;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Pages;

public partial class PagesManagement : IDisposable
{
    [Inject] private RoleManagementApiClient RoleApi { get; set; } = default!;
    [Inject] private PermissionService PermSvc { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private List<AppPageDto> Pages = new();
    private List<MenuDto> Menus = new();
    private SavePageRequest EditPage = new();
    private bool ShowForm = false;
    private bool IsLoading = false;

    // <select> binds a string; "" means "no menu" (ungrouped, top-level).
    private string MenuIdStr
    {
        get => EditPage.MenuId?.ToString() ?? "";
        set => EditPage.MenuId = string.IsNullOrEmpty(value) ? (int?)null : int.Parse(value);
    }

    private string StatusMessage = "";
    private bool IsError = false;
    private System.Timers.Timer? _statusTimer;

    // Gated by the PagesManagement module (manageable from Role Management).
    // RoleManagement rights are accepted as a fallback so existing admins keep access.
    private bool CanView   => PermSvc.CanView("PagesManagement")   || PermSvc.CanView("RoleManagement");
    private bool CanEdit   => PermSvc.CanEdit("PagesManagement")   || PermSvc.CanEdit("RoleManagement");
    private bool CanDelete => PermSvc.CanDelete("PagesManagement") || PermSvc.CanDelete("RoleManagement");

    private bool AccessDenied = false;

    protected override async Task OnInitializedAsync()
    {
        // Make sure the cached permissions exist (e.g. on a direct navigation / refresh,
        // not just after the login flow) so the gate + CRUD buttons resolve correctly.
        if (!PermSvc.IsLoaded)
            await PermSvc.LoadPermissionsAsync();

        if (!CanView)
        {
            AccessDenied = true;
            return;
        }

        Menus = await RoleApi.GetMenusAsync();
        await LoadPagesAsync();
    }

    private async Task LoadPagesAsync()
    {
        IsLoading = true;
        StateHasChanged();
        Pages = await RoleApi.GetAllPagesAsync();
        IsLoading = false;
    }

    private void ShowAddForm()
    {
        EditPage = new SavePageRequest { Flag = 1, IsActive = true };
        ShowForm = true;
    }

    private void EditAction(AppPageDto page)
    {
        EditPage = new SavePageRequest
        {
            AppPageId    = page.AppPageId,
            PageKey      = page.PageKey,
            PageName     = page.PageName,
            PageUrl      = page.PageUrl,
            IsActive     = page.IsActive,
            DisplayOrder = page.DisplayOrder,
            Icon         = page.Icon,
            MenuId       = page.MenuId,
            Flag         = 2  // Update
        };
        ShowForm = true;
    }

    private void CancelForm()
    {
        ShowForm = false;
        EditPage = new();
    }

    private async Task SavePage()
    {
        if (string.IsNullOrWhiteSpace(EditPage.PageKey))
        {
            ShowStatus("Page key is required.", isError: true);
            return;
        }
        if (string.IsNullOrWhiteSpace(EditPage.PageName))
            EditPage.PageName = EditPage.PageKey;

        var result = await RoleApi.SavePageAsync(EditPage);
        if (result?.IsSuccess == true)
        {
            ShowStatus(result.Message);
            ShowForm = false;
            await LoadPagesAsync();
        }
        else
        {
            ShowStatus(result?.Message ?? "Failed to save page.", isError: true);
        }
    }

    private async Task DeletePage(AppPageDto page)
    {
        var ok = await JS.InvokeAsync<bool>("confirm",
            $"Delete page '{page.PageName}' ({page.PageKey})? This removes its View/Edit/Delete permissions and any role grants of them.");
        if (!ok) return;

        var result = await RoleApi.DeletePageAsync(page.AppPageId);
        if (result?.IsSuccess == true)
        {
            ShowStatus("Page deleted.");
            await LoadPagesAsync();
        }
        else
        {
            ShowStatus(result?.Message ?? "Failed to delete page.", isError: true);
        }
    }

    private void ShowStatus(string message, bool isError = false)
    {
        StatusMessage = message;
        IsError = isError;
        StateHasChanged();

        _statusTimer?.Dispose();
        _statusTimer = new System.Timers.Timer(3500) { AutoReset = false };
        _statusTimer.Elapsed += (_, _) =>
        {
            StatusMessage = "";
            InvokeAsync(StateHasChanged);
            _statusTimer?.Dispose();
        };
        _statusTimer.Start();
    }

    public void Dispose() => _statusTimer?.Dispose();
}
