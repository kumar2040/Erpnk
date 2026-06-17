namespace NkplmErp.API.Controllers.TaskManagement.Model
{
    // Request shape for the task board. Flag selects the column:
    //   S = Scheduled, P = In Progress, C = Completed.
    // StartDate/EndDate are reserved for date-range filtering once the SP
    // accepts them; unused for now.
    public class TaskManagementRequestModel
    {
        public string Flag { get; set; } = "S";
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
    }
}
