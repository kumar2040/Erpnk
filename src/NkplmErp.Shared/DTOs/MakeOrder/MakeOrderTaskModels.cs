namespace NkplmErp.Shared.DTOs.MakeOrder;

/// <summary>
/// A new order submitted on PLM's "Make order" page, pushed in through the inbound API (route "make-order").
/// Plain values only; sp_PoTask_MakeOrder builds the task text and does the conversions.
/// </summary>
public class MakeOrderTaskRequest
{
    public string OrderNo { get; set; } = string.Empty;
    public string? BuyerName { get; set; }
    public string? Collection { get; set; }
    public string? OrderType { get; set; }        // 'p' production / 's' sample
    public string? ShippingDate { get; set; }     // yyyy-MM-dd
    public int? TotalPieces { get; set; }
    public int? StyleCount { get; set; }
}

public class MakeOrderTaskResultDto
{
    public int PoTaskId { get; set; }
    public bool WasCreated { get; set; }
}
