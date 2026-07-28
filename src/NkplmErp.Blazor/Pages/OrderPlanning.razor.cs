using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.JSInterop;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.Blazor.Services.Auth;
using NkplmErp.Blazor.Services.Toast;
using System.Net;

namespace NkplmErp.Blazor.Pages;

public partial class OrderPlanning
{
    [Inject]
    private IProductionPlanningService PlanningService { get; set; } = null!;

    [Inject]
    private IJSRuntime JS { get; set; } = null!;

    [Inject]
    private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    [Inject]
    private TokenProvider _tokenProvider { get; set; } = default!;

    [Inject]
    private NkplmErp.Blazor.Services.Loading.LoadingService _loading { get; set; } = default!;

    [Inject]
    private ToastService ToastService { get; set; } = default!;

    [Inject]
    private NavigationManager Navigation { get; set; } = default!;

    [Inject]
    private NkplmErp.Blazor.Services.RoleManagement.PermissionService Permissions { get; set; } = default!;

    // ---- Deep link from a Planning task card on /tasks ----
    // /order-planning?orderNo=PO-1933&gauge=7 opens that order's planning straight away.
    [Parameter, SupplyParameterFromQuery(Name = "orderNo")] public string? FromOrderNo { get; set; }
    [Parameter, SupplyParameterFromQuery(Name = "gauge")] public string? FromGauge { get; set; }

    // The order's ship month, sent alongside orderNo by sp_GetPoTask. LoadMonths otherwise
    // leaves the page on the current month, and an order shipping in another month is then
    // genuinely absent from AllOrders — see OpenFromTaskLinkAsync.
    [Parameter, SupplyParameterFromQuery(Name = "month")] public string? FromMonth { get; set; }

    private List<MonthlyOrderSummaryDto> Months { get; set; } = new();
    private List<MonthlyOrderDetailDto> AllOrders { get; set; } = new();
    private List<MonthlyOrderDetailDto> SelectedOrders { get; set; } = new();

    // ---- Inline planning selectors (progressive): Product Type -> Sample/Production -> Month -> Order ----
    private string WizardProductType { get; set; } = string.Empty;     // Knit/Weave/Silk/Linen/Other
    private string WizardOrderType { get; set; } = string.Empty;       // "Sample" / "Production"
    private static readonly string[] WizardProductTypes = { "Knit", "Weave", "Silk", "Linen", "Other" };

    // order_no -> collection type flags (Sample / Production), loaded once.
    private Dictionary<string, OrderCollectionTypeDto> OrderTypeMap { get; set; } = new();
    // True when the collection-type lookup returned nothing (proc not deployed / API not
    // restarted) - the page then shows all orders in both lists and flags it in the UI.
    private bool OrderTypesUnavailable { get; set; }
    private string OrderTypesDiag { get; set; } = string.Empty; // why it failed (for on-screen diagnosis)

