namespace NkplmErp.Shared.DTOs.Task_Gate
{
    // One model for both branches of sp_ManageTaskGate.
    //
    // Flag 'Q' fills the task fields and leaves UpdatedCount / Message null.
    // Flag 'S' fills UpdatedCount / Message and leaves the task fields null.
    // Everything is nullable for that reason — Dapper silently ignores columns
    // the current result set does not contain, so the unused half stays default.
    //
    // Property names match the proc's column aliases exactly.
    public class TaskGateResponseModel
    {
        // ---- queue row (flag 'Q') ----
        public int? TaskId { get; set; }
        public string? OrderNo { get; set; }
        public byte? Stage { get; set; }
        public string? StageName { get; set; }
        public string? Title { get; set; }
        public string? Detail { get; set; }
        public byte? PriorityId { get; set; }
        public string? PriorityName { get; set; }
        public DateTime? DueDate { get; set; }
        public bool? IsOverdue { get; set; }

        // ---- write result (flag 'S') ----
        public int? UpdatedCount { get; set; }
        public string? Message { get; set; }
    }
}
