namespace NkplmErp.Blazor.Model.Task_Gate
{
    // Client-side twin of the API's TaskGateResponseModel. Blazor has no project
    // reference to NkplmErp.API, so each side keeps its own copy of the shape.
    //
    // Flag 'Q' fills the task fields; flag 'S' fills UpdatedCount / Message.
    // Everything is nullable so one model can carry both.
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
