namespace NkplmErp.Shared.DTOs.OrderPlanningApi;

/// <summary>
/// One size line of a knitter assignment, sent to PLM (route "orderplanning/knitter-assignment") so it
/// lands in tbl_production_plan_detail. Dates are yyyy-MM-dd text; PLM validates and converts.
/// </summary>
public class KnitterAssignmentLineModel
{
    public int SizeLineId { get; set; }
    public int? OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public string Machine { get; set; } = string.Empty;
    public string FactoryType { get; set; } = string.Empty;
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}

/// <summary>PLM's reply: how many plan rows it saved, their ids, and the ids it left alone (already closed in PLM).</summary>
public class KnitterAssignmentSyncResultModel
{
    public int Saved { get; set; }
    public List<int> Ids { get; set; } = new();
    public List<int> Skipped { get; set; } = new();
}
