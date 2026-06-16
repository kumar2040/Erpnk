using NkplmErp.Shared.DTOs;

namespace NkplmErp.Application.Interfaces;

public interface IBuyerOrderSummaryService
{
    Task<IEnumerable<BuyerOrderSummaryDto>> GetBuyerOrderSummaryAsync(int year, string type,int maxrec);
    Task<IEnumerable<int>> GetBuyerOrderYearsAsync(int? customerId);
    Task<IEnumerable<BuyerOrderHistoryDto>> GetBuyerOrderHistoryAsync(int customerId, int? year = null);
    Task<IEnumerable<BuyerProfile>> GetBuyerProfileAsync(int customerId, int? year = null);
    Task<IEnumerable<AbsentBuyer>> GetAbsentBuyer();
    Task<IEnumerable<OrderStatusDetailDto>> GetOrderStatusDetailAsync(int year, string status);
    Task<IEnumerable<ProductionFlowDto>> GetProductionFlowAsync(int buyerId, string? orderNo = null);
    Task<IEnumerable<DepartmentStockDto>> GetdepartmentStockAsync(string? OrderNo, string Department);
    Task<IEnumerable<OrderViewHeaderDto>> GetOrderViewDataAsync(string orderNo);
    Task<StyleDetailsDto> GetStyleDetailsAsync(string styleNo);
    Task<IEnumerable<BuyerOrderDto>> GetBuyersOrdersAsync(int buyerId, int flag);
    Task<IEnumerable<OrderPriceAnalysisDto>> GetOrderPriceAnalysisAsync(string orderNo, decimal usdRate);
}
