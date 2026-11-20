namespace NkplmErp.Shared.DTOs.OrderPlanningApi;

/// <summary>Display name of a yarn product ("product_name: count1") from PLM, for a product id the ERP only holds as a number.</summary>
public class YarnNameModel
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
