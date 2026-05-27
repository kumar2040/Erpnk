namespace NkplmErp.Shared.DTOs;

public class MultiLineGraphSeries
{
    public string Name { get; set; } = string.Empty;
    public string Color { get; set; } = "#000000";
    public List<LineGraphDataPoint> DataPoints { get; set; } = new();
    public bool IsVisible { get; set; } = true;
}
