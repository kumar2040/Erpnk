using NkplmErp.Shared.DTOs.Yarn_Orders;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Application.Interfaces.Yarn_Orders
{
    public interface IYarnOrderService
    {
        Task<IResponse<YarnOrderResponseModel>> UpdateYarnOrderAsync(YarnOrderRequestModel request);
    }
}
