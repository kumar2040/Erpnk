namespace NkplmErp.Shared.DTOs;

// ============================================================================
// PO lifecycle task management DTOs (new /tasks page).
// Shapes mirror the sp_GetPoTask / sp_ManagePoTask result columns. Dapper maps
// by name (case-insensitive); the SP aliases columns without underscores so
// these property names line up directly.
// ============================================================================

// One card on the board / my-tasks list.
public class PoTaskCardDto
{
    public int TaskId { get; set; }
    public string? OrderNo { get; set; }
    public byte Stage { get; set; }
    public string? LinkUrl { get; set; }          // ready-to-navigate relative URL, built by sp_GetPoTask; null = not clickable
    public string? StageName { get; set; }
    public string? Title { get; set; }
    public string? FactoryType { get; set; }
    public string? Guage { get; set; }
    public byte? PriorityId { get; set; }
    public string? PriorityName { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public byte CompletionRule { get; set; }
    public string? Status { get; set; }          // stored S/P/C/H/X
    public int AssigneeTotal { get; set; }
    public int AssigneeDone { get; set; }
    public string? MyStatus { get; set; }         // caller's own assignee status (my-tasks only)
    public string? DisplayFlag { get; set; }      // S/P/C/O/H bucket the SP placed this card in
}

// Full task header for the detail drawer.
public class PoTaskDetailDto
{
    public int PoTaskId { get; set; }
    public string? OrderNo { get; set; }
    public byte Stage { get; set; }
    public string? StageName { get; set; }
    public string? Status { get; set; }
    public string? StatusName { get; set; }
    public string? FactoryType { get; set; }
    public string? Guage { get; set; }
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public int? RefId { get; set; }
    public byte? PriorityId { get; set; }
    public string? PriorityName { get; set; }
    public DateTime? NotificationDate { get; set; }
    public byte? UpdateFrequency { get; set; }
    public byte? PlanningAction { get; set; }
    public byte CompletionRule { get; set; }
    public int? QuorumCount { get; set; }
    public string? BlockedReason { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime? CreatedDate { get; set; }
}

// One assignee row (fan-out). Status is THIS person's own progress.
public class PoTaskAssigneeDto
{
    public int AssigneeId { get; set; }
    public int PoTaskId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Status { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? Note { get; set; }
    public DateTime? AssignedDate { get; set; }
}

public class PoTaskChecklistDto
{
    public int ChecklistId { get; set; }
    public string Text { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public int SortOrder { get; set; }
}

public class PoTaskAttachmentDto
{
    public int AttachmentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public int SizeBytes { get; set; }
    public string? UploadedBy { get; set; }
    public DateTime? UploadedDate { get; set; }
}

// One in-app notification (bell).
public class PoTaskNotificationDto
{
    public int NotificationId { get; set; }
    public string? UserId { get; set; }     // recipient — populated only on the outbox (PENDING) read
    public int? PoTaskId { get; set; }
    public string? Kind { get; set; }       // 'A' assigned, 'R' reminder
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedDate { get; set; }
}

public class PoTaskGroupDto
{
    public int GroupId { get; set; }
    public string GroupName { get; set; } = string.Empty;
    public string? FactoryType { get; set; }
    public int MemberCount { get; set; }
}

// One reviewed order not yet seeded into the lifecycle (sp_PoTask_PendingReviews).
public class PoOrderReviewDto
{
    public string OrderNo { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public DateTime? ReviewDate { get; set; }
}

// Bundled result of the DETAIL read (task + assignees + checklist + attachments).
public class PoTaskDetailResult
{
    public PoTaskDetailDto? Task { get; set; }
    public List<PoTaskAssigneeDto> Assignees { get; set; } = new();
    public List<PoTaskChecklistDto> Checklist { get; set; } = new();
    public List<PoTaskAttachmentDto> Attachments { get; set; } = new();
}

// ---------------------------------------------------------------- requests ----

// One optional file attached when creating a task (base64 over the wire; the
// server decodes, re-validates <= 1 MB, then stores the bytes).
public class PoTaskAttachmentUpload
{
    public string FileName { get; set; } = string.Empty;
    public string? ContentType { get; set; }
    public string ContentBase64 { get; set; } = string.Empty;
}

// The "Add Task" form payload.
public class CreatePoTaskRequest
{
    public string? OrderNo { get; set; }
    public byte? Stage { get; set; }              // null => Manual (20)
    public string? FactoryType { get; set; }
    public string? Guage { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public int? RefId { get; set; }               // optional source row id (e.g. MasterPlanChildId)
    public byte? PriorityId { get; set; }         // 1=Low 2=Medium 3=High 4=Urgent
    public DateTime? NotificationDate { get; set; }
    public byte? UpdateFrequency { get; set; }    // 0=None 1=Daily 2=Weekly 3=Biweekly 4=Monthly
    public byte? CompletionRule { get; set; }     // 1=All 2=Any 3=Quorum
    public int? QuorumCount { get; set; }
    public byte? PlanningAction { get; set; }      // Stage=Planning: 1=NewPlan 2=Modification 3=Progress 4=Cancel 5=ChangeInPo
    public DateTime? StartDate { get; set; }
    public DateTime? DueDate { get; set; }

    // Assignment: either individual staff (UserIds) or a Group (its members fan out).
    public List<string> UserIds { get; set; } = new();
    public int? GroupId { get; set; }

    // Optional checklist sub-items (the Description "+" rows).
    public List<string> ChecklistItems { get; set; } = new();

    // Optional single upload (< 1 MB).
    public PoTaskAttachmentUpload? Attachment { get; set; }
}

public class AssignPoTaskRequest
{
    public int PoTaskId { get; set; }
    public List<string> UserIds { get; set; } = new();
    public int? GroupId { get; set; }
}

// Move the caller's OWN assignee row (server takes the user from the token).
public class MyUpdatePoTaskRequest
{
    public int PoTaskId { get; set; }
    public string ToStatus { get; set; } = "P";   // S/P/C/H
    public string? Note { get; set; }
}

// Admin / single-owner parent status override.
public class TransitionPoTaskRequest
{
    public int PoTaskId { get; set; }
    public string ToStatus { get; set; } = "C";    // S/P/C/H/X
    public string? Note { get; set; }
}

public class HoldPoTaskRequest
{
    public int PoTaskId { get; set; }
    public string? BlockedReason { get; set; }
}

// Raise a Yarn issue (Stage 10) or Product return (Stage 11) against a PO.
public class RaiseExceptionRequest
{
    public string? OrderNo { get; set; }
    public byte Stage { get; set; } = 10;           // 10=Yarn issue, 11=Product return
    public string? Title { get; set; }
    public string? Detail { get; set; }
    public string? FactoryType { get; set; }
    public string? Guage { get; set; }
    public int? RelatedPoTaskId { get; set; }       // optional: hold this open linear task
}

// Capture / check the production-parameter hash for the change alert.
public class PoPlanParamRequest
{
    public string OrderNo { get; set; } = string.Empty;
    public string ParamJson { get; set; } = string.Empty;
}

// ---------------------------------------------------------------- results ----

public class CreatePoTaskResult
{
    public int PoTaskId { get; set; }
}

public class AlertCheckResult
{
    public bool Changed { get; set; }
}
