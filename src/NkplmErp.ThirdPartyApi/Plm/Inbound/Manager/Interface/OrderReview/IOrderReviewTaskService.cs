using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.ThirdPartyApi.Plm.Inbound.Manager.Interface.OrderReview;

/// <summary>
/// Creates the Planning (Stage 1) and BOM (Stage 2) tasks on the /tasks board (dbo.PoTask) for an
/// order review saved in PLM. All-or-nothing: sp_PoTask_CreateFromOrderReview runs it in one transaction.
/// </summary>
public interface IOrderReviewTaskService
{
    Task<IResponse<OrderReviewTaskResultDto>> CreateOrderReviewTasksAsync(OrderReviewTaskRequest request);
}
