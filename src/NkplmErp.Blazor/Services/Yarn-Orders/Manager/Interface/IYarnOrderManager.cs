using NkplmErp.Shared.DTOs.Yarn_Orders;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Services.Yarn_Orders.Manager.Interface
{
    public interface IYarnOrderManager
    {
        Task<IResponse<YarnOrderResponseModel>> UpdateYarnOrderAsync(YarnOrderRequestModel request);

        /// <summary>Record (or clear) a vendor sub-order's invoice number.</summary>
        Task<IResponse<YarnOrderResponseModel>> SaveInvoiceAsync(YarnOrderRequestModel request);
    }
}
