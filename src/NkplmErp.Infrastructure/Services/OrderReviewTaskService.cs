using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DataAccess.GenericRepository;
using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Infrastructure.Services;

/// <summary>
/// Order reviewed in PLM -> Planning + BOM tasks, the same tasks the old background sweep created
/// (PoTaskReminderService), but pushed in by PLM instead of pulled over the linked server.
/// This service only gathers the inputs (Production Manager assignees, BOM summary); the procedure
/// does the writes in one transaction, so either both tasks exist or neither does.
/// </summary>
public class OrderReviewTaskService(
    IGenericRepository repository,
    IPoTaskService poTaskService,
    IRoleManagementService roleService,
    IBomService bomService,
    IConfiguration configuration,
    ILogger<OrderReviewTaskService> logger) : IOrderReviewTaskService
{
    private const string CreateSp = "sp_PoTask_CreateFromOrderReview";

    public async Task<IResponse<OrderReviewTaskResultDto>> CreateOrderReviewTasksAsync(OrderReviewTaskRequest request)
    {
        try
        {
            var assignees = await GetProductionManagersAsync();
            var bomSummary = await GetBomSummaryAsync(request.OrderNo);

            var result = await repository.GetQueryFirstOrDefaultResultAsync<OrderReviewTaskResultDto>(CreateSp, new
            {
                request.ReviewId,
                OrderNo = request.OrderNo?.Trim(),
                request.Remark,
                request.ReviewDate,
                request.ReviewUser,
                request.Pc,
                AssigneeUserIds = assignees.Count > 0 ? string.Join("|", assignees) : null,
                PlanDueDays = configuration.GetValue<int?>("TaskAutomation:PlanTaskDueDays") ?? 3,
                BomNotifyAfterDays = 2,
                BomDetail = bomSummary,
                UserId = "system"
            }, CommandType.StoredProcedure);

            if (result is null || result.PlanPoTaskId <= 0 || result.BomPoTaskId <= 0)
                return Response<OrderReviewTaskResultDto>.Fail($"Planning / BOM task was not created for order {request.OrderNo}.");

            // Committed: push the "assigned" notifications. Best-effort — they stay in the DB either way.
            try { await poTaskService.DispatchPendingPushesAsync(); }
            catch (Exception ex) { logger.LogWarning(ex, "Push after order-review tasks for {OrderNo} failed.", request.OrderNo); }

            logger.LogInformation("Order review {ReviewId} ({OrderNo}): plan task {Plan}, BOM task {Bom}.",
                request.ReviewId, request.OrderNo, result.PlanPoTaskId, result.BomPoTaskId);
            return Response<OrderReviewTaskResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Creating order-review tasks for {OrderNo} (review {ReviewId}) failed; rolled back.",
                request.OrderNo, request.ReviewId);
            return Response<OrderReviewTaskResultDto>.Fail(ex.Message);
        }
    }

    // Planning and BOM work go to the Production Manager role (same rule as the sweep).
    private async Task<List<string>> GetProductionManagersAsync()
    {
        var role = configuration["TaskAutomation:ProductionManagerRoleName"] ?? "Production Manager";
        var users = await roleService.GetAllUsersWithRolesAsync();
        var ids = users.Where(u => string.Equals(u.RoleName, role, StringComparison.OrdinalIgnoreCase))
                       .Select(u => u.UserId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0)
            logger.LogWarning("Order-review tasks: role '{Role}' has no members; tasks will be unassigned.", role);
        return ids;
    }

    // Read-only BOM calculation for the task's detail line. Best-effort: the task is created without it.
    private async Task<string?> GetBomSummaryAsync(string orderNo)
    {
        try
        {
            var response = await bomService.GetYarnRequirementAsync(orderNo, 1);
            var lines = response.Succeeded && response.Data is not null ? response.Data : new List<BomYarnLineDto>();
            if (lines.Count == 0) return null;
            var shortCount = lines.Count(l => l.ShortfallKg > 0);
            return shortCount > 0
                ? $"BOM calculated: {lines.Count} yarn line(s), {shortCount} short — import needed."
                : $"BOM calculated: {lines.Count} yarn line(s), stock covers all — no import needed.";
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "BOM calc failed for {OrderNo}; creating the BOM task without a summary.", orderNo);
            return null;
        }
    }
}
