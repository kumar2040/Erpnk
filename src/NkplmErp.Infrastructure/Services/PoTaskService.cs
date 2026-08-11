using System.Data;
using Microsoft.Extensions.Logging;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.Repositories.Interface;
using NkplmErp.Shared.DataAccess.GenericRepository;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Infrastructure.Services;

/// <summary>
/// PO lifecycle task management. Reads go through sp_GetPoTask (flag-dispatched),
/// writes through sp_ManagePoTask (flag-dispatched). The stored procedures own
/// the rollup, the zero-trust scoping and the "update only your own row" rule —
/// this service just shapes parameters and results.
/// </summary>
public class PoTaskService : IPoTaskService
{
    private const string ReadSp = "sp_GetPoTask";
    private const string WriteSp = "sp_ManagePoTask";
    private const int MaxAttachmentBytes = 1024 * 1024;   // 1 MB

    private readonly IDapperRepository _repo;
    private readonly IGenericRepository _genericRepository;
    private readonly INotificationPublisher _publisher;
    private readonly ILogger<PoTaskService> _logger;

    public PoTaskService(
        IDapperRepository repo,
        IGenericRepository genericRepository,
        INotificationPublisher publisher,
        ILogger<PoTaskService> logger)
    {
        _repo = repo;
        _genericRepository = genericRepository;
        _publisher = publisher;
        _logger = logger;
    }

    // ----------------------------------------------------------------- reads ----

    public Task<List<PoTaskCardDto>> GetBoardAsync(
        string statusFlag, byte? stage, DateTime? startDate, DateTime? endDate,
        string? orderNo, string? factoryType, string userId) =>
        _repo.GetQueryResultAsync<PoTaskCardDto>(ReadSp, new
        {
            Flag = "BOARD",
            StatusFlag = SafeStatus(statusFlag),
            Stage = stage,
            StartDate = startDate,
            EndDate = endDate,
            OrderNo = Trim(orderNo),
            FactoryType = Trim(factoryType),
            UserId = userId
        }, CommandType.StoredProcedure);

    public Task<List<PoTaskCardDto>> GetMyTasksAsync(
        string statusFlag, byte? stage, DateTime? startDate, DateTime? endDate,
        string? orderNo, string? factoryType, string userId) =>
        _repo.GetQueryResultAsync<PoTaskCardDto>(ReadSp, new
        {
            Flag = "MYTASKS",
            StatusFlag = SafeStatus(statusFlag),
            Stage = stage,
            StartDate = startDate,
            EndDate = endDate,
            OrderNo = Trim(orderNo),
            FactoryType = Trim(factoryType),
            UserId = userId
        }, CommandType.StoredProcedure);

    public async Task<PoTaskDetailResult> GetDetailAsync(int poTaskId)
    {
        // Four result sets: task header, assignees, checklist, attachments.
        var sets = await _repo.GetFromMultipleQuery<PoTaskDetailDto, PoTaskAssigneeDto, PoTaskChecklistDto, PoTaskAttachmentDto>(
            ReadSp, new { Flag = "DETAIL", PoTaskId = poTaskId }, CommandType.StoredProcedure);

        return new PoTaskDetailResult
        {
            Task        = ((List<PoTaskDetailDto>)sets[0]).FirstOrDefault(),
            Assignees   = (List<PoTaskAssigneeDto>)sets[1],
            Checklist   = (List<PoTaskChecklistDto>)sets[2],
            Attachments = (List<PoTaskAttachmentDto>)sets[3]
        };
    }

    public Task<List<PoTaskGroupDto>> GetGroupsAsync() =>
        _repo.GetQueryResultAsync<PoTaskGroupDto>(ReadSp, new { Flag = "GROUPS" }, CommandType.StoredProcedure);

    public Task<List<PoTaskAssigneeDto>> GetAssigneesAsync(int poTaskId) =>
        _repo.GetQueryResultAsync<PoTaskAssigneeDto>(ReadSp,
            new { Flag = "ASSIGNEES", PoTaskId = poTaskId }, CommandType.StoredProcedure);

