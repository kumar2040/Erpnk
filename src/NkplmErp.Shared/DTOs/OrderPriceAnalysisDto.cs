namespace NkplmErp.Shared.DTOs;

public class OrderPriceAnalysisDto
{
    public long SN { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal TotalQuantity { get; set; }
    public string? StyleGuage { get; set; }
    public string? StylePly { get; set; }
    public string? YarnInfo { get; set; }
    public decimal NetWet { get; set; }
    public decimal OverratePerPcUsd { get; set; }
    public decimal FinalCostPerPcUsd { get; set; }
    public decimal GrandTotalProductionCostUsd { get; set; }
    public decimal TotalRevenueUsd { get; set; }
}
