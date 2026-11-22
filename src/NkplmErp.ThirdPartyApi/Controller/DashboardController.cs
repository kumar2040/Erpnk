using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.DTOs.OrderPlanningApi;
using NkplmErp.ThirdPartyApi.Manager.Interface;
using NkplmErp.ThirdPartyApi.Route;

namespace NkplmErp.ThirdPartyApi.Controller
{
    /// <summary>Main dashboard: running orders and production flow, backed by the PLM API.</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class DashboardController(IThirdPartyManager thirdParty, IRoleManagementService roleService) : PermissionController(roleService)
    {
        // GET api/v1/Dashboard/order-status?year=2026&status=Running
        // Running-orders list (and the dashboard order table), live from PLM.
        [HttpGet("order-status")]
        public async Task<IActionResult> GetOrderStatusAsync([FromQuery] int year, [FromQuery] string status, CancellationToken ct)
        {
            if (!await CanViewAsync("Dashboard")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<OrderStatusDetailDto>>(
                ThirdPartyRouter.OrderStatus, new { year, status }, ct));
        }

        // GET api/v1/Dashboard/production-flow?buyerId=87&orderNo=GT-26025
        // Production Flow chart: pieces per department, dispatch totals and department due dates.
        [HttpGet("production-flow")]
        public async Task<IActionResult> GetProductionFlowAsync([FromQuery] int buyerId, [FromQuery] string? orderNo, CancellationToken ct)
        {
            if (!await CanViewAsync("Dashboard")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<ProductionFlowDto>>(
                ThirdPartyRouter.ProductionFlow, new { buyerId, orderNo = orderNo ?? string.Empty }, ct));
        }
    }
}
