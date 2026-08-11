using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.API.Services;

/// <summary>
/// Background sweep that fires the PO-task "+N day" reminders. Periodically calls
/// sp_PoTask_DueReminders (via IPoTaskService), which inserts an in-app reminder
/// notification for each open assignee of a task whose NotificationDate has come
/// due, then advances/closes that date. Interval is TaskAutomation:ReminderSweepSeconds
/// (default 300s). Best-effort: a sweep error is logged and the loop continues.
/// </summary>
public class PoTaskReminderService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PoTaskReminderService> _logger;

    public PoTaskReminderService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<PoTaskReminderService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var seconds = _configuration.GetValue<int?>("TaskAutomation:ReminderSweepSeconds") ?? 300;
        if (seconds < 30) seconds = 30;   // floor: don't hammer the DB
        var period = TimeSpan.FromSeconds(seconds);

        // Small initial delay so the sweep doesn't run during app start-up.
        try { await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken); }
        catch (OperationCanceledException) { return; }

        using var timer = new PeriodicTimer(period);
        do
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IPoTaskService>();

                // Pull new order reviews from the MySQL source into the local copy first
                // (best-effort: if the linked server is down, keep sweeping what we have).
                try
                {
                    var pulled = await svc.SyncOrderReviewsAsync();
                    if (pulled > 0)
                        _logger.LogInformation("Order-review sync pulled {Count} new review(s).", pulled);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Order-review sync from linked server failed; using the local copy.");
                }

                // Seed the lifecycle for newly-reviewed orders (pulled into tbl_order_review):
                // "Create plan" task -> Production Manager, "BOM" task -> Yarn role (+BOM calc).
                await ProcessOrderReviewsAsync(scope.ServiceProvider);

                // Advance per-gauge Planning tasks from knitter records (started -> P, fully returned -> C).
                var advanced = await svc.RunPlanProgressSyncAsync();
                if (advanced > 0)
                    _logger.LogInformation("PO task plan-progress sweep advanced {Count} task(s).", advanced);

                var fired = await svc.RunDueRemindersAsync();
                if (fired > 0)
                    _logger.LogInformation("PO task reminder sweep fired {Count} task(s).", fired);

                // Push the reminder notifications (and any missed ones) over SignalR.
                await svc.DispatchPendingPushesAsync();
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PO task reminder sweep failed; will retry next interval.");
            }
        }
        while (await SafeWaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> SafeWaitAsync(PeriodicTimer timer, CancellationToken token)
    {
        try { return await timer.WaitForNextTickAsync(token); }
        catch (OperationCanceledException) { return false; }
    }

    // ======================================================================
    // Order-review sweep: for every reviewed order (local tbl_order_review)
    // with no PO-Entry task yet, seed the front of the lifecycle:
    //   • "Create plan" task (Stage 1) -> Production Manager role, due +N days
    //   • "BOM" task (Stage 2) -> Yarn role, reminder +2 days, with a yarn
    //     requirement summary computed via IBomService (knitYarnRequirement)
    // Idempotent: "a Stage-1 task exists" is the processed marker (enforced by
    // sp_PoTask_PendingReviews + the CREATE dedupe). Per-order best-effort.
    // ======================================================================
    private async Task ProcessOrderReviewsAsync(IServiceProvider sp)
    {
        var svc = sp.GetRequiredService<IPoTaskService>();

        var pending = await svc.GetPendingReviewOrdersAsync();
        if (pending.Count == 0) return;

        var roleSvc = sp.GetRequiredService<IRoleManagementService>();
        var bomSvc = sp.GetRequiredService<IBomService>();

        var pmRole = _configuration["TaskAutomation:ProductionManagerRoleName"] ?? "Production Manager";
        var yarnRole = _configuration["TaskAutomation:YarnRoleName"] ?? "Yarn";
        var dueDays = _configuration.GetValue<int?>("TaskAutomation:PlanTaskDueDays") ?? 3;

        // Planning goes to Production Manager; BOM work goes to Yarn.
        var users = (await roleSvc.GetAllUsersWithRolesAsync()).ToList();
        var pms = users.Where(u => string.Equals(u.RoleName, pmRole, StringComparison.OrdinalIgnoreCase))
                       .Select(u => u.UserId).Distinct().ToList();
        var yarnUsers = users.Where(u => string.Equals(u.RoleName, yarnRole, StringComparison.OrdinalIgnoreCase))
                             .Select(u => u.UserId).Distinct().ToList();
        if (pms.Count == 0)
            _logger.LogWarning("Order-review sweep: role '{Role}' has no members; seeded tasks will be unassigned.", pmRole);
        if (yarnUsers.Count == 0)
            _logger.LogWarning("Order-review sweep: role '{Role}' has no members; BOM tasks will be unassigned.", yarnRole);

        foreach (var review in pending)
        {
            try
            {
                // ① "Create plan" -> Production Manager.
                var detail = $"Order reviewed on {review.ReviewDate:yyyy-MM-dd}." +
                             (string.IsNullOrWhiteSpace(review.Remark) ? "" : $" Remark: {review.Remark}");
                await svc.EnsurePoEntryTaskAsync(review.OrderNo, detail, pms, dueDays, "system");

                // ② BOM calculation summary (best-effort — the task is created regardless).
                string? bomSummary = null;
                try
                {
                    var bomResponse = await bomSvc.GetYarnRequirementAsync(review.OrderNo, 1);
                    var lines = bomResponse.Succeeded && bomResponse.Data is not null
                        ? bomResponse.Data.ToList()
                        : new List<BomYarnLineDto>();
                    if (lines.Count > 0)
                    {
                        var shortCount = lines.Count(l => l.ShortfallKg > 0);
                        bomSummary = shortCount > 0
                            ? $"BOM calculated: {lines.Count} yarn line(s), {shortCount} short — import needed."
                            : $"BOM calculated: {lines.Count} yarn line(s), stock covers all — no import needed.";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "BOM calc failed for {OrderNo}; creating the BOM task without a summary.", review.OrderNo);
                }

                // ③ "BOM" -> Yarn role, +2-day reminder.
                await svc.EnsureBomTaskAsync(review.OrderNo, null, yarnUsers, 2, "system", bomSummary, review.ReviewId);

                _logger.LogInformation("Order-review sweep seeded tasks for {OrderNo}.", review.OrderNo);
            }
            catch (Exception ex)
            {
                // This order retries next tick (its Stage-1 task doesn't exist yet).
                _logger.LogWarning(ex, "Order-review sweep failed for {OrderNo}; will retry.", review.OrderNo);
            }
        }
    }
}
