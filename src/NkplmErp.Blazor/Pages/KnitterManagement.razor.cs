using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using NkplmErp.Blazor.Services.KnitterManagement;
using NkplmErp.Blazor.Services.RoleManagement;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Pages;

public partial class KnitterManagement
{
    [Inject] private KnitterManagementApiClient KnitterApi { get; set; } = default!;
    [Inject] private PermissionService PermSvc { get; set; } = default!;

    private bool CanEditKnitterMgmt   => PermSvc.CanEdit("KnitterManagement");
    private bool CanDeleteKnitterMgmt => PermSvc.CanDelete("KnitterManagement");

    // ===== State =====
    private List<KnitterManagementDto> Knitters = new();
    private List<string> GaugeOptions = new();
    private SaveKnitterRequest EditKnitter = new() { Flag = 1, IsActive = true };
    private string NewGauge = "";
    private string SearchText = "";

    private bool IsLoading = false;
    private bool AccessDenied = false;

    private string StatusMessage = "";
    private bool IsError = false;
    private System.Timers.Timer? _statusTimer;

    private IEnumerable<KnitterManagementDto> FilteredKnitters =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Knitters
            : Knitters.Where(k =>
                k.KnitterName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || k.Gauges.Any(g => g.Contains(SearchText, StringComparison.OrdinalIgnoreCase)));

    // ===== Lifecycle =====
    protected override async Task OnInitializedAsync()
    {
        if (!PermSvc.IsLoaded)
            await PermSvc.LoadPermissionsAsync();

        if (!PermSvc.CanView("KnitterManagement"))
        {
            AccessDenied = true;
            return;
        }

        await LoadKnittersAsync();
        GaugeOptions = await KnitterApi.GetGaugeOptionsAsync();
    }

    private async Task LoadKnittersAsync()
    {
        IsLoading = true;
        StateHasChanged();
        Knitters = await KnitterApi.GetAllKnittersAsync();
        IsLoading = false;
    }

    // ===== Form =====
    private void NewKnitterForm()
    {
        EditKnitter = new SaveKnitterRequest { Flag = 1, IsActive = true };
        NewGauge = "";
    }

    private void EditKnitterAction(KnitterManagementDto k)
    {
        EditKnitter = new SaveKnitterRequest
        {
            CardNo = k.CardNo,
            KnitterName = k.KnitterName,
            PRSalary = k.PRSalary,
            IsActive = k.IsActive,
            Gauges = new List<string>(k.Gauges),
            Flag = 2
        };
        NewGauge = "";
    }

    private void OnGaugeKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter") AddGauge();
    }

    private void AddGauge()
    {
        var g = NewGauge?.Trim() ?? "";
        if (g.Length == 0) return;
        if (!EditKnitter.Gauges.Any(x => string.Equals(x, g, StringComparison.OrdinalIgnoreCase)))
            EditKnitter.Gauges.Add(g);
        NewGauge = "";
    }

    private void RemoveGauge(string g) => EditKnitter.Gauges.Remove(g);

    private async Task SaveKnitter()
    {
        if (string.IsNullOrWhiteSpace(EditKnitter.KnitterName))
        {
            ShowStatus("Knitter name is required.", isError: true);
            return;
        }

        var result = await KnitterApi.SaveKnitterAsync(EditKnitter);
        if (result?.IsSuccess == true)
        {
            ShowStatus(result.Message);
            NewKnitterForm();
            await LoadKnittersAsync();
            GaugeOptions = await KnitterApi.GetGaugeOptionsAsync();
        }
        else
        {
            ShowStatus(result?.Message ?? "Failed to save knitter.", isError: true);
        }
    }

    private async Task ToggleActive(KnitterManagementDto k)
    {
        var result = await KnitterApi.SetActiveAsync(k.CardNo, !k.IsActive);
        if (result?.IsSuccess == true)
        {
            ShowStatus(result.Message);
            await LoadKnittersAsync();
        }
        else
        {
            ShowStatus(result?.Message ?? "Failed to change status.", isError: true);
        }
    }

    private async Task DeleteKnitter(KnitterManagementDto k)
    {
        if (k.ActiveAssignments > 0)
        {
            ShowStatus($"Cannot delete '{k.KnitterName}': {k.ActiveAssignments} active assignment(s). Set inactive instead.", isError: true);
            return;
        }

        var result = await KnitterApi.DeleteKnitterAsync(k.CardNo);
        if (result?.IsSuccess == true)
        {
            ShowStatus(result.Message);
            if (EditKnitter.CardNo == k.CardNo) NewKnitterForm();
            await LoadKnittersAsync();
        }
        else
        {
            ShowStatus(result?.Message ?? "Failed to delete knitter.", isError: true);
        }
    }

    // ===== Toast =====
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
}
