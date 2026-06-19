using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.API.Controllers.TaskManagement.Model;
using NkplmErp.API.Controllers.TaskManagement.Service.Interface;
using NkplmErp.Application.Interfaces;

namespace NkplmErp.API.Controllers.TaskManagement
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class TaskManagementController(
        ITaskManagementService taskManagementService,
        IRoleManagementService roleService) : ControllerBase
    {
        private readonly ITaskManagementService _taskManagementService = taskManagementService;
        private readonly IRoleManagementService _roleService = roleService;

        // Zero Trust: caller must have View on the TaskManagement module.
        private async Task<bool> CanViewTasksAsync(string userId)
        {
            var perms = await _roleService.GetUserPermissionsAsync(userId);
            return perms.CanView("TaskManagement");
        }

        // GET api/v1/TaskManagement?flag=S|P|C|O&startDate=2026-06-16&endDate=2026-06-16&orderNo=Nksh26&factoryType=knit
        // flag: S Scheduled, P In Progress, C Completed, O Overdue (Overdue overlaps the date range like S/P/C, +1-day grace at the start).
        // factoryType: admin's chosen factory. ZERO TRUST — the SP reads the caller's
        //   identity.Users.AssignedGauge (via @UserId) and a restricted user is locked to it.
        [HttpGet]
        public async Task<IActionResult> GetTasks(
            [FromQuery] string flag = "S",
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] string? orderNo = null,
            [FromQuery] string? factoryType = null,
            [FromQuery] string? subCategories = null)
        {
            // Fail CLOSED: no resolvable identity => deny (never default to unrestricted).
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (!await CanViewTasksAsync(userId)) return Forbid();

            var data = await _taskManagementService.GetTasksAsync(flag, startDate, endDate, orderNo, factoryType, subCategories, userId);
            return Ok(data);
        }

        // GET api/v1/TaskManagement/subcategories?factoryType=knit
        // Distinct gauge sub-methods for the active factory (numeric gauges collapse to
        // 'general'). Used to render the cascading sub-filter checkboxes. ZERO TRUST: the
        // SP scopes this to a restricted user's own gauge regardless of factoryType.
        [HttpGet("subcategories")]
        public async Task<IActionResult> GetSubCategories(
            [FromQuery] string? factoryType = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (!await CanViewTasksAsync(userId)) return Forbid();

            var subs = await _taskManagementService.GetSubCategoriesAsync(factoryType, startDate, endDate, userId);
            return Ok(subs);
        }

        // GET api/v1/TaskManagement/scope
        // Tells the board what factory_type filter the current user is allowed to use:
        //   - super admin (AssignedGauge null): IsRestricted=false, FactoryTypes = every factory_type (editable dropdown).
        //   - restricted user:                  IsRestricted=true,  FactoryTypes = [their gauge] (fixed dropdown).
        [HttpGet("scope")]
        public async Task<IActionResult> GetScope()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (!await CanViewTasksAsync(userId)) return Forbid();

            var assignedGauge = await _taskManagementService.GetUserAssignedGaugeAsync(userId);
            var isRestricted = !string.IsNullOrWhiteSpace(assignedGauge);

            var factoryTypes = isRestricted
                ? new List<string> { assignedGauge! }
                : (await _taskManagementService.GetFactoryTypesAsync()).ToList();

            return Ok(new TaskScopeResponseModel
            {
                IsRestricted  = isRestricted,
                AssignedGauge = isRestricted ? assignedGauge : null,
                FactoryTypes  = factoryTypes
            });
        }

        // Current user id from the JWT (mirrors RoleManagementController). The "sub"
        // fallback only matters if inbound claim mapping is ever disabled.
        private string? GetCurrentUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
    }
}
