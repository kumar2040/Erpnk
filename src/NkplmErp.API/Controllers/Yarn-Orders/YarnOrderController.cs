using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Shared.DTOs.Yarn_Orders;
using NkplmErp.Application.Interfaces.Yarn_Orders;
using NkplmErp.Application.Interfaces;

namespace NkplmErp.API.Controllers.Yarn_Orders
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class YarnOrderController(
        IYarnOrderService yarnOrderService,
        IRoleManagementService roleService) : ControllerBase
    {
        private readonly IYarnOrderService _yarnOrderService = yarnOrderService;
        private readonly IRoleManagementService _roleService = roleService;

        // Yarn orders live under the Bom module's permissions.
        private const string PageKey = "Bom";

        // Nullable + explicit 401. BomController's helper throws instead, and
        // GlobalExceptionHandler maps every exception to 500 — so that shape answers
        // a claimless caller with 500 rather than 401.
        private string? GetCurrentUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

        private async Task<bool> CanEditAsync(string userId)
        {
            var perms = await _roleService.GetUserPermissionsAsync(userId);
            return perms.CanEdit(PageKey);
        }

        // POST api/v1/YarnOrder/update
        // Body: { "yarnId": "12", "departureDate": "2026-07-20", "arrivalDate": null }
        // A null/blank date leaves that column untouched.
        [HttpPost("update")]
        public async Task<IActionResult> Update([FromBody] YarnOrderRequestModel request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (!await CanEditAsync(userId)) return Forbid();

            var result = await _yarnOrderService.UpdateYarnOrderAsync(request);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }
}
