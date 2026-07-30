using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.API.Controllers;

/// <summary>
/// PO lifecycle task management (new /tasks board). Persisted tasks with fan-out
/// assignees. Zero Trust: every action re-checks the caller's PoTask permission.
/// Reads + "update my own side" need View; assigning / overriding / creating need
/// Edit. Separate from the derived knitting board (TaskManagementController).
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class PoTaskController(
    IPoTaskService poTaskService,
    IRoleManagementService roleService) : ControllerBase
{
    private const string PageKey = "PoTask";
    private readonly IPoTaskService _poTaskService = poTaskService;
    private readonly IRoleManagementService _roleService = roleService;

    private string? GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");

    private async Task<bool> CanViewAsync(string userId) =>
        (await _roleService.GetUserPermissionsAsync(userId)).CanView(PageKey);

    private async Task<bool> CanEditAsync(string userId) =>
        (await _roleService.GetUserPermissionsAsync(userId)).CanEdit(PageKey);

    // ------------------------------------------------------------------ reads ----

    // GET api/v1/PoTask/board?statusFlag=S&stage=&startDate=&endDate=&orderNo=&factoryType=
    [HttpGet("board")]
    public async Task<IActionResult> GetBoard(
        [FromQuery] string statusFlag = "S",
        [FromQuery] byte? stage = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? orderNo = null,
        [FromQuery] string? factoryType = null)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanViewAsync(userId)) return Forbid();

        return Ok(await _poTaskService.GetBoardAsync(statusFlag, stage, startDate, endDate, orderNo, factoryType, userId));
    }

    // GET api/v1/PoTask/my?statusFlag=S&...  — the caller's own assignments
    [HttpGet("my")]
    public async Task<IActionResult> GetMyTasks(
        [FromQuery] string statusFlag = "S",
        [FromQuery] byte? stage = null,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] string? orderNo = null,
        [FromQuery] string? factoryType = null)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanViewAsync(userId)) return Forbid();

        return Ok(await _poTaskService.GetMyTasksAsync(statusFlag, stage, startDate, endDate, orderNo, factoryType, userId));
    }

    // GET api/v1/PoTask/{id}
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanViewAsync(userId)) return Forbid();

        return Ok(await _poTaskService.GetDetailAsync(id));
    }

    // GET api/v1/PoTask/groups
    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanViewAsync(userId)) return Forbid();

        return Ok(await _poTaskService.GetGroupsAsync());
    }

    // GET api/v1/PoTask/{id}/assignees
    [HttpGet("{id:int}/assignees")]
    public async Task<IActionResult> GetAssignees(int id)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanViewAsync(userId)) return Forbid();

        return Ok(await _poTaskService.GetAssigneesAsync(id));
    }

    // ----------------------------------------------------------------- writes ----

    // POST api/v1/PoTask  — create from the Add Task form (Edit)
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreatePoTaskRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanEditAsync(userId)) return Forbid();
        if (string.IsNullOrWhiteSpace(request?.Title)) return BadRequest("Title is required.");

        var id = await _poTaskService.CreateAsync(request, userId);
        return Ok(new CreatePoTaskResult { PoTaskId = id });
    }

    // POST api/v1/PoTask/assign  (Edit)
    [HttpPost("assign")]
    public async Task<IActionResult> Assign([FromBody] AssignPoTaskRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanEditAsync(userId)) return Forbid();

        await _poTaskService.AssignAsync(request, userId);
        return Ok();
    }

    // POST api/v1/PoTask/{id}/unassign?targetUserId=  (Edit) — remove one person's assignment.
    // Zero Trust: beyond the Edit permission, only the task's creator or an Admin may
    // remove an assignee, and the creator may never remove themselves.
    [HttpPost("{id:int}/unassign")]
    public async Task<IActionResult> Unassign(int id, [FromQuery] string targetUserId)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanEditAsync(userId)) return Forbid();
        if (string.IsNullOrWhiteSpace(targetUserId)) return BadRequest("targetUserId is required.");

        if (!User.IsInRole("Admin"))
        {
            var createdBy = (await _poTaskService.GetDetailAsync(id)).Task?.CreatedBy;
            if (!string.Equals(createdBy, userId, StringComparison.OrdinalIgnoreCase)) return Forbid();
            if (string.Equals(targetUserId, userId, StringComparison.OrdinalIgnoreCase))
                return BadRequest("The task creator cannot remove themselves from the task.");
        }

        await _poTaskService.UnassignAsync(id, targetUserId);
        return Ok();
    }

    // GET api/v1/PoTask/attachments/{id}  (View) — one attachment with its bytes, for download
    [HttpGet("attachments/{id:int}")]
    public async Task<IActionResult> GetAttachment(int id)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanViewAsync(userId)) return Forbid();

        var att = await _poTaskService.GetAttachmentAsync(id);
        return att is null ? NotFound() : Ok(att);
    }

    // GET api/v1/PoTask/staff  (Edit) — active users for the assign pickers.
    // Gated by the PoTask permission, NOT RoleManagement, so a task editor (e.g. a
    // Production Manager) can assign people without role-admin rights.
    [HttpGet("staff")]
    public async Task<IActionResult> GetStaff()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanEditAsync(userId)) return Forbid();

        return Ok(await _poTaskService.GetStaffAsync());
    }

    // GET api/v1/PoTask/attachments/counts  (View) — file count per task, for the board badge
    [HttpGet("attachments/counts")]
    public async Task<IActionResult> GetAttachmentCounts()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanViewAsync(userId)) return Forbid();

        return Ok(await _poTaskService.GetAttachmentCountsAsync());
    }

    // POST api/v1/PoTask/my-update  — "update my side" (View; SP enforces own row)
    [HttpPost("my-update")]
    public async Task<IActionResult> MyUpdate([FromBody] MyUpdatePoTaskRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanViewAsync(userId)) return Forbid();

        await _poTaskService.MyUpdateAsync(request, userId);
        return Ok();
    }

    // POST api/v1/PoTask/transition  — parent override (Edit)
    [HttpPost("transition")]
    public async Task<IActionResult> Transition([FromBody] TransitionPoTaskRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanEditAsync(userId)) return Forbid();

        await _poTaskService.TransitionAsync(request, userId);
        return Ok();
    }

    // POST api/v1/PoTask/hold  (Edit)
    [HttpPost("hold")]
    public async Task<IActionResult> Hold([FromBody] HoldPoTaskRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanEditAsync(userId)) return Forbid();

        await _poTaskService.HoldAsync(request, userId);
        return Ok();
    }

    // POST api/v1/PoTask/{id}/resolve  (Edit)
    [HttpPost("{id:int}/resolve")]
    public async Task<IActionResult> Resolve(int id)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanEditAsync(userId)) return Forbid();

        await _poTaskService.ResolveAsync(id, userId);
        return Ok();
    }

    // POST api/v1/PoTask/{id}/cancel  (Edit)
    [HttpPost("{id:int}/cancel")]
    public async Task<IActionResult> Cancel(int id, [FromQuery] string? note = null)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanEditAsync(userId)) return Forbid();

        await _poTaskService.CancelAsync(id, note, userId);
        return Ok();
    }

    // POST api/v1/PoTask/exception  — Yarn issue / Product return (Edit)
    [HttpPost("exception")]
    public async Task<IActionResult> RaiseException([FromBody] RaiseExceptionRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanEditAsync(userId)) return Forbid();

        var id = await _poTaskService.RaiseExceptionAsync(request, userId);
        return Ok(new CreatePoTaskResult { PoTaskId = id });
    }

    // POST api/v1/PoTask/checklist/toggle?checklistId=  (View — light edit on own work)
    [HttpPost("checklist/toggle")]
    public async Task<IActionResult> ToggleChecklist([FromQuery] int checklistId)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanViewAsync(userId)) return Forbid();

        await _poTaskService.ToggleChecklistAsync(checklistId, userId);
        return Ok();
    }

    // POST api/v1/PoTask/snapshot  — capture plan params when Planning completes (Edit)
    [HttpPost("snapshot")]
    public async Task<IActionResult> Snapshot([FromBody] PoPlanParamRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanEditAsync(userId)) return Forbid();

        await _poTaskService.SnapshotAsync(request, userId);
        return Ok();
    }

    // ---- notifications (bell) — any signed-in user sees their own ----

    // GET api/v1/PoTask/notifications?top=30
    [HttpGet("notifications")]
    public async Task<IActionResult> GetNotifications([FromQuery] int top = 30)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        return Ok(await _poTaskService.GetNotificationsAsync(userId, top));
    }

    // GET api/v1/PoTask/notifications/unread-count
    [HttpGet("notifications/unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        return Ok(new { unreadCount = await _poTaskService.GetUnreadCountAsync(userId) });
    }

    // POST api/v1/PoTask/notifications/{id}/read
    [HttpPost("notifications/{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        await _poTaskService.MarkNotificationReadAsync(id, userId);
        return Ok();
    }

    // POST api/v1/PoTask/notifications/read-all
    [HttpPost("notifications/read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        await _poTaskService.MarkAllNotificationsReadAsync(userId);
        return Ok();
    }

    // POST api/v1/PoTask/alert-check  — flag a PO whose params changed after planning (Edit)
    [HttpPost("alert-check")]
    public async Task<IActionResult> AlertCheck([FromBody] PoPlanParamRequest request)
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        if (!await CanEditAsync(userId)) return Forbid();

        var changed = await _poTaskService.AlertCheckAsync(request, userId);
        return Ok(new AlertCheckResult { Changed = changed });
    }
}
