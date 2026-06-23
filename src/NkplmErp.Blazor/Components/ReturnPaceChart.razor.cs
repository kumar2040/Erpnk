using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Components;

namespace NkplmErp.Blazor.Components;

// One returned-pieces event at a date+time (raw input; the component does the math).
public class ReturnPacePoint
{
    public DateTime Date { get; set; }   // receipt date + time
    public int Count { get; set; }
}

// Purpose-built "return pace" chart for the knitter return-detail modal.
//
// The X axis is DATETIME-scaled and DYNAMIC:
//   * It always spans at least the planned window [Start, End] (made >= 1 day so the
//     expected line is never vertical).
//   * It EXTENDS to the last actual return when a task overran its planned window, so the
//     actual line visibly runs past where the expected line ended.
//   * When everything still fits inside a single calendar day, it switches to an HOUR axis
//     (labels by time) so a one-day task's returns spread across the day instead of
//     collapsing to a single point.
// Two straight lines: Expected (dashed) 0 -> Issue across the planned window; Actual (solid)
// cumulative returns. All coordinates use the invariant culture so comma-decimal locales
// can't corrupt the SVG paths.
public partial class ReturnPaceChart : ComponentBase
{
    [Parameter] public DateTime Start { get; set; }
    [Parameter] public DateTime End { get; set; }
    [Parameter] public int Issue { get; set; }
    [Parameter] public List<ReturnPacePoint> Points { get; set; } = new();

    [Parameter] public string YLabel { get; set; } = "Pieces";

    [Parameter] public double Width { get; set; } = 760;
    [Parameter] public double Height { get; set; } = 340;
    [Parameter] public double PadLeft { get; set; } = 56;
    [Parameter] public double PadRight { get; set; } = 28;
    [Parameter] public double PadTop { get; set; } = 24;
    [Parameter] public double PadBottom { get; set; } = 52;

    private string _actualPath = "";
    private string _expectedPath = "";
    private string _xAxisTitle = "Return date";
    private readonly List<(double X, double Y)> _actualDots = new();
    private readonly List<(double X, double Y)> _expectedDots = new();
    private readonly List<(double Y, string Label)> _yTicks = new();
    private readonly List<(double X, string Label, string Anchor)> _xTicks = new();

    private double PlotMidX => PadLeft + (Width - PadLeft - PadRight) / 2;
    private double PlotMidY => PadTop + (Height - PadTop - PadBottom) / 2;

    protected override void OnParametersSet() => Build();

