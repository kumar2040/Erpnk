using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.DTOs.OrderPlanningApi;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Services.ThirdPartyApi.Manager.Interface
{
    public interface IThirdPartyApiManager
    {
        // Main dashboard
        Task<IResponse<List<OrderStatusDetailDto>>> GetOrderStatusAsync(int year, string status);
        Task<IResponse<List<ProductionFlowDto>>> GetProductionFlowAsync(int buyerId, string? orderNo);
        Task<IResponse<List<DepartmentStockDto>>> GetDepartmentStockAsync(string dept, string? orderNo);

        // Task board shipment alert
        Task<IResponse<List<ShipmentDueOrderDto>>> GetShipmentDueOrdersAsync(string from, string to, bool includeOverdue);

        // BOM page
        Task<IResponse<List<MonthlyOrderDetailDto>>> GetBomOrdersAsync(DateTime month);

        // Order planning
        Task<IResponse<List<MonthlyOrderSummaryDto>>> GetMonthsAsync(DateTime inputDate);
        Task<IResponse<List<MonthlyOrderDetailDto>>> GetOrdersAsync(DateTime month, string? knitType = null);
        Task<IResponse<List<OrderCollectionTypeDto>>> GetCollectionTypesAsync();
        Task<IResponse<List<OrderPlanningRemainingModel>>> GetRemainingAsync(string orderNo);
        Task<IResponse<OrderAnalysisResultDto>> GetOrderAnalysisAsync(string orderNo, string? knitType, int mode);
        Task<IResponse<List<OrderProductTypesModel>>> GetOrderTypesAsync(DateTime month);
        Task<IResponse<List<YarnNameModel>>> GetYarnNamesAsync(IEnumerable<string> productIds);
        Task<IResponse<List<ProductionProgressModel>>> GetProductionProgressAsync(string orderNo, string knitType);
        Task<IResponse<List<ProductionProgressModel>>> GetMasterProgressAsync(string orderNo, string knitType);
        Task<IResponse<List<PlanRowModel>>> GetPlanRowsAsync(string? orderNo = null);
        Task<IResponse<PlanRowMachineModel>> UpdatePlanRowMachineAsync(int id, List<PlanRowMachineQtyModel> machines);
        Task<IResponse<List<WeaveFactorySummaryDto>>> GetWeaveFactoriesAsync(string orderNo);
        Task<IResponse<List<OrderDeptCompletionDto>>> GetDeptCompletionDateAsync(string orderNo, string dept);
        Task<IResponse<List<OrderDetailByGuageDto>>> GetGaugeDetailsAsync(string orderNo, string gauge, string flag, string? knitType = null);
        Task<IResponse<List<YarnPlanningStatusDto>>> GetYarnAsync(string orderNo, string? gauge = null, string? ply = null);
    }
}
