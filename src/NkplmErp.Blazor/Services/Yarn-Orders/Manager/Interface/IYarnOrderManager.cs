using NkplmErp.Shared.DTOs.Yarn_Orders;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Services.Yarn_Orders.Manager.Interface
{
    public interface IYarnOrderManager
    {
        Task<IResponse<YarnOrderResponseModel>> UpdateYarnOrderAsync(YarnOrderRequestModel request);
    }
}
