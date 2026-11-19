using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.DTOs.OrderPlanningApi;
using NkplmErp.ThirdPartyApi.Manager.Interface;
using NkplmErp.ThirdPartyApi.Route;

namespace NkplmErp.API.Controllers.ThirdPartyApi
{
    /// <summary>The one controller through which the UI reaches the third-party (PLM) API.</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class ThirdPartyApiController(IThirdPartyManager thirdParty, IRoleManagementService roleService) : ControllerBase
    {
        private async Task<bool> CanViewOrderPlanningAsync()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId)) return false;
            var perms = await roleService.GetUserPermissionsAsync(userId);
            return perms.CanView("OrderPlanning");
        }

        // GET api/v1/ThirdPartyApi/months?inputDate=2026-11-19
        [HttpGet("months")]
        public async Task<IActionResult> GetMonthsAsync([FromQuery] DateTime inputDate, CancellationToken ct)
        {
            if (!await CanViewOrderPlanningAsync()) return Forbid();
            return Ok(await thirdParty.CallAsync<List<MonthlyOrderSummaryDto>>(
                ThirdPartyRouter.Months, new { inputDate = inputDate.ToString("yyyy-MM-dd") }, ct));
        }

        // GET api/v1/ThirdPartyApi/orders?month=2026-11-01
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrdersAsync([FromQuery] DateTime month, CancellationToken ct)
        {
            if (!await CanViewOrderPlanningAsync()) return Forbid();
            return Ok(await thirdParty.CallAsync<List<MonthlyOrderDetailDto>>(
                ThirdPartyRouter.Orders, new { month = month.ToString("yyyy-MM-dd") }, ct));
        }

        // GET api/v1/ThirdPartyApi/collection-types
        [HttpGet("collection-types")]
        public async Task<IActionResult> GetCollectionTypesAsync(CancellationToken ct)
        {
            if (!await CanViewOrderPlanningAsync()) return Forbid();
            return Ok(await thirdParty.CallAsync<List<OrderCollectionTypeDto>>(ThirdPartyRouter.CollectionTypes, null, ct));
        }

        // GET api/v1/ThirdPartyApi/remaining?orderNo=SND-113
        [HttpGet("remaining")]
        public async Task<IActionResult> GetRemainingAsync([FromQuery] string orderNo, CancellationToken ct)
        {
            if (!await CanViewOrderPlanningAsync()) return Forbid();
            return Ok(await thirdParty.CallAsync<List<OrderPlanningRemainingModel>>(
                ThirdPartyRouter.Remaining, new { orderNo }, ct));
        }

        // GET api/v1/ThirdPartyApi/yarn?orderNo=SND-113[&gauge=7&ply=1]   (gauge+ply => colour-wise rows)
        [HttpGet("yarn")]
        public async Task<IActionResult> GetYarnAsync([FromQuery] string orderNo, [FromQuery] string? gauge, [FromQuery] string? ply, CancellationToken ct)
        {
            if (!await CanViewOrderPlanningAsync()) return Forbid();
            object parameters = gauge is null
                ? new { orderNo, byColor = false }
                : new { orderNo, gauge, ply = ply ?? string.Empty, byColor = true };
            return Ok(await thirdParty.CallAsync<List<YarnPlanningStatusDto>>(ThirdPartyRouter.Yarn, parameters, ct));
        }
    }
}