    // ---------------------------------------------------------------- writes ----

    public async Task<int> CreateAsync(CreatePoTaskRequest req, string userId)
    {
        // CREATE inserts the task AND fans out assignees in one transaction.
        var newId = await _repo.GetQueryFirstOrDefaultResultAsync<int>(WriteSp, new
        {
            Flag = "CREATE",
            req.OrderNo,
            Stage = req.Stage,                          // null => Manual (SP default 20)
            FactoryType = Trim(req.FactoryType),
            Guage = Trim(req.Guage),
            Title = Trim(req.Title),
            Detail = req.Detail,
            req.RefId,
            req.PriorityId,
            req.NotificationDate,
            req.UpdateFrequency,
            CompletionRule = req.CompletionRule,
            req.QuorumCount,
            req.PlanningAction,
            req.StartDate,
            req.DueDate,
            AssigneeUserIds = Pipe(req.UserIds),
            req.GroupId,
            UserId = userId
        }, CommandType.StoredProcedure);

        // Checklist sub-items (the Description "+" rows).
        foreach (var item in req.ChecklistItems.Where(t => !string.IsNullOrWhiteSpace(t)))
            await AddChecklistAsync(newId, item, userId);

        // Optional uploads — one PoTaskAttachment row per file; decode + re-validate the
        // 1 MB cap per file on the server.
        foreach (var att in req.Attachments.Where(a => !string.IsNullOrEmpty(a.ContentBase64)))
        {
            var bytes = Convert.FromBase64String(att.ContentBase64);
            if (bytes.Length > MaxAttachmentBytes)
                throw new InvalidOperationException($"Attachment '{att.FileName}' exceeds the 1 MB limit.");

            await _repo.ExecuteAsync(WriteSp, new
            {
                Flag = "ATTACH",
                PoTaskId = newId,
                FileName = att.FileName,
                ContentType = att.ContentType,
                SizeBytes = bytes.Length,
                Content = bytes,
                UserId = userId
            }, CommandType.StoredProcedure);
        }

        await DispatchPendingPushesAsync();   // real-time push for the "assigned" notifications just created
        return newId;
    }

    public async Task AssignAsync(AssignPoTaskRequest req, string userId)
    {
        await _repo.ExecuteAsync(WriteSp, new
        {
            Flag = "ASSIGN",
            req.PoTaskId,
            AssigneeUserIds = Pipe(req.UserIds),
            req.GroupId,
            UserId = userId
        }, CommandType.StoredProcedure);

        await DispatchPendingPushesAsync();
    }

    // The SP's UNASSIGN flag soft-removes (PoTaskId, @UserId)'s active row and re-rolls
    // the parent up — @UserId here is the user being REMOVED, not the actor.
    public Task UnassignAsync(int poTaskId, string targetUserId) =>
        _repo.ExecuteAsync(WriteSp, new
        {
            Flag = "UNASSIGN",
            PoTaskId = poTaskId,
            UserId = targetUserId
        }, CommandType.StoredProcedure);

    public Task<PoTaskAttachmentContentDto?> GetAttachmentAsync(int attachmentId) =>
        _repo.GetQueryFirstOrDefaultResultAsync<PoTaskAttachmentContentDto?>(
            "sp_PoTask_GetAttachment", new { AttachmentId = attachmentId }, CommandType.StoredProcedure);

    public Task<List<PoTaskAttachmentCountDto>> GetAttachmentCountsAsync() =>
        _repo.GetQueryResultAsync<PoTaskAttachmentCountDto>(
            "sp_PoTask_AttachmentCounts", new { }, CommandType.StoredProcedure);

    public Task<List<PoTaskStaffDto>> GetStaffAsync() =>
        _repo.GetQueryResultAsync<PoTaskStaffDto>("sp_PoTask_Staff", new { }, CommandType.StoredProcedure);

