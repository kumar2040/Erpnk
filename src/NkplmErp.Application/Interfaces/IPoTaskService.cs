using NkplmErp.Shared.DTOs;

namespace NkplmErp.Application.Interfaces;

/// <summary>
/// PO lifecycle task management (the new /tasks board). Persisted tasks in
/// dbo.PoTask with fan-out assignees (dbo.PoTaskAssignee) rolled up by a
/// per-task CompletionRule. All data access goes through sp_GetPoTask (reads)
/// and sp_ManagePoTask (writes). Distinct from the derived knitting board
/// (spTaskManagement) that powers the existing /task page.
/// </summary>
public interface IPoTaskService
{
    // ---- reads ----

    /// <summary>One board column for the whole org, scoped to the caller's factory.</summary>
    Task<List<PoTaskCardDto>> GetBoardAsync(
        string statusFlag, byte? stage, DateTime? startDate, DateTime? endDate,
        string? orderNo, string? factoryType, string userId);

    /// <summary>One column of the caller's OWN assignments (their own status drives the bucket).</summary>
    Task<List<PoTaskCardDto>> GetMyTasksAsync(
        string statusFlag, byte? stage, DateTime? startDate, DateTime? endDate,
        string? orderNo, string userId);

    /// <summary>Full task header + assignees + checklist + attachments for the drawer.</summary>
    Task<PoTaskDetailResult> GetDetailAsync(int poTaskId);

    /// <summary>Assignment-target groups for the Add Task form.</summary>
    Task<List<PoTaskGroupDto>> GetGroupsAsync();

    /// <summary>Active assignees of one task.</summary>
    Task<List<PoTaskAssigneeDto>> GetAssigneesAsync(int poTaskId);

    // ---- writes ----

    /// <summary>Create a task (manual or stage), fan out assignees, attach a file — one round-trip. Returns the new id.</summary>
    Task<int> CreateAsync(CreatePoTaskRequest request, string userId);

    /// <summary>Add individual users and/or a group's members to a task.</summary>
    Task AssignAsync(AssignPoTaskRequest request, string userId);

    /// <summary>"Update my side": move ONLY the acting user's own assignee row, then roll up.</summary>
    Task MyUpdateAsync(MyUpdatePoTaskRequest request, string userId);

    /// <summary>Admin / single-owner parent status override.</summary>
    Task TransitionAsync(TransitionPoTaskRequest request, string userId);

    /// <summary>Park a task (On hold) with a reason.</summary>
    Task HoldAsync(HoldPoTaskRequest request, string userId);

    /// <summary>Clear a hold and recompute from assignees.</summary>
    Task ResolveAsync(int poTaskId, string userId);

    /// <summary>Cancel a task (terminal).</summary>
    Task CancelAsync(int poTaskId, string? note, string userId);

    /// <summary>Raise a Yarn issue / Product return against a PO; optionally hold the open linear task. Returns the new exception id.</summary>
    Task<int> RaiseExceptionAsync(RaiseExceptionRequest request, string userId);

    /// <summary>Append a checklist sub-item.</summary>
    Task AddChecklistAsync(int poTaskId, string text, string userId);

    /// <summary>Toggle a checklist item done/undone.</summary>
    Task ToggleChecklistAsync(int checklistId, string userId);

    /// <summary>Capture the production-parameter snapshot (call when Planning completes).</summary>
    Task SnapshotAsync(PoPlanParamRequest request, string userId);

    /// <summary>Compare current PO params to the latest snapshot; raises a change alert if they differ. Returns whether it changed.</summary>
    Task<bool> AlertCheckAsync(PoPlanParamRequest request, string userId);

    // ---- automation hooks (called by other services on save events) ----

    /// <summary>
    /// Auto-create the Planning-stage task for a plan LINE (one gauge) when a plan is
    /// saved, assigned to the given users (the master role's members). Keyed per
    /// (order, gauge line) via refId = MasterPlanChildId — idempotent per line.
    /// </summary>
    Task<int> EnsurePlanningTaskAsync(string orderNo, string? factoryType, string? guage, int? refId, IEnumerable<string> assigneeUserIds, string userId);

    /// <summary>
    /// Auto-create the BOM-stage task for an order when a BOM / yarn order is created,
    /// assigned to the given users (e.g. a yarn role's members), with the first reminder
    /// scheduled <paramref name="notifyAfterDays"/> days out. Idempotent per order.
    /// No RefId: the board derives the task's yarn order from its OrderNo (see sp_GetPoTask's
    /// LinkId), so nothing has to be stored here — and dedupe stays per (OrderNo, Stage).
    /// </summary>
    Task<int> EnsureBomTaskAsync(string orderNo, string? factoryType, IEnumerable<string> assigneeUserIds, int notifyAfterDays, string userId);

    // ---- in-app notifications (bell) ----

    /// <summary>The user's recent notifications (newest first).</summary>
    Task<List<PoTaskNotificationDto>> GetNotificationsAsync(string userId, int top = 30);

    /// <summary>Count of the user's unread notifications (for the bell badge).</summary>
    Task<int> GetUnreadCountAsync(string userId);

    /// <summary>Mark one of the user's notifications read.</summary>
    Task MarkNotificationReadAsync(int notificationId, string userId);

    /// <summary>Mark all of the user's notifications read.</summary>
    Task MarkAllNotificationsReadAsync(string userId);

    /// <summary>Fire any due "+N day" reminders (called by the background sweep). Returns how many tasks fired.</summary>
    Task<int> RunDueRemindersAsync();

    /// <summary>
    /// Advance per-gauge Planning tasks based on knitter records (tbl_knitter_record_data):
    /// started -> In Progress, fully returned -> Completed. Called by the background sweep.
    /// Returns how many tasks changed.
    /// </summary>
    Task<int> RunPlanProgressSyncAsync();

    /// <summary>Push any not-yet-pushed notifications over SignalR, then mark them pushed (outbox drain).</summary>
    Task DispatchPendingPushesAsync();
}
