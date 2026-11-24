using System.Text.Json;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Shared.DTOs.MakeOrder;
using NkplmErp.ThirdPartyApi.Common.Model;
using NkplmErp.ThirdPartyApi.Plm.Inbound.Base;
using NkplmErp.ThirdPartyApi.Plm.Inbound.Filter;
using NkplmErp.ThirdPartyApi.Plm.Inbound.Manager.Interface.MakeOrder;

namespace NkplmErp.ThirdPartyApi.Plm.Inbound.Controller.MakeOrder;

/// <summary>Inbound (PLM -> ERP): new order placed on "Make order". Guarded by ThirdPartyInboundAuthFilter.</summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ThirdPartyInbound")]
[AllowAnonymous]
[TypeFilter(typeof(ThirdPartyInboundAuthFilter))]
public class MakeOrderController(IMakeOrderTaskService makeOrderTasks) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    // POST api/v1/ThirdPartyInbound/make-order
    //   { username, password, parameters: { orderNo, buyerName, collection, orderType, shippingDate, totalPieces, styleCount } }
    // One task + a notification for the Production Manager(s), in one transaction. Any failure -> generic 400, nothing written.
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
}
