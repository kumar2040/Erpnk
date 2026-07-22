namespace NkplmErp.Shared.DTOs.TaskManagement
{
    // Request shape for the task board. Flag selects the column:
    //   S = Scheduled, P = In Progress, C = Completed, O = Overdue.
    // StartDate/EndDate are the selected period (the O flag overlaps this window like S/P/C).
    public class TaskManagementRequestModel
    {
        public string Flag { get; set; } = "S";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
