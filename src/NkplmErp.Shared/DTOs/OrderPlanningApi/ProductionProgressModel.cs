namespace NkplmErp.Shared.DTOs.OrderPlanningApi;

/// <summary>
/// What has been made of one style of an order's non-Knit product type (Weave / Silk / Linen / Other), from PLM.
/// Remaining = Qty - Made, never below 0.
/// </summary>
public class ProductionProgressModel
{
    public string Style { get; set; } = string.Empty;
    public decimal OrderQty { get; set; }
    public decimal Issued { get; set; }
    public decimal Made { get; set; }
    public decimal Remaining { get; set; }
}