    public Task<List<PoOrderReviewRankDto>> GetReviewRanksAsync() =>
        _repo.GetQueryResultAsync<PoOrderReviewRankDto>("sp_PoTask_ReviewRanks", new { }, CommandType.StoredProcedure);

    public async Task<PoTaskAgingReportResult> GetAgingReportAsync(DateTime? startDate, DateTime? endDate)
    {
        // Two result sets: per-stage aggregates, then the slowest open tasks.
        var sets = await _repo.GetFromMultipleQuery<PoTaskAgingStageDto, PoTaskAgingOpenDto>(
            "sp_PoTask_AgingReport", new { StartDate = startDate, EndDate = endDate }, CommandType.StoredProcedure);

        return new PoTaskAgingReportResult
        {
            Stages      = (List<PoTaskAgingStageDto>)sets[0],
            SlowestOpen = (List<PoTaskAgingOpenDto>)sets[1]
        };
    }

    public Task MyUpdateAsync(MyUpdatePoTaskRequest req, string userId) =>
        _repo.ExecuteAsync(WriteSp, new
        {
            Flag = "MYUPDATE",
            req.PoTaskId,
            ToStatus = SafeWriteStatus(req.ToStatus),
            req.Note,
            UserId = userId                              // SP moves ONLY this user's row
        }, CommandType.StoredProcedure);

    public Task TransitionAsync(TransitionPoTaskRequest req, string userId) =>
        _repo.ExecuteAsync(WriteSp, new
        {
            Flag = "SETSTATUS",
            req.PoTaskId,
            ToStatus = SafeWriteStatus(req.ToStatus),
            req.Note,
            UserId = userId
        }, CommandType.StoredProcedure);

    public Task HoldAsync(HoldPoTaskRequest req, string userId) =>
        _repo.ExecuteAsync(WriteSp, new
        {
            Flag = "HOLD",
            req.PoTaskId,
            req.BlockedReason,
            UserId = userId
        }, CommandType.StoredProcedure);

    public Task ResolveAsync(int poTaskId, string userId) =>
        _repo.ExecuteAsync(WriteSp, new { Flag = "RESOLVE", PoTaskId = poTaskId, UserId = userId },
            CommandType.StoredProcedure);

    public Task CancelAsync(int poTaskId, string? note, string userId) =>
        _repo.ExecuteAsync(WriteSp, new { Flag = "CANCEL", PoTaskId = poTaskId, Note = note, UserId = userId },
            CommandType.StoredProcedure);

    public Task<int> RaiseExceptionAsync(RaiseExceptionRequest req, string userId) =>
        _repo.GetQueryFirstOrDefaultResultAsync<int>(WriteSp, new
        {
            Flag = "EXCEPTION",
            req.OrderNo,
            Stage = req.Stage,
            Title = Trim(req.Title),
            Detail = req.Detail,
            FactoryType = Trim(req.FactoryType),
            Guage = Trim(req.Guage),
            PoTaskId = req.RelatedPoTaskId,              // optional: hold this open linear task
            UserId = userId
        }, CommandType.StoredProcedure);

    public Task AddChecklistAsync(int poTaskId, string text, string userId) =>
        _repo.ExecuteAsync(WriteSp, new
        {
            Flag = "CHECKLIST_ADD",
            PoTaskId = poTaskId,
            Detail = text,
            UserId = userId
        }, CommandType.StoredProcedure);

    public Task ToggleChecklistAsync(int checklistId, string userId) =>
        _repo.ExecuteAsync(WriteSp, new
        {
            Flag = "CHECKLIST_TOGGLE",
            ChecklistId = checklistId,
            UserId = userId
        }, CommandType.StoredProcedure);

    public Task SnapshotAsync(PoPlanParamRequest req, string userId) =>
        _repo.ExecuteAsync(WriteSp, new
        {
            Flag = "SNAPSHOT",
            req.OrderNo,
            req.ParamJson,
            UserId = userId
        }, CommandType.StoredProcedure);

