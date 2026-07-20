using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.API.Model.Task_Gate;
using NkplmErp.API.Services.Interface.Task_Gate;
using NkplmErp.Application.Interfaces;

namespace NkplmErp.API.Controllers.Task_Gate
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class TaskGateController(
        ITaskGateService taskGateService,
        IRoleManagementService roleService) : ControllerBase
    {
        private readonly ITaskGateService _taskGateService = taskGateService;
        private readonly IRoleManagementService _roleService = roleService;

        // The gate reads and writes PoTask data, so it lives under that module's
        // permissions — same key PoTaskController uses.
        private const string PageKey = "PoTask";

        // Nullable + explicit 401. A helper that throws would be turned into a 500
        // by GlobalExceptionHandler, so a claimless caller would get the wrong status.
        private string? GetCurrentUserId() =>
            User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

        // View is the right level for both actions: starting a task moves only the
        // caller's OWN assignee row, which is the same bar PoTaskController applies
        // to my-update. Edit would wrongly exclude ordinary assignees.
        private async Task<bool> CanViewAsync(string userId)
        {
            var perms = await _roleService.GetUserPermissionsAsync(userId);
            return perms.CanView(PageKey);
        }

        // GET api/v1/TaskGate/queue
        // The caller's not-yet-started assignments, oldest first. Empty list is a
        // normal 200 — it means the gate has nothing to show.
        [HttpGet("queue")]
        public async Task<IActionResult> GetQueue()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (!await CanViewAsync(userId)) return Forbid();

            var result = await _taskGateService.GetQueueAsync(userId);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }

        // POST api/v1/TaskGate/start
        // Body: { "taskId": "1919" }
        // Accepts one task. The acting user is taken from the token, so a caller
        // can never start someone else's row.
        [HttpPost("start")]
        public async Task<IActionResult> Start([FromBody] TaskGateRequestModel request)
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId)) return Unauthorized();
            if (!await CanViewAsync(userId)) return Forbid();

            var result = await _taskGateService.StartTaskAsync(request, userId);
            return result.Succeeded ? Ok(result) : BadRequest(result);
        }
    }
}
