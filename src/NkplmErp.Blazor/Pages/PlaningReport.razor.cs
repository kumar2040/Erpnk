using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.Blazor.Services.Auth;
using NkplmErp.Blazor.Services.Toast;
using System.Net;

namespace NkplmErp.Blazor.Pages;

public partial class PlaningReport
{
    [Inject] private IProductionPlanningService PlanningService { get; set; } = null!;
    [Inject] private AuthenticationStateProvider AuthStateProvider { get; set; } = default!;
    [Inject] private TokenProvider _tokenProvider { get; set; } = default!;
    [Inject] private ToastService ToastService { get; set; } = default!;
    [Inject] private NavigationManager Navigation { get; set; } = default!;
    [Inject] private NkplmErp.Blazor.Services.RoleManagement.PermissionService Permissions { get; set; } = default!;

    private List<PlaningReportDayDto> Days { get; set; } = new();
    private DateTime Month { get; set; } = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
    private bool IsLoading { get; set; } = true;

    // Capacity ceiling toggle: knitters (real bottleneck, default) vs machines.
    private bool UseKnitterCapacity { get; set; } = true;

    // Overlay ship-date (order_ldate) markers on the calendar.
    private bool ShowShipDates { get; set; } = true;

    private int TotalMachines => Days.FirstOrDefault()?.TotalMachines ?? 0;
    private int TotalKnitters => Days.FirstOrDefault()?.TotalKnitters ?? 0;
    private int MachineKnitterGap => Math.Max(0, TotalMachines - TotalKnitters);

    // The capacity denominator for utilisation.
    private int Capacity => UseKnitterCapacity ? TotalKnitters : TotalMachines;

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
        if (!Permissions.CanView("PlaningReport"))
        {
            Navigation.NavigateTo("/dashboard");
            return;
        }

        await LoadMonth();
        IsLoading = false;
    }

    private async Task LoadMonth()
    {
        IsLoading = true;
        StateHasChanged();
        try
        {
            var from = Month;
            var to = Month.AddMonths(1).AddDays(-1);
            Days = await PlanningService.GetPlaningReportAsync(from, to);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            HandleUnauthorized();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading planing report: {ex.Message}");
            ToastService.ShowError("Failed to load the Planing Report.");
            Days = new();
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task PrevMonth() { Month = Month.AddMonths(-1); await LoadMonth(); }
    private async Task NextMonth() { Month = Month.AddMonths(1); await LoadMonth(); }
    private async Task ThisMonth() { Month = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1); await LoadMonth(); }

    private PlaningReportDayDto? GetDay(DateTime date) =>
        Days.FirstOrDefault(d => d.Date.Date == date.Date);

    // Utilisation % for a day against the chosen capacity (busy / capacity).
    private double Utilization(PlaningReportDayDto d)
    {
        if (Capacity <= 0) return 0;
        return (double)d.BusyMachines / Capacity * 100.0;
    }

    private string UtilColor(double util)
    {
        if (util <= 0) return "#eef2f7";          // idle
        if (util < 60) return "#d7f5e6";          // green - light
        if (util < 85) return "#bfe9f5";          // blue - healthy
        if (util <= 100) return "#fde9b8";        // amber - busy
        return "#f9c9c4";                          // red - over capacity
    }

    private string UtilTextColor(double util) => util > 100 ? "#a3231a" : "#1a3353";

    // Saturated colour for the in-cell utilisation bar.
    private string UtilBarColor(double util)
    {
        if (util <= 0) return "#cbd5e1";
        if (util < 60) return "#3dbf8a";          // green
        if (util < 85) return "#2ab3b1";          // teal
        if (util <= 100) return "#f0a500";        // amber
        return "#e05252";                          // red
    }

    // ---- KPI tiles ----
    private IEnumerable<PlaningReportDayDto> WorkingDays => Days.Where(d => !d.IsSaturday);

    private double AvgUtilization =>
        WorkingDays.Any() && Capacity > 0
            ? WorkingDays.Average(d => (double)d.BusyMachines / Capacity * 100.0)
            : 0;

    private int OverCapacityDays =>
        Capacity > 0 ? WorkingDays.Count(d => d.BusyMachines > Capacity) : 0;

    // Idle capacity in "knitter-days": unused knitters summed over working days.
    private int IdleCapacityDays =>
        Capacity > 0 ? WorkingDays.Sum(d => Math.Max(0, Capacity - d.BusyMachines)) : 0;

    // ---- Calendar layout ----
    // Returns the weeks (rows) of the month; each week is 7 day-slots (null = blank).
    private List<List<DateTime?>> CalendarWeeks
    {
        get
        {
            var weeks = new List<List<DateTime?>>();
            int daysInMonth = DateTime.DaysInMonth(Month.Year, Month.Month);
            int lead = (int)Month.DayOfWeek; // Sunday = 0

            var current = new List<DateTime?>();
            for (int i = 0; i < lead; i++) current.Add(null);

            for (int day = 1; day <= daysInMonth; day++)
            {
                current.Add(new DateTime(Month.Year, Month.Month, day));
                if (current.Count == 7)
                {
                    weeks.Add(current);
                    current = new List<DateTime?>();
                }
            }
            if (current.Any())
            {
                while (current.Count < 7) current.Add(null);
                weeks.Add(current);
            }
            return weeks;
        }
    }

    private static readonly string[] WeekdayHeaders = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

    private void HandleUnauthorized()
    {
        ToastService.ShowWarning("Session Expired. Redirecting to login...");
        _tokenProvider.NotifySessionExpired();
        Navigation.NavigateTo("/login", true);
    }
}
