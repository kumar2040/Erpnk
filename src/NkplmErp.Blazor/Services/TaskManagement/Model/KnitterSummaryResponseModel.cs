namespace NkplmErp.Blazor.Services.TaskManagement.Model
{
    // Mirrors the API's KnitterSummaryResponseModel (spTaskManagement flag 'KH'):
    // one aggregated summary row for a line (MasterPlanChildId) — buyer, issued /
    // returned, order qty, machines, planned dates and the chart's RId key.
    public class KnitterSummaryResponseModel
    {
        public int TaskId { get; set; }
        public string? OrderNo { get; set; }
        public int? Qty { get; set; }
        public int? Issue { get; set; }
        public int? ReturnQty { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public int? MachineCount { get; set; }
        public string? CustomerCode { get; set; }
        public string? CustomerName { get; set; }
        public string? RId { get; set; }
    }
}
