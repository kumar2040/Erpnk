using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.ThirdPartyApi.Outbound.Base;
using NkplmErp.Shared.DTOs.OrderPlanningApi;
using NkplmErp.ThirdPartyApi.Outbound.Manager.Interface;
using NkplmErp.ThirdPartyApi.Outbound.Route;

namespace NkplmErp.ThirdPartyApi.Outbound.Controller
{
    /// <summary>Production planning page: order planning, replan and machine edit, backed by the PLM API.</summary>
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    [Authorize]
    public class ThirdPartyProductionPlanController(IThirdPartyManager thirdParty, IRoleManagementService roleService) : ThirdPartyPermissionBase(roleService)
    {
        // GET api/v1/ThirdPartyProductionPlan/months?inputDate=2026-11-19
        [HttpGet("months")]
        public async Task<IActionResult> GetMonthsAsync([FromQuery] DateTime inputDate, CancellationToken ct)
        {
            if (!await CanViewAsync("OrderPlanning")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<MonthlyOrderSummaryDto>>(
                ThirdPartyRouter.Months, new { inputDate = inputDate.ToString("yyyy-MM-dd") }, ct));
        }

        // GET api/v1/ThirdPartyProductionPlan/orders?month=2026-11-01
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrdersAsync([FromQuery] DateTime month, [FromQuery] string? knitType, CancellationToken ct)
        {
            if (!await CanViewAsync("OrderPlanning")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<MonthlyOrderDetailDto>>(
                ThirdPartyRouter.Orders, new { month = month.ToString("yyyy-MM-dd"), knitType = knitType ?? string.Empty }, ct));
        }

        // GET api/v1/ThirdPartyProductionPlan/collection-types
        [HttpGet("collection-types")]
        public async Task<IActionResult> GetCollectionTypesAsync(CancellationToken ct)
        {
            if (!await CanViewAsync("OrderPlanning")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<OrderCollectionTypeDto>>(ThirdPartyRouter.CollectionTypes, null, ct));
        }

        // GET api/v1/ThirdPartyProductionPlan/remaining?orderNo=SND-113
        [HttpGet("remaining")]
        public async Task<IActionResult> GetRemainingAsync([FromQuery] string orderNo, CancellationToken ct)
        {
            if (!await CanViewAsync("OrderPlanning")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<OrderPlanningRemainingModel>>(
                ThirdPartyRouter.Remaining, new { orderNo }, ct));
        }

        // ---- Order planning (continued) ----

        // GET api/v1/ThirdPartyProductionPlan/order-analysis?orderNo=PO-1978&mode=1[&knitType=Knit]
        // mode 1 = per product type, 2 = print/emb styles of one type, 3 = print/emb totals.
        [HttpGet("order-analysis")]
        public async Task<IActionResult> GetOrderAnalysisAsync([FromQuery] string orderNo, [FromQuery] string? knitType, [FromQuery] int mode, CancellationToken ct)
        {
            if (!await CanViewAsync("OrderPlanning")) return Forbid();
            return Ok(await thirdParty.CallAsync<OrderAnalysisResultDto>(
                ThirdPartyRouter.Analysis, new { orderNo, knitType = knitType ?? string.Empty, mode }, ct));
        }

        // GET api/v1/ThirdPartyProductionPlan/order-types?month=2026-11-01
        // Product types of every order in the month, in one call (order dropdown filter).
        [HttpGet("order-types")]
        public async Task<IActionResult> GetOrderTypesAsync([FromQuery] DateTime month, CancellationToken ct)
        {
            if (!await CanViewAsync("OrderPlanning")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<OrderProductTypesModel>>(
                ThirdPartyRouter.OrderTypes, new { month = month.ToString("yyyy-MM-dd") }, ct));
        }

        // GET api/v1/ThirdPartyProductionPlan/weave-factories?orderNo=PO-1978
        // Weave pieces per factory for the order, nothing hidden (adds up to the order's Weave quantity).
        [HttpGet("weave-factories")]
        public async Task<IActionResult> GetWeaveFactoriesAsync([FromQuery] string orderNo, CancellationToken ct)
        {
            if (!await CanViewAsync("OrderPlanning")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<WeaveFactorySummaryDto>>(
                ThirdPartyRouter.WeaveFactories, new { orderNo }, ct));
        }

        // GET api/v1/ThirdPartyProductionPlan/production-progress?orderNo=PO-1968&knitType=Weave
        // Per style: ordered / issued / made / remaining for one non-Knit product type.
        [HttpGet("production-progress")]
        public async Task<IActionResult> GetProductionProgressAsync([FromQuery] string orderNo, [FromQuery] string knitType, CancellationToken ct)
        {
            if (!await CanViewAsync("OrderPlanning")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<ProductionProgressModel>>(
                ThirdPartyRouter.ProductionProgress, new { orderNo, knitType }, ct));
        }

