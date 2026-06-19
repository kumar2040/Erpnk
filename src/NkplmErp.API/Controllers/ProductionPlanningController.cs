using System.Security.Claims;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[Authorize]
public class ProductionPlanningController : ControllerBase
{
    private readonly IProductionPlanningService _productionPlanningService;
    private readonly IRoleManagementService _roleService;

    public ProductionPlanningController(IProductionPlanningService productionPlanningService, IRoleManagementService roleService)
    {
        _productionPlanningService = productionPlanningService;
        _roleService = roleService;
    }

    private string GetCurrentUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("User identity not found in token.");

    // The caller's effective permissions, including their full scope set
    // (union across every role they hold). Empty set => unrestricted (admin).
    // The scope set holds DEPARTMENT / KnitType values (knit/weave/silk/linen/other).
    private Task<UserPermissionsResponse> GetCurrentPermissionsAsync() =>
        _roleService.GetUserPermissionsAsync(GetCurrentUserId());

    // Department gate for a read/write that belongs to one or more KnitTypes.
    // Unrestricted users pass. Restricted users pass if they have ANY access
    // (whole-department or a specific value) within one of the endpoint's departments.
    private async Task<bool> CanAccessDeptAsync(params string[] departments)
    {
        var perms = await GetCurrentPermissionsAsync();
        return perms.CanAccessDept(departments);
    }

    // A fabric "master"/tailor row carries an id/name but not its department, and a
    // tailor's value (e.g. 't1') can span silk/linen/other. Allow it if any of those
    // departments paired with the master's id/name is in the caller's scope.
    private static readonly string[] FabricDepts = { "silk", "linen", "other" };
    private static bool MasterAllowed(UserPermissionsResponse perms, string? masterId, string? masterName) =>
        FabricDepts.Any(d => perms.IsRowAllowed(d, masterId) || perms.IsRowAllowed(d, masterName));

    // Server identity for audit fields (never trust client-supplied user ids).
    private string GetCurrentUserName() =>
        User.Identity?.Name
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? GetCurrentUserId();

    // Zero Trust: page permission check at the API boundary (UI checks are cosmetic).
    private async Task<bool> HasPermissionAsync(string pageKey, string action)
    {
        var perms = await _roleService.GetUserPermissionsAsync(GetCurrentUserId());
        return action switch
        {
            "Edit" => perms.CanEdit(pageKey),
            "Delete" => perms.CanDelete(pageKey),
            _ => perms.CanView(pageKey)
        };
    }

    // Two-level ownership check for plan mutations: scoped users can only touch a plan
    // whose (department, gauge value) matches one of their scope entries.
    private async Task<bool> OwnsPlanAsync(int planDetailId)
    {
        var perms = await GetCurrentPermissionsAsync();
        if (perms.IsUnrestricted) return true; // unrestricted user

        var planKnitType = await _productionPlanningService.GetPlanKnitTypeAsync(planDetailId);
        var planGauge    = await _productionPlanningService.GetPlanGaugeAsync(planDetailId);
        return perms.IsRowAllowed(planKnitType, planGauge);
    }

    [HttpGet("monthly-summary")]
    public async Task<IActionResult> GetMonthlySummary([FromQuery] DateTime inputDate)
    {
        var result = await _productionPlanningService.GetMonthlySummaryAsync(inputDate);
        return Ok(result);
    }

    [HttpGet("monthly-details")]
    public async Task<IActionResult> GetMonthlyOrderDetails([FromQuery] DateTime inputDate)
    {
        var result = await _productionPlanningService.GetMonthlyOrderDetailsAsync(inputDate);
        return Ok(result);
    }

    [HttpGet("order-collection-types")]
    public async Task<IActionResult> GetOrderCollectionTypes()
    {
        var result = await _productionPlanningService.GetOrderCollectionTypesAsync();
        return Ok(result);
    }
    
    [HttpGet("order-wise-planning")]
    public async Task<IActionResult> OrderWisePlanning([FromQuery] string orderNo, [FromQuery] int flag = 0)
    {
        var result = await _productionPlanningService.GetOrderProductionStatusAsync(orderNo, flag);
        return Ok(result);
    }
    
    [HttpGet("order-dept-completion-date")]
    public async Task<IActionResult> GetOrderDeptCompletionDate([FromQuery] string orderNo, [FromQuery] string deptName)
    {
        var result = await _productionPlanningService.GetOrderDeptCompletionDateAsync(orderNo, deptName);
        return Ok(result);
    }
    
