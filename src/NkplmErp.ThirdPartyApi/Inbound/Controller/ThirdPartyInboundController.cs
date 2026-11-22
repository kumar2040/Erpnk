using System.Text.Json;
using Asp.Versioning;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.DTOs.MakeOrder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.ThirdPartyApi.Inbound.Base;
using NkplmErp.ThirdPartyApi.Inbound.Filter;
using NkplmErp.ThirdPartyApi.Common.Model;

namespace NkplmErp.ThirdPartyApi.Inbound.Controller
{
    /// <summary>
    /// Inbound API: PLM (PHP) calls NkplmErp here — the reverse of ThirdPartyManager. Not JWT-protected;
    /// every route is guarded by ThirdPartyInboundAuthFilter (rate limit, allowed base URL, credentials).
    /// Body: { username, password, parameters: { ... } }. Reply: { success, route, data } or 400 Bad Request.
    /// </summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [AllowAnonymous]
    [TypeFilter(typeof(ThirdPartyInboundAuthFilter))]
    public class ThirdPartyInboundController(IOrderReviewTaskService orderReviewTasks, IMakeOrderTaskService makeOrderTasks) : ControllerBase
    {
        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

        // POST api/v1/ThirdPartyInbound/order-review
        //   { username, password, parameters: { reviewId, orderNo, remark, reviewDate, reviewUser, pc } }
        // PLM calls this right after saving an order review: creates the Planning + BOM tasks on /tasks
        // in one transaction. Any failure -> generic 400, nothing written, so PLM rolls its review back.
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

        // POST api/v1/ThirdPartyInbound/make-order
        //   { username, password, parameters: { orderNo, buyerName, collection, orderType, shippingDate, totalPieces, styleCount } }
        // PLM calls this when a new order is submitted on "Make order": one task + a notification for the
        // Production Manager(s), in one transaction. Any failure -> generic 400, nothing written, so PLM rolls back.
        [HttpPost("make-order")]
        public Task<IActionResult> MakeOrderAsync([FromBody] ThirdPartyRequestModel request) =>
            ThirdPartyInboundResponse.RespondAsync(HttpContext, "make-order", request.Parameters, async p =>
            {
                var order = p.Deserialize<MakeOrderTaskRequest>(Json)
                            ?? throw new InvalidOperationException("make-order: parameters missing.");
                var result = await makeOrderTasks.CreateMakeOrderTaskAsync(order);
                if (!result.Succeeded)
                    throw new InvalidOperationException($"make-order {order.OrderNo}: {result.Messages}");
                return result.Data;
            });

        // POST api/v1/ThirdPartyInbound/ping   { username, password, parameters: {} }
        // Connectivity check for the PHP side: passes only when every gateway check passes.
        [HttpPost("ping")]
        public Task<IActionResult> PingAsync([FromBody] ThirdPartyRequestModel request) =>
            ThirdPartyInboundResponse.RespondAsync(HttpContext, "ping", request.Parameters,
                _ => Task.FromResult<object?>(new { message = "pong", serverTime = DateTime.Now }));
    }
}
