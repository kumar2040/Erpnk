namespace NkplmErp.Blazor.Services.TaskManagement.Model
{
    // Mirrors the API's TaskManagementResponseModel. JSON is deserialized
    // case-insensitively, so property names just need to match.
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
        // KnitterId = knitter card_no; Issue = SUM(pics); ReturnQty = SUM(ret_pic).
        public string? KnitterId { get; set; }
        public int? Issue { get; set; }
        public int? ReturnQty { get; set; }

        // Buyer for the line's order; CustomerCode shows beside the order no,
        // CustomerName is revealed on hover.
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }

        // Comma-delimited knitter-record ids (flag 'P') — used by the return-detail chart.
        public string? RId { get; set; }

        public string? OrderStatus { get; set; }
        public string? PlaningStatus { get; set; }
        public string? PlanWorkingStatus { get; set; }
    }
}
