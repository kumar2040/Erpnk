using NkplmErp.API.Model.Yarn_Orders;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.API.Services.Interface.Yarn_Orders
{
    public interface IYarnOrderService
    {
        Task<IResponse<YarnOrderResponseModel>> UpdateYarnOrderAsync(YarnOrderRequestModel request);
    }
}
