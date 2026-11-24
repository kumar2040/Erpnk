using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.ThirdPartyApi.Plm.Outbound.Base;
using NkplmErp.ThirdPartyApi.Plm.Outbound.Manager.Interface;
using NkplmErp.ThirdPartyApi.Plm.Outbound.Route;

namespace NkplmErp.ThirdPartyApi.Plm.Outbound.Controller
{
    /// <summary>BOM page: the order list, backed by the PLM API. (The yarn requirement itself is served by BomController.)</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class ThirdPartyBomController(IThirdPartyManager thirdParty, IRoleManagementService roleService) : ThirdPartyPermissionBase(roleService)
    {
        // The BOM page is guarded by the yarn-orders page key everywhere else (BomController, the page itself).
        private const string PageKey = "yarn-orders";

        // GET api/v1/ThirdPartyBom/orders?month=2026-11-01
        // Orders shipping in the month that still have pieces to knit, planned or not.
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrdersAsync([FromQuery] DateTime month, CancellationToken ct)
        {
            if (!await CanViewAsync(PageKey)) return Forbid();
            return Ok(await thirdParty.CallAsync<List<MonthlyOrderDetailDto>>(
                ThirdPartyRouter.BomOrders, new { month = month.ToString("yyyy-MM-dd") }, ct));
        }
    }
}