    private void Build()
    {
        _actualPath = "";
        _expectedPath = "";
        _actualDots.Clear();
        _expectedDots.Clear();
        _yTicks.Clear();
        _xTicks.Clear();

        var plotW = Width - PadLeft - PadRight;
        var plotH = Height - PadTop - PadBottom;

        // Planned window — guarantee at least one day so the expected line is never vertical.
        var plannedStart = Start;
        var plannedEnd = End;
        if (plannedEnd <= plannedStart) plannedEnd = plannedStart.AddDays(1);

        var returns = (Points ?? new()).OrderBy(p => p.Date).ToList();

        // Dynamic axis: extend to cover actual returns (task overran its planned window).
        var axisStart = plannedStart;
        var axisEnd = plannedEnd;
        if (returns.Count > 0)
        {
            if (returns[0].Date < axisStart) axisStart = returns[0].Date;
            if (returns[^1].Date > axisEnd) axisEnd = returns[^1].Date;
        }
        if (axisEnd <= axisStart) axisEnd = axisStart.AddDays(1);

        // Single-day (everything within ~a day) -> hour axis: anchor to the calendar day and
        // label by time, so the day's returns spread across hours.
        var timeMode = (axisEnd - axisStart).TotalDays <= 1.5;
        if (timeMode)
        {
            var day = axisStart.Date;
            axisStart = day;
            axisEnd = day.AddDays(1);
            if (plannedEnd > axisEnd) axisEnd = plannedEnd;
            if (returns.Count > 0 && returns[^1].Date > axisEnd) axisEnd = returns[^1].Date;
        }
        _xAxisTitle = timeMode ? "Return time" : "Return date";

        var totalSec = (axisEnd - axisStart).TotalSeconds;
        if (totalSec <= 0) totalSec = 1;
        double XFor(DateTime dt) => PadLeft + Math.Clamp((dt - axisStart).TotalSeconds / totalSec, 0, 1) * plotW;

        // Cumulative actual.
        var cum = new List<(DateTime At, int V)>();
        var run = 0;
        foreach (var r in returns) { run += r.Count; cum.Add((r.Date, run)); }

        // Nice integer Y axis covering max(Issue, returned).
        var rawMax = Math.Max(Issue, run);
        if (rawMax <= 0) rawMax = 1;
        var step = (int)Math.Ceiling(rawMax / 4.0);
        if (step < 1) step = 1;
        var maxY = step * 4;
        double YFor(double v) => PadTop + (1 - v / maxY) * plotH;

        // Expected: 0 at plannedStart -> Issue at plannedEnd; flat before/after across the axis.
        var exp = new List<(double X, double Y)>();
        if (axisStart < plannedStart) exp.Add((XFor(axisStart), YFor(0)));
        exp.Add((XFor(plannedStart), YFor(0)));
        exp.Add((XFor(plannedEnd), YFor(Issue)));
        if (axisEnd > plannedEnd) exp.Add((XFor(axisEnd), YFor(Issue)));
        _expectedPath = BuildPath(exp);
        _expectedDots.Add((XFor(plannedStart), YFor(0)));
        _expectedDots.Add((XFor(plannedEnd), YFor(Issue)));

        // Actual: 0 at axisStart -> cumulative at each return -> flat to axisEnd.
        var act = new List<(double X, double Y)> { (XFor(axisStart), YFor(0)) };
        foreach (var c in cum)
        {
            act.Add((XFor(c.At), YFor(c.V)));
            _actualDots.Add((XFor(c.At), YFor(c.V)));
        }
        act.Add((XFor(axisEnd), YFor(cum.Count > 0 ? cum[^1].V : 0)));
        _actualPath = BuildPath(act);

        // Y ticks.
        for (var i = 0; i <= 4; i++)
        {
            var v = step * i;
            _yTicks.Add((YFor(v), v.ToString(CultureInfo.InvariantCulture)));
        }

        // X ticks: spaced to the available width so labels never collide (~1 per 72px),
        // with the first/last anchored to the plot edges so they don't clip.
        var spanDays = (axisEnd - axisStart).TotalDays;
        var maxLabels = timeMode ? 5 : 6;
        var count = Math.Clamp((int)(plotW / 72.0), 2, maxLabels);
        for (var i = 0; i < count; i++)
        {
            var frac = (double)i / (count - 1);
            var dt = axisStart.AddSeconds(frac * totalSec);
            var anchor = i == 0 ? "start" : (i == count - 1 ? "end" : "middle");
            _xTicks.Add((PadLeft + frac * plotW, FmtX(dt, spanDays, timeMode), anchor));
        }
    }

    private static string FmtX(DateTime dt, double spanDays, bool timeMode)
    {
        if (timeMode) return dt.ToString("HH:mm");
        if (spanDays > 366) return dt.ToString("MMM yy");
        return dt.ToString("dd MMM");
    }

    private static string BuildPath(List<(double X, double Y)> pts)
    {
        if (pts.Count == 0) return "";
        var sb = new StringBuilder();
        sb.Append($"M {F(pts[0].X)} {F(pts[0].Y)}");
        for (var i = 1; i < pts.Count; i++) sb.Append($" L {F(pts[i].X)} {F(pts[i].Y)}");
        return sb.ToString();
    }

    private static string F(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
}
