using Microsoft.AspNetCore.Components;

namespace NkplmErp.Blazor.Components;

/// <summary>
/// Modern popup date picker. Works in two modes:
///   Single  — bind with @bind-Value (DateTime?)
///   Range   — set Range="true" and bind @bind-StartDate / @bind-EndDate
/// Pure C# (no JS); closes on outside click via a transparent backdrop.
/// </summary>
public partial class DatePicker : ComponentBase
{
    [Parameter] public bool Range { get; set; }

    // ---- single ----
    [Parameter] public DateTime? Value { get; set; }
    [Parameter] public EventCallback<DateTime?> ValueChanged { get; set; }

    // ---- range ----
    [Parameter] public DateTime? StartDate { get; set; }
    [Parameter] public EventCallback<DateTime?> StartDateChanged { get; set; }
    [Parameter] public DateTime? EndDate { get; set; }
    [Parameter] public EventCallback<DateTime?> EndDateChanged { get; set; }

    // ---- options ----
    [Parameter] public string Placeholder { get; set; } = "Select date";
    [Parameter] public string Format { get; set; } = "dd MMM yyyy";
    [Parameter] public DateTime? MinDate { get; set; }
    [Parameter] public DateTime? MaxDate { get; set; }
    [Parameter] public bool Disabled { get; set; }

    private bool _open;
    private DateTime _view = DateTime.Today;

    private static readonly string[] WeekDays = { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" };

    private bool HasValue => Range ? (StartDate.HasValue || EndDate.HasValue) : Value.HasValue;

    private string DisplayText
    {
        get
        {
            if (Range)
            {
                if (!StartDate.HasValue && !EndDate.HasValue) return Placeholder;
                var s = StartDate?.ToString(Format) ?? "…";
                var e = EndDate?.ToString(Format) ?? "…";
                return $"{s}  →  {e}";
            }
            return Value?.ToString(Format) ?? Placeholder;
        }
    }

    private void Toggle()
    {
        if (Disabled) return;
        _open = !_open;
        if (_open) SyncView();
    }

    private void Close() => _open = false;

    private void SyncView()
    {
        var anchor = Range ? (StartDate ?? EndDate) : Value;
        var d = anchor ?? DateTime.Today;
        _view = new DateTime(d.Year, d.Month, 1);
    }

    private IEnumerable<DateTime> GridDays()
    {
        var first = new DateTime(_view.Year, _view.Month, 1);
        var offset = (int)first.DayOfWeek;          // Sunday = 0
        var start = first.AddDays(-offset);
        for (int i = 0; i < 42; i++) yield return start.AddDays(i);
    }

    private async Task PickDay(DateTime d)
    {
        if (IsDisabledDay(d)) return;

        if (!Range)
        {
            await ValueChanged.InvokeAsync(d.Date);
            _open = false;
            return;
        }

        // Range: first click (or restart) sets start & clears end; second sets end.
        if (!StartDate.HasValue || EndDate.HasValue)
        {
            await StartDateChanged.InvokeAsync(d.Date);
            await EndDateChanged.InvokeAsync(null);
        }
        else if (d.Date < StartDate.Value.Date)
        {
            await StartDateChanged.InvokeAsync(d.Date);   // earlier pick becomes the new start
        }
        else
        {
            await EndDateChanged.InvokeAsync(d.Date);
            _open = false;
        }
    }

    private void PrevMonth() => _view = _view.AddMonths(-1);
    private void NextMonth() => _view = _view.AddMonths(1);
    private void PrevYear() => _view = _view.AddYears(-1);
    private void NextYear() => _view = _view.AddYears(1);

    private async Task GoToday()
    {
        var t = DateTime.Today;
        _view = new DateTime(t.Year, t.Month, 1);
        if (!Range)
        {
            await ValueChanged.InvokeAsync(t);
            _open = false;
        }
    }

    private async Task Clear()
    {
        if (Range)
        {
            await StartDateChanged.InvokeAsync(null);
            await EndDateChanged.InvokeAsync(null);
        }
        else
        {
            await ValueChanged.InvokeAsync(null);
        }
    }

    // ---- day classification ----
    private bool IsOtherMonth(DateTime d) => d.Month != _view.Month;
    private bool IsToday(DateTime d) => d.Date == DateTime.Today;
    private bool IsDisabledDay(DateTime d) =>
        (MinDate.HasValue && d.Date < MinDate.Value.Date) ||
        (MaxDate.HasValue && d.Date > MaxDate.Value.Date);

    private bool IsRangeStart(DateTime d) => Range && StartDate?.Date == d.Date;
    private bool IsRangeEnd(DateTime d) => Range && EndDate?.Date == d.Date;
    private bool IsInRange(DateTime d) =>
        Range && StartDate.HasValue && EndDate.HasValue &&
        d.Date > StartDate.Value.Date && d.Date < EndDate.Value.Date;
    private bool IsSelectedSingle(DateTime d) => !Range && Value?.Date == d.Date;

    private string DayClass(DateTime d)
    {
        var c = new List<string> { "dp-day" };
        if (IsOtherMonth(d)) c.Add("other");
        if (IsToday(d)) c.Add("today");
        if (IsDisabledDay(d)) c.Add("disabled");
        if (IsInRange(d)) c.Add("inrange");
        if (IsRangeStart(d)) c.Add("rstart");
        if (IsRangeEnd(d)) c.Add("rend");
        if (IsSelectedSingle(d)) c.Add("sel");
        return string.Join(' ', c);
    }
}
