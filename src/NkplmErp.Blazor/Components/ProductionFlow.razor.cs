using Microsoft.AspNetCore.Components;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Components;

public partial class ProductionFlow : ComponentBase
{
    /// <summary>
    /// The list of production flow records to display as cards.
    /// </summary>
    [Inject]
    public IBuyerOrderSummaryService BuyerOrderSummaryService { get; set; } = default!;

    /// <summary>
    /// The list of production flow records to display as cards.
    /// </summary>
    [Parameter]
    public IEnumerable<ProductionFlowDto> Items { get; set; } = Enumerable.Empty<ProductionFlowDto>();

    /// <summary>
    /// Raised when the user clicks the "Details →" link on a card.
    /// </summary>
    [Parameter]
    public EventCallback<ProductionFlowDto> OnOrderSelected { get; set; }

    // ── Popup State ──────────────────────────────────────────────────────────
    private bool IsStockPopupVisible { get; set; }
    private bool IsLoadingStock { get; set; }
    private string ActiveStockTab { get; set; } = "Stock";
    private string SelectedDeptName { get; set; } = string.Empty;
    private string SelectedDeptCode { get; set; } = string.Empty;
    private string SelectedOrderNo { get; set; } = string.Empty;
    private IEnumerable<DepartmentStockDto> StockItems { get; set; } = Enumerable.Empty<DepartmentStockDto>();
    private IEnumerable<DepartmentStockDto> DetailsItems { get; set; } = Enumerable.Empty<DepartmentStockDto>();
    private List<string> SizeHeaderColumns { get; set; } = new();
    private List<string> DetailsSizeHeaders { get; set; } = new();

    // ── Order Detail Popup State ──────────────────────────────────────────────
    private bool IsOrderDetailVisible { get; set; }
    private bool IsLoadingOrderDetail { get; set; }
    private IEnumerable<OrderViewHeaderDto> SelectedOrderDetails { get; set; } = Enumerable.Empty<OrderViewHeaderDto>();
    
    // ── Style Detail Popup State ──────────────────────────────────────────────
    private bool IsStyleModalVisible { get; set; }
    private bool IsLoadingStyleHistory { get; set; }
    private string? SelectedStyleNo { get; set; }
    private StyleDetailsDto? SelectedStyleDetails { get; set; }

    private async Task ShowDepartmentDetails(string orderNo, string deptCode, string deptName)
    {
        SelectedOrderNo = orderNo;
        SelectedDeptCode = deptCode;
        SelectedDeptName = deptName;
        IsLoadingStock = true;
        IsStockPopupVisible = true;
        ActiveStockTab = "Stock";
        StockItems = Enumerable.Empty<DepartmentStockDto>();
        DetailsItems = Enumerable.Empty<DepartmentStockDto>();

        try
        {
            StockItems = await BuyerOrderSummaryService.GetdepartmentStockAsync(orderNo, deptCode);
            
            // Extract unique size headers across all items
            SizeHeaderColumns = StockItems
                .SelectMany(x => x.Sizes.Keys)
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => GetSizeSortOrder(x))
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching stock: {ex.Message}");
        }
        finally
        {
            IsLoadingStock = false;
        }
    }

    private async Task SetActiveTab(string tab)
    {
        ActiveStockTab = tab;
        if (tab == "Details" && !DetailsItems.Any())
        {
            IsLoadingStock = true;
            try
            {
                // Pass orderNo as null for department-wide stock details
                DetailsItems = await BuyerOrderSummaryService.GetdepartmentStockAsync(null, SelectedDeptCode);
                DetailsSizeHeaders = DetailsItems
                    .SelectMany(x => x.Sizes.Keys)
                    .Select(x => x.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => GetSizeSortOrder(x))
                    .ToList();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error fetching details: {ex.Message}");
            }
            finally
            {
                IsLoadingStock = false;
            }
        }
    }

    private void CloseStockPopup()
    {
        IsStockPopupVisible = false;
        StockItems = Enumerable.Empty<DepartmentStockDto>();
    }

    private async Task ShowOrderDetails(string orderNo)
    {
        if (string.IsNullOrEmpty(orderNo)) return;
        
        SelectedOrderNo = orderNo;
        IsLoadingOrderDetail = true;
        IsOrderDetailVisible = true;
        SelectedOrderDetails = Enumerable.Empty<OrderViewHeaderDto>();

        try
        {
            SelectedOrderDetails = await BuyerOrderSummaryService.GetOrderViewDataAsync(orderNo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching order details: {ex.Message}");
        }
        finally
        {
            IsLoadingOrderDetail = false;
        }
    }

    private async Task ShowStyleDetails(string styleNo)
    {
        if (string.IsNullOrEmpty(styleNo)) return;
        
        SelectedStyleNo = styleNo;
        IsLoadingStyleHistory = true;
        IsStyleModalVisible = true;
        SelectedStyleDetails = null;

        try
        {
            SelectedStyleDetails = await BuyerOrderSummaryService.GetStyleDetailsAsync(styleNo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching style details: {ex.Message}");
        }
        finally
        {
            IsLoadingStyleHistory = false;
        }
    }

    private void CloseOrderDetail()
    {
        IsOrderDetailVisible = false;
        SelectedOrderDetails = Enumerable.Empty<OrderViewHeaderDto>();
    }

    private static int GetSizeSortOrder(string size)
    {
        var fixedOrder = new List<string> { "XXXS", "XXS", "XS", "S", "M", "L", "XL", "XXL", "XXXL", "OSFA" };
        int idx = fixedOrder.IndexOf(size.ToUpper());
        return idx == -1 ? 999 : idx;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates how many days remain until the shipping date.
    /// Returns null if ShippingDate is not set.
    /// </summary>
    private static int? DaysLeft(ProductionFlowDto order)
    {
        if (order.ShippingDate is null) return null;
        var today = DateOnly.FromDateTime(DateTime.Today);
        return order.ShippingDate.Value.DayNumber - today.DayNumber;
    }

    /// <summary>
    /// Returns true if the remaining days are less than 8.
    /// </summary>
    private static bool IsUrgent(int? daysLeft) => daysLeft.HasValue && daysLeft.Value < 8;

    /// <summary>
    /// Returns the progress percentage based on packed vs total PCS.
    /// Clamps result between 0 and 100.
    /// </summary>
    private static int ProgressPercent(ProductionFlowDto order)
    {
        if (order.PCS is null or 0) return 0;
        var packed = (order.totalPacked is null or 0)
            ? (order.PCK ?? 0) + (order.Total_Dispatch ?? 0) + (order.totalDispatched ?? 0)
            : order.totalPacked.Value;
        var pct = (int)Math.Round(packed * 100m / order.PCS.Value);
        return Math.Clamp(pct, 0, 100);
    }

    /// <summary>
    /// Returns the Tailwind CSS class for the progress bar fill colour
    /// based on the current percentage.
    /// </summary>
    private static string ProgressBarColor(int pct) => pct switch
    {
        >= 80 => "bg-emerald-500",
        >= 50 => "bg-indigo-600",
        >= 25 => "bg-amber-500",
        _      => "bg-rose-500"
    };
}
