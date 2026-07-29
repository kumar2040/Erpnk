using NkplmErp.Shared.DTOs.Yarn_Orders;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Application.Interfaces.Yarn_Orders
{
    public interface IYarnOrderService
    {
        Task<IResponse<YarnOrderResponseModel>> UpdateYarnOrderAsync(YarnOrderRequestModel request);

        /// <summary>
        /// Record (or clear) a vendor sub-order's invoice number — the "yarn arrived from the
        /// vendor and is ready for use" event. When it is the last outstanding invoice on the
        /// parent order, the procedure also raises the Planning task.
        /// </summary>
        Task<IResponse<YarnOrderResponseModel>> SaveInvoiceAsync(YarnOrderRequestModel request, string userId);
    }
}
