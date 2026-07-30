using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NkplmErp.API.Services;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.API.Controllers;

/// <summary>
/// Bill of Materials — yarn requirement vs main-store stock, producing the
/// import decision for an order. Read-only.
/// Zero Trust: re-checks the caller's Bom page permission on the server.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class BomController(
    IBomService bomService,
    IRoleManagementService roleService,
    IPoTaskService poTaskService,
    IConfiguration configuration,
    ILogger<BomController> logger) : ControllerBase
{
    private const string PageKey = "yarn-orders";
    private const int BomNotifyAfterDays = 2;   // first reminder = creation + 2 days
    private readonly IBomService _bomService = bomService;
    private readonly IRoleManagementService _roleService = roleService;
    private readonly IPoTaskService _poTaskService = poTaskService;
    private readonly IConfiguration _configuration = configuration;
    private readonly ILogger<BomController> _logger = logger;

    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("User identity not found in token.");

    private async Task<bool> CanViewAsync()
    {
        var perms = await _roleService.GetUserPermissionsAsync(GetCurrentUserId());
        return perms.CanView(PageKey);
    }

    private async Task<bool> CanEditAsync()
    {
        var perms = await _roleService.GetUserPermissionsAsync(GetCurrentUserId());
        return perms.CanEdit(PageKey);
    }

    /// <summary>Yarn requirement / import decision for an order.</summary>
    [HttpGet("yarn-requirement")]
    public async Task<IActionResult> GetYarnRequirement([FromQuery] string orderNo, [FromQuery] int flag = 1)
    {
        if (!await CanViewAsync()) return Forbid();
        if (string.IsNullOrWhiteSpace(orderNo))
            return BadRequest("orderNo is required.");

        return Ok(await _bomService.GetYarnRequirementAsync(orderNo, flag));
    }

    /// <summary>Place a yarn order (header + per-order detail). Returns the generated yo_no.</summary>
    [HttpPost("yarn-order")]
    public async Task<IActionResult> PlaceYarnOrder([FromBody] PlaceYarnOrderRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (request?.Lines == null || request.Lines.Count == 0)
            return BadRequest("No lines to place.");

        var result = await _bomService.PlaceYarnOrderAsync(request, GetCurrentUserId());

        // Automation hook: placing the BOM / yarn order fulfils that order's BOM task —
        // mark it Completed (creating it first if it never existed).
        if (result.IsSuccess)
            await AdvanceBomTasksAsync(request, result.YoNo);

        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    // For each distinct order in the yarn order: ensure its BOM-stage task exists (assigned
    // to the Production Manager role — same owner as the review sweep's seeding), then
    // transition it to Completed. Idempotent per order. Best-effort: a hook failure never
    // breaks placing the yarn order.
    private async Task AdvanceBomTasksAsync(PlaceYarnOrderRequest request, string? yoNo)
    {
        try
        {
            var userId = GetCurrentUserId();
            var pmRole = _configuration["TaskAutomation:ProductionManagerRoleName"] ?? "Production Manager";
            var yarnRole = _configuration["TaskAutomation:YarnRoleName"] ?? "Yarn";

            var users = (await _roleService.GetAllUsersWithRolesAsync()).ToList();
            var members = users
                .Where(u => string.Equals(u.RoleName, pmRole, StringComparison.OrdinalIgnoreCase))
                .Select(u => u.UserId)
                .Distinct()
                .ToList();
            var yarnUsers = users
                .Where(u => string.Equals(u.RoleName, yarnRole, StringComparison.OrdinalIgnoreCase))
                .Select(u => u.UserId)
                .Distinct()
                .ToList();

            var orders = request.Lines
                .Select(l => l.OrderNo)
                .Where(o => !string.IsNullOrWhiteSpace(o))
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var orderNo in orders)
            {
                // Ensure returns the task id (idempotent per order — creates or reuses).
                var taskId = await _poTaskService.EnsureBomTaskAsync(orderNo, null, members, BomNotifyAfterDays, userId);
                if (taskId > 0)
                {
                    await _poTaskService.TransitionAsync(new TransitionPoTaskRequest
                    {
                        PoTaskId = taskId,
                        ToStatus = "C",
                        Note = $"Yarn order {yoNo} created — BOM done."
                    }, userId);
                }

                // Follow-up: the Yarn role now places the actual vendor order(s).
                await _poTaskService.CreateAsync(new CreatePoTaskRequest
                {
                    OrderNo = orderNo,
                    Title = $"Make yarn order - {orderNo}",
                    Detail = $"BOM {yoNo} placed for {orderNo}. Split by vendor on the Yarn Orders page and send the purchase order(s) to the supplier(s).",
                    PriorityId = 2,
                    CompletionRule = 2,        // any one completes
                    StartDate = DateTime.Today,
                    UserIds = yarnUsers
                }, userId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BOM task-complete hook failed.");
        }
    }

    /// <summary>All saved yarn orders (headers), newest first.</summary>
    /// <param name="status">Order-state filter from spDropdown 'YarnOrderStatus':
    /// 'O' ordered, 'N' not ordered, omitted for every header.</param>
    [HttpGet("yarn-orders")]
    public async Task<IActionResult> GetYarnOrders([FromQuery] string? status = null)
    {
        if (!await CanViewAsync()) return Forbid();
        return Ok(await _bomService.GetYarnOrdersAsync(status));
    }

    /// <summary>Detail lines of a saved yarn order.</summary>
    [HttpGet("yarn-orders/{yoId:int}")]
    public async Task<IActionResult> GetYarnOrderDetail(int yoId)
    {
        if (!await CanViewAsync()) return Forbid();
        return Ok(await _bomService.GetYarnOrderDetailAsync(yoId));
    }

    /// <summary>Production order numbers that already have a yarn order placed.</summary>
    [HttpGet("ordered-orders")]
    public async Task<IActionResult> GetYarnOrderedOrders()
    {
        if (!await CanViewAsync()) return Forbid();
        return Ok(await _bomService.GetYarnOrderedOrdersAsync());
    }

    /// <summary>Vendor sub-orders already placed under a parent yarn order.</summary>
    [HttpGet("yarn-orders/{yoId:int}/vendor-orders")]
    public async Task<IActionResult> GetYarnVendorOrders(int yoId)
    {
        if (!await CanViewAsync()) return Forbid();
        return Ok(await _bomService.GetYarnVendorOrdersAsync(yoId));
    }

    /// <summary>Place a vendor sub-order under a parent yarn order.</summary>
    [HttpPost("yarn-orders/{yoId:int}/vendor-orders")]
    public async Task<IActionResult> PlaceYarnVendorOrder(int yoId, [FromBody] SaveYarnVendorOrderRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (request?.Lines == null || request.Lines.Count == 0)
            return BadRequest("No lines to place.");

        request.YoId = yoId;
        var result = await _bomService.PlaceYarnVendorOrderAsync(request, GetCurrentUserId());

        // Lifecycle hook: placing a vendor order creates a follow-up task.
        if (result.IsSuccess)
        {
            var firstOrder = request.Lines.Select(l => l.OrderNo).FirstOrDefault(o => !string.IsNullOrWhiteSpace(o));
            await CreateLifecycleTaskAsync(firstOrder,
                $"Yarn order {result.VyoNo} placed — {request.Vendor}",
                $"Vendor order {result.VyoNo} placed for {request.Vendor}: {request.Lines.Count} line(s), {result.TotalKg:N2} kg. Follow up to confirm acceptance & departure date.",
                priorityId: 2);
        }
        return result.IsSuccess ? Ok(result) : BadRequest(result);
    }

    /// <summary>Set the vendor-confirmed departure date; creates a tracking task.</summary>
    [HttpPost("vendor-orders/{vyoId:int}/departure")]
    public async Task<IActionResult> SetDepartureDate(int vyoId, [FromBody] SetVendorOrderDateRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (!await _bomService.SetYarnVendorOrderDateAsync(vyoId, "departure", request.Date))
            return NotFound("Vendor order not found.");

        var export = await _bomService.GetYarnVendorOrderAsync(vyoId);
        var firstOrder = export.Lines.Select(l => l.OrderNo).FirstOrDefault(o => !string.IsNullOrWhiteSpace(o));
        await CreateLifecycleTaskAsync(firstOrder,
            $"Departure confirmed {export.Header?.VyoNo} — {export.Header?.Vendor}",
            $"Vendor confirmed departure on {request.Date:dd MMM yyyy} for {export.Header?.VyoNo}. Track shipment until arrival.",
            priorityId: 2);
        return Ok(new { ok = true });
    }

    /// <summary>Set the arrival / ETA date; creates a receiving task.</summary>
    [HttpPost("vendor-orders/{vyoId:int}/arrival")]
    public async Task<IActionResult> SetArrivalDate(int vyoId, [FromBody] SetVendorOrderDateRequest request)
    {
        if (!await CanEditAsync()) return Forbid();
        if (!await _bomService.SetYarnVendorOrderDateAsync(vyoId, "arrival", request.Date))
            return NotFound("Vendor order not found.");

        var export = await _bomService.GetYarnVendorOrderAsync(vyoId);
        var firstOrder = export.Lines.Select(l => l.OrderNo).FirstOrDefault(o => !string.IsNullOrWhiteSpace(o));
        await CreateLifecycleTaskAsync(firstOrder,
            $"Yarn arriving {export.Header?.VyoNo} — {export.Header?.Vendor}",
            $"Arrival/ETA {request.Date:dd MMM yyyy} for {export.Header?.VyoNo}. Receive & put away the yarn.",
            priorityId: 3);
        return Ok(new { ok = true });
    }

    /// <summary>
    /// Flag one or more dropped colors on a vendor sub-order (sp_ManageYarnOrder flag 'D').
    /// One transaction: sets is_dropped/drop_date/drop_by/drop_note on the parent detail
    /// lines, queues outbox mails in tblMailLog (Admin/Manager recipients) and writes
    /// in-app PoTaskNotification rows (Kind 'D') for the bell.
    /// </summary>
    [HttpPost("vendor-orders/{vyoId:int}/drop-color")]
    public async Task<IActionResult> DropColor(int vyoId, [FromBody] DropColorRequest request)
    {
        if (!await CanEditAsync()) return Forbid();

        var colors = (request?.Colors ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (colors.Count == 0)
            return BadRequest(new DropColorResult { Succeeded = false, Message = "Select at least one color to drop." });

        var result = await _bomService.DropYarnColorsAsync(vyoId, colors, request!.Note, GetCurrentUserId());
        return result.Succeeded ? Ok(result) : BadRequest(result);
    }

    // Resolve task assignees: the configured yarn role's members PLUS the acting user.
    private async Task<List<string>> ResolveTaskAssigneesAsync(string actingUserId)
    {
        var yarnRole = _configuration["TaskAutomation:YarnRoleName"] ?? "Yarn";
        var members = (await _roleService.GetAllUsersWithRolesAsync())
            .Where(u => string.Equals(u.RoleName, yarnRole, StringComparison.OrdinalIgnoreCase))
            .Select(u => u.UserId);
        return members.Append(actingUserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    // Best-effort manual PoTask creation for a vendor-order lifecycle event.
    private async Task CreateLifecycleTaskAsync(string? orderNo, string title, string detail, byte priorityId)
    {
        try
        {
            var userId = GetCurrentUserId();
            var assignees = await ResolveTaskAssigneesAsync(userId);
            await _poTaskService.CreateAsync(new CreatePoTaskRequest
            {
                OrderNo = orderNo,
                Stage = 12,                    // Yarn order — so the board can label + link it
                                               // to /yarn-orders (otherwise it defaults to Manual)
                Title = title,
                Detail = detail,
                PriorityId = priorityId,
                CompletionRule = 2,            // Any assignee completes
                UserIds = assignees
            }, userId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Yarn-order lifecycle task hook failed.");
        }
    }

    /// <summary>Download a vendor sub-order as an Excel (.xlsx) purchase order.</summary>
    [HttpGet("vendor-orders/{vyoId:int}/excel")]
    public async Task<IActionResult> DownloadVendorOrderExcel(int vyoId)
    {
        if (!await CanViewAsync()) return Forbid();

        var export = await _bomService.GetYarnVendorOrderAsync(vyoId);
        if (export.Header == null) return NotFound("Vendor order not found.");

        var bytes = YarnOrderExcelBuilder.Build(export);
        var fileName = $"{export.Header.VyoNo}.xlsx";
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
