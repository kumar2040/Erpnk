using Microsoft.AspNetCore.Components;
using NkplmErp.Shared.DTOs;
using System.Text;
using System.Linq;

namespace NkplmErp.Blazor.Components;

public partial class MultiLineGraph : ComponentBase
{
    [Parameter] public List<MultiLineGraphSeries> Series { get; set; } = new();
    [Parameter] public string Title { get; set; } = "Statistics";
    [Parameter] public string? XLabel { get; set; }
    [Parameter] public string? YLabel { get; set; } = "Quantity";
    [Parameter] public double Height { get; set; } = 300;
    [Parameter] public double Width { get; set; } = 800;
    [Parameter] public double Padding { get; set; } = 40;

    private List<(double X, double Y, string Label, double Value, string Color, string SeriesName)> _allPoints = new();
    private List<(MultiLineGraphSeries Series, string Path, List<(double X, double Y, string Label, double Value)> Points)> _seriesDrawData = new();
    private List<(double Y, string Label)> _yTicks = new();

    protected override void OnParametersSet()
    {
        CalculatePaths();
    }

    private void CalculatePaths()
    {
        _allPoints.Clear();
        _seriesDrawData.Clear();
        _yTicks.Clear();

        var visibleSeries = Series.Where(s => s.IsVisible).ToList();
        if (!visibleSeries.Any() || !visibleSeries.Any(s => s.DataPoints.Any()))
            return;

        var allPointsData = visibleSeries.SelectMany(s => s.DataPoints).ToList();
        var maxVal = allPointsData.Max(p => p.Value);
        if (maxVal == 0) maxVal = 1;

        var labels = visibleSeries.First().DataPoints.Select(p => p.Label).ToList();

        var usableWidth = Width - (Padding * 2);
        var usableHeight = Height - (Padding * 2);
        var stepX = usableWidth / (labels.Count > 1 ? labels.Count - 1 : 1);

        foreach (var s in visibleSeries)
        {
            var points = s.DataPoints.Select((p, i) => (
                X: Padding + (i * stepX),
                Y: Height - Padding - (p.Value / maxVal * usableHeight),
                Label: p.Label,
                Value: p.Value
            )).ToList();

            if (points.Any())
            {
                var path = BuildSmoothPath(points);
                _seriesDrawData.Add((s, path, points));
                foreach (var pt in points)
                {
                    _allPoints.Add((pt.X, pt.Y, pt.Label, pt.Value, s.Color, s.Name));
                }
            }
        }

        // Calculate Y-Axis Ticks (5 ticks)
        for (int i = 0; i <= 4; i++)
        {
            var val = (maxVal / 4) * i;
            var y = Height - Padding - (val / maxVal * usableHeight);
            _yTicks.Add((y, val.ToString("N0")));
        }
    }

    private string BuildSmoothPath(List<(double X, double Y, string Label, double Value)> pts)
    {
        if (pts.Count < 2) return string.Empty;

        var sb = new StringBuilder();
        sb.Append($"M {pts[0].X} {pts[0].Y}");

        for (int i = 0; i < pts.Count - 1; i++)
        {
            var p0 = pts[i];
            var p1 = pts[i + 1];
            
            var cp1X = p0.X + (p1.X - p0.X) / 2;
            var cp1Y = p0.Y;
            var cp2X = p0.X + (p1.X - p0.X) / 2;
            var cp2Y = p1.Y;

            sb.Append($" C {cp1X} {cp1Y}, {cp2X} {cp2Y}, {p1.X} {p1.Y}");
        }

        return sb.ToString();
    }
}
