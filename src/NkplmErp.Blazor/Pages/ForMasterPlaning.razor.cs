using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.Blazor.Services.Auth;
using NkplmErp.Blazor.Services.Toast;
using System.Net;

namespace NkplmErp.Blazor.Pages;

public partial class ForMasterPlaning
{
    [Inject] private IProductionPlanningService PlanningService { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private TokenProvider _tokenProvider { get; set; } = default!;
    [Inject] private ToastService ToastService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private NkplmErp.Blazor.Services.RoleManagement.PermissionService Permissions { get; set; } = default!;

    private List<MasterPlanningRowDto> Rows { get; set; } = new();
    private List<KnitterDto> Knitters { get; set; } = new();
    private List<KnitterBusyDto> KnitterBusy { get; set; } = new();

    // Selected knitter (CardNo) per machine group, keyed by the group key.
    private Dictionary<string, string> SelectedKnitter { get; set; } = new();

    // Left-hand selection (the machine group whose detail is shown on the right).
    private string? SelectedGroupKey { get; set; }

    // One left-hand master row: a distinct Order + Gauge + Machine with its total qty.
    public class PlanGroup
    {
        public string OrderNo { get; set; } = string.Empty;
        public string Guage { get; set; } = string.Empty;
        public string Machine { get; set; } = string.Empty;
        public int? MachineID { get; set; }
        public decimal Qty { get; set; }
        public DateTime? StartDate { get; set; }
        public List<MasterPlanningRowDto> Details { get; set; } = new();
        // Date is part of the key so the same machine planned for today AND tomorrow
        // shows as two separate rows.
        public string Key => $"{OrderNo}|{Guage}|{MachineID}|{Machine}|{StartDate:yyyyMMdd}";
    }

    private List<PlanGroup> Groups =>
        Rows.GroupBy(r => new { r.OrderNo, r.Guage, r.Machine, r.MachineID, Start = r.StartDate })
            .Select(g => new PlanGroup
            {
                OrderNo = g.Key.OrderNo,
                Guage = g.Key.Guage,
                Machine = g.Key.Machine,
                MachineID = g.Key.MachineID,
                Qty = g.Sum(x => RowTotal(x)),
                StartDate = g.Key.Start,
                Details = g.ToList()
            })
            .OrderBy(g => g.StartDate)
            .ThenBy(g => g.OrderNo)
            .ThenBy(g => g.MachineID)
            .ToList();

    // Date window: the list defaults to StartDate of today or tomorrow; "Next Day"
    // walks the window forward one day at a time. WindowDays = 2 (default) or 7.
    private DateTime ViewDate { get; set; } = DateTime.Today;
    private string GridSearch { get; set; } = string.Empty;
    private int WindowDays { get; set; } = 2;

    private string WindowLabel => WindowDays == 2
        ? $"{ViewDate:dd-MMM} & {ViewDate.AddDays(1):dd-MMM}"
        : $"{ViewDate:dd-MMM} – {ViewDate.AddDays(WindowDays - 1):dd-MMM}";

    private void SetWindowDays(int days)
    {
        WindowDays = days;
        ResetSelection();
    }

    // Assignment status filter for the left list.
    private string AssignFilter { get; set; } = "All"; // All / Unassigned / Assigned

    private void SetAssignFilter(string f)
    {
        AssignFilter = f;
        ResetSelection();
    }

    // Date + search filtered groups (before the status filter) — used for counts.
    private List<PlanGroup> BaseVisibleGroups
    {
        get
        {
            var from = ViewDate.Date;
            var to = ViewDate.Date.AddDays(WindowDays - 1);

            IEnumerable<PlanGroup> q = Groups.Where(g =>
                g.StartDate.HasValue &&
                g.StartDate.Value.Date >= from &&
                g.StartDate.Value.Date <= to);

            if (!string.IsNullOrWhiteSpace(GridSearch))
            {
                var s = GridSearch.Trim();
                q = q.Where(g =>
                    (g.OrderNo?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (g.Machine?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (g.Guage?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            return q.ToList();
        }
    }

    private int AssignedCount => BaseVisibleGroups.Count(g => GetAssignedCardNo(g) != null);
    private int UnassignedCount => BaseVisibleGroups.Count(g => GetAssignedCardNo(g) == null);

    private List<PlanGroup> VisibleGroups
    {
        get
        {
            var baseList = BaseVisibleGroups;
            return AssignFilter switch
            {
                "Unassigned" => baseList.Where(g => GetAssignedCardNo(g) == null).ToList(),
                "Assigned" => baseList.Where(g => GetAssignedCardNo(g) != null).ToList(),
                _ => baseList
            };
        }
    }

    private PlanGroup? SelectedGroup => Groups.FirstOrDefault(g => g.Key == SelectedGroupKey);

    private void SelectGroup(PlanGroup g) => SelectedGroupKey = g.Key;

    private void ResetSelection() => SelectedGroupKey = VisibleGroups.FirstOrDefault()?.Key;

    // ---- Prev / Next navigation across the visible machine list ----
    private int CurrentIndex => VisibleGroups.FindIndex(g => g.Key == SelectedGroupKey);
    private bool CanPrev => CurrentIndex > 0;
    private bool CanNext => CurrentIndex >= 0 && CurrentIndex < VisibleGroups.Count - 1;

    private void SelectNextGroup()
    {
        var list = VisibleGroups;
        if (!list.Any()) return;
        int idx = CurrentIndex;
        if (idx < 0) SelectedGroupKey = list.First().Key;
        else if (idx < list.Count - 1) SelectedGroupKey = list[idx + 1].Key;
    }

    private void SelectPrevGroup()
    {
        var list = VisibleGroups;
        if (!list.Any()) return;
        int idx = CurrentIndex;
        if (idx < 0) SelectedGroupKey = list.First().Key;
        else if (idx > 0) SelectedGroupKey = list[idx - 1].Key;
    }

    private void NextDay()
    {
        ViewDate = ViewDate.AddDays(1);
        ResetSelection();
    }

    private void PrevDay()
    {
        ViewDate = ViewDate.AddDays(-1);
        ResetSelection();
    }

    private void GoToday()
    {
        ViewDate = DateTime.Today;
        ResetSelection();
    }

    private void OnSearchInput(ChangeEventArgs e)
    {
        GridSearch = e.Value?.ToString() ?? string.Empty;
        ResetSelection();
    }

    private bool IsLoading { get; set; } = true;

    // Size columns shown in the grid (matches the pivoted procedure output).
    private static readonly (string Key, Func<MasterPlanningRowDto, decimal> Get)[] SizeColumns =
    {
        ("XXXS", r => r.XXXS),
        ("XXS",  r => r.XXS),
        ("XS",   r => r.XS),
        ("S",    r => r.S),
        ("M",    r => r.M),
        ("L",    r => r.L),
        ("XL",   r => r.XL),
        ("XXL",  r => r.XXL),
        ("XXXL", r => r.XXXL),
        ("OSFA", r => r.OSFA),
    };

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        if (authState.User.Identity?.IsAuthenticated != true)
        {
            Navigation.NavigateTo("/login", true);
            return;
        }

        // Zero Trust: page is permission-guarded like the rest of the app.
        if (!Permissions.IsLoaded)
        {
            await Permissions.LoadPermissionsAsync();
        }
        if (!Permissions.CanView("ForMasterPlaning"))
        {
            Navigation.NavigateTo("/dashboard");
            return;
        }

        await LoadData();
        IsLoading = false;
    }

    private async Task LoadData()
    {
        IsLoading = true;
        StateHasChanged();
        try
        {
            Rows = await PlanningService.GetMasterPlanningAsync(null, null);

            // Load knitters (all gauges) so each row can offer the knitters for its gauge.
            Knitters = await PlanningService.GetKnittersByGaugeAsync(null);

            // Load existing knitter busy windows so we can block double-booking.
            KnitterBusy = await PlanningService.GetKnitterBusyAsync();

            // Pre-select already-assigned knitters so the dropdown reflects saved assignments.
            foreach (var g in Groups)
            {
                var card = GetAssignedCardNo(g);
                if (!string.IsNullOrEmpty(card)) SelectedKnitter[g.Key] = card;
            }

            // Auto-select the first visible (today/tomorrow) group so the detail isn't empty.
            ResetSelection();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            HandleUnauthorized();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading master planning: {ex.Message}");
            ToastService.ShowError("Failed to load master planning data.");
            Rows = new();
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private decimal RowTotal(MasterPlanningRowDto r) => SizeColumns.Sum(c => c.Get(r));

    // Machine busy window for a group: earliest StartDate -> latest EndDate of its plans.
    private DateTime? GroupBusyFrom(PlanGroup g) =>
        g.Details.Where(d => d.StartDate.HasValue).Select(d => d.StartDate).DefaultIfEmpty(null).Min();

    private DateTime? GroupBusyTo(PlanGroup g) =>
        g.Details.Where(d => d.EndDate.HasValue).Select(d => d.EndDate).DefaultIfEmpty(null).Max();

    // Total of a single size column across the selected group's detail rows.
    private decimal GroupSizeTotal(PlanGroup g, Func<MasterPlanningRowDto, decimal> getSize) =>
        g.Details.Sum(getSize);

    // Knitters whose gauge matches this row's gauge (numeric compare, ignoring GG/G suffixes).
    private List<KnitterDto> KnittersForGauge(string? gauge)
    {
        if (Knitters == null || Knitters.Count == 0) return new();

        var normalized = (gauge ?? string.Empty)
            .Replace("GG", "", StringComparison.OrdinalIgnoreCase)
            .Replace("G", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "");

        if (decimal.TryParse(normalized, out var g))
        {
            return Knitters.Where(k => k.GaugeValue.HasValue && k.GaugeValue.Value == g).ToList();
        }
        return Knitters;
    }

    // ---- Knitter assignment confirmation ----
    private bool ShowKnitterConfirm { get; set; }
    private string? PendingGroupKey { get; set; }
    private string PendingCardNo { get; set; } = string.Empty;
    private string PendingKnitterName { get; set; } = string.Empty;
    private int KnitterSelectVersion { get; set; }   // bump to force the dropdown to revert
    private bool IsSavingKnitter { get; set; }

    // Card no of the knitter already assigned to this group's plans (null when unassigned).
    private string? GetAssignedCardNo(PlanGroup g)
    {
        if (KnitterBusy == null || KnitterBusy.Count == 0) return null;
        var planIds = g.Details.Select(d => d.PlanID).ToHashSet();
        return KnitterBusy.FirstOrDefault(b => planIds.Contains(b.PlanId))?.CardNo;
    }

    // Assignment status of a group: Unassigned / Assigned / Completed.
    private string GetGroupStatus(PlanGroup g)
    {
        var planIds = g.Details.Select(d => d.PlanID).ToHashSet();
        var rows = KnitterBusy.Where(b => planIds.Contains(b.PlanId)).ToList();
        if (!rows.Any()) return "Unassigned";
        return rows.All(r => string.Equals(r.Status, "Completed", StringComparison.OrdinalIgnoreCase))
            ? "Completed" : "Assigned";
    }

    // Latest date the knitter is busy until, overlapping this group's window (null = free).
    private DateTime? GetKnitterBusyTill(string? cardNo, PlanGroup group)
    {
        if (string.IsNullOrEmpty(cardNo) || KnitterBusy.Count == 0) return null;
        var from = (GroupBusyFrom(group) ?? group.StartDate)?.Date;
        var to = (GroupBusyTo(group) ?? group.StartDate)?.Date;
        if (from == null || to == null) return null;

        var ownPlanIds = group.Details.Select(d => d.PlanID).Distinct().ToHashSet();
        var overlapping = KnitterBusy.Where(b =>
            string.Equals(b.CardNo, cardNo, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(b.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
            !ownPlanIds.Contains(b.PlanId) &&
            b.FromDate.Date <= to.Value &&
            from.Value <= b.ToDate.Date).ToList();

        return overlapping.Any() ? overlapping.Max(b => b.ToDate) : null;
    }

    // "Name (CardNo)" label for the assigned knitter, or null.
    private string? GetAssignedKnitterLabel(PlanGroup g)
    {
        var card = GetAssignedCardNo(g);
        if (string.IsNullOrEmpty(card)) return null;
        var name = Knitters.FirstOrDefault(k => string.Equals(k.CardNo, card, StringComparison.OrdinalIgnoreCase))?.KnitterName;
        return string.IsNullOrEmpty(name) ? card : $"{name} ({card})";
    }

    // Is this knitter already busy during the selected machine's date window
    // (overlapping a different plan)? Used to block double-booking.
    private bool IsKnitterBusy(string? cardNo, PlanGroup group)
    {
        if (string.IsNullOrEmpty(cardNo) || KnitterBusy == null || KnitterBusy.Count == 0) return false;

        var from = (GroupBusyFrom(group) ?? group.StartDate)?.Date;
        var to = (GroupBusyTo(group) ?? group.StartDate)?.Date;
        if (from == null || to == null) return false;

        // Exclude this group's own plans so re-selecting the same knitter isn't "busy".
        var ownPlanIds = group.Details.Select(d => d.PlanID).Distinct().ToHashSet();

        return KnitterBusy.Any(b =>
            string.Equals(b.CardNo, cardNo, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(b.Status, "Completed", StringComparison.OrdinalIgnoreCase) &&
            !ownPlanIds.Contains(b.PlanId) &&
            b.FromDate.Date <= to.Value &&
            from.Value <= b.ToDate.Date);
    }

    // ---- Auto-suggest knitter: free in this window, least loaded first ----
    private void SuggestKnitter(PlanGroup g)
    {
        var candidates = KnittersForGauge(g.Guage)
            .Where(k => !IsKnitterBusy(k.CardNo, g))
            .OrderBy(k => KnitterBusy.Count(b =>
                string.Equals(b.CardNo, k.CardNo, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(b.Status, "Completed", StringComparison.OrdinalIgnoreCase)))   // least active load
            .ThenBy(k => k.KnitterName)
            .ToList();

        var best = candidates.FirstOrDefault();
        if (best == null)
        {
            ToastService.ShowWarning("No free knitter available for this gauge in this date window.");
            return;
        }

        // Goes through the normal selection flow -> confirmation popup -> save.
        OnKnitterSelected(g.Key, best.CardNo);
    }

    // ---- Assignment history (audit) ----
    private bool ShowHistory { get; set; }
    private bool IsLoadingHistory { get; set; }
    private List<KnitterAssignmentHistoryDto> HistoryRows { get; set; } = new();

    private async Task OpenHistory()
    {
        ShowHistory = true;
        IsLoadingHistory = true;
        StateHasChanged();
        try
        {
            HistoryRows = await PlanningService.GetKnitterAssignmentHistoryAsync(30);
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Failed to load history: {ex.Message}");
            HistoryRows = new();
        }
        finally
        {
            IsLoadingHistory = false;
            StateHasChanged();
        }
    }

    private void CloseHistory() => ShowHistory = false;

    // ---- Complete / Unassign actions ----
    private async Task CompleteAssignment(PlanGroup g)
    {
        var label = GetAssignedKnitterLabel(g) ?? "this knitter";
        bool ok = await JS.InvokeAsync<bool>("confirm", $"Mark {g.Machine} ({label}) as COMPLETED?");
        if (!ok) return;

        try
        {
            foreach (var pid in g.Details.Select(d => d.PlanID).Where(id => id > 0).Distinct())
            {
                await PlanningService.ManageKnitterAssignmentAsync(pid, "complete");
            }
            KnitterBusy = await PlanningService.GetKnitterBusyAsync();
            ToastService.ShowSuccess($"{g.Machine} marked as completed.");
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Failed to complete: {ex.Message}");
        }
        StateHasChanged();
    }

    private async Task UnassignKnitter(PlanGroup g)
    {
        var label = GetAssignedKnitterLabel(g) ?? "the knitter";
        bool ok = await JS.InvokeAsync<bool>("confirm", $"Remove {label} from {g.Machine}?");
        if (!ok) return;

        try
        {
            foreach (var pid in g.Details.Select(d => d.PlanID).Where(id => id > 0).Distinct())
            {
                await PlanningService.ManageKnitterAssignmentAsync(pid, "unassign");
            }
            SelectedKnitter.Remove(g.Key);
            KnitterSelectVersion++;
            KnitterBusy = await PlanningService.GetKnitterBusyAsync();
            ToastService.ShowSuccess($"Knitter unassigned from {g.Machine}.");
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Failed to unassign: {ex.Message}");
        }
        StateHasChanged();
    }

    // Print the day's roster (print stylesheet shows only the roster section).
    private async Task PrintRoster() => await JS.InvokeVoidAsync("window.print");

    private void OnKnitterSelected(string groupKey, string? cardNo)
    {
        cardNo ??= string.Empty;

        // Clearing the selection just unassigns locally (no confirmation).
        if (string.IsNullOrEmpty(cardNo))
        {
            SelectedKnitter[groupKey] = string.Empty;
            return;
        }

        var group = Groups.FirstOrDefault(g => g.Key == groupKey);
        var name = group != null
            ? KnittersForGauge(group.Guage).FirstOrDefault(k => k.CardNo == cardNo)?.KnitterName ?? string.Empty
            : string.Empty;

        PendingGroupKey = groupKey;
        PendingCardNo = cardNo;
        PendingKnitterName = name;
        ShowKnitterConfirm = true;
    }

    private void CancelKnitterAssignment()
    {
        ShowKnitterConfirm = false;
        KnitterSelectVersion++; // revert the dropdown to its stored value
    }

    private PlanGroup? PendingGroup => Groups.FirstOrDefault(g => g.Key == PendingGroupKey);

    private async Task ConfirmKnitterAssignment()
    {
        var group = PendingGroup;
        if (group == null || string.IsNullOrEmpty(PendingGroupKey)) { ShowKnitterConfirm = false; return; }

        IsSavingKnitter = true;
        try
        {
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var userId = authState.User.Identity?.Name ?? "system";

            var planIds = group.Details.Select(d => d.PlanID).Where(id => id > 0).Distinct().ToList();
            bool conflict = false;
            foreach (var pid in planIds)
            {
                // Server re-checks availability; false = knitter already booked (double-booking blocked).
                var saved = await PlanningService.SaveKnitterAssignmentAsync(pid, PendingCardNo, PendingKnitterName, userId);
                if (!saved) conflict = true;
            }

            if (conflict)
            {
                KnitterBusy = await PlanningService.GetKnitterBusyAsync(); // sync to server truth
                KnitterSelectVersion++;
                ToastService.ShowWarning($"{PendingKnitterName} is already booked in this period (assigned elsewhere just now). Please pick another knitter.");
                ShowKnitterConfirm = false;
                IsSavingKnitter = false;
                return;
            }

            SelectedKnitter[PendingGroupKey] = PendingCardNo;

            // Refresh busy windows so the new assignment blocks future double-booking.
            KnitterBusy = await PlanningService.GetKnitterBusyAsync();

            ToastService.ShowSuccess($"Knitter {PendingKnitterName} assigned to {group.Machine}.");
            ShowKnitterConfirm = false;

            // Auto-advance to the next UNASSIGNED machine so assigning flows hands-free.
            var nextUnassigned = VisibleGroups.FirstOrDefault(x => x.Key != group.Key && GetAssignedCardNo(x) == null);
            if (nextUnassigned != null) SelectedGroupKey = nextUnassigned.Key;
        }
        catch (Exception ex)
        {
            ToastService.ShowError($"Failed to assign knitter: {ex.Message}");
            KnitterSelectVersion++; // revert the dropdown on failure
            ShowKnitterConfirm = false;
        }
        finally
        {
            IsSavingKnitter = false;
        }
    }

    private void HandleUnauthorized()
    {
        ToastService.ShowWarning("Session Expired. Redirecting to login...");
        _tokenProvider.NotifySessionExpired();
        Navigation.NavigateTo("/login", true);
    }
}
