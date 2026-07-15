using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Services.ProductionPlanning;

public class ProductionPlanningService : IProductionPlanningService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProductionPlanningService> _logger;

    public ProductionPlanningService(HttpClient httpClient, ILogger<ProductionPlanningService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IEnumerable<MonthlyOrderSummaryDto>> GetMonthlySummaryAsync(DateTime inputDate)
    {
        var url = $"api/v1/ProductionPlanning/monthly-summary?inputDate={inputDate:yyyy-MM-dd}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<IEnumerable<MonthlyOrderSummaryDto>>() ?? Enumerable.Empty<MonthlyOrderSummaryDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMonthlySummaryAsync");
            throw;
        }
    }

    public async Task<IEnumerable<MonthlyOrderDetailDto>> GetMonthlyOrderDetailsAsync(DateTime inputDate)
    {
        var url = $"api/v1/ProductionPlanning/monthly-details?inputDate={inputDate:yyyy-MM-dd}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                var list = await response.Content.ReadFromJsonAsync<IEnumerable<MonthlyOrderDetailDto>>() ?? Enumerable.Empty<MonthlyOrderDetailDto>();
                Console.WriteLine($"DEBUG: GetMonthlyOrderDetailsAsync returned {list.Count()} items for URL: {url}");
                return list;
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMonthlyOrderDetailsAsync");
            throw;
        }
    }
    public async Task<IEnumerable<OrderCollectionTypeDto>> GetOrderCollectionTypesAsync()
    {
        var url = "api/v1/ProductionPlanning/order-collection-types";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<IEnumerable<OrderCollectionTypeDto>>() ?? Enumerable.Empty<OrderCollectionTypeDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrderCollectionTypesAsync");
            throw;
        }
    }

    public async Task<OrderProductionStatusDto> GetOrderProductionStatusAsync(string orderNo, int flag)
    {
        var url = $"api/v1/ProductionPlanning/order-wise-planning?orderNo={orderNo}&flag={flag}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<OrderProductionStatusDto>() ?? new OrderProductionStatusDto();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrderProductionStatusAsync");
            throw;
        }
    }
    public async Task<OrderDeptCompletionDto?> GetOrderDeptCompletionDateAsync(string orderNo, string deptName)
    {
        var url = $"api/v1/ProductionPlanning/order-dept-completion-date?orderNo={orderNo}&deptName={deptName}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<OrderDeptCompletionDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrderDeptCompletionDateAsync");
            throw;
        }
    }
    public async Task<IEnumerable<GaugeUtilizationDto>> GetGaugeUtilizationReportAsync(double? targetGauge)
    {
        var url = $"api/v1/ProductionPlanning/gauge-utilization-report";
        if (targetGauge.HasValue) url += $"?targetGauge={targetGauge}";
        
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<IEnumerable<GaugeUtilizationDto>>() ?? Enumerable.Empty<GaugeUtilizationDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetGaugeUtilizationReportAsync");
            throw;
        }
    }

    public async Task<OrderPlanningDetailDto> GetOrderPlanningDetailAsync(string orderNo, int flag, string? gauge = null, string? ply = null)
    {
        var url = $"api/v1/ProductionPlanning/order-planning-detail?orderNo={orderNo}&flag={flag}";
        if (!string.IsNullOrEmpty(gauge)) url += $"&gauge={gauge}";
        if (!string.IsNullOrEmpty(ply)) url += $"&ply={ply}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<OrderPlanningDetailDto>() ?? new OrderPlanningDetailDto();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrderPlanningDetailAsync");
            throw;
        }
    }

    public async Task<IEnumerable<OrderDetailByGuageDto>> GetOrderDetailByGuageAsync(string orderNo, string guage, string? flag = null)
    {
        var url = $"api/v1/ProductionPlanning/order-detail-by-gauge?orderNo={orderNo}&guage={guage}";
        if (!string.IsNullOrEmpty(flag)) url += $"&flag={flag}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<IEnumerable<OrderDetailByGuageDto>>() ?? Enumerable.Empty<OrderDetailByGuageDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrderDetailByGuageAsync");
            throw;
        }
    }

    public async Task<OrderAnalysisResultDto> GetOrderAnalysisAsync(string orderNo, string? knitType, int mode)
    {
        var url = $"api/v1/ProductionPlanning/order-analysis-while-planing?orderNo={orderNo}&mode={mode}";
        if (!string.IsNullOrEmpty(knitType)) url += $"&knitType={knitType}";

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<OrderAnalysisResultDto>() ?? new OrderAnalysisResultDto();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetOrderAnalysisAsync");
            throw;
        }
    }

    public async Task<FabricAnalysisPlanDto> GetFabricAnalysisPlanAsync(string orderNo, string fabricType, int flag)
    {
        var url = $"api/v1/ProductionPlanning/fabric-analysis-plan-api?orderNo={orderNo}&fabricType={fabricType}&flag={flag}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<FabricAnalysisPlanDto>() ?? new FabricAnalysisPlanDto();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetFabricAnalysisPlanAsync");
            throw;
        }
    }

    public async Task<WeaveAnalysisPlanDto> GetWeaveAnalysisPlanAsync(string orderNo, string? factoryName, int flag)
    {
        var url = $"api/v1/ProductionPlanning/weave-analysis-plan-api?orderNo={orderNo}&factoryName={factoryName}&flag={flag}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<WeaveAnalysisPlanDto>() ?? new WeaveAnalysisPlanDto();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetWeaveAnalysisPlanAsync");
            throw;
        }
    }

    public async Task<int> SavePlanAsync(string orderNo, string guage, DateTime startDate, DateTime endDate, decimal qty, int machine, string orderType, string knitType, string userId, DateTime createdDate, List<PlanSizeLineDto>? sizeLines = null, string? machineNo = null, int? machineId = null, bool isOvertime = false, decimal overtimeHours = 0, bool workSaturday = false)
    {
        var url = "api/v1/ProductionPlanning/plan";
        var request = new SavePlanRequestDto
        {
            OrderNo = orderNo,
            Guage = guage,
            StartDate = startDate,
            EndDate = endDate,
            Qty = qty,
            Machine = machine,
            OrderType = orderType,
            KnitType = knitType,
            UserId = userId,
            CreatedDate = createdDate,
            SizeLines = sizeLines,
            MachineNo = machineNo,
            MachineId = machineId,
            IsOvertime = isOvertime,
            OvertimeHours = overtimeHours,
            WorkSaturday = workSaturday
        };
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<int>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SavePlanAsync");
            throw;
        }
    }

    public async Task<IEnumerable<PlannedDataDto>> GetPlannedDataByOrderAsync(string orderNo, string? gauge = null, decimal? qty = null)
    {
        var url = $"api/v1/ProductionPlanning/planned-data-by-order?orderNo={orderNo}";
        if (!string.IsNullOrEmpty(gauge)) url += $"&gauge={gauge}";
        if (qty.HasValue) url += $"&qty={qty.Value}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<IEnumerable<PlannedDataDto>>() ?? Enumerable.Empty<PlannedDataDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPlannedDataByOrderAsync");
            throw;
        }
    }

    public async Task<bool> DeletePlanDetailAsync(int planDetailId)
    {
        var url = $"api/v1/ProductionPlanning/plan/{planDetailId}";
        try
        {
            var response = await _httpClient.DeleteAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<bool>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DeletePlanDetailAsync");
            throw;
        }
    }

    public async Task<bool> UpdatePlanDetailAsync(int planDetailId, DateTime startDate, DateTime endDate, decimal qty, int machine, string userId)
    {
        var url = $"api/v1/ProductionPlanning/plan/{planDetailId}?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}&qty={qty}&machine={machine}&userId={userId}";
        try
        {
            var response = await _httpClient.PutAsync(url, null);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<bool>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdatePlanDetailAsync");
            throw;
        }
    }

    public async Task<List<KnitGanttChartDto>> GetKnitGanttChartDataAsync(DateTime? startDate, DateTime? endDate, string? orderNo, string? gauge)
    {
        var url = $"api/v1/ProductionPlanning/knit-gantt-chart?";
        var queryParams = new List<string>();
        if (startDate.HasValue) queryParams.Add($"startDate={startDate.Value:yyyy-MM-dd}");
        if (endDate.HasValue) queryParams.Add($"endDate={endDate.Value:yyyy-MM-dd}");
        if (!string.IsNullOrEmpty(orderNo)) queryParams.Add($"orderNo={Uri.EscapeDataString(orderNo)}");
        if (!string.IsNullOrEmpty(gauge)) queryParams.Add($"gauge={Uri.EscapeDataString(gauge)}");

        if (queryParams.Count > 0)
        {
            url += string.Join("&", queryParams);
        }

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<KnitGanttChartDto>>() ?? new List<KnitGanttChartDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetKnitGanttChartDataAsync");
            throw;
        }
    }

    public async Task<List<MasterPlanningRowDto>> GetMasterPlanningAsync(string? orderNo = null, string? gauge = null)
    {
        var url = "api/v1/ProductionPlanning/master-planning";
        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(orderNo)) queryParams.Add($"orderNo={Uri.EscapeDataString(orderNo)}");
        if (!string.IsNullOrEmpty(gauge)) queryParams.Add($"gauge={Uri.EscapeDataString(gauge)}");
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<MasterPlanningRowDto>>() ?? new List<MasterPlanningRowDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMasterPlanningAsync");
            throw;
        }
    }

    public async Task<bool> SaveKnitterAssignmentAsync(int masterPlanDetailId, string cardNo, string? knitterName, string? assignedBy)
    {
        var url = "api/v1/ProductionPlanning/knitter-assignment";
        var request = new SaveKnitterAssignmentRequestDto
        {
            MasterPlanDetailId = masterPlanDetailId,
            CardNo = cardNo,
            KnitterName = knitterName,
            AssignedBy = assignedBy
        };
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<bool>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in SaveKnitterAssignmentAsync");
            throw;
        }
    }

    public async Task<List<PlanSizeLineEditDto>> GetPlanSizeLinesAsync(int masterPlanDetailId)
    {
        var url = $"api/v1/ProductionPlanning/plan-size-lines?masterPlanDetailId={masterPlanDetailId}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<PlanSizeLineEditDto>>() ?? new List<PlanSizeLineEditDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPlanSizeLinesAsync");
            throw;
        }
    }

    public async Task<SizeLineUpdateResultDto> UpdatePlanSizeLineAsync(int sizeLineId, decimal qty)
    {
        var url = $"api/v1/ProductionPlanning/plan-size-line?sizeLineId={sizeLineId}&qty={qty}";
        try
        {
            var response = await _httpClient.PutAsync(url, null);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<SizeLineUpdateResultDto>() ?? new SizeLineUpdateResultDto { Success = false };
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in UpdatePlanSizeLineAsync");
            throw;
        }
    }

    public async Task<List<KnitterAssignmentHistoryDto>> GetKnitterAssignmentHistoryAsync(int days = 30)
    {
        var url = $"api/v1/ProductionPlanning/knitter-assignment-history?days={days}";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<KnitterAssignmentHistoryDto>>() ?? new List<KnitterAssignmentHistoryDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetKnitterAssignmentHistoryAsync");
            throw;
        }
    }

    public async Task<bool> ManageKnitterAssignmentAsync(int masterPlanDetailId, string action)
    {
        var url = $"api/v1/ProductionPlanning/knitter-assignment-manage?masterPlanDetailId={masterPlanDetailId}&action={Uri.EscapeDataString(action)}";
        try
        {
            var response = await _httpClient.PostAsync(url, null);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<bool>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in ManageKnitterAssignmentAsync");
            throw;
        }
    }

    public async Task<List<KnitterBusyDto>> GetKnitterBusyAsync()
    {
        var url = "api/v1/ProductionPlanning/knitter-busy";
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<KnitterBusyDto>>() ?? new List<KnitterBusyDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetKnitterBusyAsync");
            throw;
        }
    }

    public async Task<List<PlaningReportDayDto>> GetPlaningReportAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var url = "api/v1/ProductionPlanning/planing-report";
        var queryParams = new List<string>();
        if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<PlaningReportDayDto>>() ?? new List<PlaningReportDayDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetPlaningReportAsync");
            throw;
        }
    }

    public async Task<List<KnitterStaffingDayDto>> GetKnitterStaffingAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var url = "api/v1/ProductionPlanning/knitter-staffing";
        var queryParams = new List<string>();
        if (fromDate.HasValue) queryParams.Add($"fromDate={fromDate.Value:yyyy-MM-dd}");
        if (toDate.HasValue) queryParams.Add($"toDate={toDate.Value:yyyy-MM-dd}");
        if (queryParams.Count > 0) url += "?" + string.Join("&", queryParams);

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<KnitterStaffingDayDto>>() ?? new List<KnitterStaffingDayDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetKnitterStaffingAsync");
            throw;
        }
    }

    public async Task<List<KnitterDto>> GetKnittersByGaugeAsync(string? gauge = null)
    {
        var url = "api/v1/ProductionPlanning/knitters-by-gauge";
        if (!string.IsNullOrEmpty(gauge)) url += $"?gauge={Uri.EscapeDataString(gauge)}";

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<KnitterDto>>() ?? new List<KnitterDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetKnittersByGaugeAsync");
            throw;
        }
    }

    public async Task<List<MachinePlaningDto>> GetMachinePlaningAsync(string? targetGauge = null)
    {
        var url = $"api/v1/ProductionPlanning/machine-planing?";
        var queryParams = new List<string>();
        if (!string.IsNullOrEmpty(targetGauge)) queryParams.Add($"targetGauge={Uri.EscapeDataString(targetGauge)}");

        if (queryParams.Count > 0)
        {
            url += string.Join("&", queryParams);
        }

        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<List<MachinePlaningDto>>() ?? new List<MachinePlaningDto>();
            }
            throw new HttpRequestException($"API Error {response.StatusCode}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetMachinePlaningAsync");
            throw;
        }
    }
}

