namespace NkplmErp.API.Controllers.TaskManagement.Model
{
    // One production plan line (a "task") returned by spTaskManagement.
    // Property names match the SP's aliased columns so Dapper maps by name.
    public class TaskManagementResponseModel
    {
        public int TaskId { get; set; }

        public string? OrderNo { get; set; }
        public string? OrderType { get; set; }
        public string? ProductionType { get; set; }
        public string? FactoryType { get; set; }

        public string? Machine { get; set; }
        public int? MachineCount { get; set; }
        public string? Guage { get; set; }
        public int? Qty { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        // Knitter-detail (In Progress card): one row per (line, knitter).
        // KnitterId = tbl_knitter_record_data.knitter (card_no);
        // Issue = SUM(pics); ReturnQty = SUM(ret_pic). Null for other flags.
        public string? KnitterId { get; set; }
        public int? Issue { get; set; }
        public int? ReturnQty { get; set; }

        // Buyer for the line's order (size -> order -> customer). Shown beside the
        // order no (code) with the name revealed on hover.
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }

        public string? OrderStatus { get; set; }
        public string? PlaningStatus { get; set; }
        public string? PlanWorkingStatus { get; set; }
    }
}
