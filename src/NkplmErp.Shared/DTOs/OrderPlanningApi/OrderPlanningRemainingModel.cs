namespace NkplmErp.Shared.DTOs.OrderPlanningApi;

/// <summary>Live per-gauge workload from PLM (already-received pieces are not counted again).</summary>
public class OrderPlanningRemainingModel
{
    public string Gauge { get; set; } = string.Empty;
    public decimal BacklogQty { get; set; }
    public decimal BacklogDays { get; set; }
    public decimal NewOrderQty { get; set; }
    public decimal NewOrderDays { get; set; }
}
