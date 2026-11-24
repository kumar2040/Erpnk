using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.ThirdPartyApi.Plm.Outbound.Base;
using NkplmErp.Shared.DTOs.OrderPlanningApi;
using NkplmErp.ThirdPartyApi.Plm.Outbound.Manager.Interface;
using NkplmErp.ThirdPartyApi.Plm.Outbound.Route;

namespace NkplmErp.ThirdPartyApi.Plm.Outbound.Controller.Dashboard
{
    /// <summary>Main dashboard: running orders and production flow, backed by the PLM API.</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class ThirdPartyDashboardController(IThirdPartyManager thirdParty, IRoleManagementService roleService) : ThirdPartyPermissionBase(roleService)
    {
        // GET api/v1/ThirdPartyDashboard/order-status?year=2026&status=Running
        // Running-orders list (and the dashboard order table), live from PLM.
        [HttpGet("order-status")]
        public async Task<IActionResult> GetOrderStatusAsync([FromQuery] int year, [FromQuery] string status, CancellationToken ct)
        {
            if (!await CanViewAsync("Dashboard")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<OrderStatusDetailDto>>(
                ThirdPartyRouter.OrderStatus, new { year, status }, ct));
        }

        // GET api/v1/ThirdPartyDashboard/department-stock?dept=MND&orderNo=HEY-26008   (orderNo omitted = department-wide)
        // Stock behind a Production Flow department circle, sizes pivoted.
        [HttpGet("department-stock")]
        public async Task<IActionResult> GetDepartmentStockAsync([FromQuery] string dept, [FromQuery] string? orderNo, CancellationToken ct)
        {
            if (!await CanViewAsync("Dashboard")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<DepartmentStockDto>>(
                ThirdPartyRouter.DepartmentStock, new { dept, orderNo = orderNo ?? string.Empty }, ct));
        }

        // GET api/v1/ThirdPartyDashboard/production-flow?buyerId=87&orderNo=GT-26025
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
