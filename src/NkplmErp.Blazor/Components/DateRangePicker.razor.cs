using Microsoft.AspNetCore.Components;

namespace NkplmErp.Blazor.Components;

public partial class DateRangePicker : ComponentBase
{
    [Parameter] public DateTime? StartDate { get; set; }
    [Parameter] public EventCallback<DateTime?> StartDateChanged { get; set; }
    [Parameter] public DateTime? EndDate { get; set; }
    [Parameter] public EventCallback<DateTime?> EndDateChanged { get; set; }

    /// <summary>Fired whenever a range is committed (preset click or custom Apply), and once on init.</summary>
    [Parameter] public EventCallback OnRangeChanged { get; set; }

    /// <summary>Preset applied on first render when no StartDate/EndDate is supplied.</summary>
    [Parameter] public string InitialPreset { get; set; } = "30days";

    [Parameter] public string Label { get; set; } = "Date Range";

    private record Preset(string Key, string Label);
    private static readonly Preset[] Presets =
    {
        new("today", "Today"), new("yesterday", "Yesterday"), new("thisweek", "This Week"),
        new("last7days", "Last 7 Days"), new("30days", "30 Days"),
        new("thismonth", "This Month"), new("lastmonth", "Last Month")
    };

    private static readonly string[] DayLabels = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };

    private string ActiveViewMode { get; set; } = "30days";
    private bool IsOpen { get; set; }
    private DateTime? TempStartDate { get; set; }
    private DateTime? TempEndDate { get; set; }
    private int DateSelectionStep { get; set; }
    private DateTime? LeftCalendarMonth { get; set; }
    private DateTime? HoveredDate { get; set; }

    private bool _initialised;

    protected override async Task OnInitializedAsync()
    {
        if (_initialised) return;
        _initialised = true;

        LeftCalendarMonth = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        // If the parent didn't seed a range, apply the initial preset and notify (one initial fetch).
        if (StartDate == null && EndDate == null && !string.IsNullOrEmpty(InitialPreset))
        {
            ActiveViewMode = InitialPreset;
            var (s, e) = Compute(InitialPreset);
            await CommitAsync(s, e);
        }
    }

    /// <summary>Public — lets a parent reset to a preset (e.g. a Reset button).</summary>
    public Task ApplyPresetAsync(string preset) => ApplyPreset(preset);

    private async Task CommitAsync(DateTime? start, DateTime? end)
    {
        StartDate = start;
        EndDate = end;
        await StartDateChanged.InvokeAsync(start);
        await EndDateChanged.InvokeAsync(end);
        await OnRangeChanged.InvokeAsync();
    }

    private (DateTime start, DateTime end) Compute(string preset)
    {
        var today = DateTime.Today;
        return preset switch
        {
            "today"     => (today, today),
            "yesterday" => (today.AddDays(-1), today.AddDays(-1)),
            "thisweek"  => (today.AddDays(-(int)today.DayOfWeek), today.AddDays(-(int)today.DayOfWeek).AddDays(6)),
            "last7days" => (today.AddDays(-6), today),
            "30days"    => (today, today.AddMonths(1)),
            "thismonth" => (new DateTime(today.Year, today.Month, 1), new DateTime(today.Year, today.Month, 1).AddMonths(1).AddDays(-1)),
            "lastmonth" => (new DateTime(today.Year, today.Month, 1).AddMonths(-1), new DateTime(today.Year, today.Month, 1).AddDays(-1)),
            _           => (today, today.AddMonths(1)),
        };
    }

    private void ToggleDropdown()
    {
        if (IsOpen) { IsOpen = false; return; }
        IsOpen = true;
        TempStartDate = StartDate;
        TempEndDate = EndDate;
        DateSelectionStep = 0;
        LeftCalendarMonth = new DateTime((TempStartDate ?? DateTime.Today).Year, (TempStartDate ?? DateTime.Today).Month, 1);
    }

    private void Cancel() => IsOpen = false;

    private string GetLabel()
    {
        if (StartDate == null || EndDate == null) return "Select Date Range";
        return $"{StartDate.Value:MMMM dd, yyyy} – {EndDate.Value:MMMM dd, yyyy}";
    }

    private void PrevMonth() { if (LeftCalendarMonth != null) LeftCalendarMonth = LeftCalendarMonth.Value.AddMonths(-1); }
    private void NextMonth() { if (LeftCalendarMonth != null) LeftCalendarMonth = LeftCalendarMonth.Value.AddMonths(1); }

    private async Task ApplyPreset(string preset)
    {
        ActiveViewMode = preset;
        IsOpen = false;
        var (s, e) = Compute(preset);
        await CommitAsync(s, e);
    }

    private void SelectCustom()
    {
        ActiveViewMode = "custom";
        TempStartDate = StartDate;
        TempEndDate = EndDate;
        DateSelectionStep = 0;
        LeftCalendarMonth = new DateTime((TempStartDate ?? DateTime.Today).Year, (TempStartDate ?? DateTime.Today).Month, 1);
    }

    private void OnDayClick(DateTime date)
    {
        if (DateSelectionStep == 0)
        {
            TempStartDate = date;
            TempEndDate = null;
            DateSelectionStep = 1;
            ActiveViewMode = "custom";
        }
        else if (date < TempStartDate)
        {
            TempStartDate = date;
            TempEndDate = null;
            DateSelectionStep = 1;
        }
        else
        {
            TempEndDate = date;
            DateSelectionStep = 0;
        }
    }

    private void OnDayHover(DateTime date) { if (DateSelectionStep == 1) HoveredDate = date; }

    private bool IsDaySelected(DateTime date) => date == TempStartDate || date == TempEndDate;

    private bool IsDayInRange(DateTime date)
    {
        if (TempStartDate == null) return false;
        if (TempEndDate != null) return date > TempStartDate && date < TempEndDate;
        if (DateSelectionStep == 1 && HoveredDate != null && HoveredDate > TempStartDate)
            return date > TempStartDate && date < HoveredDate;
        return false;
    }

    private static List<DateTime> GetCalendarDaysList(int year, int month)
    {
        var firstDay = new DateTime(year, month, 1);
        var startOfWeek = firstDay.AddDays(-(int)firstDay.DayOfWeek);
        var days = new List<DateTime>();
        for (int i = 0; i < 42; i++) days.Add(startOfWeek.AddDays(i));
        return days;
    }

    private async Task ApplyCustom()
    {
        IsOpen = false;
        await CommitAsync(TempStartDate, TempEndDate);
    }
}
