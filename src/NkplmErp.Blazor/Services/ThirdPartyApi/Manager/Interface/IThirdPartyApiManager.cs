using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.DTOs.OrderPlanningApi;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Services.ThirdPartyApi.Manager.Interface
{
    public interface IThirdPartyApiManager
    {
        Task<IResponse<List<MonthlyOrderSummaryDto>>> GetMonthsAsync(DateTime inputDate);
        Task<IResponse<List<MonthlyOrderDetailDto>>> GetOrdersAsync(DateTime month);
        Task<IResponse<List<OrderCollectionTypeDto>>> GetCollectionTypesAsync();
        Task<IResponse<List<OrderPlanningRemainingModel>>> GetRemainingAsync(string orderNo);
        Task<IResponse<List<YarnPlanningStatusDto>>> GetYarnAsync(string orderNo, string? gauge = null, string? ply = null);
    }
}