        // GET api/v1/ThirdPartyProductionPlan/master-progress?orderNo=PO-1978&knitType=Silk
        // Same figures as production-progress, one row per tailor master (Style holds the master name).
        [HttpGet("master-progress")]
        public async Task<IActionResult> GetMasterProgressAsync([FromQuery] string orderNo, [FromQuery] string knitType, CancellationToken ct)
        {
            if (!await CanViewAsync("OrderPlanning")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<ProductionProgressModel>>(
                ThirdPartyRouter.MasterProgress, new { orderNo, knitType }, ct));
        }

        // GET api/v1/ThirdPartyProductionPlan/plan-rows[?orderNo=GT-133]
        // Open plan rows in PLM (Replan list) for every order, or one order; machine is empty when none is assigned.
        [HttpGet("plan-rows")]
        public async Task<IActionResult> GetPlanRowsAsync([FromQuery] string? orderNo, CancellationToken ct)
        {
            if (!await CanViewAsync("OrderPlanning")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<PlanRowModel>>(ThirdPartyRouter.PlanRows, new { orderNo = orderNo ?? string.Empty }, ct));
        }

        // POST api/v1/ThirdPartyProductionPlan/plan-row-machine   { id, machines: [{ machineNo, qty }] }
        // Edits the machines of one open PLM plan row (qty split over them); only PLM's table is changed.
        [HttpPost("plan-row-machine")]
        public async Task<IActionResult> UpdatePlanRowMachineAsync([FromBody] PlanRowMachineModel request, CancellationToken ct)
        {
            if (!await CanEditAsync("OrderPlanning")) return Forbid();
            return Ok(await thirdParty.CallAsync<PlanRowMachineModel>(
                ThirdPartyRouter.UpdateMachine,
                new { id = request.Id, machines = (request.Machines ?? new()).Select(m => new { machineNo = m.MachineNo, qty = m.Qty }).ToList() }, ct));
        }

        // GET api/v1/ThirdPartyProductionPlan/yarn-names?productIds=192,4,228
        // Yarn display names ("product_name: count1") for product ids.
        [HttpGet("yarn-names")]
        public async Task<IActionResult> GetYarnNamesAsync([FromQuery] string productIds, CancellationToken ct)
        {
            if (!await CanViewAsync("OrderPlanning")) return Forbid();
            var ids = (productIds ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(i => int.TryParse(i, out _)).Distinct().Take(500).ToList();
            return Ok(await thirdParty.CallAsync<List<YarnNameModel>>(ThirdPartyRouter.YarnNames, new { productIds = ids }, ct));
        }

        // GET api/v1/ThirdPartyProductionPlan/dept-completion-date?orderNo=GT-26025&dept=KNIT
        [HttpGet("dept-completion-date")]
        public async Task<IActionResult> GetDeptCompletionDateAsync([FromQuery] string orderNo, [FromQuery] string? dept, CancellationToken ct)
        {
            if (!await CanViewAsync("OrderPlanning")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<OrderDeptCompletionDto>>(
                ThirdPartyRouter.DeptCompletionDate, new { orderNo, dept = dept ?? "KNIT" }, ct));
        }

        // GET api/v1/ThirdPartyProductionPlan/gauge-details?orderNo=PO-1978&gauge=16&flag=1
        // flag 1 = this order's lines, 0 = backlog of earlier-shipping orders; lines with nothing left are omitted.
        [HttpGet("gauge-details")]
        public async Task<IActionResult> GetGaugeDetailsAsync([FromQuery] string orderNo, [FromQuery] string? gauge, [FromQuery] string? flag, [FromQuery] string? knitType, CancellationToken ct)
        {
            if (!await CanViewAsync("OrderPlanning")) return Forbid();
            return Ok(await thirdParty.CallAsync<List<OrderDetailByGuageDto>>(
                ThirdPartyRouter.GaugeDetails, new { orderNo, gauge = gauge ?? string.Empty, flag = flag ?? "1", knitType = knitType ?? string.Empty }, ct));
        }

        // GET api/v1/ThirdPartyProductionPlan/yarn?orderNo=SND-113[&byColor=true&gauge=7&ply=1]
        // byColor=true => one row per colour of that yarn/gauge/ply. A blank gauge/ply is valid (arrives as null).
        [HttpGet("yarn")]
        public async Task<IActionResult> GetYarnAsync([FromQuery] string orderNo, [FromQuery] bool byColor, [FromQuery] string? gauge, [FromQuery] string? ply, CancellationToken ct)
        {
            if (!await CanViewAsync("OrderPlanning")) return Forbid();
            object parameters = byColor
                ? new { orderNo, gauge = gauge ?? string.Empty, ply = ply ?? string.Empty, byColor = true }
                : new { orderNo, byColor = false };
            return Ok(await thirdParty.CallAsync<List<YarnPlanningStatusDto>>(ThirdPartyRouter.Yarn, parameters, ct));
        }
    }
}