    public async Task<bool> AlertCheckAsync(PoPlanParamRequest req, string userId) =>
        await _repo.GetQueryFirstOrDefaultResultAsync<bool>(WriteSp, new
        {
            Flag = "ALERTCHECK",
            req.OrderNo,
            req.ParamJson,
            UserId = userId
        }, CommandType.StoredProcedure);

    // -------------------------------------------------------- automation hooks ----

    public Task<int> EnsurePlanningTaskAsync(string orderNo, string? factoryType, string? guage, int? refId, IEnumerable<string> assigneeUserIds, string userId) =>
        CreateAsync(new CreatePoTaskRequest
        {
            OrderNo = orderNo,
            Stage = 3,                               // Planning
            FactoryType = factoryType,
            Guage = guage,
            Title = string.IsNullOrWhiteSpace(guage) ? $"Planning — {orderNo}" : $"Planning — {orderNo} ({guage})",
            Detail = "Auto-created when a plan was saved.",
            PlanningAction = 1,                      // NewPlan
            RefId = refId,                           // MasterPlanChildId (the gauge line)
            StartDate = DateTime.Today,
            CompletionRule = 1,
            UserIds = assigneeUserIds?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList() ?? new()
        }, userId);

    public async Task<int> EnsureBomTaskAsync(string orderNo, string? factoryType, IEnumerable<string> assigneeUserIds, int notifyAfterDays, string userId, string? detail = null, int? reviewId = null)
    {
        var result = await _genericRepository.GetQueryFirstOrDefaultResultAsync<PoTaskBomAttachResultDto>(
            WriteSp, new
        {
            Flag = "BOMATTACH",
            OrderNo = orderNo,
            FactoryType = factoryType,
            Detail = detail,
            AssigneeUserIds = string.Join("|", assigneeUserIds?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct() ?? Enumerable.Empty<string>()),
            UserId = userId,
            ReviewId = reviewId,
            NotifyAfterDays = notifyAfterDays
        }, CommandType.StoredProcedure);
        if (result is null || result.PoTaskId <= 0)
            throw new InvalidOperationException($"BOM task attach/create returned no task id for order {orderNo}.");

        return result.PoTaskId;
    }

    public Task<List<PoOrderReviewDto>> GetPendingReviewOrdersAsync() =>
        _genericRepository.GetQueryResultAsync<PoOrderReviewDto>(ReadSp,
            new { Flag = "PENDINGREVIEWS", Top = 50 }, CommandType.StoredProcedure);

    public Task<int> EnsurePoEntryTaskAsync(string orderNo, string? detail, IEnumerable<string> assigneeUserIds, int dueDays, string userId) =>
        CreateAsync(new CreatePoTaskRequest
        {
            OrderNo = orderNo,
            Stage = 1,                               // PoEntry — "Create plan"
            Title = $"{orderNo} reviewed — create production plan",
            Detail = detail,
            StartDate = DateTime.Today,
            DueDate = DateTime.Today.AddDays(dueDays),
            CompletionRule = 2,                      // Any: one PM planning it completes it for all
            UserIds = assigneeUserIds?.Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().ToList() ?? new()
        }, userId);

    public Task<int> CompleteStageAsync(string orderNo, byte stage, string? note, string userId) =>
        _repo.GetQueryFirstOrDefaultResultAsync<int>("sp_PoTask_CompleteStage",
            new { OrderNo = orderNo, Stage = stage, Note = note, UserId = userId },
            CommandType.StoredProcedure);

