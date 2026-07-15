using Microsoft.AspNetCore.Components;
using NkplmErp.Blazor.Services.MachineManagement;
using NkplmErp.Blazor.Services.RoleManagement;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Pages;

public partial class MachineManagement
{
    [Inject] private MachineManagementApiClient MachineApi { get; set; } = default!;
    [Inject] private PermissionService PermSvc { get; set; } = default!;

    private bool CanEditMachineMgmt   => PermSvc.CanEdit("MachineManagement");
    private bool CanDeleteMachineMgmt => PermSvc.CanDelete("MachineManagement");

    // ===== State =====
    private List<MachineManagementDto> Machines = new();
    private List<string> GaugeOptions = new();
    private SaveMachineRequest EditMachine = new() { Flag = 1, IsActive = true };
    private string SearchText = "";

    private bool IsLoading = false;
    private bool AccessDenied = false;

    private string StatusMessage = "";
    private bool IsError = false;
    private System.Timers.Timer? _statusTimer;

    private IEnumerable<MachineManagementDto> FilteredMachines =>
        string.IsNullOrWhiteSpace(SearchText)
            ? Machines
            : Machines.Where(m =>
                m.MachineNo.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
                || (m.Gauge?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false)
                || (m.Size?.Contains(SearchText, StringComparison.OrdinalIgnoreCase) ?? false));

    // ===== Lifecycle =====
    protected override async Task OnInitializedAsync()
    {
        if (!PermSvc.IsLoaded)
            await PermSvc.LoadPermissionsAsync();

        if (!PermSvc.CanView("MachineManagement"))
        {
            AccessDenied = true;
            return;
        }

        await LoadMachinesAsync();
        GaugeOptions = await MachineApi.GetGaugeOptionsAsync();
    }

    private async Task LoadMachinesAsync()
    {
        IsLoading = true;
        StateHasChanged();
        Machines = await MachineApi.GetAllMachinesAsync();
        IsLoading = false;
    }

    // ===== Form =====
    private void NewMachineForm()
    {
        EditMachine = new SaveMachineRequest { Flag = 1, IsActive = true };
    }

    private void EditMachineAction(MachineManagementDto m)
    {
        EditMachine = new SaveMachineRequest
        {
            MachineId = m.MachineId,
            MachineNo = m.MachineNo,
            Gauge = m.Gauge,
            Size = m.Size,
            IsActive = m.IsActive,
            Flag = 2
        };
    }

    private async Task SaveMachine()
    {
        if (string.IsNullOrWhiteSpace(EditMachine.MachineNo))
        {
            ShowStatus("Machine number is required.", isError: true);
            return;
        }

        var result = await MachineApi.SaveMachineAsync(EditMachine);
        if (result?.IsSuccess == true)
        {
            ShowStatus(result.Message);
            NewMachineForm();
            await LoadMachinesAsync();
            GaugeOptions = await MachineApi.GetGaugeOptionsAsync();
        }
        else
        {
            ShowStatus(result?.Message ?? "Failed to save machine.", isError: true);
        }
    }

    private async Task ToggleActive(MachineManagementDto m)
    {
        var result = await MachineApi.SetActiveAsync(m.MachineId, !m.IsActive);
        if (result?.IsSuccess == true)
        {
            ShowStatus(result.Message);
            await LoadMachinesAsync();
        }
        else
        {
            ShowStatus(result?.Message ?? "Failed to change status.", isError: true);
        }
    }

    private async Task DeleteMachine(MachineManagementDto m)
    {
        if (m.ActivePlans > 0)
        {
            ShowStatus($"Cannot delete '{m.MachineNo}': referenced by {m.ActivePlans} plan(s). Set inactive instead.", isError: true);
            return;
        }

        var result = await MachineApi.DeleteMachineAsync(m.MachineId);
        if (result?.IsSuccess == true)
        {
            ShowStatus(result.Message);
            if (EditMachine.MachineId == m.MachineId) NewMachineForm();
            await LoadMachinesAsync();
        }
        else
        {
            ShowStatus(result?.Message ?? "Failed to delete machine.", isError: true);
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
