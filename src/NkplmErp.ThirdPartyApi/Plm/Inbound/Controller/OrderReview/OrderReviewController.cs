using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Shared.DTOs;
using NkplmErp.ThirdPartyApi.Common.Model;
using NkplmErp.ThirdPartyApi.Plm.Inbound.Base;
using NkplmErp.ThirdPartyApi.Plm.Inbound.Filter;
using NkplmErp.ThirdPartyApi.Plm.Inbound.Manager.Interface.OrderReview;

namespace NkplmErp.ThirdPartyApi.Plm.Inbound.Controller.OrderReview;

/// <summary>Inbound (PLM -> ERP): order review saved. Guarded by ThirdPartyInboundAuthFilter; body { username, password, parameters }.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ThirdPartyInbound")]
[AllowAnonymous]
[TypeFilter(typeof(ThirdPartyInboundAuthFilter))]
public class OrderReviewController(IOrderReviewTaskService orderReviewTasks) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    // POST api/v1/ThirdPartyInbound/order-review
    //   { username, password, parameters: { reviewId, orderNo, remark, reviewDate, reviewUser, pc } }
    // Creates the Planning + BOM tasks on /tasks in one transaction. Any failure -> generic 400, nothing written.
    [HttpPost("order-review")]
    public Task<IActionResult> OrderReviewAsync([FromBody] ThirdPartyRequestModel request) =>
        ThirdPartyInboundResponse.RespondAsync(HttpContext, "order-review", request.Parameters, async p =>
        {
            var review = p.Deserialize<OrderReviewTaskRequest>(Json)
                         ?? throw new InvalidOperationException("order-review: parameters missing.");
            var result = await orderReviewTasks.CreateOrderReviewTasksAsync(review);
            if (!result.Succeeded)
                throw new InvalidOperationException($"order-review {review.OrderNo}: {result.Messages}");
            return result.Data;
        });
}
