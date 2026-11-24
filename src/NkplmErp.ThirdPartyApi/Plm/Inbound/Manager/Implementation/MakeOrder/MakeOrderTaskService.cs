using System.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NkplmErp.Application.Interfaces;
using NkplmErp.ThirdPartyApi.Plm.Inbound.Manager.Interface.MakeOrder;
using NkplmErp.Shared.DataAccess.GenericRepository;
using NkplmErp.Shared.DTOs.MakeOrder;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.ThirdPartyApi.Plm.Inbound.Manager.Implementation.MakeOrder;

/// <summary>
/// Order placed in PLM -> task + notification. This service only picks who is notified (by notification
/// type); sp_PoTask_MakeOrder does the writes in one transaction, so either everything exists or nothing does.
/// </summary>
public class MakeOrderTaskService(
    IGenericRepository repository,
    IPoTaskService poTaskService,
    IRoleManagementService roleService,
    IConfiguration configuration,
    ILogger<MakeOrderTaskService> logger) : IMakeOrderTaskService
{
    private const string Sp = "sp_PoTask_MakeOrder";
    private const string NotificationType = "make_order";

    public async Task<IResponse<MakeOrderTaskResultDto>> CreateMakeOrderTaskAsync(MakeOrderTaskRequest request)
    {
        try
        {
            var assignees = await GetRecipientsAsync(NotificationType);

            var result = await repository.GetQueryFirstOrDefaultResultAsync<MakeOrderTaskResultDto>(Sp, new
            {
                Flag = "MAKE_ORDER",
                OrderNo = request.OrderNo?.Trim(),
                request.BuyerName,
                request.Collection,
                request.OrderType,
                request.ShippingDate,
                request.TotalPieces,
                request.StyleCount,
                AssigneeUserIds = assignees.Count > 0 ? string.Join("|", assignees) : null,
                DueDays = configuration.GetValue<int?>("TaskAutomation:MakeOrderTaskDueDays") ?? 3,
                UserId = "system"
            }, CommandType.StoredProcedure);

            if (result is null || result.PoTaskId <= 0)
                return Response<MakeOrderTaskResultDto>.Fail($"Task was not created for new order {request.OrderNo}.");

            // Committed: push the "assigned" notifications. Best-effort — they stay in the DB either way.
            try { await poTaskService.DispatchPendingPushesAsync(); }
            catch (Exception ex) { logger.LogWarning(ex, "Push after make-order task for {OrderNo} failed.", request.OrderNo); }

            logger.LogInformation("Make order {OrderNo}: task {TaskId} (new: {New}).", request.OrderNo, result.PoTaskId, result.WasCreated);
            return Response<MakeOrderTaskResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Creating the make-order task for {OrderNo} failed; rolled back.", request.OrderNo);
            return Response<MakeOrderTaskResultDto>.Fail(ex.Message);
        }
    }

    // Who gets the notification, chosen by its type. make_order -> the Production Manager role.
    private async Task<List<string>> GetRecipientsAsync(string notificationType)
    {
        var role = notificationType switch
        {
            NotificationType => configuration["TaskAutomation:ProductionManagerRoleName"] ?? "Production Manager",
            _ => throw new InvalidOperationException($"No recipients are defined for notification type '{notificationType}'.")
        };

        var users = await roleService.GetAllUsersWithRolesAsync();
        var ids = users.Where(u => string.Equals(u.RoleName, role, StringComparison.OrdinalIgnoreCase))
                       .Select(u => u.UserId).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
        if (ids.Count == 0)
            logger.LogWarning("Make-order task: role '{Role}' has no members; the task will be unassigned.", role);
        return ids;
    }
}
