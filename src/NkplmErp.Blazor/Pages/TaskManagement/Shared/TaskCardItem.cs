namespace NkplmErp.Blazor.Pages.TaskManagement.Shared
{
    // Simple model that one TaskCard column renders. Pass a list of these and
    // the card draws the name / assignee / dates / priority itself.
    public class TaskCardItem
    {
        public int TaskId { get; set; }
        public string TaskName { get; set; } = string.Empty;

        // Order number (knitting) — shown on the card body line.
        public string? OrderNo { get; set; }

        // true  -> show the team name, false -> show the staff name
        public bool IsTeam { get; set; }
        public string? StaffName { get; set; }
        public string? TeamName { get; set; }

        // Number of machines used (knitting); shown in place of a machine name.
        public int? MachineCount { get; set; }

        public string? Assignee { get; set; }

        public DateTime TaskStartDate { get; set; }
        public DateTime TaskEndDate { get; set; }

        public int RecurringTypeId { get; set; }
        public string? StatusName { get; set; }

        public int PriorityId { get; set; }
        public string? PriorityName { get; set; }

        // Production quantity (knitting). Rendered as a neutral badge when set.
        public int? Qty { get; set; }
    }
}
