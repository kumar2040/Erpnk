namespace NkplmErp.Shared.DTOs.OrderPlanningApi;

/// <summary>Product types (Knit / Weave / Silk / Linen / Other ...) that have pieces in one order, from PLM.</summary>
public class OrderProductTypesModel
{
    public string OrderNo { get; set; } = string.Empty;
    public List<string> KnitTypes { get; set; } = new();
}