    // order_no -> product types it contains (Knit/Weave/Silk/Linen/Other), scanned per month.
    private Dictionary<string, HashSet<string>> OrderProductTypeMap { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    private bool IsScanningOrderTypes { get; set; }

    // Orders for the current month filtered by BOTH the chosen product type AND Sample/Production.
    private List<MonthlyOrderDetailDto> FilteredOrders =>
        AllOrders.Where(o =>
            (WizardOrderType == "Sample" ? o.IsSample :
             WizardOrderType == "Production" ? o.IsProduction : true)
            && OrderHasProductType(o.OrderNo)).ToList();

    private bool OrderHasProductType(string? orderNo)
    {
        if (string.IsNullOrEmpty(WizardProductType)) return true;
        // Not yet classified (scan in progress) -> don't hide it prematurely.
        if (orderNo == null || !OrderProductTypeMap.TryGetValue(orderNo.Trim(), out var types)) return true;
        // Couldn't classify this order (analysis returned nothing) -> show it rather than hide.
        if (types.Count == 0) return true;
        return types.Contains(WizardProductType);
    }

    private bool IsOrderDropdownOpen { get; set; } = false;
    
    private DateTime SelectedMonth { get; set; } = DateTime.Now;
    private string SelectedMonthStr 
    { 
        get => SelectedMonth.ToString("yyyy-MM-dd");
        set 
        {
            if (DateTime.TryParse(value, out var date))
            {
                SelectedMonth = date;
                _ = LoadOrders();
            }
        }
    }
    private DateTime? KnitCompleteDate { get; set; }
    private DateTime? RequiredCompletionDate { get; set; }
    private DateTime? KnitCompleteDateWithOT { get; set; }
    private decimal DepartmentLoad { get; set; } = 30;

    private List<GaugeUtilizationDto> GaugeUtilization { get; set; } = new();
    private bool IsLoading { get; set; } = true;
    private bool IsAnalysing { get; set; } = false;
    private bool IsAnalysisModalOpen { get; set; } = false;
    private bool DataHasLoaded { get; set; } = false;
    
    private bool IsGaugeDetailModalOpen { get; set; } = false;
    private bool IsLoadingGaugeDetail { get; set; } = false;
    private List<OrderDetailByGuageDto> GaugeDetails { get; set; } = new();
    private string SelectedGauge { get; set; } = string.Empty;
    private bool IsPlanningDetailsModalOpen { get; set; } = false;

    // Weave Modal state properties
    private bool IsWeavePlanningDetailsModalOpen { get; set; } = false;
    private string SelectedWeaveFactory { get; set; } = string.Empty;
    private WeaveAnalysisPlanDto WeaveFactoryDetails { get; set; } = new();
    private string SelectedWeaveGauge { get; set; } = string.Empty;
    private List<OrderDetailByGuageDto> WeaveModalStyles { get; set; } = new();
    private List<PlannedDataDto> WeaveDbPlannedPlans { get; set; } = new();
    private List<PlannedDataDto> WeaveOrderAllPlannedPlans { get; set; } = new();
    private bool IsLoadingWeaveModalStyles { get; set; } = false;
    private bool IsWeaveFullyPlannedEditMode { get; set; } = false;
    private int WeaveEditingPlanId { get; set; } = 0;

    // Weave planning inputs
    private decimal WeavePlanQty { get; set; }
    private int WeavePlanMachines { get; set; } = 1;
    private DateTime WeavePlanStartDate { get; set; } = DateTime.Today;
    private int WeaveMaxMachinesAvailable { get; set; } = 99;

    private decimal WeaveBaseDays { get; set; } = 1;
    private int WeaveBaseMachines { get; set; } = 1;
    private decimal WeaveBaseQty { get; set; } = 1;

    private DateTime? _customWeavePlanEndDate;
    private DateTime WeavePlanEndDate
    {
        get
        {
            if (string.Equals(SelectedWeaveFactory?.Trim(), "Gyatri Pashmina", StringComparison.OrdinalIgnoreCase))
            {
                return _customWeavePlanEndDate ?? DateTime.Today.AddDays(16);
            }
            if (WeaveBaseDays <= 0 || WeaveBaseQty <= 0 || WeavePlanMachines <= 0) return WeavePlanStartDate;
            int effectiveBaseMachines = WeaveBaseMachines > 0 ? WeaveBaseMachines : 1;
            decimal capPerMc = (WeaveBaseQty / WeaveBaseDays) / (decimal)effectiveBaseMachines;
            if (capPerMc <= 0) return WeavePlanStartDate;
            double daysNeeded = (double)(WeavePlanQty / (capPerMc * WeavePlanMachines));
            return AddWorkingDays(WeavePlanStartDate, daysNeeded);
        }
        set
        {
            if (string.Equals(SelectedWeaveFactory?.Trim(), "Gyatri Pashmina", StringComparison.OrdinalIgnoreCase))
            {
                _customWeavePlanEndDate = value;
            }
        }
    }

    private DateTime _weaveEditStartDate = DateTime.Now;
    private DateTime WeaveEditStartDate
    {
        get => _weaveEditStartDate;
        set
        {
            _weaveEditStartDate = value;
            if (WeaveEditingPlanId > 0)
            {
                RecalculateWeaveEditMaxMachines();
            }
        }
    }

    private int WeaveEditMachines { get; set; } = 1;
    private decimal WeaveEditQty { get; set; } = 0;
    private int WeaveEditMaxMachines { get; set; } = 99;
    private decimal WeaveEditMaxQty { get; set; } = 999999;

    private DateTime WeaveEditEndDate
    {
        get
        {
            if (WeaveBaseDays <= 0 || WeaveBaseQty <= 0 || WeaveEditMachines <= 0) return WeaveEditStartDate;
            int effectiveBaseMachines = WeaveBaseMachines > 0 ? WeaveBaseMachines : 1;
            decimal capPerMc = (WeaveBaseQty / WeaveBaseDays) / (decimal)effectiveBaseMachines;
            if (capPerMc <= 0) return WeaveEditStartDate;
            double daysNeeded = (double)(WeaveEditQty / (capPerMc * WeaveEditMachines));
            return AddWorkingDays(WeaveEditStartDate, daysNeeded);
        }
    }

    private List<OrderDetailByGuageDto> ModalStyles { get; set; } = new();
    private List<PlannedDataDto> DbPlannedPlans { get; set; } = new();
    private List<MachinePlaningDto> MachinePlaningList { get; set; } = new();
    private List<MachinePlaningDto> SelectedMachinesList { get; set; } = new();
    private bool IsMachineDropdownOpen { get; set; } = false;
    private List<PlannedDataDto> OrderAllPlannedPlans { get; set; } = new();
    private string SelectedModalGauge { get; set; } = string.Empty;
    private bool IsLoadingModalStyles { get; set; } = false;
    private bool IsGanttModalOpen { get; set; } = false;
    private string GanttModalGauge { get; set; } = string.Empty;
    private List<KnitGanttChartDto> GanttChartPlans { get; set; } = new();
    private DateTime TodayDate => DateTime.Today;
    
    private bool IsAlertOpen { get; set; } = false;
    private string AlertTitle { get; set; } = "Allocation Limit Warning";
    private string AlertMessage { get; set; } = string.Empty;
    private string AlertType { get; set; } = "warning";

    // Display order for the Order Summary knit types.
    private static readonly string[] KnitTypeOrder = { "Knit", "Weave", "Silk", "Linen", "Other" };
    private int KnitTypeRank(string? type)
    {
        int i = Array.FindIndex(KnitTypeOrder, t => string.Equals(t, type?.Trim(), StringComparison.OrdinalIgnoreCase));
        return i < 0 ? KnitTypeOrder.Length : i;
    }

    private void ShowAlert(string title, string message, string type = "warning")
    {
        AlertTitle = title;
        AlertMessage = message;
        AlertType = type;
        IsAlertOpen = true;
        StateHasChanged();
    }
    
    public class ManualPlanEntry
    {
        public DateTime StartDate { get; set; }
        public string Gauge { get; set; } = string.Empty;
        public int Machines { get; set; }
        public decimal Qty { get; set; }
        public DateTime EndDate { get; set; }
    }
    
    private List<ManualPlanEntry> ManualPlans { get; set; } = new();

    public class GaugePlanInput
    {
        public DateTime StartDate { get; set; } = DateTime.Today;
        public decimal Qty { get; set; }
        public int Machines { get; set; } = 1;
    }

    private Dictionary<string, GaugePlanInput> GaugeInputs { get; set; } = new();

    private GaugePlanInput GetGaugeInput(string gauge)
    {
        var key = gauge?.Trim().ToUpper() ?? "";
        if (string.IsNullOrEmpty(key)) return new GaugePlanInput();

        if (!GaugeInputs.TryGetValue(key, out var input))
        {
            decimal remaining = 0;
            DateTime startDate = DateTime.Today.AddDays(1);
            int suggestedMc = 1;

            if (SelectedKnitType == "Silk" || SelectedKnitType == "Other" || SelectedKnitType == "Linen")
            {
                var masterData = FabricAnalysisData?.MasterWorkload?.FirstOrDefault(m => 
                    string.Equals(m.MasterId?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase) || 
                    string.Equals(m.MasterName?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
                
                var plannedQty = OrderAllPlannedPlans?.Where(p => 
                    string.Equals(p.Gauge?.Trim(), masterData?.MasterId?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Gauge?.Trim(), masterData?.MasterName?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(p.Gauge?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase)
                ).Sum(p => p.Quantity) ?? 0;
                
                remaining = masterData != null ? Math.Max(0, masterData.NewOrderQty - plannedQty) : 0;
                startDate = (masterData?.MasterFreeDate ?? DateTime.Today).AddDays(1);
                suggestedMc = masterData != null && masterData.RunningMachines.HasValue && masterData.RunningMachines.Value > 0 ? masterData.RunningMachines.Value : 1;
            }
            else
            {
                var machineData = PlanningDetail?.MachineStatus?.FirstOrDefault(m => string.Equals(m.Gauge?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
                var plannedQty = OrderAllPlannedPlans?.Where(p => string.Equals(p.Gauge?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase)).Sum(p => p.Quantity) ?? 0;
                remaining = machineData != null ? Math.Max(0, machineData.NewOrderQty - plannedQty) : 0;
                startDate = GetGaugeFreeDate(gauge ?? "").AddDays(1);
                suggestedMc = machineData != null && machineData.SuggestedNewOrderMachines > 0 ? machineData.SuggestedNewOrderMachines : 1;
            }

            input = new GaugePlanInput
            {
                StartDate = startDate,
                Qty = remaining,
                Machines = suggestedMc
            };
            GaugeInputs[key] = input;
        }
        return input;
    }

    // ---- Overtime & Saturday-working options (optional) ----
    private const decimal StandardHoursPerDay = 8m;

    private bool _enableOvertime = false;
    private bool EnableOvertime
    {
        get => _enableOvertime;
        set { _enableOvertime = value; RecalculateDeadlineFeasibility(); }
    }

    private decimal _overtimeHoursPerDay = 4m;
    private decimal OvertimeHoursPerDay
    {
        get => _overtimeHoursPerDay;
        set { _overtimeHoursPerDay = value < 0 ? 0 : value; RecalculateDeadlineFeasibility(); }
    }

    // When on, Saturdays are treated as working days (overtime / holiday working).
    private bool _workSaturday = false;
    private bool WorkSaturday
    {
        get => _workSaturday;
        set { _workSaturday = value; RecalculateDeadlineFeasibility(); }
    }

    // Output multiplier from overtime: longer hours => more output per machine-day.
    private decimal OvertimeFactor =>
        EnableOvertime && OvertimeHoursPerDay > 0
            ? (StandardHoursPerDay + OvertimeHoursPerDay) / StandardHoursPerDay
            : 1m;

    // Plan start = machine free date + 1 day. If that lands on Saturday (a holiday),
    // push to Sunday — unless Saturday working is enabled.
    private DateTime GetMachinePlanStartDate(DateTime freeDate)
    {
        DateTime start = freeDate.AddDays(1);
        if (!WorkSaturday && start.DayOfWeek == DayOfWeek.Saturday)
        {
            start = start.AddDays(1);
        }
        return start;
    }

    private DateTime AddWorkingDays(DateTime startDate, double workingDays)
    {
        if (workingDays <= 0) return NormalizeOffSaturday(startDate);

        DateTime currentDate = startDate;
        int wholeDays = (int)Math.Floor(workingDays);
        double fraction = workingDays - wholeDays;

        // Add the whole working days (Saturdays count only when Saturday working is on).
        int daysAdded = 0;
        while (daysAdded < wholeDays)
        {
            currentDate = currentDate.AddDays(1);
            if (WorkSaturday || currentDate.DayOfWeek != DayOfWeek.Saturday)
            {
                daysAdded++;
            }
        }

        // Add the fractional day if there is any
        if (fraction > 0)
        {
            currentDate = currentDate.AddDays(fraction);
        }

        // If the end date lands on Saturday (holiday), move it to Sunday — unless Saturday working is on.
        return NormalizeOffSaturday(currentDate);
    }

    // Saturday is a holiday: push any date that lands on it to Sunday, unless Saturday working is enabled.
    private DateTime NormalizeOffSaturday(DateTime date)
    {
        if (WorkSaturday) return date;
        return date.DayOfWeek == DayOfWeek.Saturday ? date.AddDays(1) : date;
    }

    private decimal ComputeDaysForInput(MachinePlanningStatusDto? item, GaugePlanInput input)
    {
        if (item == null || input == null || input.Machines <= 0 || item.NewOrderDays <= 0 || item.NewOrderQty <= 0) return 0;
        
        // NewOrderDays are single-machine days (style_target = pcs/day per machine).
        decimal capPerMc = item.NewOrderQty / item.NewOrderDays;
        
        if (capPerMc <= 0) return 0;
        
        return input.Qty / (capPerMc * input.Machines);
    }

    private DateTime ComputeEndDateForInput(MachinePlanningStatusDto? item, GaugePlanInput input)
    {
        if (item == null || input == null) return DateTime.Today;
        var days = ComputeDaysForInput(item, input);
        return AddWorkingDays(input.StartDate, (double)days);
    }
    
    // Planning Form Fields
    private string PlanGauge { get; set; } = string.Empty;
    
    private decimal _planQty;
    private decimal PlanQty 
    { 
        get => _planQty; 
        set 
        { 
            _planQty = value; 
            if (SelectedKnitType == "Knit")
            {
                AutoSelectKnitMachines();
            }
        } 
    }

    // ---- Knit deadline: ship date minus a fixed buffer for downstream departments ----
    private const int ShipBufferDays = 10;

    // The date knitting must finish by: ship - 10 days, or the 65% lead-time rule
    // when the entry date is known - whichever is STRICTER (earlier).
    private DateTime? GetKnitDeadline() => GetKnitDeadlineFor(SelectedOrders.LastOrDefault());

    // Same rule for any order (used by deadline-ordered bulk planning / EDD).
    private DateTime? GetKnitDeadlineFor(MonthlyOrderDetailDto? orderRef)
    {
        if (orderRef == null || orderRef.OrderLDate <= DateTime.MinValue) return null;

        DateTime ship = orderRef.OrderLDate.Date;
        DateTime deadline = ship.AddDays(-ShipBufferDays);

        if (orderRef.OrderEntryDate.HasValue && orderRef.OrderEntryDate.Value.Date < ship)
        {
            var leadDays = (ship - orderRef.OrderEntryDate.Value.Date).TotalDays;
            var pctDeadline = orderRef.OrderEntryDate.Value.Date.AddDays(leadDays * 0.65);
            if (pctDeadline < deadline) deadline = pctDeadline;
        }
        return deadline;
    }

    // Saturdays falling inclusively between two dates (the holidays a plan would skip).
    private int CountSaturdays(DateTime from, DateTime to)
    {
        if (to.Date < from.Date) return 0;
        int c = 0;
        for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
            if (d.DayOfWeek == DayOfWeek.Saturday) c++;
        return c;
    }

    // Saturdays inside the current plan window (earliest start -> projected end). These
    // are the holidays enabling "Work Saturdays" would convert into working days.
    private int SaturdaysInPlanWindow
    {
        get
        {
            if (SelectedMachinesList == null || !SelectedMachinesList.Any()) return 0;
            var start = GetMachinePlanStartDate(SelectedMachinesList.Min(m => m.FreeDate));
            var end = GetMaxSelectedEndDate();
            return CountSaturdays(start, end);
        }
    }

    // Working days between two dates (Saturdays count only when Work Saturdays is on).
    private int CountWorkingDays(DateTime from, DateTime to)
    {
        if (to.Date <= from.Date) return 0;
        int days = 0;
        for (var d = from.Date; d < to.Date; d = d.AddDays(1))
        {
            if (WorkSaturday || d.DayOfWeek != DayOfWeek.Saturday) days++;
        }
        return days;
    }

    // Set by AutoSelectKnitMachines: the deadline can't be met even with all machines.
    private bool DeadlineInfeasible { get; set; }
    private double DeadlineSuggestedOtHours { get; set; }
    private string DeadlineSuggestionMessage { get; set; } = string.Empty;
    private DateTime? CurrentKnitDeadline { get; set; }

    // Knitters of the selected gauge (staffing ceiling for concurrent machines).
    private int GaugeKnitterCount { get; set; }
    // Set when knitter availability in the window forced fewer machines than ideal.
    private bool KnitterWindowLimited { get; set; }
    private int KnitterIdealN { get; set; }

    // ADVISORY ONLY (Phase 1): skill-aware factory staffing for the plan window.
    // Days that cannot be fully staffed by skilled knitters (bipartite matching across
    // ALL gauges). Does NOT influence machine selection - purely informational.
    // Hidden for now: set true to show the preview strip (engine stays built either way).
    private bool ShowStaffingAdvisory { get; set; } = false;
    private List<KnitterStaffingDayDto> WindowStaffing { get; set; } = new();

    // ---- Knitter schedule popup: who (of this gauge's knitters) is busy, from-to ----
    private bool IsKnitterScheduleOpen { get; set; }
    private bool IsLoadingKnitterSchedule { get; set; }
    private List<KnitterDto> ScheduleKnitters { get; set; } = new();
    private List<KnitterBusyDto> ScheduleBusy { get; set; } = new();

    private async Task OpenKnitterSchedule()
    {
        IsKnitterScheduleOpen = true;
        IsLoadingKnitterSchedule = true;
        ScheduleKnitters = new();
        ScheduleBusy = new();
        StateHasChanged();
        try
        {
            var knitters = await PlanningService.GetKnittersByGaugeAsync(SelectedModalGauge);
            var busy = await PlanningService.GetKnitterBusyAsync();
            ScheduleKnitters = knitters.ToList();
            var cards = ScheduleKnitters.Select(k => k.CardNo?.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToHashSet();
            // This gauge's knitters that are still committed (not completed).
            ScheduleBusy = busy
                .Where(b => cards.Contains(b.CardNo?.Trim()) && !string.Equals(b.Status, "Completed", StringComparison.OrdinalIgnoreCase))
                .OrderBy(b => b.FromDate)
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"OpenKnitterSchedule error: {ex.Message}");
        }
        finally
        {
            IsLoadingKnitterSchedule = false;
            StateHasChanged();
        }
    }

    private void CloseKnitterSchedule()
    {
        IsKnitterScheduleOpen = false;
        StateHasChanged();
    }

    private async Task LoadWindowStaffing()
    {
        WindowStaffing = new();
        if (!ShowStaffingAdvisory || SelectedKnitType != "Knit") return; // hidden -> skip the call too
        try
        {
            var from = DateTime.Today;
            var to = GetKnitDeadline() ?? DateTime.Today.AddDays(42);
            if (to < from) to = from.AddDays(42);
            var all = await PlanningService.GetKnitterStaffingAsync(from, to);
            WindowStaffing = all.Where(s => !s.Staffable).ToList(); // only the problem days
        }
        catch
        {
            WindowStaffing = new(); // advisory - never break planning on failure
        }
    }

    // A machine kept running by the new plan occupies a knitter until the common
    // finish; every other gauge machine occupies one until its own free date. At no
    // day in the window may occupied machines exceed the gauge's knitter count.
    private bool KnitterWindowFeasible(List<MachinePlaningDto> selected, DateTime finish)
    {
        int k = GaugeKnitterCount;
        if (k <= 0 || MachinePlaningList == null || !MachinePlaningList.Any()) return true; // no data - don't block

        var selectedIds = selected.Select(s => s.Machine_ID).ToHashSet();

        // Concurrency only rises when a selected machine begins its new work,
        // so checking at each selected start date covers the worst days.
        foreach (var d in selected.Select(m => GetMachinePlanStartDate(m.FreeDate).Date).Distinct())
        {
            if (d > finish.Date) continue;
            int occupied = MachinePlaningList.Count(m =>
                m.FreeDate.Date >= d ||                  // still on its existing queue
                selectedIds.Contains(m.Machine_ID));     // extended by our plan until the finish
            if (occupied > k) return false;
        }
        return true;
    }

    private void AutoSelectKnitMachines()
    {
        if (MachinePlaningList == null || !MachinePlaningList.Any())
        {
            SelectedMachinesList = new();
            return;
        }

        // BaseDays are single-machine days (style_target = pcs/day per machine),
        // so the per-machine rate is simply qty/days - no further division.
        decimal capPerMc = (BaseQty / BaseDays) * OvertimeFactor;

        if (capPerMc <= 0 || PlanQty <= 0)
        {
            SelectedMachinesList = new();
            return;
        }

        // One machine per "machine group": a big color (qty >= threshold) gets its own
        // machine; small colors (qty < threshold) of the SAME style merge onto one machine.
        int groupCount = GetOrderedGroupKeys().Count;

        // Workload cap: don't open more machines than the quantity justifies. We aim for
        // at least ~MinQtyPerMachine pieces per machine, so a small order spread over many
        // styles (e.g. 120 pcs across 13 styles) doesn't grab one machine per style.
        int workloadN = (int)Math.Ceiling((double)(PlanQty / MinQtyPerMachine));
        if (workloadN < 1) workloadN = 1;

        // Use the style/colour groups, but never more machines than the workload warrants.
        int idealN = Math.Min(groupCount, workloadN);
        if (idealN < 1) idealN = 1;

        // Deadline-driven minimum: knitting must end by ship - 10 days (or the 65%
        // rule, whichever is stricter) so downstream departments keep their window.
        DeadlineInfeasible = false;
        DeadlineSuggestedOtHours = 0;
        CurrentKnitDeadline = GetKnitDeadline();
        double totalWorkDays = (double)(PlanQty / capPerMc);
        int availableDays = 0;
        if (CurrentKnitDeadline != null)
        {
            var earliestStart = GetMachinePlanStartDate(MachinePlaningList.Min(m => m.FreeDate));
            availableDays = CountWorkingDays(earliestStart, CurrentKnitDeadline.Value);
            if (availableDays > 0)
            {
                int minMachinesByDate = (int)Math.Ceiling(totalWorkDays / availableDays);
                if (idealN < minMachinesByDate) idealN = minMachinesByDate;
            }
        }

        // Capacity cap: never more machines than are physically available for the gauge.
        if (idealN > MachinePlaningList.Count) idealN = MachinePlaningList.Count;

        // Labor cap: never more concurrent machines than the gauge has knitters
        // (TrueGaugeLimit = min(machines, knitters) from the capacity proc).
        var capData = PlanningDetail?.MachineStatus?.FirstOrDefault(m =>
            string.Equals(m.Gauge?.Trim(), PlanGauge?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (capData != null && capData.TrueGaugeLimit > 0 && idealN > capData.TrueGaugeLimit)
        {
            idealN = capData.TrueGaugeLimit;
        }

        var sortedMachines = MachinePlaningList.OrderBy(m => m.FreeDate).ToList();

        // Earliest-finish selection with finish-aligned shares: for each candidate
        // count, size every machine's share to its available days (waterline) so all
        // machines would FINISH together, then keep the count whose common finish is
        // earliest. A machine that frees up too late naturally gets a ~zero share and
        // is dropped, so it can never push the end date out.
        int bestN = 1;
        DateTime bestEnd = DateTime.MaxValue;
        Dictionary<int, decimal> bestTargets = new();
        int maxN = Math.Min(idealN, sortedMachines.Count);
        KnitterWindowLimited = false;
        KnitterIdealN = 0;
        for (int n = 1; n <= maxN; n++)
        {
            var subset = sortedMachines.Take(n).ToList();
            var (targets, finish) = ComputeFinishAlignedPlan(subset, PlanQty, capPerMc);

            // Staffing ceiling: running n machines in this window must not need
            // more knitters than the gauge has free, day by day.
            if (n > 1 && !KnitterWindowFeasible(subset, finish))
            {
                KnitterWindowLimited = true;
                KnitterIdealN = maxN;
                break; // occupancy only grows with n - larger counts are infeasible too
            }

            // <= : on a same-date tie prefer more machines (colour separation);
            // useless late machines are filtered out below by their zero share.
            if (finish.Date <= bestEnd.Date) { bestN = n; bestEnd = finish; bestTargets = targets; }
        }

        SelectedMachinesList = sortedMachines.Take(bestN)
            .Where(m => bestTargets.TryGetValue(m.Machine_ID, out var t) && t >= 1m)
            .ToList();
        if (!SelectedMachinesList.Any())
        {
            SelectedMachinesList = sortedMachines.Take(1).ToList();
        }
        PlanMachines = SelectedMachinesList.Count;

        BuildSizeAllocations();
    }

    // Finish-aligned ("waterline") plan: shares are sized to each machine's available
    // working days so every machine finishes on the SAME date. Late starters get less;
    // a machine whose start lies at/after the common finish gets 0.
    // Returns each machine's qty target and the common finish date.
    private (Dictionary<int, decimal> Targets, DateTime Finish) ComputeFinishAlignedPlan(
        List<MachinePlaningDto> machines, decimal totalQty, decimal capPerMc)
    {
        var targets = new Dictionary<int, decimal>();
        if (machines == null || machines.Count == 0 || totalQty <= 0 || capPerMc <= 0)
            return (targets, PlanStartDate);

        var ordered = machines.OrderBy(m => m.FreeDate).ToList();
        DateTime earliest = GetMachinePlanStartDate(ordered[0].FreeDate);

        // Head start (working days) the earliest machine has over each other machine.
        var offsets = new List<double>();
        foreach (var m in ordered)
        {
            var start = GetMachinePlanStartDate(m.FreeDate);
            offsets.Add(Math.Max(0, CountWorkingDays(earliest, start) - 1));
        }

        double totalDays = (double)(totalQty / capPerMc);

        // Waterline T (working days after the earliest start): machines with
        // offset < T run for (T - offset) days and those runs sum to totalDays.
        double T = totalDays;
        double offSum = 0;
        for (int k = 1; k <= ordered.Count; k++)
        {
            offSum += offsets[k - 1];
            double t = (totalDays + offSum) / k;
            if (t >= offsets[k - 1] && (k == ordered.Count || t <= offsets[k]))
            {
                T = t;
                break;
            }
            if (k == ordered.Count) T = t;
        }

        for (int i = 0; i < ordered.Count; i++)
        {
            targets[ordered[i].Machine_ID] = offsets[i] >= T
                ? 0
                : (decimal)(T - offsets[i]) * capPerMc;
        }

        return (targets, AddWorkingDays(earliest, T));
    }

    // ---- Overview-grid suggestion: auto machine count + Est. End per gauge ----
    // Computed without opening the popup, so planners see the recommendation inline.
    private Dictionary<string, (int Mc, DateTime End)> GaugeSuggestions { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<MachinePlaningDto>> _gaugeMachineCache = new(StringComparer.OrdinalIgnoreCase);
    private bool IsScanningSuggestions { get; set; }

    // Pure projection mirroring AutoSelectKnitMachines' machine-count choice + finish date,
    // using only the gauge's machine list and order figures (no style breakdown needed).
    private (int Mc, DateTime End) SuggestGaugePlan(MachinePlanningStatusDto item, List<MachinePlaningDto> machines)
    {
        if (machines == null || !machines.Any() || item.NewOrderQty <= 0 || item.NewOrderDays <= 0)
            return (item.SuggestedNewOrderMachines, DateTime.Today);

        decimal capPerMc = item.NewOrderQty / item.NewOrderDays; // single-machine rate, no OT on overview
        if (capPerMc <= 0) return (item.SuggestedNewOrderMachines, DateTime.Today);

        decimal qty = item.NewOrderQty;
        int idealN = (int)Math.Ceiling((double)(qty / MinQtyPerMachine));
        if (idealN < 1) idealN = 1;

        // Deadline-driven minimum (ship - buffer / 65%).
        var deadline = GetKnitDeadlineFor(SelectedOrders.LastOrDefault());
        if (deadline != null)
        {
            var earliestStart = GetMachinePlanStartDate(machines.Min(m => m.FreeDate));
            int availDays = CountWorkingDays(earliestStart, deadline.Value);
            if (availDays > 0)
            {
                int minByDate = (int)Math.Ceiling((double)(qty / capPerMc) / availDays);
                if (idealN < minByDate) idealN = minByDate;
            }
        }

        idealN = Math.Min(idealN, machines.Count);
        if (item.TrueGaugeLimit > 0) idealN = Math.Min(idealN, item.TrueGaugeLimit);
        if (idealN < 1) idealN = 1;

        var sorted = machines.OrderBy(m => m.FreeDate).ToList();
        int bestN = 1;
        DateTime bestEnd = DateTime.MaxValue;
        Dictionary<int, decimal> bestTargets = new();
        for (int n = 1; n <= idealN; n++)
        {
            var (targets, finish) = ComputeFinishAlignedPlan(sorted.Take(n).ToList(), qty, capPerMc);
            if (finish.Date <= bestEnd.Date) { bestN = n; bestEnd = finish; bestTargets = targets; }
        }

        // Match AutoSelectKnitMachines exactly: it drops machines whose finish-aligned
        // share is < 1 piece, so the shown count must exclude those too.
        int effectiveN = sorted.Take(bestN)
            .Count(m => bestTargets.TryGetValue(m.Machine_ID, out var t) && t >= 1m);
        if (effectiveN < 1) effectiveN = 1;

        return (effectiveN, bestEnd == DateTime.MaxValue ? DateTime.Today : bestEnd);
    }

    // Scan all unplanned knit gauges and cache their suggested machine count + Est. End.
    private async Task ScanGaugeSuggestions()
    {
        GaugeSuggestions = new(StringComparer.OrdinalIgnoreCase);
        if (SelectedKnitType != "Knit" || PlanningDetail?.MachineStatus == null) return;

        var gauges = PlanningDetail.MachineStatus
            .Where(m => m.NewOrderQty > 0 && !string.IsNullOrEmpty(m.Gauge))
            .ToList();
        if (!gauges.Any()) return;

        IsScanningSuggestions = true;
        StateHasChanged();

        using var gate = new SemaphoreSlim(6);
        var tasks = gauges.Select(async item =>
        {
            var gauge = item.Gauge!.Trim();
            await gate.WaitAsync();
            try
            {
                if (!_gaugeMachineCache.TryGetValue(gauge, out var machines))
                {
                    machines = (await PlanningService.GetMachinePlaningAsync(gauge)).ToList();
                    lock (_gaugeMachineCache) { _gaugeMachineCache[gauge] = machines; }
                }
                var (mc, end) = SuggestGaugePlan(item, machines);
                lock (GaugeSuggestions) { GaugeSuggestions[gauge] = (mc, end); }
            }
            catch { /* leave gauge without a suggestion */ }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);

        IsScanningSuggestions = false;
        StateHasChanged();
    }

    // ---- Style / Color / Size allocation (Knit) ----

    private static readonly string[] SizeColumns =
        { "XXXS", "XXS", "S", "M", "L", "XL", "XXL", "XXXL", "OSFA" };

    private static decimal GetSizeQty(OrderDetailByGuageDto s, string sizeKey) => sizeKey switch
    {
        "XXXS" => s.XXXS,
        "XXS" => s.XXS,
        "S" => s.S,
        "M" => s.M,
        "L" => s.L,
        "XL" => s.XL,
        "XXL" => s.XXL,
        "XXXL" => s.XXXL,
        "OSFA" => s.OSFA,
        _ => 0
    };

    public class PlanSizeRow
    {
        public int OrderId { get; set; }
        public string StyleNo { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Size { get; set; } = string.Empty;
        public decimal TotalQty { get; set; }
        // machineId -> qty allocated to that machine for this style/color/size
        public Dictionary<int, decimal> PerMachine { get; set; } = new();
    }

    private List<PlanSizeRow> SizeAllocationRows { get; set; } = new();

    // Colours with a line qty below this merge with other small colours of the SAME
    // style onto one machine; colours at/above it get their own machine.
    private const decimal SmallColorQtyThreshold = 20m;

    // Target minimum pieces per machine for auto-selection. Caps the machine count so a
    // small order isn't spread thin (e.g. 120 pcs => ceil(120/30) = 4 machines max).
    private const decimal MinQtyPerMachine = 30m;

    // Total qty of an order line (one style+color) across all sizes (full order figures).
    private static decimal GetLineQty(OrderDetailByGuageDto s) =>
        SizeColumns.Sum(k => GetSizeQty(s, k));

    // Sum of all order-line sizes for the gauge (full order figures).
    private decimal FullSizeTotal() =>
        ModalStyles?.Where(s => !string.IsNullOrWhiteSpace(s.StyleNo)).Sum(s => GetLineQty(s)) ?? 0;

    // Factor that scales the full order sizes down to the remaining balance (PlanQty).
    private decimal ScaleFactor()
    {
        var full = FullSizeTotal();
        if (full <= 0 || PlanQty <= 0) return 0;
        return PlanQty / full;
    }

    // The "machine group" an order line belongs to (using the scaled/planned line qty):
    //  - big color (qty >= threshold)  -> its own group (style + color)
    //  - small color (qty < threshold) -> grouped with same-style small colors (style only)
    private string GetLineGroupKey(OrderDetailByGuageDto s)
    {
        string style = s.StyleNo?.Trim().ToUpperInvariant() ?? "";
        string color = s.OrderColor?.Trim().ToUpperInvariant() ?? "";
        decimal scaledLineQty = GetLineQty(s) * ScaleFactor();
        return scaledLineQty >= SmallColorQtyThreshold ? $"B|{style}|{color}" : $"S|{style}";
    }

    // Distinct machine-group keys in first-seen order.
    private List<string> GetOrderedGroupKeys()
    {
        var keys = new List<string>();
        if (ModalStyles == null) return keys;
        foreach (var s in ModalStyles.Where(s => !string.IsNullOrWhiteSpace(s.StyleNo)))
        {
            var k = GetLineGroupKey(s);
            if (!keys.Contains(k)) keys.Add(k);
        }
        return keys;
    }

    // Assign each machine-group to one machine (a big color = one machine; small
    // colors of the same style share a machine). The full order sizes are scaled
    // down proportionally so the grand total equals PlanQty (the remaining balance),
    // using a largest-remainder split so the integer cells sum EXACTLY to PlanQty.
    private void BuildSizeAllocations()
    {
        SizeAllocationRows = new();

        if (SelectedKnitType != "Knit") return;
        if (ModalStyles == null || !ModalStyles.Any()) return;
        if (SelectedMachinesList == null || !SelectedMachinesList.Any()) return;

        int n = SelectedMachinesList.Count;
        var machineIds = SelectedMachinesList.Select(m => m.Machine_ID).ToList();

        // Flatten every non-zero size line (full order figures).
        var lines = new List<(OrderDetailByGuageDto S, string Size, decimal Full)>();
        foreach (var s in ModalStyles.Where(s => !string.IsNullOrWhiteSpace(s.StyleNo)))
        {
            foreach (var sizeKey in SizeColumns)
            {
                decimal q = GetSizeQty(s, sizeKey);
                if (q > 0) lines.Add((s, sizeKey, q));
            }
        }
        if (lines.Count == 0) return;

        decimal fullTotal = lines.Sum(l => l.Full);
        int target = (int)Math.Round(PlanQty, MidpointRounding.AwayFromZero);
        if (target < 0) target = 0;

        // Proportionally scale each size line to hit `target` exactly (largest remainder).
        var scaled = new int[lines.Count];
        if (fullTotal > 0 && target > 0)
        {
            var fracs = new List<(int Idx, decimal Frac)>();
            int floorSum = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                decimal exact = lines[i].Full * target / fullTotal;
                int fl = (int)Math.Floor(exact);
                scaled[i] = fl;
                floorSum += fl;
                fracs.Add((i, exact - fl));
            }
            int leftover = target - floorSum;
            foreach (var f in fracs.OrderByDescending(x => x.Frac).Take(Math.Max(0, leftover)))
            {
                scaled[f.Idx] += 1;
            }
        }

        // Total (scaled) qty per machine-group, used to balance load across machines.
        var groupKeys = GetOrderedGroupKeys();
        var groupTotal = new Dictionary<string, decimal>();
        for (int i = 0; i < lines.Count; i++)
        {
            if (scaled[i] <= 0) continue;
            var gk = GetLineGroupKey(lines[i].S);
            groupTotal[gk] = (groupTotal.TryGetValue(gk, out var t) ? t : 0) + scaled[i];
        }

        // Finish-aligned targets: each machine's fair share given its own start date
        // (late starters get less so all machines finish together). Falls back to an
        // equal split when capacity figures are unusable.
        decimal scaledTotal = scaled.Sum();
        decimal allocCapPerMc = BaseDays > 0 && BaseQty > 0 ? (BaseQty / BaseDays) * OvertimeFactor : 0;
        var (mcTargets, _) = ComputeFinishAlignedPlan(SelectedMachinesList, scaledTotal, allocCapPerMc);
        foreach (var id in machineIds)
        {
            if (!mcTargets.ContainsKey(id)) mcTargets[id] = scaledTotal / n;
        }

        // Groups are placed against those targets: machines with the largest
        // remaining target capacity (deficit) get the biggest groups.
        var load = machineIds.ToDictionary(id => id, _ => 0m);

        var orderedGroups = groupKeys
            .OrderByDescending(k => groupTotal.TryGetValue(k, out var t) ? t : 0)
            .ToList();

        var groupMachine = new Dictionary<string, int>();

        // Phase 1 - keep one group per machine (one colour per machine): pair the
        // biggest groups with the lightest machines.
        var machinesByCapacity = machineIds.OrderByDescending(id => mcTargets[id]).ToList();
        int phase1 = Math.Min(orderedGroups.Count, n);
        for (int i = 0; i < phase1; i++)
        {
            int targetMachine = machinesByCapacity[i];
            groupMachine[orderedGroups[i]] = targetMachine;
            load[targetMachine] += groupTotal.TryGetValue(orderedGroups[i], out var gt) ? gt : 0;
        }

        // Phase 2 - any extra groups (more colours than machines) go to the machine
        // with the most remaining target capacity, keeping finishes aligned.
        for (int i = phase1; i < orderedGroups.Count; i++)
        {
            int targetMachine = machineIds.OrderByDescending(id => mcTargets[id] - load[id]).First();
            groupMachine[orderedGroups[i]] = targetMachine;
            load[targetMachine] += groupTotal.TryGetValue(orderedGroups[i], out var gt) ? gt : 0;
        }

        for (int i = 0; i < lines.Count; i++)
        {
            if (scaled[i] <= 0) continue;

            var s = lines[i].S;
            var key = GetLineGroupKey(s);
            int assignedMachineId = groupMachine.TryGetValue(key, out var mid) ? mid : machineIds[0];

            var row = new PlanSizeRow
            {
                OrderId = s.OrderId,
                StyleNo = s.StyleNo ?? string.Empty,
                Color = s.OrderColor ?? string.Empty,
                Size = lines[i].Size,
                TotalQty = scaled[i]
            };

            // Full (scaled) size qty goes to the assigned machine; 0 on the rest (editable).
            foreach (var machineId in machineIds)
            {
                row.PerMachine[machineId] = machineId == assignedMachineId ? scaled[i] : 0;
            }

            SizeAllocationRows.Add(row);
        }

        // Phase 3 - spillover: when more machines are selected than there are
        // style/colour groups (e.g. the ship-date rule forced extra machines),
        // machines left with 0 qty pull whole SIZE LINES from the most-loaded
        // machines until loads even out. A single size line is never split;
        // a colour MAY end up on two machines - that's the finish-date trade-off.
        SpillOverToIdleMachines(machineIds, mcTargets);

        // Re-evaluate the ship-date deadline against the ACTUAL projected end date,
        // so manual machine selection / qty edits also trigger the warning.
        RecalculateDeadlineFeasibility();
    }

    // Deadline feasibility from the real allocation: compares the projected max end
    // date of the current selection against the knit deadline (ship - buffer / 65%).
    private void RecalculateDeadlineFeasibility()
    {
        DeadlineInfeasible = false;
        DeadlineSuggestedOtHours = 0;
        DeadlineSuggestionMessage = string.Empty;
        CurrentKnitDeadline = GetKnitDeadline();

        if (CurrentKnitDeadline == null || SelectedKnitType != "Knit") return;
        if (SelectedMachinesList == null || !SelectedMachinesList.Any()) return;

        DateTime deadline = CurrentKnitDeadline.Value.Date;
        if (GetMaxSelectedEndDate().Date <= deadline) return;

        DeadlineInfeasible = true;

        // Smart suggestions simulation:
        bool canMeetWithSaturdaysOnly = false;
        decimal neededOtHoursOnly = 0;
        decimal neededOtHoursWithSaturdays = 0;

        // 1. Can we meet it with Saturdays only (if Saturdays is not already enabled)?
        if (!WorkSaturday)
        {
            DateTime endWithSaturdays = SimulateEndDate(true, EnableOvertime, OvertimeHoursPerDay);
            if (endWithSaturdays.Date <= deadline)
            {
                canMeetWithSaturdaysOnly = true;
            }
        }

        // 2. What OT hours do we need under current Saturday setting?
        // Loop from 0.5 to 8.0 hours
        for (decimal ot = 0.5m; ot <= 8.0m; ot += 0.5m)
        {
            DateTime endWithOt = SimulateEndDate(WorkSaturday, true, ot);
            if (endWithOt.Date <= deadline)
            {
                neededOtHoursOnly = ot;
                break;
            }
        }

        // 3. What OT hours do we need if we ALSO enable Saturday working?
        if (!WorkSaturday)
        {
            for (decimal ot = 0.5m; ot <= 8.0m; ot += 0.5m)
            {
                DateTime endWithBoth = SimulateEndDate(true, true, ot);
                if (endWithBoth.Date <= deadline)
                {
                    neededOtHoursWithSaturdays = ot;
                    break;
                }
            }
        }

        // How many Saturdays sit in the window - the holidays "Work Saturdays" would recover.
        int satCount = SaturdaysInPlanWindow;
        string satLabel = $"enable <b>Work Saturdays</b> ({satCount} Saturday{(satCount == 1 ? "" : "s")} in window)";

        // Build the suggestion message
        var suggestions = new List<string>();

        if (neededOtHoursOnly > 0)
        {
            suggestions.Add($"run <b class=\"text-teal-700 bg-teal-50 px-1.5 py-0.5 rounded border border-teal-100\">{neededOtHoursOnly:N1} overtime hrs/day</b>");
            DeadlineSuggestedOtHours = (double)neededOtHoursOnly;
        }

        if (canMeetWithSaturdaysOnly)
        {
            suggestions.Add(satLabel);
        }

        if (suggestions.Any())
        {
            DeadlineSuggestionMessage = "Suggested: " + string.Join(" or ", suggestions) + ".";
        }
        else if (neededOtHoursWithSaturdays > 0)
        {
            // Requires BOTH Saturday working and overtime
            DeadlineSuggestionMessage = $"Suggested: {satLabel} AND run <b class=\"text-teal-700 bg-teal-50 px-1.5 py-0.5 rounded border border-teal-100\">{neededOtHoursWithSaturdays:N1} overtime hrs/day</b>.";
        }
        else
        {
            // Even max OT + Saturday can't fully meet it: still surface OT/Saturday as
            // partial mitigation, but lead with the real fix (more machines / earlier start).
            DateTime bestPossible = SimulateEndDate(true, true, 8m);
            string gainNote = bestPossible.Date <= GetMaxSelectedEndDate().Date
                ? string.Empty
                : $" Max overtime (8 hrs/day) + Work Saturdays would pull the finish to <b>{bestPossible:dd-MMM-yyyy}</b>, still past the deadline.";
            DeadlineSuggestionMessage =
                "Suggested: add machines (or start earlier). " +
                $"Enabling <b>Overtime</b> and <b>Work Saturdays</b> ({satCount} Saturday{(satCount == 1 ? "" : "s")} in window) on the selected machines helps but is not enough alone." +
                gainNote;
        }
    }

    private DateTime SimulateEndDate(bool workSaturday, bool enableOvertime, decimal overtimeHours)
    {
        if (SelectedMachinesList == null || !SelectedMachinesList.Any() || BaseDays <= 0 || BaseQty <= 0) 
            return DateTime.Today;

        decimal testFactor = enableOvertime && overtimeHours > 0
            ? (StandardHoursPerDay + overtimeHours) / StandardHoursPerDay
            : 1m;

        decimal capPerMc = (BaseQty / BaseDays) * testFactor;
        if (capPerMc <= 0) return DateTime.Today;

        DateTime max = DateTime.MinValue;
        foreach (var machine in SelectedMachinesList)
        {
            decimal qtyPerMc = SizeAllocationRows.Any()
                ? GetMachineAllocatedQty(machine.Machine_ID)
                : PlanQty / (decimal)SelectedMachinesList.Count;

            double daysNeeded = (double)(qtyPerMc / capPerMc);
            DateTime start = GetMachinePlanStartDateSimulated(machine.FreeDate, workSaturday);

            DateTime end = start;
            if (daysNeeded > 0)
            {
                int wholeDays = (int)Math.Floor(daysNeeded);
                double fraction = daysNeeded - wholeDays;
                int daysAdded = 0;
                while (daysAdded < wholeDays)
                {
                    end = end.AddDays(1);
                    if (workSaturday || end.DayOfWeek != DayOfWeek.Saturday)
                    {
                        daysAdded++;
                    }
                }
                if (fraction > 0)
                {
                    end = end.AddDays(fraction);
                }
                if (!workSaturday && end.DayOfWeek == DayOfWeek.Saturday)
                {
                    end = end.AddDays(1);
                }
            }

            if (end > max) max = end;
        }

        return max == DateTime.MinValue ? DateTime.Today : max;
    }

    private DateTime GetMachinePlanStartDateSimulated(DateTime freeDate, bool workSaturday)
    {
        DateTime start = freeDate.AddDays(1);
        if (!workSaturday && start.DayOfWeek == DayOfWeek.Saturday)
        {
            start = start.AddDays(1);
        }
        return start;
    }

    // Move size lines from overloaded machines onto idle ones (whole lines only).
    private void SpillOverToIdleMachines(List<int> machineIds, Dictionary<int, decimal>? targets = null)
    {
        if (machineIds.Count < 2 || !SizeAllocationRows.Any()) return;

        decimal total = SizeAllocationRows.Sum(r => r.PerMachine.Values.Sum());
        if (total <= 0) return;

        // Per-machine targets: finish-aligned shares when provided, equal split otherwise.
        decimal equalShare = total / machineIds.Count;
        var goal = machineIds.ToDictionary(
            id => id,
            id => targets != null && targets.TryGetValue(id, out var t) ? t : equalShare);

        // Move whole lines from over-target machines to the most under-target machine,
        // but only while each move genuinely reduces the overall deviation.
        for (int guard = 0; guard < 500; guard++)
        {
            var loads = machineIds.ToDictionary(id => id, id => GetMachineAllocatedQty(id));

            var under = machineIds
                .Select(id => (Id: id, Deficit: goal[id] - loads[id]))
                .OrderByDescending(x => x.Deficit)
                .First();
            if (under.Deficit < 1m) break; // everyone is at (or within a piece of) target

            var over = machineIds
                .Select(id => (Id: id, Surplus: loads[id] - goal[id]))
                .OrderByDescending(x => x.Surplus)
                .First();
            if (over.Surplus <= 0) break;

            // Lines currently sitting on the over-target machine.
            var candidates = SizeAllocationRows
                .Where(r => r.PerMachine.TryGetValue(over.Id, out var q) && q > 0)
                .ToList();
            if (candidates.Count <= 1 && loads[under.Id] > 0) break; // never strip a machine's last line

            decimal need = Math.Min(under.Deficit, over.Surplus);
            var line = candidates
                .OrderBy(r => Math.Abs(r.PerMachine[over.Id] - need))
                .FirstOrDefault();
            if (line == null) break;

            decimal qty = line.PerMachine[over.Id];

            // Only move if it brings both machines closer to their targets overall.
            decimal devBefore = Math.Abs(over.Surplus) + Math.Abs(under.Deficit);
            decimal devAfter = Math.Abs(over.Surplus - qty) + Math.Abs(under.Deficit - qty);
            if (devAfter >= devBefore) break;

            line.PerMachine[over.Id] = 0;
            line.PerMachine[under.Id] = (line.PerMachine.TryGetValue(under.Id, out var cur) ? cur : 0) + qty;
        }
    }

    // Latest (max) estimated end date across all selected machines.
    private DateTime GetMaxSelectedEndDate()
    {
        if (SelectedMachinesList == null || !SelectedMachinesList.Any()) return PlanEndDate;

        // BaseDays are single-machine days (style_target = pcs/day per machine),
        // so the per-machine rate is simply qty/days - no further division.
        decimal capPerMc = (BaseQty / BaseDays) * OvertimeFactor;

        DateTime max = DateTime.MinValue;
        foreach (var machine in SelectedMachinesList)
        {
            decimal qtyPerMc = SizeAllocationRows.Any()
                ? GetMachineAllocatedQty(machine.Machine_ID)
                : PlanQty / (decimal)SelectedMachinesList.Count;
            double daysNeeded = capPerMc > 0 ? (double)(qtyPerMc / capPerMc) : 0;
            DateTime end = AddWorkingDays(GetMachinePlanStartDate(machine.FreeDate), daysNeeded);
            if (end > max) max = end;
        }
        return max == DateTime.MinValue ? PlanEndDate : max;
    }

    // Start/end window a single selected machine would occupy under the current plan.
    private (DateTime Start, DateTime End) GetMachineWindow(MachinePlaningDto machine)
    {
        var start = GetMachinePlanStartDate(machine.FreeDate);
        decimal capPerMc = (BaseDays > 0) ? (BaseQty / BaseDays) * OvertimeFactor : 0;
        decimal qty = SizeAllocationRows.Any()
            ? GetMachineAllocatedQty(machine.Machine_ID)
            : PlanQty / (decimal)Math.Max(1, SelectedMachinesList.Count);
        double days = capPerMc > 0 ? (double)(qty / capPerMc) : 0;
        return (start, AddWorkingDays(start, days));
    }

    // Factory-wide knitter ceiling: on no working day may total busy machines (all
    // gauges) exceed total knitters (1 knitter runs 1 machine at a time). Returns the
    // worst offending day + the machine count it would need, or null if within capacity.
    private async Task<(DateTime Day, int Needed, int Knitters)?> CheckKnitterCapacityAsync()
    {
        if (SelectedKnitType != "Knit" || SelectedMachinesList == null || !SelectedMachinesList.Any())
            return null;

        var windows = SelectedMachinesList.Select(GetMachineWindow).ToList();
        var from = windows.Min(w => w.Start).Date;
        var to = windows.Max(w => w.End).Date;

        int knitters;
        List<KnitterStaffingDayDto> staffing;
        try
        {
            // Knit-only busy machines per day (weave/silk use no knitters).
            staffing = await PlanningService.GetKnitterStaffingAsync(from, to);
            // Factory total knitters comes from the report.
            var report = await PlanningService.GetPlaningReportAsync(from, to);
            knitters = report.FirstOrDefault()?.TotalKnitters ?? 0;
        }
        catch { return null; } // never block a save because the capacity lookup failed

        if (knitters <= 0) return null;

        (DateTime Day, int Needed, int Knitters)? worst = null;
        for (var d = from; d <= to; d = d.AddDays(1))
        {
            if (d.DayOfWeek == DayOfWeek.Saturday && !WorkSaturday) continue; // holiday
            int existing = staffing.FirstOrDefault(r => r.Date.Date == d.Date)?.MachinesRunning ?? 0;
            int added = windows.Count(w => d.Date >= w.Start.Date && d.Date <= w.End.Date);
            int total = existing + added;
            if (total > knitters && (worst == null || total > worst.Value.Needed))
                worst = (d, total, knitters);
        }
        return worst;
    }

    // Total currently allocated to a machine across all size rows.
    private decimal GetMachineAllocatedQty(int machineId) =>
        SizeAllocationRows.Sum(r => r.PerMachine.TryGetValue(machineId, out var q) ? q : 0);

    private decimal GetSizeRowAllocated(PlanSizeRow row) =>
        row.PerMachine.Values.Sum();

    // Bumped to force the size-cell <input>s to recreate so the DOM reflects a reset value
    // (Blazor won't update an input when the new value equals what it last rendered).
    private int CellResetVersion { get; set; } = 0;

    private void SetSizeCell(PlanSizeRow row, int machineId, decimal value)
    {
        if (value < 0) value = 0;

        // Machine qty (the row's allocation across all machines) must not exceed Plan Qty.
        decimal othersTotal = row.PerMachine.Where(kv => kv.Key != machineId).Sum(kv => kv.Value);
        if (othersTotal + value > row.TotalQty)
        {
            // Reset this machine to the maximum it can take (its Plan Qty).
            decimal maxForMachine = row.TotalQty - othersTotal;
            if (maxForMachine < 0) maxForMachine = 0;
            row.PerMachine[machineId] = maxForMachine;
            CellResetVersion++; // force the input to recreate so it shows the reset value

            ShowAlert(
                "Allocation Limit Exceeded",
                $"Machine quantity for {row.StyleNo} / {row.Color} / {row.Size} cannot exceed the Plan Qty ({row.TotalQty:N0}). It has been reset to {maxForMachine:N0}.",
                "warning");
            StateHasChanged();
            return;
        }

        row.PerMachine[machineId] = value;
        RecalculateDeadlineFeasibility();
        StateHasChanged();
    }

    private void OnSizeCellChanged(PlanSizeRow row, int machineId, object? value)
    {
        decimal.TryParse(value?.ToString(), out var q);
        SetSizeCell(row, machineId, q);
    }

    // Grand total currently allocated across every machine and size row.
    private decimal TotalSizeAllocated => SizeAllocationRows.Sum(r => r.PerMachine.Values.Sum());

    private int MaxMachinesAvailable { get; set; } = 99;

    private int _planMachines = 1;
    private int PlanMachines 
    { 
        get => _planMachines; 
        set { _planMachines = value > MaxMachinesAvailable ? MaxMachinesAvailable : (value < 1 ? 1 : value); } 
    }

    private DateTime _planStartDate = DateTime.Now;
    private DateTime PlanStartDate 
    { 
        get => _planStartDate; 
        set 
        { 
            _planStartDate = value; 
            RecalculateMaxMachines();
            StateHasChanged(); 
        } 
    }

    private void RecalculateMaxMachines()
    {
        // Silk/Other/Linen are outstation masters: machines are not planned here.
        // The Mc input is hidden and the plan always saves Machines = 1.
        if (IsMasterBasedType)
        {
            MaxMachinesAvailable = 1;
            PlanMachines = 1;
            return;
        }

        if (PlanningDetail?.MachineStatus == null) return;

        var machineData = PlanningDetail.MachineStatus.FirstOrDefault(m => string.Equals(m.Gauge?.Trim(), PlanGauge?.Trim(), StringComparison.OrdinalIgnoreCase));
        int totalLimit = (machineData != null && machineData.TrueGaugeLimit > 0) ? machineData.TrueGaugeLimit : 2;

        int freeMachines = totalLimit;

        if (PlanningDetail?.ForwardTimeline != null && !string.IsNullOrEmpty(PlanGauge))
        {
            var timelineEntry = PlanningDetail.ForwardTimeline
                .FirstOrDefault(t => string.Equals(t.Gauge?.Trim(), PlanGauge?.Trim(), StringComparison.OrdinalIgnoreCase) 
                                     && t.PlanSnapshotDate.Date == PlanStartDate.Date);

            if (timelineEntry != null)
            {
                int capLimit = timelineEntry.TotalActiveCapacityLimit > 0 ? timelineEntry.TotalActiveCapacityLimit : totalLimit;
                freeMachines = timelineEntry.ImmediateFreeMachines > 0 ? timelineEntry.ImmediateFreeMachines : capLimit;
            }
            else
            {
                var timelineForGauge = PlanningDetail.ForwardTimeline
                    .Where(t => string.Equals(t.Gauge?.Trim(), PlanGauge?.Trim(), StringComparison.OrdinalIgnoreCase))
                    .OrderBy(t => t.PlanSnapshotDate)
                    .ToList();

                if (timelineForGauge.Any())
                {
                    var lastEntry = timelineForGauge.Last();
                    if (PlanStartDate.Date > lastEntry.PlanSnapshotDate.Date)
                    {
                        freeMachines = lastEntry.TotalActiveCapacityLimit > 0 ? lastEntry.TotalActiveCapacityLimit : totalLimit;
                    }
                    else if (PlanStartDate.Date < timelineForGauge.First().PlanSnapshotDate.Date)
                    {
                        freeMachines = timelineForGauge.First().TotalActiveCapacityLimit > 0 ? timelineForGauge.First().TotalActiveCapacityLimit : totalLimit;
                    }
                    else
                    {
                        var precedingEntry = timelineForGauge
                            .Where(t => t.PlanSnapshotDate.Date < PlanStartDate.Date)
                            .OrderByDescending(t => t.PlanSnapshotDate)
                            .FirstOrDefault();

                        if (precedingEntry != null)
                        {
                            int capLimit = precedingEntry.TotalActiveCapacityLimit > 0 ? precedingEntry.TotalActiveCapacityLimit : totalLimit;
                            freeMachines = precedingEntry.ImmediateFreeMachines > 0 ? precedingEntry.ImmediateFreeMachines : capLimit;
                        }
                    }
                }
            }
        }

        MaxMachinesAvailable = freeMachines > 0 ? freeMachines : (machineData != null && machineData.TrueGaugeLimit > 0 ? machineData.TrueGaugeLimit : 1);
        if (PlanMachines > MaxMachinesAvailable)
        {
            PlanMachines = MaxMachinesAvailable;
        }
    }

    private decimal DailyProductionPerMachine { get; set; } = 0;
    private string SearchStatus { get; set; } = "";
    private decimal BaseDays { get; set; }
    private int BaseMachines { get; set; }
    private decimal BaseQty { get; set; }

    // Silk/Other/Linen plan against a MASTER's team capacity, not per-machine style targets.
    private bool IsMasterBasedType =>
        SelectedKnitType == "Silk" || SelectedKnitType == "Other" || SelectedKnitType == "Linen";

    private DateTime PlanEndDate 
    {
        get
        {
            if (BaseDays <= 0 || BaseQty <= 0 || PlanMachines <= 0) return PlanStartDate;

            // Knit: BaseDays are single-machine days (style_target = pcs/day per machine).
            // Silk/Other/Linen are outstation masters: machines are irrelevant - the
            // master delivers at his TEAM rate (BaseQty/BaseDays), Mc always saved as 1.
            decimal capPerMc = (BaseQty / BaseDays) * OvertimeFactor;

            if (capPerMc <= 0) return PlanStartDate;

            double daysNeeded = IsMasterBasedType
                ? (double)(PlanQty / capPerMc)
                : (double)(PlanQty / (capPerMc * PlanMachines));
            return AddWorkingDays(PlanStartDate, daysNeeded);
        }
    }

    // Guards Confirm against double-clicks while the saves are in flight.
    private bool IsConfirmSaving { get; set; }

    private async Task AddManualPlan(string gauge)
    {
        if (IsConfirmSaving) return;
        IsConfirmSaving = true;
        StateHasChanged();
        try
        {
            await AddManualPlanCore(gauge);
        }
        finally
        {
            IsConfirmSaving = false;
            StateHasChanged();
        }
    }

    private async Task AddManualPlanCore(string gauge)
    {
        if (string.IsNullOrEmpty(gauge)) return;
        
        var orderNo = SelectedOrders.LastOrDefault()?.OrderNo ?? "";
        
        // 1. VALIDATION
        if (SelectedKnitType == "Knit")
        {
            if (SelectedMachinesList == null || !SelectedMachinesList.Any())
            {
                ShowAlert("Machine Selection Required", "Please select at least one machine from the dropdown popover checklist to allocate your Knit planning quota.", "warning");
                return;
            }

            // Factory-wide knitter ceiling: total busy machines on any day must not
            // exceed total knitters (1 knitter per machine). Hard block.
            var capHit = await CheckKnitterCapacityAsync();
            if (capHit != null)
            {
                if (!IsBulkBusy)
                {
                    ShowAlert("Knitter capacity exceeded",
                        $"On {capHit.Value.Day:dd-MMM-yyyy} this plan would need {capHit.Value.Needed} machines running at once, " +
                        $"but the factory has only {capHit.Value.Knitters} knitters (1 knitter runs 1 machine). " +
                        "Reduce machines, or shift the dates so they don't overlap other plans.", "warning");
                }
                return; // block the save
            }
        }

        if (SelectedKnitType == "Silk" || SelectedKnitType == "Other" || SelectedKnitType == "Linen")
        {
            var masterData = FabricAnalysisData?.MasterWorkload?.FirstOrDefault(m => 
                string.Equals(m.MasterId?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase) || 
                string.Equals(m.MasterName?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
                
            if (masterData != null)
            {
                // Validate against the SAME gauge-specific list PlanQty was derived from
                // (DbPlannedPlans), so the check can't disagree with the shown remaining.
                var currentPlanned = DbPlannedPlans.Sum(p => p.Quantity);

                var remaining = masterData.NewOrderQty - currentPlanned;
                if (PlanQty > remaining)
                {
                    ShowAlert("Allocation Limit Exceeded", $"The quantity ({PlanQty:N0}) exceeds the remaining required quantity ({remaining:N0}) for master '{masterData.MasterName}'. Please reduce the allocation quantity to proceed.", "warning");
                    return;
                }
            }
        }
        else
        {
            var machineData = PlanningDetail?.MachineStatus?.FirstOrDefault(m => string.Equals(m.Gauge?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (machineData != null)
            {
                // Validate against the SAME gauge-specific list PlanQty was derived from.
                var currentPlanned = DbPlannedPlans.Sum(p => p.Quantity);
                var remaining = machineData.NewOrderQty - currentPlanned;
                if (PlanQty > remaining)
                {
                    ShowAlert("Allocation Limit Exceeded", $"The quantity ({PlanQty:N0}) exceeds the remaining required quantity ({remaining:N0}) for gauge '{gauge}'. Please reduce the allocation quantity to proceed.", "warning");
                    return;
                }
            }
        }

        string orderType = "";
        if (SelectedKnitType == "Silk" || SelectedKnitType == "Other" || SelectedKnitType == "Linen")
        {
            orderType = "Silk";
        }
        else
        {
            var machineData = PlanningDetail?.MachineStatus?.FirstOrDefault(m => string.Equals(m.Gauge?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
            orderType = machineData?.NewOrderType ?? "";
        }
        
        // Knit-deadline check: knitting must finish by ship - 10 days (or the 65%
        // lead-time rule when stricter) so downstream departments keep their window.
        var orderRef = SelectedOrders.LastOrDefault();
        var knitDeadlineOpt = GetKnitDeadline();
        // Ship-10d rule applies to KNIT planning only; Silk/Other/Linen/Weave keep their own flow.
        // Skipped during bulk approval - the user already reviewed deadlines in the preview.
        if (!IsBulkBusy && SelectedKnitType == "Knit" && orderRef != null && knitDeadlineOpt != null)
        {
            DateTime shipDate = orderRef.OrderLDate.Date;
            DateTime knitDeadline = knitDeadlineOpt.Value;

            DateTime knitEnd = (SelectedKnitType == "Knit" && SelectedMachinesList.Any())
                ? GetMaxSelectedEndDate()
                : PlanEndDate;

            if (knitEnd.Date > knitDeadline.Date)
            {
                bool proceed = await JS.InvokeAsync<bool>("confirm",
                    $"Knitting ends {knitEnd:dd-MMM-yyyy} but the knit deadline is {knitDeadline:dd-MMM-yyyy} " +
                    $"(at least {ShipBufferDays} days before ship {shipDate:dd-MMM-yyyy} are reserved for later departments).\n\n" +
                    "Consider enabling Overtime / Work Saturdays.\n\nContinue anyway?");
                if (!proceed) return;
            }
        }

        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.Identity?.Name ?? "system";
        var createdDate = DateTime.Now;

        try
        {
            if (SelectedKnitType == "Knit" && SelectedMachinesList.Any())
            {
                // BaseDays are single-machine days (style_target = pcs/day per machine).
                decimal capPerMc = (BaseQty / BaseDays) * OvertimeFactor;

                int selectedCount = SelectedMachinesList.Count;

                // Distribute PlanQty among machines exactly like the dropdown table shows:
                // whole-number base share, with the remainder spread +1 across the first machines.
                decimal baseSharedQty = selectedCount > 0 ? Math.Floor(PlanQty / selectedCount) : 0;
                decimal remainderQty = selectedCount > 0 ? (PlanQty - (baseSharedQty * selectedCount)) : 0;

                // Save in the same order the table renders (MachinePlaningList order among selected),
                // so the remainder lands on the same machines the user saw.
                var orderedSelected = MachinePlaningList != null && MachinePlaningList.Any()
                    ? MachinePlaningList.Where(m => SelectedMachinesList.Any(s => s.Machine_ID == m.Machine_ID)).ToList()
                    : SelectedMachinesList;

                bool hasSizeData = SizeAllocationRows.Any();

                int selectedIdx = 0;
                foreach (var machine in orderedSelected)
                {
                    // Build this machine's style/color/size lines from the (editable) allocation grid.
                    List<PlanSizeLineDto>? sizeLines = null;
                    decimal qtyPerMc;

                    if (hasSizeData)
                    {
                        sizeLines = SizeAllocationRows
                            .Where(r => r.PerMachine.TryGetValue(machine.Machine_ID, out var q) && q > 0)
                            .Select(r => new PlanSizeLineDto
                            {
                                OrderId = r.OrderId,
                                StyleNo = r.StyleNo,
                                Color = r.Color,
                                Size = r.Size,
                                Qty = r.PerMachine[machine.Machine_ID]
                            })
                            .ToList();

                        // Machine plan qty is the sum of its assigned size lines, so the
                        // MasterPlanDetail.Qty always matches the size breakdown.
                        qtyPerMc = sizeLines.Sum(l => l.Qty);

                        if (qtyPerMc <= 0)
                        {
                            selectedIdx++;
                            continue; // nothing assigned to this machine
                        }
                    }
                    else
                    {
                        // No size data: fall back to whole-number division of PlanQty.
                        qtyPerMc = baseSharedQty;
                        if (selectedIdx < remainderQty)
                        {
                            qtyPerMc += 1;
                        }
                    }
                    selectedIdx++;

                    DateTime startDate = GetMachinePlanStartDate(machine.FreeDate);
                    double daysNeeded = capPerMc > 0 ? (double)(qtyPerMc / capPerMc) : 0;
                    DateTime endDate = AddWorkingDays(startDate, daysNeeded);

                    int materId = await PlanningService.SavePlanAsync(
                        orderNo,
                        gauge,
                        startDate,
                        endDate,
                        qtyPerMc,
                        machine.Machine_ID,
                        orderType,
                        SelectedKnitType,
                        userId,
                        createdDate,
                        sizeLines,
                        machine.MachineNo,    // Machine column -> name e.g. KN-56
                        machine.Machine_ID,   // MachineID column -> numeric id e.g. 25
                        EnableOvertime,
                        EnableOvertime ? OvertimeHoursPerDay : 0,
                        WorkSaturday
                    );

                    ManualPlans.Add(new ManualPlanEntry
                    {
                        StartDate = startDate,
                        Gauge = gauge,
                        Machines = 1,
                        Qty = qtyPerMc,
                        EndDate = endDate
                    });
                }

                SelectedMachinesList = new(); // Reset selection
                SizeAllocationRows = new();
            }
            else
            {
                var endDate = PlanEndDate;
                int materId = await PlanningService.SavePlanAsync(
                    orderNo,
                    gauge,
                    PlanStartDate,
                    endDate,
                    PlanQty,
                    PlanMachines,
                    orderType,
                    SelectedKnitType,
                    userId,
                    createdDate
                );

                ManualPlans.Add(new ManualPlanEntry
                {
                    StartDate = PlanStartDate,
                    Gauge = gauge,
                    Machines = PlanMachines,
                    Qty = PlanQty,
                    EndDate = endDate
                });
            }
            
            // Clear the cached input so it gets recalculated with new remaining Qty
            var key = gauge.Trim().ToUpper();
            GaugeInputs.Remove(key);
            
            if (!string.IsNullOrEmpty(orderNo))
            {
                if (SelectedKnitType == "Silk" || SelectedKnitType == "Other" || SelectedKnitType == "Linen")
                {
                    FabricAnalysisData = await PlanningService.GetFabricAnalysisPlanAsync(orderNo, SelectedKnitType, 1);
                }
                else
                {
                    PlanningDetail = await PlanningService.GetOrderPlanningDetailAsync(orderNo, 0);
                }
                
                Mode1Analysis = await PlanningService.GetOrderAnalysisAsync(orderNo, null, 1);
                var allPlans = await PlanningService.GetPlannedDataByOrderAsync(orderNo);
                OrderAllPlannedPlans = allPlans.ToList();

                var dbPlans = await PlanningService.GetPlannedDataByOrderAsync(orderNo, gauge);
                DbPlannedPlans = dbPlans.ToList();

                if (SelectedKnitType == "Knit")
                {
                    var machineList = await PlanningService.GetMachinePlaningAsync(gauge);
                    MachinePlaningList = machineList.ToList();

                    // Refresh the all-gauges machine list so the Planning Details
                    // start dates of OTHER gauges stay truthful after this save.
                    var allMachines = await PlanningService.GetMachinePlaningAsync(null);
                    AllMachinePlaningList = allMachines.ToList();
                }

                if (SelectedKnitType == "Silk" || SelectedKnitType == "Other" || SelectedKnitType == "Linen")
                {
                    var masterData = FabricAnalysisData?.MasterWorkload?.FirstOrDefault(m => 
                        string.Equals(m.MasterId?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase) || 
                        string.Equals(m.MasterName?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
                        
                    if (masterData != null)
                    {
                        var plannedQty = DbPlannedPlans.Sum(p => p.Quantity);
                        if (plannedQty >= masterData.NewOrderQty)
                        {
                            IsFullyPlannedEditMode = true;
                        }
                    }
                }
                else
                {
                    var updatedMachineData = PlanningDetail?.MachineStatus?.FirstOrDefault(m => string.Equals(m.Gauge?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
                    if (updatedMachineData != null)
                    {
                        var plannedQty = DbPlannedPlans.Sum(p => p.Quantity);
                        if (plannedQty >= updatedMachineData.NewOrderQty)
                        {
                            IsFullyPlannedEditMode = true;
                        }
                    }
                }
            }
            
            StateHasChanged();
        }
        catch (Exception ex)
        {
            ShowAlert("Planning Failed", ex.Message, "error");
        }
    }

    private void ToggleMachineDropdown()
    {
        IsMachineDropdownOpen = !IsMachineDropdownOpen;
    }

    // Close the large Machine Allocation popup (clears the selected gauge).
    private void CloseGaugeAllocation()
    {
        SelectedModalGauge = string.Empty;
        IsMachineDropdownOpen = false;
        StateHasChanged();
    }

    // ---- Saved plan: view/edit its style/color/size lines ----
    private int ExpandedPlanId { get; set; } = 0;
    private bool IsLoadingSizeLines { get; set; }
    private List<PlanSizeLineEditDto> ExpandedSizeLines { get; set; } = new();

    private async Task TogglePlanSizeLines(int planId)
    {
        if (ExpandedPlanId == planId) { ExpandedPlanId = 0; ExpandedSizeLines = new(); StateHasChanged(); return; }

        ExpandedPlanId = planId;
        IsLoadingSizeLines = true;
        ExpandedSizeLines = new();
        StateHasChanged();
        try
        {
            ExpandedSizeLines = await PlanningService.GetPlanSizeLinesAsync(planId);
        }
        catch (Exception ex)
        {
            ShowAlert("Load Failed", ex.Message, "error");
        }
        finally
        {
            IsLoadingSizeLines = false;
            StateHasChanged();
        }
    }

    // Bumped to force the expanded size-line inputs to re-render after a server clamp.
    private int SizeLineResetVersion { get; set; } = 0;

    private async Task SaveSizeLineQty(PlanSizeLineEditDto line, object? value)
    {
        decimal.TryParse(value?.ToString(), out var qty);
        if (qty < 0) qty = 0;
        try
        {
            var result = await PlanningService.UpdatePlanSizeLineAsync(line.SizeLineId, qty);
            if (result.Success)
            {
                line.Qty = result.FinalQty;

                if (result.WasClamped)
                {
                    SizeLineResetVersion++; // make the input show the clamped value
                    ShowAlert(
                        "Order Size Limit Exceeded",
                        $"{line.StyleNo} / {line.Color} / {line.Size}: the entered quantity exceeds the order's size quantity. " +
                        $"It has been reset to the maximum allowed ({result.MaxAllowed:N0}).",
                        "warning");
                }
                // Refresh plan totals so the saved list reflects the new sum.
                var orderNo = SelectedOrders.LastOrDefault()?.OrderNo ?? "";
                if (!string.IsNullOrEmpty(orderNo))
                {
                    var dbPlans = await PlanningService.GetPlannedDataByOrderAsync(orderNo, SelectedModalGauge);
                    DbPlannedPlans = dbPlans.ToList();
                    var allPlans = await PlanningService.GetPlannedDataByOrderAsync(orderNo);
                    OrderAllPlannedPlans = allPlans.ToList();
                }

                if (result.WasClamped)
                {
                    ToastService.ShowError($"Qty exceeds order size limit — reset to {result.MaxAllowed:N0}.");
                }
                else
                {
                    ToastService.ShowSuccess("Size line updated.");
                }
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Update Failed", ex.Message, "error");
        }
        StateHasChanged();
    }

    // ---- Bulk: plan all remaining (unplanned/partial) Knit gauges ----
    public class BulkPlanRow
    {
        public string OrderNo { get; set; } = string.Empty;
        public string Gauge { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public int Machines { get; set; }
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public DateTime? Deadline { get; set; }
        public bool DeadlineMet { get; set; } = true;
        public bool Selected { get; set; } = true;
    }

    private bool IsBulkOpen { get; set; }
    private bool IsBulkBusy { get; set; }
    private string BulkPhase { get; set; } = "";   // Scanning... / Planning...
    private List<BulkPlanRow> BulkRows { get; set; } = new();

    // Bulk scope: when true, PLAN ALL spans every order in the month (deadline-ordered
    // flow engine); when false it stays on the current order's gauges (legacy behaviour).
    private bool BulkAllOrders { get; set; } = true;

    // In-memory projected free date per machine, so the preview reflects the queue
    // each earlier (order,gauge) would create - same chaining the DB gives on approval.
    private readonly Dictionary<int, DateTime> _bulkOverlay = new();

    // Apply the overlay to the freshly-loaded machine list so auto-selection sees the
    // dates the running queue would leave, then re-run selection against them.
    private void ApplyBulkOverlayAndReselect()
    {
        if (MachinePlaningList == null || !MachinePlaningList.Any()) return;
        foreach (var m in MachinePlaningList)
        {
            if (_bulkOverlay.TryGetValue(m.Machine_ID, out var projected) && projected.Date > m.FreeDate.Date)
            {
                m.FreeDate = projected;
            }
        }
        AutoSelectKnitMachines(); // re-selects + rebuilds allocations against overlaid dates
    }

    // After a (order,gauge) is placed in the preview, advance the overlay for the
    // machines it used to its projected end, so the next order queues behind it.
    private void AdvanceBulkOverlay()
    {
        DateTime end = GetMaxSelectedEndDate();
        foreach (var m in SelectedMachinesList)
        {
            _bulkOverlay[m.Machine_ID] = end;
        }
    }

    private List<string> GetRemainingKnitGauges()
    {
        if (SelectedKnitType != "Knit" || PlanningDetail?.MachineStatus == null) return new();
        return PlanningDetail.MachineStatus
            .Where(m => m.NewOrderQty > 0)
            .Where(m =>
            {
                var planned = OrderAllPlannedPlans
                    .Where(p => string.Equals(p.Gauge?.Trim(), m.Gauge?.Trim(), StringComparison.OrdinalIgnoreCase))
                    .Sum(p => p.Quantity);
                return planned < m.NewOrderQty;
            })
            .Select(m => m.Gauge ?? "")
            .Where(g => !string.IsNullOrEmpty(g))
            .ToList();
    }

    // The orders this bulk run covers: every month order with remaining knit work,
    // in EDD order (earliest knit deadline first) so urgent orders claim early slots.
    private List<MonthlyOrderDetailDto> GetBulkOrders()
    {
        var pool = BulkAllOrders && AllOrders.Any()
            ? AllOrders
            : SelectedOrders;
        return pool
            .Where(o => o != null && !string.IsNullOrEmpty(o.OrderNo))
            .OrderBy(o => GetKnitDeadlineFor(o) ?? DateTime.MaxValue)
            .ThenBy(o => o.OrderLDate)
            .ToList();
    }

    // Restore the order AND the product-type view the planner had before the bulk run
    // (bulk forces SelectedKnitType="Knit"), reloading the correct analysis for it.
    private async Task RestoreOrderContextAsync(List<MonthlyOrderDetailDto> original, string originalKnitType)
    {
        SelectedOrders = original;
        SelectedModalGauge = string.Empty;
        SelectedKnitType = originalKnitType;

        var current = SelectedOrders.LastOrDefault();
        if (current != null && !string.IsNullOrEmpty(originalKnitType))
        {
            // Re-run the same analysis load the user's tab uses (Knit/Weave/Silk/...).
            await OnOrderSummaryRowClick(originalKnitType);
        }
    }

    // Phase 1: deadline-ordered scan across orders. The overlay chains machine queues so
    // the preview shows what approval will actually produce (urgent orders first).
    private async Task OpenBulkPlan()
    {
        var orders = GetBulkOrders();
        if (!orders.Any())
        {
            ToastService.ShowInfo("No orders to plan.");
            return;
        }

        var originalOrders = SelectedOrders;
        var originalKnitType = SelectedKnitType;
        IsBulkOpen = true;
        IsBulkBusy = true;
        BulkPhase = "Scanning orders by deadline...";
        BulkRows = new();
        _bulkOverlay.Clear();
        StateHasChanged();

        try
        {
            foreach (var order in orders)
            {
                SelectedOrders = new List<MonthlyOrderDetailDto> { order };
                SelectedKnitType = "Knit";
                await LoadOrderProductionStatus(order.OrderNo);

                foreach (var g in GetRemainingKnitGauges())
                {
                    await SelectGaugeInModal(g);    // loads styles/machines + auto-selects
                    ApplyBulkOverlayAndReselect();  // chain behind the running queue
                    if (PlanQty <= 0 || !SelectedMachinesList.Any()) continue;

                    var deadline = GetKnitDeadlineFor(order);
                    var end = GetMaxSelectedEndDate();
                    BulkRows.Add(new BulkPlanRow
                    {
                        OrderNo = order.OrderNo,
                        Gauge = g,
                        Qty = PlanQty,
                        Machines = SelectedMachinesList.Count,
                        Start = GetMachinePlanStartDate(SelectedMachinesList.Min(m => m.FreeDate)),
                        End = end,
                        Deadline = deadline,
                        DeadlineMet = deadline == null || end.Date <= deadline.Value.Date
                    });

                    AdvanceBulkOverlay(); // next order queues behind this allocation
                }
            }
        }
        finally
        {
            await RestoreOrderContextAsync(originalOrders, originalKnitType);
            IsBulkBusy = false;
            BulkPhase = "";
            StateHasChanged();
        }
    }

    // Phase 2: plan the approved rows in the SAME deadline order, reusing the exact
    // manual Confirm flow. Each save re-reads machine free dates from the DB, so the
    // real machine queues chain automatically - no overlay needed here.
    private async Task ApproveBulkPlan()
    {
        var selected = BulkRows.Where(r => r.Selected).ToList();
        if (!selected.Any()) { IsBulkOpen = false; return; }

        var originalOrders = SelectedOrders;
        var originalKnitType = SelectedKnitType;
        IsBulkBusy = true;
        BulkPhase = "Planning...";
        StateHasChanged();

        int doneCount = 0;
        var skipped = new List<string>();
        try
        {
            // Group by order to minimise context reloads; orders already in EDD sequence.
            foreach (var grp in selected.GroupBy(r => r.OrderNo))
            {
                var order = AllOrders.FirstOrDefault(o => o.OrderNo == grp.Key)
                            ?? originalOrders.FirstOrDefault(o => o.OrderNo == grp.Key);
                if (order == null) { skipped.AddRange(grp.Select(r => $"{r.OrderNo}/{r.Gauge}")); continue; }

                SelectedOrders = new List<MonthlyOrderDetailDto> { order };
                SelectedKnitType = "Knit";
                await LoadOrderProductionStatus(order.OrderNo);

                foreach (var row in grp)
                {
                    await SelectGaugeInModal(row.Gauge);
                    // Nothing left to plan for this gauge (already saved by an earlier row
                    // or balance changed) - record it so it isn't dropped silently.
                    if (PlanQty <= 0 || !SelectedMachinesList.Any())
                    {
                        skipped.Add($"{row.OrderNo}/{row.Gauge}");
                        continue;
                    }
                    await AddManualPlan(row.Gauge);
                    doneCount++;
                }
            }
        }
        finally
        {
            await RestoreOrderContextAsync(originalOrders, originalKnitType);
            IsBulkBusy = false;
            BulkPhase = "";
            IsBulkOpen = false;
            StateHasChanged();
        }

        ToastService.ShowSuccess($"Bulk planning done: {doneCount} gauge plan(s) saved.");
        if (skipped.Any())
        {
            ToastService.ShowInfo($"{skipped.Count} row(s) skipped (already planned / no balance): {string.Join(", ", skipped.Take(8))}{(skipped.Count > 8 ? "…" : "")}");
        }
    }

    // Switch bulk scope (all orders vs current order) and re-scan the preview.
    private async Task SetBulkScope(bool allOrders)
    {
        if (IsBulkBusy || BulkAllOrders == allOrders) return;
        BulkAllOrders = allOrders;
        await OpenBulkPlan();
    }

    private void CloseBulkPlan()
    {
        if (IsBulkBusy) return;
        IsBulkOpen = false;
        BulkRows = new();
        _bulkOverlay.Clear();
        StateHasChanged();
    }

    private async Task OpenGanttModal(string gauge)
    {
        if (string.IsNullOrEmpty(gauge)) return;
        GanttModalGauge = gauge;
        
        try
        {
            // Fetch Gantt chart data specifically for this gauge
            GanttChartPlans = await PlanningService.GetKnitGanttChartDataAsync(null, null, null, gauge);
            IsGanttModalOpen = true;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            ShowAlert("Gantt Load Failed", ex.Message, "error");
        }
    }

    private void OnMachineSelectChanged(MachinePlaningDto machine, bool isChecked)
    {
        if (isChecked)
        {
            if (!SelectedMachinesList.Any(m => m.Machine_ID == machine.Machine_ID))
            {
                SelectedMachinesList.Add(machine);
            }
        }
        else
        {
            var item = SelectedMachinesList.FirstOrDefault(m => m.Machine_ID == machine.Machine_ID);
            if (item != null)
            {
                SelectedMachinesList.Remove(item);
            }
        }
        
        // Dynamically update PlanMachines count based on selection
        PlanMachines = SelectedMachinesList.Count > 0 ? SelectedMachinesList.Count : 1;
        BuildSizeAllocations();
        StateHasChanged();
    }

    private void SelectAllMachines()
    {
        SelectedMachinesList.Clear();
        foreach (var m in MachinePlaningList)
        {
            SelectedMachinesList.Add(m);
        }
        PlanMachines = SelectedMachinesList.Count > 0 ? SelectedMachinesList.Count : 1;
        BuildSizeAllocations();
        StateHasChanged();
    }

    private void DeselectAllMachines()
    {
        SelectedMachinesList.Clear();
        PlanMachines = 1;
        BuildSizeAllocations();
        StateHasChanged();
    }

    private void DeleteManualPlan(ManualPlanEntry plan)
    {
        ManualPlans.Remove(plan);
        StateHasChanged();
    }

    private async Task SelectGaugeInModal(string gauge)
    {
        SearchStatus = $"INIT:[{gauge}]";
        SelectedModalGauge = gauge;
        IsFullyPlannedEditMode = false;
        EditingPlanId = 0;
        SelectedMachinesList = new();
        IsMachineDropdownOpen = false;
        GaugeKnitterCount = 0;
        KnitterWindowLimited = false;
        
        // 1. POPULATE FORM IMMEDIATELY (Don't wait for API)
        if (SelectedKnitType == "Silk" || SelectedKnitType == "Other" || SelectedKnitType == "Linen")
        {
            var masterData = FabricAnalysisData.MasterWorkload.FirstOrDefault(m => m.NewOrderQty > 0 && 
                (string.Equals(m.MasterId?.ToString()?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase) || 
                 string.Equals(m.MasterName?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase)));
                 
            if (masterData != null)
            {
                var defaultMc = (masterData.RunningMachines ?? 0) > 0 ? (masterData.RunningMachines ?? 0) : 1;
                var baseDays = masterData.NewOrderDaysByCapacity ?? 0;
                
                SearchStatus += $" FOUND SILK! Qty:{masterData.NewOrderQty}";
                BaseDays = baseDays;
                BaseMachines = defaultMc;
                BaseQty = masterData.NewOrderQty;
                
                PlanGauge = gauge;
                PlanQty = masterData.NewOrderQty;

                _planStartDate = (masterData.MasterFreeDate ?? DateTime.Today).AddDays(1);
                // Outstation master: machines not shown in the form, always saved as 1.
                // Overtime/Saturday options are knit-only - clear any leftover flags so
                // they cannot silently shorten the master's estimated end date.
                PlanMachines = 1;
                EnableOvertime = false;
                WorkSaturday = false;
                RecalculateMaxMachines();
            }
            else
            {
                SearchStatus += " NOT FOUND IN SILK LIST";
            }
        }
        else
        {
            var machineData = PlanningDetail.MachineStatus.FirstOrDefault(m => m.NewOrderQty > 0 && string.Equals(m.Gauge?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
            if (machineData != null)
            {
                if (machineData.NewOrderDays == 0)
                {
                    SelectedModalGauge = string.Empty;
                    ShowAlert("Planning Target Missing", "style may not have target, entr target before planig", "warning");
                    IsLoadingModalStyles = false;
                    StateHasChanged();
                    return;
                }

                SearchStatus += $" FOUND! Qty:{machineData.NewOrderQty}";
                BaseDays = machineData.NewOrderDays;
                BaseMachines = machineData.SuggestedNewOrderMachines;
                BaseQty = machineData.NewOrderQty;
                
                PlanGauge = gauge;
                PlanQty = machineData.NewOrderQty;

                // Set the start date to the machine free date calculated from the database + 1 day
                _planStartDate = GetGaugeFreeDate(gauge).AddDays(1);
                PlanMachines = 1;
                RecalculateMaxMachines();
            }
            else
            {
                SearchStatus += " NOT FOUND IN LIST";
            }
        }
        
        ModalStyles = new();
        DbPlannedPlans = new();
        MachinePlaningList = new();
        IsLoadingModalStyles = true;
        StateHasChanged(); // Show form updates NOW

        try
        {
            var orderNo = SelectedOrders.LastOrDefault()?.OrderNo ?? "";
            SearchStatus += $" | Fetching Styles and Plans for {orderNo}...";
            
            // Knit uses flag "2" so the styles list also carries the per-size breakdown.
            string stylesFlag = (SelectedKnitType == "Silk" || SelectedKnitType == "Other" || SelectedKnitType == "Linen")
                ? SelectedKnitType
                : "2";
            var stylesTask = PlanningService.GetOrderDetailByGuageAsync(orderNo, gauge, stylesFlag);
            var plansTask = PlanningService.GetPlannedDataByOrderAsync(orderNo, gauge);
            
            Task<List<MachinePlaningDto>>? machineTask = null;
            Task<List<KnitterDto>>? knittersTask = null;
            if (SelectedKnitType == "Knit")
            {
                machineTask = PlanningService.GetMachinePlaningAsync(gauge);
                knittersTask = PlanningService.GetKnittersByGaugeAsync(gauge);
            }

            if (machineTask != null && knittersTask != null)
            {
                await Task.WhenAll(stylesTask, plansTask, machineTask, knittersTask);
                MachinePlaningList = machineTask.Result.ToList();
                // Staffing ceiling for the window check in auto machine selection.
                GaugeKnitterCount = knittersTask.Result
                    .Select(kn => kn.CardNo)
                    .Distinct()
                    .Count();
            }
            else
            {
                await Task.WhenAll(stylesTask, plansTask);
            }

            ModalStyles = stylesTask.Result.ToList();
            DbPlannedPlans = plansTask.Result.ToList();
            SearchStatus += " | Styles and Plans Loaded.";
            
            // Adjust PlanQty based on planned quantity
            var currentPlannedQty = DbPlannedPlans.Sum(p => p.Quantity);
            
            if (SelectedKnitType == "Silk" || SelectedKnitType == "Other" || SelectedKnitType == "Linen")
            {
                var masterData = FabricAnalysisData.MasterWorkload.FirstOrDefault(m => m.NewOrderQty > 0 && 
                    (string.Equals(m.MasterId?.ToString()?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase) || 
                     string.Equals(m.MasterName?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase)));
                     
                if (masterData != null)
                {
                    if (currentPlannedQty >= masterData.NewOrderQty)
                    {
                        PlanQty = 0;
                    }
                    else
                    {
                        PlanQty = masterData.NewOrderQty - currentPlannedQty;
                    }
                }
            }
            else
            {
                var machineData = PlanningDetail.MachineStatus.FirstOrDefault(m => m.NewOrderQty > 0 && string.Equals(m.Gauge?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (machineData != null)
                {
                    if (currentPlannedQty >= machineData.NewOrderQty)
                    {
                        PlanQty = 0;
                    }
                    else
                    {
                        PlanQty = machineData.NewOrderQty - currentPlannedQty;
                    }
                }
            }

            if (SelectedKnitType == "Knit")
            {
                AutoSelectKnitMachines();
                IsMachineDropdownOpen = true;
                await LoadWindowStaffing(); // advisory skill-aware staffing (no effect on selection)
            }

            IsLoadingModalStyles = false;
            StateHasChanged();
        }
        catch (Exception ex)
        {
            SearchStatus += $" | API ERR: {ex.Message}";
            IsLoadingModalStyles = false;
            StateHasChanged();
        }
    }

    private DateTime GetGaugeFreeDate(string gauge)
    {
        DateTime freeDate = DateTime.Today;
        if (PlanningDetail?.ForwardTimeline != null)
        {
            var timelineForGauge = PlanningDetail.ForwardTimeline
                .Where(t => string.Equals(t.Gauge?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase))
                .OrderBy(t => t.PlanSnapshotDate)
                .ToList();

            if (timelineForGauge.Any())
            {
                var firstEntry = timelineForGauge.First();
                if (firstEntry.FreeMachinesDate != DateTime.MinValue)
                {
                    freeDate = firstEntry.FreeMachinesDate;
                }
                else if (firstEntry.ImmediateFreeMachines > 0)
                {
                    freeDate = firstEntry.TodayDate != DateTime.MinValue ? firstEntry.TodayDate : DateTime.Today;
                }
                else
                {
                    DateTime? firstFreeDate = null;
                    foreach (var t in timelineForGauge)
                    {
                        int freeMachines = t.TotalActiveCapacityLimit - t.EngagedMachines;
                        if (freeMachines > 0)
                        {
                            firstFreeDate = t.PlanSnapshotDate;
                            break;
                        }
                    }

                    if (firstFreeDate != null)
                    {
                        freeDate = firstFreeDate.Value;
                    }
                    else
                    {
                        var validReleaseDates = timelineForGauge
                            .Where(t => t.EngagedMachinesReleaseDate > DateTime.Today)
                            .Select(t => t.EngagedMachinesReleaseDate)
                            .ToList();

                        if (validReleaseDates.Any())
                        {
                            freeDate = validReleaseDates.Min();
                        }
                        else
                        {
                            freeDate = timelineForGauge.Last().PlanSnapshotDate;
                        }
                    }
                }
            }
        }
        return freeDate;
    }

    // Machine list across ALL gauges (from the machinePlaning proc) so the
    // Planning Details start dates use the same free dates as the machine list.
    private List<MachinePlaningDto> AllMachinePlaningList { get; set; } = new();

    // Suggested free date for a gauge:
    //  - if a machine is free TODAY (earliest free date <= today) -> today
    //  - otherwise                                                -> the earliest free date
    //    (the min end date among the gauge's machines)
    private DateTime GetGaugeSuggestedFreeDate(string gauge)
    {
        DateTime today = DateTime.Today;

        // For the gauge currently open in the modal, use the SAME machine free dates
        // that the machine-allocation list shows (MachinePlaningList), so the suggested
        // start matches the per-machine start dates on the right.
        if (SelectedKnitType == "Knit"
            && MachinePlaningList != null && MachinePlaningList.Any()
            && string.Equals(SelectedModalGauge?.Trim(), gauge?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            DateTime minMachineFree = MachinePlaningList.Min(m => m.FreeDate);
            return minMachineFree.Date <= today ? today : minMachineFree;
        }

        // Any other gauge: use the full machine list (same proc as the machine list)
        // so the suggested start matches what machine selection will show.
        if (AllMachinePlaningList.Any() && TryParseGauge(gauge, out double g))
        {
            var gaugeMachines = AllMachinePlaningList.Where(m => m.Gauge.HasValue && m.Gauge.Value == g).ToList();
            if (gaugeMachines.Any())
            {
                DateTime minFree = gaugeMachines.Min(m => m.FreeDate);
                return minFree.Date <= today ? today : minFree;
            }
        }

        DateTime earliestFree = GetGaugeFreeDate(gauge); // fallback: forward-timeline free date
        return earliestFree.Date <= today ? today : earliestFree;
    }

    private static bool TryParseGauge(string? gauge, out double value)
    {
        var normalized = (gauge ?? string.Empty)
            .Replace("GG", "", StringComparison.OrdinalIgnoreCase)
            .Replace("G", "", StringComparison.OrdinalIgnoreCase)
            .Replace(" ", "");
        return double.TryParse(normalized, out value);
    }

    private async Task OpenPlanningDetailsModal()
    {
        IsPlanningDetailsModalOpen = true;
        ModalStyles = new(); // Clear previous selection
        SelectedModalGauge = string.Empty;
        var orderNo = SelectedOrders.LastOrDefault()?.OrderNo ?? "";
        if (!string.IsNullOrEmpty(orderNo))
        {
            try
            {
                var allPlans = await PlanningService.GetPlannedDataByOrderAsync(orderNo);
                OrderAllPlannedPlans = allPlans.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading all planned plans: {ex.Message}");
                OrderAllPlannedPlans = new();
            }
        }
        else
        {
            OrderAllPlannedPlans = new();
        }

        // Load machine free dates for ALL gauges so Planning Details start dates
        // match the machine list (same machinePlaning source).
        try
        {
            var allMachines = await PlanningService.GetMachinePlaningAsync(null);
            AllMachinePlaningList = allMachines.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading machine planing list: {ex.Message}");
            AllMachinePlaningList = new();
        }

        StateHasChanged();
    }

    // Open the Planning popup AND pre-select the given gauge (so its allocation
    // panel is ready). Used when a gauge chip is clicked in the analysis grid.
    private async Task OpenPlanningForGauge(string gauge)
    {
        await OpenPlanningDetailsModal();
        if (!string.IsNullOrWhiteSpace(gauge))
        {
            await SelectGaugeInModal(gauge);
        }
    }

    private void ClosePlanningDetailsModal()
    {
        IsPlanningDetailsModalOpen = false;
        StateHasChanged();
    }

    private async Task OpenWeavePlanningDetailsModal(string factoryName)
    {
        IsWeavePlanningDetailsModalOpen = true;
        
        var orderNo = SelectedOrders.LastOrDefault()?.OrderNo ?? "";
        if (!string.IsNullOrEmpty(orderNo))
        {
            try
            {
                var allPlans = await PlanningService.GetPlannedDataByOrderAsync(orderNo);
                WeaveOrderAllPlannedPlans = allPlans.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error pre-loading weave planned plans: {ex.Message}");
            }
        }
        
        if (string.IsNullOrEmpty(factoryName))
        {
            SelectedWeaveFactory = string.Empty;
            WeaveModalStyles = new();
            WeaveDbPlannedPlans = new();
            IsLoadingWeaveModalStyles = false;
            StateHasChanged();
        }
        else
        {
            await SelectWeaveFactoryInModal(factoryName);
        }
    }

    private void CloseWeavePlanningDetailsModal()
    {
        IsWeavePlanningDetailsModalOpen = false;
        SelectedWeaveFactory = string.Empty;
        StateHasChanged();
    }

    private async Task SelectWeaveFactoryInModal(string factoryName)
    {
        SelectedWeaveFactory = factoryName;
        IsWeaveFullyPlannedEditMode = false;
        WeaveEditingPlanId = 0;

        if (string.Equals(factoryName?.Trim(), "Gyatri Pashmina", StringComparison.OrdinalIgnoreCase))
        {
            _customWeavePlanEndDate = DateTime.Today.AddDays(16);
        }
        else
        {
            _customWeavePlanEndDate = null;
        }

        // 1. Determine base variables for calculations from FactorySummaries
        var factoryInfo = WeaveAnalysisData?.FactorySummaries?.FirstOrDefault(f => string.Equals(f.WeaveFactory?.Trim(), factoryName?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (factoryInfo != null)
        {
            WeaveBaseQty = factoryInfo.Qty > 0 ? factoryInfo.Qty : 100;
            WeaveBaseDays = factoryInfo.ReqMachineDays > 0 
                ? (decimal)factoryInfo.ReqMachineDays 
                : Math.Max(1m, Math.Ceiling(WeaveBaseQty / 10m));
            WeaveBaseMachines = 1;
            
            WeavePlanQty = WeaveBaseQty;
            WeavePlanStartDate = DateTime.Today.AddDays(1);
            WeavePlanMachines = 1;
            WeaveMaxMachinesAvailable = 10; // Default max limit for Weave
        }
        else
        {
            WeaveBaseQty = 100;
            WeaveBaseDays = 10;
            WeaveBaseMachines = 1;
            WeavePlanQty = 100;
            WeavePlanStartDate = DateTime.Today.AddDays(1);
            WeavePlanMachines = 1;
            WeaveMaxMachinesAvailable = 10;
        }

        WeaveModalStyles = new();
        WeaveDbPlannedPlans = new();
        IsLoadingWeaveModalStyles = true;
        StateHasChanged();

        try
        {
            var orderNo = SelectedOrders.LastOrDefault()?.OrderNo ?? "";
            
            // Fetch factory specific Yarn and style balances
            WeaveFactoryDetails = await PlanningService.GetWeaveAnalysisPlanAsync(orderNo, factoryName, 1);
            
            // Get saved allocations for this order and factory (passing factoryName as gauge!)
            var plansTask = PlanningService.GetPlannedDataByOrderAsync(orderNo, factoryName);

            // Get styles for this factory (since factory has style print/emb summary, we map WeavePrintEmbroiderySummaryDto to ModalStyles)
            WeaveModalStyles = WeaveFactoryDetails.PrintEmbroiderySummaries.Select(p => new OrderDetailByGuageDto
            {
                StyleNo = p.StyleNo,
                OrderPics = p.Qty,
                RequireDays = Math.Max(1.0, (double)p.Qty / 10.0),
                // Look up print/emb status from Mode2Analysis
                PrintStatus = (Mode2Analysis?.SummaryAnalysis?.FirstOrDefault(s => string.Equals(s.Style?.Trim(), p.StyleNo?.Trim(), StringComparison.OrdinalIgnoreCase))?.Print == 1) ? "OK" : "",
                EmbdStatus = (Mode2Analysis?.SummaryAnalysis?.FirstOrDefault(s => string.Equals(s.Style?.Trim(), p.StyleNo?.Trim(), StringComparison.OrdinalIgnoreCase))?.Emb == 1) ? "OK" : ""
            }).ToList();

            var plans = await plansTask;
            WeaveDbPlannedPlans = plans.ToList();

            var currentPlannedQty = WeaveDbPlannedPlans.Sum(p => p.Quantity);
            if (currentPlannedQty >= WeaveBaseQty)
            {
                WeavePlanQty = 0;
            }
            else
            {
                WeavePlanQty = WeaveBaseQty - currentPlannedQty;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in SelectWeaveFactoryInModal: {ex.Message}");
        }
        finally
        {
            IsLoadingWeaveModalStyles = false;
            StateHasChanged();
        }
    }

    private void RecalculateWeaveEditMaxMachines()
    {
        WeaveEditMaxMachines = 10;
        if (WeaveEditMachines > WeaveEditMaxMachines)
        {
            WeaveEditMachines = WeaveEditMaxMachines;
        }
    }

    // Guards the Weave Confirm button against double-clicks while saving.
    private bool IsWeaveConfirmSaving { get; set; }

    private async Task AddWeaveManualPlan()
    {
        if (IsWeaveConfirmSaving) return;            // ignore double-clicks
        if (string.IsNullOrEmpty(SelectedWeaveFactory)) return;
        var orderNo = SelectedOrders.LastOrDefault()?.OrderNo ?? "";
        if (string.IsNullOrEmpty(orderNo)) return;

        var currentPlanned = WeaveDbPlannedPlans.Sum(p => p.Quantity);
        var remaining = WeaveBaseQty - currentPlanned;
        if (WeavePlanQty > remaining)
        {
            ShowAlert("Allocation Limit Exceeded", $"The quantity ({WeavePlanQty:N0}) exceeds the remaining required quantity ({remaining:N0}) for factory '{SelectedWeaveFactory}'. Please reduce the allocation quantity to proceed.", "warning");
            return;
        }

        IsWeaveConfirmSaving = true;
        StateHasChanged();

        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.Identity?.Name ?? "system";
        var createdDate = DateTime.Now;

        try
        {
            int childId = await PlanningService.SavePlanAsync(
                orderNo,
                SelectedWeaveFactory,
                WeavePlanStartDate,
                WeavePlanEndDate,
                WeavePlanQty,
                WeavePlanMachines,
                "WEAVE_ORDER",
                "Weave",
                userId,
                createdDate
            );

            if (childId > 0)
            {
                ToastService.ShowSuccess("Weave allocation saved successfully.");
                
                // Reload data
                var allPlans = await PlanningService.GetPlannedDataByOrderAsync(orderNo);
                WeaveOrderAllPlannedPlans = allPlans.ToList();

                await SelectWeaveFactoryInModal(SelectedWeaveFactory);
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Weave Planning Failed", ex.Message, "error");
        }
        finally
        {
            IsWeaveConfirmSaving = false;
            StateHasChanged();
        }
    }

    private void StartWeavePlanEdit(PlannedDataDto plan)
    {
        WeaveEditingPlanId = plan.MasterPlanChildId;
        _weaveEditStartDate = plan.StartDate;
        WeaveEditQty = plan.Quantity;

        int machines = 1;
        if (!string.IsNullOrEmpty(plan.Mc))
        {
            var match = System.Text.RegularExpressions.Regex.Match(plan.Mc, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int parsed))
            {
                machines = parsed;
            }
        }
        WeaveEditMachines = machines;

        var factoryInfo = WeaveAnalysisData?.FactorySummaries?.FirstOrDefault(f => string.Equals(f.WeaveFactory?.Trim(), plan.Gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (factoryInfo != null)
        {
            WeaveBaseQty = factoryInfo.Qty > 0 ? factoryInfo.Qty : 100;
            WeaveBaseDays = factoryInfo.ReqMachineDays > 0 
                ? (decimal)factoryInfo.ReqMachineDays 
                : Math.Max(1m, Math.Ceiling(WeaveBaseQty / 10m));
            WeaveBaseMachines = 1;
        }

        WeaveEditMaxMachines = 10;
        var currentPlanned = WeaveDbPlannedPlans.Where(p => p.MasterPlanChildId != plan.MasterPlanChildId).Sum(p => p.Quantity);
        WeaveEditMaxQty = WeaveBaseQty - currentPlanned;

        StateHasChanged();
    }

    private void CancelWeavePlanEdit()
    {
        WeaveEditingPlanId = 0;
        StateHasChanged();
    }

    private async Task SaveWeavePlanEdit(PlannedDataDto plan)
    {
        if (WeaveEditingPlanId == 0) return;
        var orderNo = SelectedOrders.LastOrDefault()?.OrderNo ?? "";
        if (string.IsNullOrEmpty(orderNo)) return;

        var currentPlanned = WeaveDbPlannedPlans.Where(p => p.MasterPlanChildId != plan.MasterPlanChildId).Sum(p => p.Quantity);
        var remaining = WeaveBaseQty - currentPlanned;
        if (WeaveEditQty > remaining)
        {
            ShowAlert("Allocation Limit Exceeded", $"The quantity ({WeaveEditQty:N0}) exceeds the remaining required quantity ({remaining:N0}) for factory '{SelectedWeaveFactory}'. Please reduce the allocation quantity to proceed.", "warning");
            return;
        }

        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.Identity?.Name ?? "system";

        try
        {
            bool success = await PlanningService.UpdatePlanDetailAsync(
                plan.MasterPlanChildId,
                WeaveEditStartDate,
                WeaveEditEndDate,
                WeaveEditQty,
                WeaveEditMachines,
                userId
            );

            if (success)
            {
                WeaveEditingPlanId = 0;
                ToastService.ShowSuccess("Weave allocation updated successfully.");
                
                // Reload
                var allPlans = await PlanningService.GetPlannedDataByOrderAsync(orderNo);
                WeaveOrderAllPlannedPlans = allPlans.ToList();

                await SelectWeaveFactoryInModal(plan.Gauge);
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Update Failed", ex.Message, "error");
        }
        finally
        {
            StateHasChanged();
        }
    }

    private async Task DeleteWeavePlanDetail(PlannedDataDto plan)
    {
        bool confirm = await JS.InvokeAsync<bool>("confirm", $"Are you sure you want to delete this weave allocation of Qty: {plan.Quantity:N0}?");
        if (!confirm) return;

        var orderNo = SelectedOrders.LastOrDefault()?.OrderNo ?? "";
        if (string.IsNullOrEmpty(orderNo)) return;

        try
        {
            bool success = await PlanningService.DeletePlanDetailAsync(plan.MasterPlanChildId);
            if (success)
            {
                ToastService.ShowSuccess("Weave allocation deleted successfully.");

                // Reload
                var allPlans = await PlanningService.GetPlannedDataByOrderAsync(orderNo);
                WeaveOrderAllPlannedPlans = allPlans.ToList();

                await SelectWeaveFactoryInModal(plan.Gauge);
            }
        }
        catch (Exception ex)
        {
            ShowAlert("Delete Failed", ex.Message, "error");
        }
        finally
        {
            StateHasChanged();
        }
    }

    private bool IsYarnColorModalOpen { get; set; } = false;
    private bool IsLoadingYarnColor { get; set; } = false;
    private List<YarnPlanningStatusDto> YarnColorDetails { get; set; } = new();

    private async Task OpenYarnColorModal(string gauge, string yarnName, string ply)
    {
        SelectedGauge = gauge;
        IsYarnColorModalOpen = true;
        IsLoadingYarnColor = true;
        YarnColorDetails = new();
        StateHasChanged();

        try
        {
            var orderNo = SelectedOrders.LastOrDefault()?.OrderNo ?? "";
            var result = await PlanningService.GetOrderPlanningDetailAsync(orderNo, 1, gauge, ply);
            // Result 1 (YarnStatus) will contain the color-wise grouping when flag=1
            // We filter by Yarn Name to show only colors for THAT yarn
            YarnColorDetails = result.YarnStatus.Where(y => y.Yarn == yarnName).ToList();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            HandleUnauthorized();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading orders: {ex.Message}");
        }
        finally
        {
            IsLoadingYarnColor = false;
            StateHasChanged();
        }
    }

    private void HandleUnauthorized()
    {
        ToastService.ShowWarning("Session Expired. Redirecting to login...");
        _tokenProvider.NotifySessionExpired();
        Navigation.NavigateTo("/login", true);
    }

    private async Task OpenGaugeDetailModal(string gauge, string flag = "0")
    {
        SelectedGauge = gauge;
        IsGaugeDetailModalOpen = true;
        IsLoadingGaugeDetail = true;
        StateHasChanged();

        try
        {
            var orderNo = SelectedOrders.LastOrDefault()?.OrderNo ?? "";
            string stylesFlag = (SelectedKnitType == "Silk" || SelectedKnitType == "Other" || SelectedKnitType == "Linen")
                ? SelectedKnitType
                : flag;
            var result = await PlanningService.GetOrderDetailByGuageAsync(orderNo, gauge, stylesFlag);
            GaugeDetails = result.ToList();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            HandleUnauthorized();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading gauge details: {ex.Message}");
            GaugeDetails = new();
        }
        finally
        {
            IsLoadingGaugeDetail = false;
            StateHasChanged();
        }
    }

    private void ToggleAnalysisModal()
    {
        IsAnalysisModalOpen = !IsAnalysisModalOpen;
        StateHasChanged();
    }

    protected override async Task OnInitializedAsync()
    {
        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user.Identity?.IsAuthenticated != true)
        {
            Navigation.NavigateTo("/login", true);
            return;
        }

        if (!Permissions.IsLoaded)
        {
            await Permissions.LoadPermissionsAsync();
        }

        if (!Permissions.CanView("OrderPlanning"))
        {
            Navigation.NavigateTo("/dashboard");
            return;
        }

        // Veil the whole arrival when a task card sent us here. A plain visit paints the page
        // while it fills and needs no veil; the deep link then goes on to pull production
        // status, open the modal and select the gauge, which is the stretch that felt dead.
        var fromTaskLink = !string.IsNullOrWhiteSpace(FromOrderNo);
        if (fromTaskLink)
            _loading.Show($"Loading planning for {FromOrderNo}…");

        try
        {
            await LoadOrderCollectionTypes();
            await LoadMonths();
            ApplyLinkedMonth();
            await LoadOrders();
            await LoadGaugeUtilization();
            IsLoading = false;

            await OpenFromTaskLinkAsync();
        }
        finally
        {
            // finally: a throw anywhere above must not strand the user behind a veil that
            // never lifts. IsLoading is settled here too, for the same reason.
            IsLoading = false;
            if (fromTaskLink) _loading.Hide();
        }
    }

    // Arrived from a Planning task card. Selects the task's order and opens its planning
    // modal on the task's gauge, following the same order -> production status -> gauge
    // sequence the bulk planner uses, so the modal gets the context it expects.
    //
    // AllOrders only holds the SELECTED MONTH's orders, so an order planned in another
    // month genuinely isn't here. That is reported rather than silently ignored, otherwise
    // the click just looks broken.
    private async Task OpenFromTaskLinkAsync()
    {
        if (string.IsNullOrWhiteSpace(FromOrderNo)) return;

        var order = AllOrders.FirstOrDefault(o =>
            string.Equals(o.OrderNo, FromOrderNo, StringComparison.OrdinalIgnoreCase));

        if (order is null)
        {
            ToastService.ShowInfo($"Order {FromOrderNo} isn't in the selected month — pick its month to plan it.");
            return;
        }

        SelectedOrders = new List<MonthlyOrderDetailDto> { order };
        SelectedKnitType = "Knit";
        await LoadOrderProductionStatus(order.OrderNo);
        await OpenPlanningForGauge(FromGauge ?? string.Empty);
    }

    private async Task LoadGaugeUtilization()
    {
        try
        {
            var result = await PlanningService.GetGaugeUtilizationReportAsync(null);
            GaugeUtilization = result.ToList();
            StateHasChanged();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            HandleUnauthorized();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading gauge utilization: {ex.Message}");
        }
    }

    // Runs between LoadMonths and LoadOrders so the month the link asked for is the one
    // actually queried. Snaps to the matching dropdown entry when there is one, so the
    // control agrees with what got loaded; an unlisted month is still honoured.
    private void ApplyLinkedMonth()
    {
        if (string.IsNullOrWhiteSpace(FromMonth)
            || !DateTime.TryParse(FromMonth, CultureInfo.InvariantCulture, DateTimeStyles.None, out var linked))
            return;

        var match = Months.FirstOrDefault(m => m.MonthStartDate.Year == linked.Year
                                            && m.MonthStartDate.Month == linked.Month);
        SelectedMonth = match?.MonthStartDate ?? linked;
    }

    private async Task LoadMonths()
    {
        try
        {
            var result = await PlanningService.GetMonthlySummaryAsync(DateTime.Now);
            Months = result.ToList();

            if (Months.Any())
            {
                // Default to the CURRENT month's option (so the dropdown shows it),
                // falling back to the first month if the current one isn't listed.
                var now = DateTime.Now;
                var current = Months.FirstOrDefault(m =>
                    m.MonthStartDate.Year == now.Year && m.MonthStartDate.Month == now.Month);
                SelectedMonth = (current ?? Months.First()).MonthStartDate;
            }
            StateHasChanged();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            HandleUnauthorized();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading months: {ex.Message}");
        }
    }

    // Load order -> Sample/Production map once (used to filter the order list by the wizard step).
    private async Task LoadOrderCollectionTypes()
    {
        try
        {
            var result = (await PlanningService.GetOrderCollectionTypesAsync()).ToList();
            // Group case-insensitively and merge duplicate order numbers (OR the flags),
            // so two rows differing only by case/spacing can't collide in the dictionary.
            OrderTypeMap = result
                .Where(t => !string.IsNullOrEmpty(t.OrderNo))
                .GroupBy(t => t.OrderNo.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    g => g.Key,
                    g => new OrderCollectionTypeDto
                    {
                        OrderNo = g.Key,
                        IsSample = g.Any(x => x.IsSample),
                        IsProduction = g.Any(x => x.IsProduction)
                    },
                    StringComparer.OrdinalIgnoreCase);
            OrderTypesUnavailable = OrderTypeMap.Count == 0;
            OrderTypesDiag = OrderTypesUnavailable
                ? $"Call OK but {result.Count} rows / {OrderTypeMap.Count} mapped"
                : string.Empty;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading order collection types: {ex.Message}");
            OrderTypeMap = new();
            OrderTypesUnavailable = true;
            OrderTypesDiag = ex.Message;
        }
    }

    // Stamp each order with its Sample/Production flags from the collection-type map.
    private void TagOrdersWithCollectionType()
    {
        // If we have NO collection data at all (proc not deployed / returned nothing),
        // don't force everything to Production - show every order under both lists so
        // the user is never blocked. Real filtering kicks in once the proc returns data.
        bool noCollectionData = OrderTypeMap.Count == 0;

        foreach (var o in AllOrders)
        {
            if (noCollectionData)
            {
                o.IsSample = true;
                o.IsProduction = true;
                continue;
            }

            if (o.OrderNo != null && OrderTypeMap.TryGetValue(o.OrderNo.Trim(), out var t))
            {
                o.IsSample = t.IsSample;
                o.IsProduction = t.IsProduction;
            }
            else
            {
                // Known data set but this order has no collection rows: treat as Production.
                o.IsSample = false;
                o.IsProduction = true;
            }
        }
    }

    // Classify each order in the month by the product types it contains, so the order
    // list can be filtered by the chosen Product Type. Cached per month load; throttled.
    private async Task ScanOrderProductTypes()
    {
        OrderProductTypeMap = new(StringComparer.OrdinalIgnoreCase);
        if (!AllOrders.Any()) return;

        IsScanningOrderTypes = true;
        StateHasChanged();

        using var gate = new SemaphoreSlim(6);
        var tasks = AllOrders.Select(async o =>
        {
            await gate.WaitAsync();
            try
            {
                var analysis = await PlanningService.GetOrderAnalysisAsync(o.OrderNo, null, 1);
                var types = analysis.DetailedAnalysis?
                    .Where(d => d.TotalQty > 0 && !string.IsNullOrWhiteSpace(d.KnitType))
                    .Select(d => d.KnitType.Trim())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)
                    ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                lock (OrderProductTypeMap) { OrderProductTypeMap[o.OrderNo.Trim()] = types; }
            }
            catch { /* leave unclassified - it just won't be hidden */ }
            finally { gate.Release(); }
        });
        await Task.WhenAll(tasks);

        IsScanningOrderTypes = false;
        StateHasChanged();
    }

    private async Task LoadOrders()
    {
        try
        {
            var result = await PlanningService.GetMonthlyOrderDetailsAsync(SelectedMonth);
            AllOrders = result.ToList();
            TagOrdersWithCollectionType();
            SelectedOrders.Clear(); // Reset selection when month changes
            StateHasChanged();
            await ScanOrderProductTypes(); // classify by product type for the order filter
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            HandleUnauthorized();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading orders: {ex.Message}");
        }
    }

    // Product Type / Sample-Production changed: reset any in-progress selection so the
    // newly chosen context starts clean (order list re-filters via FilteredOrders).
    private void OnWizardProductTypeChanged()
    {
        SelectedKnitType = WizardProductType; // picking an order auto-opens this analysis
        WizardOrderType = string.Empty;       // progressive: re-pick Sample/Production next
        ResetPlanningContext();
    }

    private void OnWizardOrderTypeChanged()
    {
        ResetPlanningContext();
    }

    private void ResetPlanningContext()
    {
        SelectedOrders.Clear();
        Mode1Analysis = new();
        Mode2Analysis = new();
        Mode3Analysis = new();
        PlanningDetail = new();
        DataHasLoaded = false;
        SelectedModalGauge = string.Empty;
        StateHasChanged();
    }

    private async Task OnMonthChanged(ChangeEventArgs e)
    {
        if (DateTime.TryParse(e.Value?.ToString(), out var date))
        {
            SelectedMonth = date;
            await LoadOrders();
        }
    }

    private async Task ToggleOrderSelection(MonthlyOrderDetailDto order)
    {
        await JS.InvokeVoidAsync("console.log", $"Toggling selection for: {order.OrderNo}");
        if (SelectedOrders.Any(o => o.OrderNo == order.OrderNo))
        {
            SelectedOrders.Clear();
            await JS.InvokeVoidAsync("console.log", $"Removed {order.OrderNo}. Total selected: {SelectedOrders.Count}");
        }
        else
        {
            SelectedOrders.Clear();
            SelectedOrders.Add(order);
            await JS.InvokeVoidAsync("console.log", $"Added {order.OrderNo}. Total selected: {SelectedOrders.Count}");
        }

        // Fetch Order Analysis (Mode 1 and Mode 3) for the currently selected order (or clear if none)
        if (SelectedOrders.Any())
        {
            try
            {
                var lastOrder = SelectedOrders.Last().OrderNo;
                Mode1Analysis = await PlanningService.GetOrderAnalysisAsync(lastOrder, null, 1);
                Mode3Analysis = await PlanningService.GetOrderAnalysisAsync(lastOrder, null, 3);
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                HandleUnauthorized();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating order analysis: {ex.Message}");
            }
        }
        else
        {
            Mode1Analysis = new();
            Mode2Analysis = new();
            Mode3Analysis = new();
        }

        // Reset analysis state whenever selection changes
        DataHasLoaded = false;
        PlanningDetail = new();
        Mode2Analysis = new(); // Reset Mode 2
        CloseOrderDropdown();
        StateHasChanged();

        // Wizard flow: product type was chosen up-front, so picking an order
        // immediately opens that type's analysis (no need to click the summary row).
        if (SelectedOrders.Any() && !string.IsNullOrEmpty(WizardProductType))
        {
            await OnOrderSummaryRowClick(WizardProductType);
        }
    }

    private string SelectedKnitType { get; set; } = string.Empty;
    private FabricAnalysisPlanDto FabricAnalysisData { get; set; } = new();
    private WeaveAnalysisPlanDto WeaveAnalysisData { get; set; } = new();

    private async Task OnOrderSummaryRowClick(string knitType)
    {
        SelectedKnitType = knitType;
        if (!SelectedOrders.Any()) 
        {
            await JS.InvokeVoidAsync("console.log", "Analysis aborted: No orders selected.");
            return;
        }

        IsAnalysing = true;
        DataHasLoaded = false;
        // Reset data containers to avoid showing stale data
        FabricAnalysisData = new();
        WeaveAnalysisData = new();
        PlanningDetail = new();
        
        StateHasChanged();
        
        // Force Blazor to render the loading spinner
        await Task.Yield();
        await Task.Delay(200);

        try
        {
            var order = SelectedOrders.Last();
            await JS.InvokeVoidAsync("console.log", $"Starting analysis for Order: {order.OrderNo}, KnitType: {knitType}");
            
            if (knitType == "Knit")
            {
                await LoadOrderProductionStatus(order.OrderNo);
            }
            else if (knitType == "Weave")
            {
                WeaveAnalysisData = await PlanningService.GetWeaveAnalysisPlanAsync(order.OrderNo, null, 1);
            }
            else
            {
                FabricAnalysisData = await PlanningService.GetFabricAnalysisPlanAsync(order.OrderNo, knitType, 1);
            }
            
            // Mode 2 Analysis for Style Print/Emb counts
            Mode2Analysis = await PlanningService.GetOrderAnalysisAsync(order.OrderNo, knitType, 2);
            
            DataHasLoaded = true;
            await JS.InvokeVoidAsync("console.log", $"Analysis complete for {knitType}");
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            HandleUnauthorized();
        }
        catch (Exception ex)
        {
            await JS.InvokeVoidAsync("console.log", $"Analysis failed: {ex.Message}");
            DataHasLoaded = false;
        }
        finally
        {
            IsAnalysing = false;
            StateHasChanged();
        }
    }

    private OrderProductionStatusDto OrderStatusData { get; set; } = new();
    private OrderPlanningDetailDto PlanningDetail { get; set; } = new();
    private OrderAnalysisResultDto Mode1Analysis { get; set; } = new();
    private OrderAnalysisResultDto Mode2Analysis { get; set; } = new();
    private OrderAnalysisResultDto Mode3Analysis { get; set; } = new();

    private async Task LoadOrderProductionStatus(string orderNo)
    {
        try
        {
            OrderStatusData = await PlanningService.GetOrderProductionStatusAsync(orderNo, 0);
            PlanningDetail = await PlanningService.GetOrderPlanningDetailAsync(orderNo, 0);

            try
            {
                var allPlans = await PlanningService.GetPlannedDataByOrderAsync(orderNo);
                OrderAllPlannedPlans = allPlans.ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading all planned plans inside LoadOrderProductionStatus: {ex.Message}");
                OrderAllPlannedPlans = new();
            }

            // Fetch Knit Completion Date from API
            var completionData = await PlanningService.GetOrderDeptCompletionDateAsync(orderNo, "KNIT");
            if (completionData != null)
            {
                KnitCompleteDate = completionData.DeptCompletionDate;
                RequiredCompletionDate = completionData.DeptCompletionDate;
            }

            StateHasChanged();

            // Inline auto-selection suggestion (machine count + Est. End) per gauge.
            await ScanGaugeSuggestions();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            HandleUnauthorized();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading production status: {ex.Message}");
        }
    }

    private void ToggleOrderDropdown()
    {
        IsOrderDropdownOpen = !IsOrderDropdownOpen;
    }

    private void CloseOrderDropdown()
    {
        IsOrderDropdownOpen = false;
    }

    private void SelectAllOrders()
    {
        if (SelectedOrders.Count == AllOrders.Count)
        {
            SelectedOrders.Clear();
        }
        else
        {
            SelectedOrders.Clear();
            SelectedOrders.AddRange(AllOrders);
        }
        StateHasChanged();
    }

    private List<KnitterItem> Knitters { get; set; } = new()
    {
        new() { Name = "Babita Maharjan", CardNo = "020", Working = "12, 1" },
        new() { Name = "Bhagwoti Thapa", CardNo = "021", Working = "7, 8" },
        new() { Name = "Anjali Maharjan", CardNo = "031", Working = "7, 8" },
        new() { Name = "Shrijana Kuwar", CardNo = "034", Working = "7, 8" },
        new() { Name = "Ramita Shrestha", CardNo = "037", Working = "16, 1" }
    };

    public class MachineGridItem
    {
        public string Gauge { get; set; } = string.Empty;
        public int OrderQty { get; set; }
        public int FreeMachine { get; set; }
        public string NextMachine { get; set; } = string.Empty;
        public int TotalMachine { get; set; }
        public DateTime? ExpectedDate { get; set; }
        public string EstimatedDays { get; set; } = string.Empty;
    }

    public class YarnAnalysisItem
    {
        public string Yarn { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public int Available { get; set; }
        public int Required { get; set; }
        public string YarnArrival { get; set; } = string.Empty;
    }

    public class KnitterItem
    {
        public string Name { get; set; } = string.Empty;
        public string CardNo { get; set; } = string.Empty;
        public string Working { get; set; } = string.Empty;
    }

    private bool IsFullyPlannedEditMode { get; set; } = false;
    private int EditingPlanId { get; set; } = 0;
    
    private DateTime _editStartDate = DateTime.Now;
    private DateTime EditStartDate
    {
        get => _editStartDate;
        set
        {
            _editStartDate = value;
            if (EditingPlanId > 0)
            {
                var editingPlan = DbPlannedPlans.FirstOrDefault(p => p.MasterPlanChildId == EditingPlanId)
                               ?? OrderAllPlannedPlans.FirstOrDefault(p => p.MasterPlanChildId == EditingPlanId);
                if (editingPlan != null)
                {
                    RecalculateEditMaxMachines(editingPlan.Gauge);
                }
            }
        }
    }
    
    private int EditMachines { get; set; } = 1;
    private decimal EditQty { get; set; } = 0;
    private int EditMaxMachines { get; set; } = 99;
    private decimal EditMaxQty { get; set; } = 999999;

    private DateTime EditEndDate
    {
        get
        {
            if (BaseDays <= 0 || BaseQty <= 0 || EditMachines <= 0) return EditStartDate;
            // Knit: BaseDays are single-machine days. Silk/Other/Linen are outstation
            // masters: machines are irrelevant - dates follow the master's team rate.
            decimal capPerMc = (BaseQty / BaseDays) * OvertimeFactor;
            if (capPerMc <= 0) return EditStartDate;
            double daysNeeded = IsMasterBasedType
                ? (double)(EditQty / capPerMc)
                : (double)(EditQty / (capPerMc * EditMachines));
            return AddWorkingDays(EditStartDate, daysNeeded);
        }
    }

    private void ToggleFullyPlannedEdit()
    {
        IsFullyPlannedEditMode = !IsFullyPlannedEditMode;
        if (!IsFullyPlannedEditMode)
        {
            EditingPlanId = 0;
        }
        StateHasChanged();
    }

    private void StartPlanEdit(PlannedDataDto plan)
    {
        EditingPlanId = plan.MasterPlanChildId;
        _editStartDate = plan.StartDate; // direct assignment to backing field to avoid premature recalculation
        EditQty = plan.Quantity;
        
        int machines = 1;
        if (!string.IsNullOrEmpty(plan.Mc))
        {
            var match = System.Text.RegularExpressions.Regex.Match(plan.Mc, @"\d+");
            if (match.Success && int.TryParse(match.Value, out int parsed))
            {
                machines = parsed;
            }
        }
        EditMachines = IsMasterBasedType ? 1 : machines; // outstation masters: Mc fixed at 1

        // Initialize base variables for the plan's gauge so that EditEndDate is calculated correctly!
        var machineData = PlanningDetail?.MachineStatus?.FirstOrDefault(m => string.Equals(m.Gauge?.Trim(), plan.Gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (machineData != null)
        {
            BaseDays = machineData.NewOrderDays;
            BaseMachines = machineData.SuggestedNewOrderMachines;
            BaseQty = machineData.NewOrderQty;
        }

        // Calculate max values for this edit!
        RecalculateEditMaxMachines(plan.Gauge);
        RecalculateEditMaxQty(plan);

        StateHasChanged();
    }

    private void RecalculateEditMaxMachines(string gauge)
    {
        if (PlanningDetail?.MachineStatus == null || string.IsNullOrEmpty(gauge)) return;

        var machineData = PlanningDetail.MachineStatus.FirstOrDefault(m => string.Equals(m.Gauge?.Trim(), gauge.Trim(), StringComparison.OrdinalIgnoreCase));
        int totalLimit = (machineData != null && machineData.TrueGaugeLimit > 0) ? machineData.TrueGaugeLimit : 2;

        int freeMachines = totalLimit;

        if (PlanningDetail?.ForwardTimeline != null)
        {
            var timelineEntry = PlanningDetail.ForwardTimeline
                .FirstOrDefault(t => string.Equals(t.Gauge?.Trim(), gauge.Trim(), StringComparison.OrdinalIgnoreCase) 
                                     && t.PlanSnapshotDate.Date == EditStartDate.Date);

            if (timelineEntry != null)
            {
                int capLimit = timelineEntry.TotalActiveCapacityLimit > 0 ? timelineEntry.TotalActiveCapacityLimit : totalLimit;
                freeMachines = timelineEntry.ImmediateFreeMachines > 0 ? timelineEntry.ImmediateFreeMachines : capLimit;
            }
            else
            {
                var timelineForGauge = PlanningDetail.ForwardTimeline
                    .Where(t => string.Equals(t.Gauge?.Trim(), gauge.Trim(), StringComparison.OrdinalIgnoreCase))
                    .OrderBy(t => t.PlanSnapshotDate)
                    .ToList();

                if (timelineForGauge.Any())
                {
                    var lastEntry = timelineForGauge.Last();
                    if (EditStartDate.Date > lastEntry.PlanSnapshotDate.Date)
                    {
                        freeMachines = lastEntry.TotalActiveCapacityLimit > 0 ? lastEntry.TotalActiveCapacityLimit : totalLimit;
                    }
                    else if (EditStartDate.Date < timelineForGauge.First().PlanSnapshotDate.Date)
                    {
                        freeMachines = timelineForGauge.First().TotalActiveCapacityLimit > 0 ? timelineForGauge.First().TotalActiveCapacityLimit : totalLimit;
                    }
                    else
                    {
                        var precedingEntry = timelineForGauge
                            .Where(t => t.PlanSnapshotDate.Date < EditStartDate.Date)
                            .OrderByDescending(t => t.PlanSnapshotDate)
                            .FirstOrDefault();

                        if (precedingEntry != null)
                        {
                            int capLimit = precedingEntry.TotalActiveCapacityLimit > 0 ? precedingEntry.TotalActiveCapacityLimit : totalLimit;
                            freeMachines = precedingEntry.ImmediateFreeMachines > 0 ? precedingEntry.ImmediateFreeMachines : capLimit;
                        }
                    }
                }
            }
        }

        EditMaxMachines = freeMachines > 0 ? freeMachines : (machineData != null && machineData.TrueGaugeLimit > 0 ? machineData.TrueGaugeLimit : 1);
        
        if (EditMachines > EditMaxMachines)
        {
            EditMachines = EditMaxMachines;
        }
    }

    private void RecalculateEditMaxQty(PlannedDataDto plan)
    {
        var machineData = PlanningDetail?.MachineStatus?.FirstOrDefault(m => string.Equals(m.Gauge?.Trim(), plan.Gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (machineData != null)
        {
            var currentPlanned = DbPlannedPlans.Where(p => p.MasterPlanChildId != plan.MasterPlanChildId).Sum(p => p.Quantity);
            EditMaxQty = machineData.NewOrderQty - currentPlanned;
        }
        else
        {
            EditMaxQty = 999999;
        }
    }

    private void CancelPlanEdit()
    {
        EditingPlanId = 0;
        StateHasChanged();
    }

    private async Task SavePlanEdit(PlannedDataDto plan)
    {
        if (EditingPlanId == 0) return;
        var orderNo = SelectedOrders.LastOrDefault()?.OrderNo ?? "";
        if (string.IsNullOrEmpty(orderNo)) return;

        var machineData = PlanningDetail?.MachineStatus?.FirstOrDefault(m => string.Equals(m.Gauge?.Trim(), plan.Gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
        if (machineData != null)
        {
            var currentPlanned = DbPlannedPlans.Where(p => p.MasterPlanChildId != plan.MasterPlanChildId).Sum(p => p.Quantity);
            var remaining = machineData.NewOrderQty - currentPlanned;
            if (EditQty > remaining)
            {
                ShowAlert("Allocation Limit Exceeded", $"The quantity ({EditQty:N0}) exceeds the remaining required quantity ({remaining:N0}) for gauge '{plan.Gauge}'. Please reduce the allocation quantity to proceed.", "warning");
                return;
            }
        }

        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        var userId = authState.User.Identity?.Name ?? "system";

        try
        {
            bool success = await PlanningService.UpdatePlanDetailAsync(
                plan.MasterPlanChildId,
                EditStartDate,
                EditEndDate,
                EditQty,
                EditMachines,
                userId
            );

            if (success)
            {
                EditingPlanId = 0;
                ToastService.ShowSuccess("Plan updated successfully.");
                
                // Clear the cached input so it gets recalculated
                if (!string.IsNullOrEmpty(plan.Gauge))
                {
                    GaugeInputs.Remove(plan.Gauge.Trim().ToUpper());
                }

                PlanningDetail = await PlanningService.GetOrderPlanningDetailAsync(orderNo, 0);
                Mode1Analysis = await PlanningService.GetOrderAnalysisAsync(orderNo, null, 1);
                var dbPlans = await PlanningService.GetPlannedDataByOrderAsync(orderNo, plan.Gauge);
                DbPlannedPlans = dbPlans.ToList();

                if (SelectedKnitType == "Knit")
                {
                    var machineList = await PlanningService.GetMachinePlaningAsync(plan.Gauge);
                    MachinePlaningList = machineList.ToList();
                }

                var allPlans = await PlanningService.GetPlannedDataByOrderAsync(orderNo);
                OrderAllPlannedPlans = allPlans.ToList();

                var updatedMachineData = PlanningDetail?.MachineStatus?.FirstOrDefault(m => string.Equals(m.Gauge?.Trim(), plan.Gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (updatedMachineData != null)
                {
                    var plannedQty = DbPlannedPlans.Sum(p => p.Quantity);
                    if (plannedQty >= updatedMachineData.NewOrderQty)
                    {
                        PlanQty = 0;
                    }
                    else
                    {
                        PlanQty = updatedMachineData.NewOrderQty - plannedQty;
                    }
                }
            }
            else
            {
                ShowAlert("Update Failed", "The server returned an error while updating the plan.", "error");
            }
            StateHasChanged();
        }
        catch (Exception ex)
        {
            ShowAlert("Update Failed", ex.Message, "error");
        }
    }

    private async Task DeletePlanDetail(PlannedDataDto plan)
    {
        // Warn if a knitter is already assigned to this plan - deleting removes the assignment too.
        string knitterWarning = "";
        try
        {
            var busy = await PlanningService.GetKnitterBusyAsync();
            var assigned = busy.FirstOrDefault(b => b.PlanId == plan.MasterPlanChildId);
            if (assigned != null)
            {
                knitterWarning = $"\n\nWARNING: knitter (card {assigned.CardNo}) is assigned to this plan - deleting will remove that assignment.";
            }
        }
        catch { /* warning is best-effort; deletion check must not block */ }

        bool confirm = await JS.InvokeAsync<bool>("confirm", $"Are you sure you want to delete this plan of Qty: {plan.Quantity:N0}?{knitterWarning}");
        if (!confirm) return;

        var orderNo = SelectedOrders.LastOrDefault()?.OrderNo ?? "";
        if (string.IsNullOrEmpty(orderNo)) return;

        try
        {
            bool success = await PlanningService.DeletePlanDetailAsync(plan.MasterPlanChildId);
            if (success)
            {
                ToastService.ShowSuccess("Plan deleted successfully.");

                // Clear the cached input so it gets recalculated
                if (!string.IsNullOrEmpty(plan.Gauge))
                {
                    GaugeInputs.Remove(plan.Gauge.Trim().ToUpper());
                }

                PlanningDetail = await PlanningService.GetOrderPlanningDetailAsync(orderNo, 0);
                Mode1Analysis = await PlanningService.GetOrderAnalysisAsync(orderNo, null, 1);
                var dbPlans = await PlanningService.GetPlannedDataByOrderAsync(orderNo, plan.Gauge);
                DbPlannedPlans = dbPlans.ToList();

                if (SelectedKnitType == "Knit")
                {
                    var machineList = await PlanningService.GetMachinePlaningAsync(plan.Gauge);
                    MachinePlaningList = machineList.ToList();
                }

                var allPlans = await PlanningService.GetPlannedDataByOrderAsync(orderNo);
                OrderAllPlannedPlans = allPlans.ToList();

                var updatedMachineData = PlanningDetail?.MachineStatus?.FirstOrDefault(m => string.Equals(m.Gauge?.Trim(), plan.Gauge?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (updatedMachineData != null)
                {
                    var plannedQty = DbPlannedPlans.Sum(p => p.Quantity);
                    if (plannedQty >= updatedMachineData.NewOrderQty)
                    {
                        PlanQty = 0;
                    }
                    else
                    {
                        PlanQty = updatedMachineData.NewOrderQty - plannedQty;
                    }
                }
                
                if (!DbPlannedPlans.Any())
                {
                    IsFullyPlannedEditMode = false;
                }
            }
            else
            {
                ShowAlert("Delete Failed", "The server returned an error while deleting the plan.", "error");
            }
            StateHasChanged();
        }
        catch (Exception ex)
        {
            ShowAlert("Delete Failed", ex.Message, "error");
        }
    }
}
