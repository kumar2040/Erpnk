using NkplmErp.Blazor.Services.ThirdPartyApi.Manager.Interface;
using NkplmErp.Blazor.Services.ThirdPartyApi.Manager.Route;
using NkplmErp.Blazor.Shared.Http;
using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.DTOs.OrderPlanningApi;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Services.ThirdPartyApi.Manager.Implementation
{
    public class ThirdPartyApiManager(IHttpServices http) : IThirdPartyApiManager
    {
        public Task<IResponse<List<MonthlyOrderSummaryDto>>> GetMonthsAsync(DateTime inputDate) =>
            http.GetAsync<List<MonthlyOrderSummaryDto>>(ThirdPartyApiEndpoint.Months(inputDate));

        public Task<IResponse<List<MonthlyOrderDetailDto>>> GetOrdersAsync(DateTime month) =>
            http.GetAsync<List<MonthlyOrderDetailDto>>(ThirdPartyApiEndpoint.Orders(month));

        public Task<IResponse<List<OrderCollectionTypeDto>>> GetCollectionTypesAsync() =>
            http.GetAsync<List<OrderCollectionTypeDto>>(ThirdPartyApiEndpoint.CollectionTypes);

        public Task<IResponse<List<OrderPlanningRemainingModel>>> GetRemainingAsync(string orderNo) =>
            http.GetAsync<List<OrderPlanningRemainingModel>>(ThirdPartyApiEndpoint.Remaining(orderNo));

        public Task<IResponse<List<YarnPlanningStatusDto>>> GetYarnAsync(string orderNo, string? gauge = null, string? ply = null) =>
            http.GetAsync<List<YarnPlanningStatusDto>>(ThirdPartyApiEndpoint.Yarn(orderNo, gauge, ply));
    }
}
