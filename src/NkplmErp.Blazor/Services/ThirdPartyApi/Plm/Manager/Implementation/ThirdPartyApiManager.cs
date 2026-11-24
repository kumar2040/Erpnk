using NkplmErp.Blazor.Services.ThirdPartyApi.Plm.Manager.Interface;
using NkplmErp.Blazor.Services.ThirdPartyApi.Plm.Manager.Route;
using NkplmErp.Blazor.Shared.Http;
using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.DTOs.OrderPlanningApi;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Services.ThirdPartyApi.Plm.Manager.Implementation
{
    public class ThirdPartyApiManager(IHttpServices http) : IThirdPartyApiManager
    {
        public Task<IResponse<List<OrderStatusDetailDto>>> GetOrderStatusAsync(int year, string status) =>
            http.GetAsync<List<OrderStatusDetailDto>>(ThirdPartyApiEndpoint.OrderStatus(year, status));

        public Task<IResponse<List<ProductionFlowDto>>> GetProductionFlowAsync(int buyerId, string? orderNo) =>
            http.GetAsync<List<ProductionFlowDto>>(ThirdPartyApiEndpoint.ProductionFlow(buyerId, orderNo));

        public Task<IResponse<List<DepartmentStockDto>>> GetDepartmentStockAsync(string dept, string? orderNo) =>
            http.GetAsync<List<DepartmentStockDto>>(ThirdPartyApiEndpoint.DepartmentStock(dept, orderNo));

        public Task<IResponse<List<ShipmentDueOrderDto>>> GetShipmentDueOrdersAsync(string from, string to, bool includeOverdue) =>
            http.GetAsync<List<ShipmentDueOrderDto>>(ThirdPartyApiEndpoint.ShipmentDueOrders(from, to, includeOverdue));

        public Task<IResponse<List<MonthlyOrderDetailDto>>> GetBomOrdersAsync(DateTime month) =>
            http.GetAsync<List<MonthlyOrderDetailDto>>(ThirdPartyApiEndpoint.BomOrders(month));

        public Task<IResponse<List<MonthlyOrderSummaryDto>>> GetMonthsAsync(DateTime inputDate) =>
            http.GetAsync<List<MonthlyOrderSummaryDto>>(ThirdPartyApiEndpoint.Months(inputDate));

        public Task<IResponse<List<MonthlyOrderDetailDto>>> GetOrdersAsync(DateTime month, string? knitType = null) =>
            http.GetAsync<List<MonthlyOrderDetailDto>>(ThirdPartyApiEndpoint.Orders(month, knitType));

        public Task<IResponse<List<OrderCollectionTypeDto>>> GetCollectionTypesAsync() =>
            http.GetAsync<List<OrderCollectionTypeDto>>(ThirdPartyApiEndpoint.CollectionTypes);

        public Task<IResponse<List<OrderPlanningRemainingModel>>> GetRemainingAsync(string orderNo) =>
            http.GetAsync<List<OrderPlanningRemainingModel>>(ThirdPartyApiEndpoint.Remaining(orderNo));

        public Task<IResponse<OrderAnalysisResultDto>> GetOrderAnalysisAsync(string orderNo, string? knitType, int mode) =>
            http.GetAsync<OrderAnalysisResultDto>(ThirdPartyApiEndpoint.OrderAnalysis(orderNo, knitType, mode));

        public Task<IResponse<List<WeaveFactorySummaryDto>>> GetWeaveFactoriesAsync(string orderNo) =>
            http.GetAsync<List<WeaveFactorySummaryDto>>(ThirdPartyApiEndpoint.WeaveFactories(orderNo));

        public Task<IResponse<List<ProductionProgressModel>>> GetProductionProgressAsync(string orderNo, string knitType) =>
            http.GetAsync<List<ProductionProgressModel>>(ThirdPartyApiEndpoint.ProductionProgress(orderNo, knitType));

        public Task<IResponse<List<ProductionProgressModel>>> GetMasterProgressAsync(string orderNo, string knitType) =>
            http.GetAsync<List<ProductionProgressModel>>(ThirdPartyApiEndpoint.MasterProgress(orderNo, knitType));

        public Task<IResponse<List<PlanRowModel>>> GetPlanRowsAsync(string? orderNo = null) =>
            http.GetAsync<List<PlanRowModel>>(ThirdPartyApiEndpoint.PlanRows(orderNo));

        public Task<IResponse<PlanRowMachineModel>> UpdatePlanRowMachineAsync(int id, List<PlanRowMachineQtyModel> machines) =>
            http.PostAsJsonAsync<PlanRowMachineModel>(ThirdPartyApiEndpoint.PlanRowMachine, new PlanRowMachineModel { Id = id, Machines = machines });

        public Task<IResponse<List<YarnNameModel>>> GetYarnNamesAsync(IEnumerable<string> productIds) =>
            http.GetAsync<List<YarnNameModel>>(ThirdPartyApiEndpoint.YarnNames(productIds));

        public Task<IResponse<List<OrderProductTypesModel>>> GetOrderTypesAsync(DateTime month) =>
            http.GetAsync<List<OrderProductTypesModel>>(ThirdPartyApiEndpoint.OrderTypes(month));

        public Task<IResponse<List<OrderDeptCompletionDto>>> GetDeptCompletionDateAsync(string orderNo, string dept) =>
            http.GetAsync<List<OrderDeptCompletionDto>>(ThirdPartyApiEndpoint.DeptCompletionDate(orderNo, dept));

        public Task<IResponse<List<OrderDetailByGuageDto>>> GetGaugeDetailsAsync(string orderNo, string gauge, string flag, string? knitType = null) =>
            http.GetAsync<List<OrderDetailByGuageDto>>(ThirdPartyApiEndpoint.GaugeDetails(orderNo, gauge, flag, knitType));

        public Task<IResponse<List<YarnPlanningStatusDto>>> GetYarnAsync(string orderNo, string? gauge = null, string? ply = null) =>
            http.GetAsync<List<YarnPlanningStatusDto>>(ThirdPartyApiEndpoint.Yarn(orderNo, gauge, ply));
    }
}
