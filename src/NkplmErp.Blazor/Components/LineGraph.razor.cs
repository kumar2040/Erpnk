using Microsoft.AspNetCore.Components;
using NkplmErp.Shared.DTOs;
using System.Text;

namespace NkplmErp.Blazor.Components;

public partial class LineGraph : ComponentBase
{
    [Parameter] public List<LineGraphDataPoint> DataPoints { get; set; } = new();
    [Parameter] public string Title { get; set; } = "Statistics";
    [Parameter] public string StrokeColor { get; set; } = "#3b82f6";
    [Parameter] public string FillColor { get; set; } = "#3b82f6";
    [Parameter] public string? XLabel { get; set; }
    [Parameter] public string? YLabel { get; set; } = "Quantity";
    [Parameter] public double Height { get; set; } = 400;
    [Parameter] public double Width { get; set; } = 900;
    [Parameter] public double Padding { get; set; } = 75;

    private string _linePath = string.Empty;
    private string _areaPath = string.Empty;
    private List<(double X, double Y, string Label, double Value)> _points = new();
    private List<(double Y, string Label)> _yTicks = new();

    protected override void OnParametersSet()
    {
        CalculatePaths();
    }

    private void CalculatePaths()
    {
        if (DataPoints == null || !DataPoints.Any())
        {
            _linePath = string.Empty;
            _areaPath = string.Empty;
            _points.Clear();
            return;
        }

        var maxVal = DataPoints.Max(p => p.Value);
        if (maxVal == 0) maxVal = 1;

        var usableWidth = Width - (Padding * 2);
        var usableHeight = Height - (Padding * 2);
        var stepX = usableWidth / (DataPoints.Count > 1 ? DataPoints.Count - 1 : 1);

        _points = DataPoints.Select((p, i) => (
            X: Padding + (i * stepX),
            Y: Height - Padding - (p.Value / maxVal * usableHeight),
            Label: p.Label,
            Value: p.Value
        )).ToList();

        _linePath = BuildSmoothPath(_points);
        
        var areaSb = new StringBuilder(_linePath);
        areaSb.Append($" L {_points.Last().X} {Height - Padding}");
        areaSb.Append($" L {_points.First().X} {Height - Padding}");
        areaSb.Append(" Z");
        _areaPath = areaSb.ToString();

        // Calculate Y-Axis Ticks (5 ticks)
        _yTicks.Clear();
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
            
            // Simple cubic bezier control points
            var cp1X = p0.X + (p1.X - p0.X) / 2;
            var cp1Y = p0.Y;
            var cp2X = p0.X + (p1.X - p0.X) / 2;
            var cp2Y = p1.Y;

            sb.Append($" C {cp1X} {cp1Y}, {cp2X} {cp2Y}, {p1.X} {p1.Y}");
        }

        return sb.ToString();
    }
}