    public async Task<IResponse<PoTaskBomCompleteResultDto>> CompleteBomOrderAsync(int poTaskId, string orderNo, string? note, string userId)
    {
        try
        {
            var result = await _genericRepository.GetQueryFirstOrDefaultResultAsync<PoTaskBomCompleteResultDto>(WriteSp,
                new { Flag = "BOMCOMPLETE", PoTaskId = poTaskId, OrderNo = orderNo, Note = note, UserId = userId },
                CommandType.StoredProcedure);
            return Response<PoTaskBomCompleteResultDto>.Success(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Completing BOM order {OrderNo} on task {PoTaskId} failed.", orderNo, poTaskId);
            return Response<PoTaskBomCompleteResultDto>.Fail(ex.Message);
        }
    }

    // -------------------------------------------------------- notifications ----

    private const string NotifySp = "sp_ManagePoTaskNotification";

    public Task<List<PoTaskNotificationDto>> GetNotificationsAsync(string userId, int top = 30) =>
        _repo.GetQueryResultAsync<PoTaskNotificationDto>(NotifySp,
            new { Flag = "LIST", UserId = userId, Top = top }, CommandType.StoredProcedure);

    public Task<int> GetUnreadCountAsync(string userId) =>
        _repo.GetQueryFirstOrDefaultResultAsync<int>(NotifySp,
            new { Flag = "UNREAD", UserId = userId }, CommandType.StoredProcedure);

    public Task MarkNotificationReadAsync(int notificationId, string userId) =>
        _repo.ExecuteAsync(NotifySp,
            new { Flag = "MARKREAD", NotificationId = notificationId, UserId = userId }, CommandType.StoredProcedure);

    public Task MarkAllNotificationsReadAsync(string userId) =>
        _repo.ExecuteAsync(NotifySp,
            new { Flag = "MARKALLREAD", UserId = userId }, CommandType.StoredProcedure);

    public Task<int> RunDueRemindersAsync() =>
        _repo.GetQueryFirstOrDefaultResultAsync<int>("sp_PoTask_DueReminders", new { }, CommandType.StoredProcedure);

    public Task<int> RunPlanProgressSyncAsync() =>
        _repo.GetQueryFirstOrDefaultResultAsync<int>("sp_PoTask_SyncPlanProgress", new { }, CommandType.StoredProcedure);

    // Pull new order reviews from the MySQL source (linked server) into the local
    // tbl_order_review copy the pending-review sweep reads. Idempotent by id.
    public Task<int> SyncOrderReviewsAsync() =>
        _repo.GetQueryFirstOrDefaultResultAsync<int>("sp_SyncOrderReviews", new { }, CommandType.StoredProcedure);

    // Outbox drain: push every not-yet-pushed notification to its recipient over SignalR,
    // then mark them pushed. Called after writes that create notifications and by the
    // background sweep. Best-effort — persisted notifications still show via the reads.
    public async Task DispatchPendingPushesAsync()
    {
        var pending = await _repo.GetQueryResultAsync<PoTaskNotificationDto>(NotifySp,
            new { Flag = "PENDING", Top = 200 }, CommandType.StoredProcedure);
        if (pending.Count == 0) return;

        foreach (var n in pending.Where(n => !string.IsNullOrEmpty(n.UserId)))
            await _publisher.PushAsync(n.UserId!, n);

        var ids = string.Join("|", pending.Select(p => p.NotificationId));
        await _repo.ExecuteAsync(NotifySp, new { Flag = "MARKPUSHED", Ids = ids }, CommandType.StoredProcedure);
    }

    // ----------------------------------------------------------------- helpers ----

    private static string? Trim(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // Pipe-join user ids for the SP's STRING_SPLIT; null when empty (no fan-out).
    private static string? Pipe(IEnumerable<string>? ids)
    {
        var list = ids?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).Distinct().ToList();
        return list is { Count: > 0 } ? string.Join("|", list) : null;
    }

    // Read buckets: S/P/C/O/H (default S).
    private static string SafeStatus(string? flag)
    {
        var f = flag?.Trim().ToUpperInvariant();
        return f is "P" or "C" or "O" or "H" ? f : "S";
    }

    // Stored statuses a caller may set: S/P/C/H/X (default S).
    private static string SafeWriteStatus(string? status)
    {
        var s = status?.Trim().ToUpperInvariant();
        return s is "P" or "C" or "H" or "X" ? s : "S";
    }
}
