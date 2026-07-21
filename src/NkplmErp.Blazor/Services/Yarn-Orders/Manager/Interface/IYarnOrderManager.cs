using NkplmErp.Blazor.Model.Yarn_Orders;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Services.Yarn_Orders.Manager.Interface
{
    public interface IYarnOrderManager
    {
        Task<IResponse<YarnOrdersResponseModel>> UpdateYarnOrderAsync(YarnOrdersRequestModel request);
    }
}
