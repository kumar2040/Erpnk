using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.ThirdPartyApi.Plm.Outbound.Base;
using NkplmErp.ThirdPartyApi.Plm.Outbound.Manager.Interface;
using NkplmErp.ThirdPartyApi.Plm.Outbound.Route;

namespace NkplmErp.ThirdPartyApi.Plm.Outbound.Controller.Shipment
{
    /// <summary>Task board shipment alert card: orders due to ship in a date range, backed by the PLM API.</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class ThirdPartyShipmentController(IThirdPartyManager thirdParty, IRoleManagementService roleService) : ThirdPartyPermissionBase(roleService)
    {
        // The card sits on the task board, which is guarded by the PoTask page key.
        private const string PageKey = "PoTask";

        // GET api/v1/ThirdPartyShipment/due-orders?from=2026-11-23&to=2026-11-30&includeOverdue=true
        [HttpGet("due-orders")]
        public async Task<IActionResult> GetDueOrdersAsync([FromQuery] string from, [FromQuery] string to,
            [FromQuery] bool includeOverdue, CancellationToken ct)
        {
            if (!await CanViewAsync(PageKey)) return Forbid();
            // Dates stay strings end to end; PLM parses them.
            return Ok(await thirdParty.CallAsync<List<ShipmentDueOrderDto>>(
                ThirdPartyRouter.ShipmentDueOrders, new { from, to, includeOverdue }, ct));
        }
    }
}
