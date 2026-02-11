using Microsoft.AspNetCore.Components;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Pages;

public partial class OrdersDashboard : ComponentBase
{
    [Inject] public IBuyerOrderSummaryService BuyerOrderSummaryService { get; set; } = default!;

    private List<OrderStatus> OrderStatusData = new();
    
    // Keeping other hardcoded lists for now as we only have API for OrderStatus
    private List<ProductionStatus> ProductionStatusData = new()
    {
        new("01", "Gerstaat", "PO-100", 110, new DateTime(2025, 12, 5), 80, new DateTime(2025, 12, 4)),
        new("02", "Friendly Hunting Gmbh", "PO-200", 160, new DateTime(2025, 12, 16), 10, new DateTime(2025, 12, 8)),
        new("03", "Marita", "PO-500", 140, new DateTime(2025, 11, 5), 20, new DateTime(2025, 12, 1)),
        new("04", "Nlunomads", "PO-300", 200, new DateTime(2025, 12, 13), 30, new DateTime(2025, 12, 12)),
        new("05", "ATM", "PO-600", 500, new DateTime(2025, 12, 7), 40, new DateTime(2025, 12, 6)),
        new("06", "Anish", "PO-800", 500, new DateTime(2025, 12, 10), 50, new DateTime(2025, 12, 8)),
        new("07", "Neela", "PO-800", 700, new DateTime(2025, 12, 10), 60, new DateTime(2025, 12, 9)),
        new("08", "Sushant", "PO-700", 800, new DateTime(2025, 12, 22), 70, new DateTime(2025, 12, 11)),
        new("09", "Brahme", "PO-400", 900, new DateTime(2025, 12, 29), 80, new DateTime(2025, 12, 21)),
        new("10", "David", "PO-250", 850, new DateTime(2025, 12, 23), 100, new DateTime(2025, 12, 22))
    };

    private List<SettingPiece> SettingPieceData = new()
    {
        new("01", "PO-100", "FINA-3712", new DateTime(2025, 12, 5)),
        new("02", "PO-200", "FINA-3712", new DateTime(2025, 12, 5)),
        new("03", "PO-300", "FINA-3712", new DateTime(2025, 10, 5)),
        new("04", "PO-400", "FINA-3712", new DateTime(2025, 12, 5)),
        new("05", "PO-500", "FINA-3712", new DateTime(2025, 12, 5)),
        new("06", "PO-600", "FINA-3712", new DateTime(2025, 12, 5)),
        new("07", "PO-700", "FINA-3712", new DateTime(2025, 12, 5)),
        new("08", "PO-800", "FINA-3712", new DateTime(2025, 12, 5)),
        new("09", "PO-900", "FINA-3712", new DateTime(2025, 12, 5)),
        new("10", "PO-1000", "FINA-3712", new DateTime(2025, 12, 5))
    };

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            try 
            {
                var data = await BuyerOrderSummaryService.GetBuyerOrderSummaryAsync(2026, "All");
                OrderStatusData = data.Select((d, index) => new OrderStatus(
                    (index + 1).ToString("D2"), 
                    d.CustomerName, 
                    d.TotalOrder, 
                    d.RunningOrder, 
                    d.NotStartedOrder
                )).ToList();
                
                StateHasChanged();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching dashboard data: {ex.Message}");
            }
        }
    }

    public record OrderStatus(string SN, string ClientName, int NumberOfPO, int RunningQuantity, int WaitingPO);
    public record ProductionStatus(string SN, string Buyer, string OrderNo, int Quantity, DateTime ShippingDate, int Progress, DateTime Projected);
    public record SettingPiece(string SN, string OrderNo, string Style, DateTime StartDate);
}
