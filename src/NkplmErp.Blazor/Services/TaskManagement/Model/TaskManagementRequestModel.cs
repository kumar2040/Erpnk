namespace NkplmErp.Blazor.Services.TaskManagement.Model
{
    // Flag selects the column: S = Scheduled, P = In Progress, C = Completed.
    public class TaskManagementRequestModel
    {
        public string Flag { get; set; } = "S";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
