using NkplmErp.Application.Interfaces;

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
}
