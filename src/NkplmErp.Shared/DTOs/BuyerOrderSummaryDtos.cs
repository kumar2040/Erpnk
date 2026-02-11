namespace NkplmErp.Shared.DTOs;
public class BuyerOrderSummaryDto
{
     public long SN { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int NotStartedOrder { get; set; }
    public int RunningOrder { get; set; }
    public int TotalOrder { get; set; } 
}