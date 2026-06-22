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

        // Resolved gauge / tailor name (from the SP). Shown beside the order
        // number as "OrderNo (Guage)" when present.
        public string? Guage { get; set; }

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

        // ---- Knitter-detail Progress card (one card per knitter on a job) ----
        // PO No is the existing TaskId (MasterPlanChildId). These three carry the
        // per-knitter values from tbl_knitter_record_data.
        public string? KnitterId { get; set; }   // knitter card_no
        public int? Issue { get; set; }           // pics issued to the knitter
        public int? ReturnQty { get; set; }       // ret_pic returned by the knitter

        // Buyer for the line's order. CustomerCode shows beside the order no;
        // CustomerName is revealed on hover over the code.
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
    }
}
