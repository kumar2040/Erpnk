namespace NkplmErp.API.Model.TaskManagement
{
    // One aggregated summary row for a single production line (MasterPlanChildId),
    // returned by spTaskManagement flag 'KH'. Feeds the order return-detail modal's
    // left panel (buyer + Issued/Returned/Order Qty/Machines) and the RId used to
    // load the return-pace chart. Property names match the SP's aliased columns.
    public class KnitterSummaryResponseModel
    {
        public int TaskId { get; set; }          // MasterPlanChildId (the gauge line)
        public string? OrderNo { get; set; }
        public int? Qty { get; set; }             // order qty (mpd.Qty)
        public int? Issue { get; set; }           // SUM(pics) issued
        public int? ReturnQty { get; set; }       // SUM(ret_pic) returned
        public DateTime? StartDate { get; set; }  // MIN(knd) — null if no knitting yet
        public DateTime? EndDate { get; set; }    // MAX(will_ret_daate)/knd
        public int? MachineCount { get; set; }    // per-order knit-machine count
        public string? CustomerCode { get; set; } // buyer code
        public string? CustomerName { get; set; } // buyer name
        public string? RId { get; set; }          // comma-delimited knitter-record ids (chart key); null if none
    }
}