    [HttpGet("gauge-utilization-report")]
    public async Task<IActionResult> GetGaugeUtilizationReport([FromQuery] double? targetGauge)
    {
        // Knit-department capacity report. Restrict to the caller's allowed knit gauges.
        var perms = await GetCurrentPermissionsAsync();
        if (!perms.CanAccessDept("knit")) return Ok(Array.Empty<GaugeUtilizationDto>());

        var result = await _productionPlanningService.GetGaugeUtilizationReportAsync(targetGauge);

        if (!perms.IsUnrestricted)
            result = result.Where(r => perms.IsRowAllowed("knit", r.Gauge.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToList();

        return Ok(result);
    }

    [HttpGet("order-planning-detail")]
    public async Task<IActionResult> GetOrderPlanningDetail([FromQuery] string orderNo, [FromQuery] int flag = 0, [FromQuery] string? gauge = null, [FromQuery] string? ply = null)
    {
        // Knit-department detail (gauge-based). Non-knit-scoped users see nothing.
        if (!await CanAccessDeptAsync("knit")) return Ok(new OrderPlanningDetailDto());

        var result = await _productionPlanningService.GetOrderPlanningDetailAsync(orderNo, flag, gauge, ply);
        return Ok(result);
    }

    [HttpGet("order-detail-by-gauge")]
    public async Task<IActionResult> GetOrderdetailByGuage([FromQuery] string orderNo, [FromQuery] string guage, [FromQuery] string? flag = null)
    {
        // Knit-department detail for a specific gauge: the requested gauge must be
        // within the caller's knit scope.
        var perms = await GetCurrentPermissionsAsync();
        if (!perms.IsUnrestricted && !perms.IsRowAllowed("knit", guage))
            return Ok(Array.Empty<OrderDetailByGuageDto>());

        var result = await _productionPlanningService.GetOrderDetailByGuageAsync(orderNo, guage, flag);
        return Ok(result);
    }

    [HttpGet("order-analysis-while-planing")]
    public async Task<IActionResult> GetOrderAnalysis([FromQuery] string orderNo, [FromQuery] string? knitType, [FromQuery] int mode)
    {
        var result = await _productionPlanningService.GetOrderAnalysisAsync(orderNo, knitType, mode);

        // Department scoping: a user restricted to e.g. {Knit} (or weave+GyatriPashmina)
        // only sees the breakdown rows for departments they have any access to.
        var perms = await GetCurrentPermissionsAsync();
        if (!perms.IsUnrestricted && result?.DetailedAnalysis != null)
        {
            result.DetailedAnalysis = result.DetailedAnalysis
                .Where(d => perms.CanAccessDept(d.KnitType))
                .ToList();
        }

        return Ok(result);
    }

    [HttpGet("fabric-analysis-plan-api")]
    public async Task<IActionResult> GetFabricAnalysisPlan([FromQuery] string orderNo, [FromQuery] string fabricType, [FromQuery] int flag)
    {
        // Fabric analysis covers the Silk / Linen / Other departments. A user with no
        // access to any of them sees nothing; otherwise trim masters to their scope.
        var perms = await GetCurrentPermissionsAsync();
        if (!perms.CanAccessDept("silk", "linen", "other"))
            return Ok(new FabricAnalysisPlanDto());

        var result = await _productionPlanningService.GetFabricAnalysisPlanAsync(orderNo, fabricType, flag);

        if (!perms.IsUnrestricted && result?.MasterWorkload != null)
        {
            result.MasterWorkload = result.MasterWorkload
                .Where(m => MasterAllowed(perms, m.MasterId, m.MasterName))
                .ToList();
        }

        return Ok(result);
    }

    [HttpGet("weave-analysis-plan-api")]
    public async Task<IActionResult> GetWeaveAnalysisPlan([FromQuery] string orderNo, [FromQuery] string? factoryName, [FromQuery] int flag = 1)
    {
        // Weave department. Non-weave-scoped users see nothing; otherwise trim the
        // factory summaries to the factories in the caller's scope.
        var perms = await GetCurrentPermissionsAsync();
        if (!perms.CanAccessDept("weave")) return Ok(new WeaveAnalysisPlanDto());

        var result = await _productionPlanningService.GetWeaveAnalysisPlanAsync(orderNo, factoryName, flag);

        if (!perms.IsUnrestricted && result?.FactorySummaries != null)
        {
            result.FactorySummaries = result.FactorySummaries
                .Where(f => perms.IsRowAllowed("weave", f.WeaveFactory))
                .ToList();
        }

        return Ok(result);
    }

    [HttpPost("plan")]
    public async Task<IActionResult> SavePlan([FromBody] SavePlanRequestDto request)
    {
        if (!await HasPermissionAsync("OrderPlanning", "Edit")) return Forbid();

        var perms = await GetCurrentPermissionsAsync();
        if (!perms.IsUnrestricted && !perms.IsRowAllowed(request.KnitType, request.Guage))
        {
            return Forbid(); // Zero Trust: block saving a plan outside the caller's scope
        }

        // Audit identity always comes from the token, never the client payload.
        request.UserId = GetCurrentUserName();

        var result = await _productionPlanningService.SavePlanAsync(
            request.OrderNo,
            request.Guage,
            request.StartDate,
            request.EndDate,
            request.Qty,
            request.Machine,
            request.OrderType,
            request.KnitType,
            request.UserId,
            request.CreatedDate,
            request.SizeLines,
            request.MachineNo,
            request.MachineId,
            request.IsOvertime,
            request.OvertimeHours,
            request.WorkSaturday
        );
        return Ok(result);
    }

    [HttpGet("planned-data-by-order")]
    public async Task<IActionResult> GetPlannedDataByOrder([FromQuery] string orderNo, [FromQuery] string? gauge = null, [FromQuery] decimal? qty = null)
    {
        // Planned data spans ALL departments (knit gauge #, weave factory, tailor master).
        // Keep only rows whose (department, gauge value) is in the caller's scope.
        var perms = await GetCurrentPermissionsAsync();
        var result = await _productionPlanningService.GetPlannedDataByOrderAsync(orderNo, gauge, qty);

        if (!perms.IsUnrestricted)
            result = result.Where(r => perms.IsRowAllowed(r.KnitType, r.Gauge)).ToList();

        return Ok(result);
    }

    [HttpDelete("plan/{id}")]
    public async Task<IActionResult> DeletePlanDetail(int id)
    {
        if (!await HasPermissionAsync("OrderPlanning", "Delete")) return Forbid();
        if (!await OwnsPlanAsync(id)) return Forbid(); // Zero Trust: only plans under their gauge

        var result = await _productionPlanningService.DeletePlanDetailAsync(id);
        return Ok(result);
    }

    [HttpPut("plan/{id}")]
    public async Task<IActionResult> UpdatePlanDetail(
        int id,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate,
        [FromQuery] decimal qty,
        [FromQuery] int machine,
        [FromQuery] string userId)
    {
        if (!await HasPermissionAsync("OrderPlanning", "Edit")) return Forbid();
        if (!await OwnsPlanAsync(id)) return Forbid(); // Zero Trust: only plans under their gauge

        // Audit identity from the token, not the query string.
        var result = await _productionPlanningService.UpdatePlanDetailAsync(id, startDate, endDate, qty, machine, GetCurrentUserName());
        return Ok(result);
    }

    [HttpGet("knit-gantt-chart")]
    public async Task<IActionResult> GetKnitGanttChartData(
        [FromQuery] DateTime? startDate = null, 
        [FromQuery] DateTime? endDate = null, 
        [FromQuery] string? orderNo = null, 
        [FromQuery] string? gauge = null)
    {
        var perms = await GetCurrentPermissionsAsync();
        var result = await _productionPlanningService.GetKnitGanttChartDataAsync(startDate, endDate, orderNo, gauge);

        if (!perms.IsUnrestricted)
            result = result.Where(r => perms.IsRowAllowed(r.KnitType, r.Guage)).ToList();

        return Ok(result);
    }

    [HttpGet("machine-planing")]
    public async Task<IActionResult> GetMachinePlaning([FromQuery] string? targetGauge = null)
    {
        // machine planning is knit-only; restrict to the caller's allowed knit gauges.
        var perms = await GetCurrentPermissionsAsync();
        if (!perms.CanAccessDept("knit")) return Ok(new List<MachinePlaningDto>());

        var result = await _productionPlanningService.GetMachinePlaningAsync(targetGauge);

        if (!perms.IsUnrestricted)
            result = result.Where(r => perms.IsRowAllowed("knit", r.Gauge?.ToString(System.Globalization.CultureInfo.InvariantCulture))).ToList();

        return Ok(result);
    }

    [HttpGet("master-planning")]
    public async Task<IActionResult> GetMasterPlanning([FromQuery] string? orderNo = null, [FromQuery] string? gauge = null)
    {
        // Master view spans departments; keep only rows in the caller's scope.
        var perms = await GetCurrentPermissionsAsync();
        var result = await _productionPlanningService.GetMasterPlanningAsync(orderNo, gauge);

        if (!perms.IsUnrestricted)
            result = result.Where(r => perms.IsRowAllowed(r.KnitType, r.Guage)).ToList();

        return Ok(result);
    }

    [HttpGet("knitters-by-gauge")]
    public async Task<IActionResult> GetKnittersByGauge([FromQuery] string? gauge = null)
    {
        // Knitters are knit resources; restrict to the caller's allowed knit gauges.
        var perms = await GetCurrentPermissionsAsync();
        if (!perms.CanAccessDept("knit")) return Ok(new List<KnitterDto>());

        var result = await _productionPlanningService.GetKnittersByGaugeAsync(gauge);

        if (!perms.IsUnrestricted)
            result = result.Where(r => perms.IsRowAllowed("knit", r.Gauge)).ToList();

        return Ok(result);
    }

    [HttpGet("planing-report")]
    public async Task<IActionResult> GetPlaningReport([FromQuery] DateTime? fromDate = null, [FromQuery] DateTime? toDate = null)
    {
        var result = await _productionPlanningService.GetPlaningReportAsync(fromDate, toDate);
        return Ok(result);
    }

    [HttpPost("knitter-assignment")]
    public async Task<IActionResult> SaveKnitterAssignment([FromBody] SaveKnitterAssignmentRequestDto request)
    {
        if (!await HasPermissionAsync("ForMasterPlaning", "Edit")) return Forbid();
        if (!await OwnsPlanAsync(request.MasterPlanDetailId)) return Forbid();

        var result = await _productionPlanningService.SaveKnitterAssignmentAsync(
            request.MasterPlanDetailId, request.CardNo, request.KnitterName, GetCurrentUserName());
        return Ok(result);
    }

    [HttpGet("knitter-busy")]
    public async Task<IActionResult> GetKnitterBusy()
    {
        var result = await _productionPlanningService.GetKnitterBusyAsync();
        return Ok(result);
    }

    [HttpPost("knitter-assignment-manage")]
    public async Task<IActionResult> ManageKnitterAssignment([FromQuery] int masterPlanDetailId, [FromQuery] string action)
    {
        // Removing/unassigning a knitter is a Delete action; other actions (complete,
        // reassign, ...) are Edit. Gate on the right permission so an Edit-only user
        // cannot remove an assignment.
        var normalized = action?.Trim().ToLowerInvariant();
        var isRemoval  = normalized is "unassign" or "remove" or "delete";
        var requiredAction = isRemoval ? "Delete" : "Edit";

        if (!await HasPermissionAsync("ForMasterPlaning", requiredAction)) return Forbid();
        if (!await OwnsPlanAsync(masterPlanDetailId)) return Forbid();

        var result = await _productionPlanningService.ManageKnitterAssignmentAsync(masterPlanDetailId, action);
        return Ok(result);
    }

    [HttpGet("knitter-assignment-history")]
    public async Task<IActionResult> GetKnitterAssignmentHistory([FromQuery] int days = 30)
    {
        var result = await _productionPlanningService.GetKnitterAssignmentHistoryAsync(days);
        return Ok(result);
    }

    [HttpGet("plan-size-lines")]
    public async Task<IActionResult> GetPlanSizeLines([FromQuery] int masterPlanDetailId)
    {
        if (!await OwnsPlanAsync(masterPlanDetailId)) return Forbid(); // Zero Trust: only own gauge's plans

        var result = await _productionPlanningService.GetPlanSizeLinesAsync(masterPlanDetailId);
        return Ok(result);
    }

    [HttpPut("plan-size-line")]
    public async Task<IActionResult> UpdatePlanSizeLine([FromQuery] int sizeLineId, [FromQuery] decimal qty)
    {
        if (!await HasPermissionAsync("OrderPlanning", "Edit")) return Forbid();

        // Zero Trust: the size line must belong to a plan under the caller's gauge.
        var parentPlanId = await _productionPlanningService.GetSizeLinePlanIdAsync(sizeLineId);
        if (parentPlanId <= 0 || !await OwnsPlanAsync(parentPlanId)) return Forbid();

        var result = await _productionPlanningService.UpdatePlanSizeLineAsync(sizeLineId, qty);
        return Ok(result);
    }
}
