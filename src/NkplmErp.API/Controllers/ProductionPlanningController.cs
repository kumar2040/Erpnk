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

    private async Task<string?> GetCurrentAssignedGaugeAsync()
    {
        var userId = GetCurrentUserId();
        var perms = await _roleService.GetUserPermissionsAsync(userId);
        return perms.AssignedGauge;
    }

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

    // Gauge ownership check for plan mutations (scoped users can only touch their own plans).
    private async Task<bool> OwnsPlanAsync(int planDetailId)
    {
        var assignedGauge = await GetCurrentAssignedGaugeAsync();
        if (string.IsNullOrEmpty(assignedGauge)) return true; // unrestricted user

        var planGauge = await _productionPlanningService.GetPlanGaugeAsync(planDetailId);
        return string.Equals(planGauge?.Trim(), assignedGauge.Trim(), StringComparison.OrdinalIgnoreCase);
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
        var assignedGauge = await GetCurrentAssignedGaugeAsync();
        if (!string.IsNullOrEmpty(assignedGauge) && double.TryParse(assignedGauge, out var doubleGauge))
        {
            targetGauge = doubleGauge;
        }

        var result = await _productionPlanningService.GetGaugeUtilizationReportAsync(targetGauge);
        return Ok(result);
    }

    [HttpGet("order-planning-detail")]
    public async Task<IActionResult> GetOrderPlanningDetail([FromQuery] string orderNo, [FromQuery] int flag = 0, [FromQuery] string? gauge = null, [FromQuery] string? ply = null)
    {
        var assignedGauge = await GetCurrentAssignedGaugeAsync();
        if (!string.IsNullOrEmpty(assignedGauge))
        {
            gauge = assignedGauge;
        }

        var result = await _productionPlanningService.GetOrderPlanningDetailAsync(orderNo, flag, gauge, ply);
        return Ok(result);
    }

    [HttpGet("order-detail-by-gauge")]
    public async Task<IActionResult> GetOrderdetailByGuage([FromQuery] string orderNo, [FromQuery] string guage, [FromQuery] string? flag = null)
    {
        var assignedGauge = await GetCurrentAssignedGaugeAsync();
        if (!string.IsNullOrEmpty(assignedGauge))
        {
            guage = assignedGauge;
        }

        var result = await _productionPlanningService.GetOrderDetailByGuageAsync(orderNo, guage, flag);
        return Ok(result);
    }

    [HttpGet("order-analysis-while-planing")]
    public async Task<IActionResult> GetOrderAnalysis([FromQuery] string orderNo, [FromQuery] string? knitType, [FromQuery] int mode)
    {
        var result = await _productionPlanningService.GetOrderAnalysisAsync(orderNo, knitType, mode);
        return Ok(result);
    }

    [HttpGet("fabric-analysis-plan-api")]
    public async Task<IActionResult> GetFabricAnalysisPlan([FromQuery] string orderNo, [FromQuery] string fabricType, [FromQuery] int flag)
    {
        var result = await _productionPlanningService.GetFabricAnalysisPlanAsync(orderNo, fabricType, flag);

        // Zero Trust: a master-scoped user (AssignedGauge = master id like 't1') only
        // sees their own master's workload (Silk / Linen / Other).
        var assignedGauge = await GetCurrentAssignedGaugeAsync();
        if (!string.IsNullOrEmpty(assignedGauge) && result?.MasterWorkload != null)
        {
            result.MasterWorkload = result.MasterWorkload
                .Where(m =>
                    string.Equals(m.MasterId?.Trim(), assignedGauge.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(m.MasterName?.Trim(), assignedGauge.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return Ok(result);
    }

    [HttpGet("weave-analysis-plan-api")]
    public async Task<IActionResult> GetWeaveAnalysisPlan([FromQuery] string orderNo, [FromQuery] string? factoryName, [FromQuery] int flag = 1)
    {
        // Zero Trust: a factory-scoped user (AssignedGauge = weave factory name like
        // 'Gyatri Pashmina') is locked to their own factory.
        var assignedGauge = await GetCurrentAssignedGaugeAsync();
        if (!string.IsNullOrEmpty(assignedGauge))
        {
            factoryName = assignedGauge;
        }

        var result = await _productionPlanningService.GetWeaveAnalysisPlanAsync(orderNo, factoryName, flag);

        if (!string.IsNullOrEmpty(assignedGauge) && result?.FactorySummaries != null)
        {
            result.FactorySummaries = result.FactorySummaries
                .Where(f => string.Equals(f.WeaveFactory?.Trim(), assignedGauge.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return Ok(result);
    }

    [HttpPost("plan")]
    public async Task<IActionResult> SavePlan([FromBody] SavePlanRequestDto request)
    {
        if (!await HasPermissionAsync("OrderPlanning", "Edit")) return Forbid();

        var assignedGauge = await GetCurrentAssignedGaugeAsync();
        if (!string.IsNullOrEmpty(assignedGauge))
        {
            if (!string.Equals(request.Guage, assignedGauge, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid(); // Zero Trust: block saving plan under other gauges
            }
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
        var assignedGauge = await GetCurrentAssignedGaugeAsync();
        if (!string.IsNullOrEmpty(assignedGauge))
        {
            gauge = assignedGauge;
        }

        var result = await _productionPlanningService.GetPlannedDataByOrderAsync(orderNo, gauge, qty);
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
        var assignedGauge = await GetCurrentAssignedGaugeAsync();
        if (!string.IsNullOrEmpty(assignedGauge))
        {
            gauge = assignedGauge;
        }

        var result = await _productionPlanningService.GetKnitGanttChartDataAsync(startDate, endDate, orderNo, gauge);
        return Ok(result);
    }

    [HttpGet("machine-planing")]
    public async Task<IActionResult> GetMachinePlaning([FromQuery] string? targetGauge = null)
    {
        var assignedGauge = await GetCurrentAssignedGaugeAsync();
        if (!string.IsNullOrEmpty(assignedGauge))
        {
            targetGauge = assignedGauge;
        }

        var result = await _productionPlanningService.GetMachinePlaningAsync(targetGauge);
        return Ok(result);
    }

    [HttpGet("master-planning")]
    public async Task<IActionResult> GetMasterPlanning([FromQuery] string? orderNo = null, [FromQuery] string? gauge = null)
    {
        // Master view: if the user is scoped to a gauge, restrict to it.
        var assignedGauge = await GetCurrentAssignedGaugeAsync();
        if (!string.IsNullOrEmpty(assignedGauge))
        {
            gauge = assignedGauge;
        }

        var result = await _productionPlanningService.GetMasterPlanningAsync(orderNo, gauge);
        return Ok(result);
    }

    [HttpGet("knitters-by-gauge")]
    public async Task<IActionResult> GetKnittersByGauge([FromQuery] string? gauge = null)
    {
        var assignedGauge = await GetCurrentAssignedGaugeAsync();
        if (!string.IsNullOrEmpty(assignedGauge))
        {
            gauge = assignedGauge;
        }

        var result = await _productionPlanningService.GetKnittersByGaugeAsync(gauge);
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
        if (!await HasPermissionAsync("ForMasterPlaning", "Edit")) return Forbid();
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
        var result = await _productionPlanningService.GetPlanSizeLinesAsync(masterPlanDetailId);
        return Ok(result);
    }

    [HttpPut("plan-size-line")]
    public async Task<IActionResult> UpdatePlanSizeLine([FromQuery] int sizeLineId, [FromQuery] decimal qty)
    {
        if (!await HasPermissionAsync("OrderPlanning", "Edit")) return Forbid();

        var result = await _productionPlanningService.UpdatePlanSizeLineAsync(sizeLineId, qty);
        return Ok(result);
    }
}
