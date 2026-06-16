using NkplmErp.Shared.DTOs;

namespace NkplmErp.Application.Interfaces;

public interface IProductionPlanningService
{
    Task<IEnumerable<MonthlyOrderSummaryDto>> GetMonthlySummaryAsync(DateTime inputDate);
    Task<IEnumerable<MonthlyOrderDetailDto>> GetMonthlyOrderDetailsAsync(DateTime inputDate);
    Task<IEnumerable<OrderCollectionTypeDto>> GetOrderCollectionTypesAsync();
    Task<OrderProductionStatusDto> GetOrderProductionStatusAsync(string orderNo, int flag);
    Task<OrderDeptCompletionDto?> GetOrderDeptCompletionDateAsync(string orderNo, string deptName);
    Task<IEnumerable<GaugeUtilizationDto>> GetGaugeUtilizationReportAsync(double? targetGauge);
    Task<OrderPlanningDetailDto> GetOrderPlanningDetailAsync(string orderNo, int flag, string? gauge = null, string? ply = null);
    Task<IEnumerable<OrderDetailByGuageDto>> GetOrderDetailByGuageAsync(string orderNo, string guage, string? flag = null);
    Task<OrderAnalysisResultDto> GetOrderAnalysisAsync(string orderNo, string? knitType, int mode);
    Task<FabricAnalysisPlanDto> GetFabricAnalysisPlanAsync(string orderNo, string fabricType, int flag);
    Task<WeaveAnalysisPlanDto> GetWeaveAnalysisPlanAsync(string orderNo, string? factoryName, int flag);
    Task<int> SavePlanAsync(string orderNo, string guage, DateTime startDate, DateTime endDate, decimal qty, int machine, string orderType, string knitType, string userId, DateTime createdDate, List<PlanSizeLineDto>? sizeLines = null, string? machineNo = null, int? machineId = null, bool isOvertime = false, decimal overtimeHours = 0, bool workSaturday = false);
    Task<IEnumerable<PlannedDataDto>> GetPlannedDataByOrderAsync(string orderNo, string? gauge = null, decimal? qty = null);
    Task<bool> DeletePlanDetailAsync(int planDetailId);
    Task<bool> UpdatePlanDetailAsync(int planDetailId, DateTime startDate, DateTime endDate, decimal qty, int machine, string userId);
    Task<List<KnitGanttChartDto>> GetKnitGanttChartDataAsync(DateTime? startDate, DateTime? endDate, string? orderNo, string? gauge);
    Task<List<MachinePlaningDto>> GetMachinePlaningAsync(string? targetGauge = null);
    Task<List<MasterPlanningRowDto>> GetMasterPlanningAsync(string? orderNo = null, string? gauge = null);
    Task<List<KnitterDto>> GetKnittersByGaugeAsync(string? gauge = null);
    Task<List<PlaningReportDayDto>> GetPlaningReportAsync(DateTime? fromDate = null, DateTime? toDate = null);
    Task<bool> SaveKnitterAssignmentAsync(int masterPlanDetailId, string cardNo, string? knitterName, string? assignedBy);
    Task<List<KnitterBusyDto>> GetKnitterBusyAsync();
    Task<bool> ManageKnitterAssignmentAsync(int masterPlanDetailId, string action);
    Task<List<KnitterAssignmentHistoryDto>> GetKnitterAssignmentHistoryAsync(int days = 30);
    Task<List<PlanSizeLineEditDto>> GetPlanSizeLinesAsync(int masterPlanDetailId);
    Task<SizeLineUpdateResultDto> UpdatePlanSizeLineAsync(int sizeLineId, decimal qty);

    // Gauge of a single plan row - used by the API for Zero-Trust mutation checks.
    // Default implementation so HTTP-client implementations don't need it.
    Task<string?> GetPlanGaugeAsync(int planDetailId) => Task.FromResult<string?>(null);
}

