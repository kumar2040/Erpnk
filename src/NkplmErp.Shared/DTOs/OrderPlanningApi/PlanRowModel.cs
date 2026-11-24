namespace NkplmErp.Shared.DTOs.OrderPlanningApi;

/// <summary>
/// One Replan row. Source "PLM": an old plan row in PLM (tbl_production_plan_detail, status_ 0) with no machine yet;
/// Id is the PLM row id. Source "ERP": one size line of an MSSQL plan with no knitter assigned (sp_Replan 'L');
/// Id is MasterPlanDetailSize.id and PlanId the MasterPlanDetail it belongs to.
/// </summary>
public class PlanRowModel
{
    public int Id { get; set; }
    public int PlanId { get; set; }
    public string Source { get; set; } = "PLM";
    public int OrderId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string StyleNo { get; set; } = string.Empty;
    public string Gauge { get; set; } = string.Empty;
    public double StyleTarget { get; set; }
    public string KnitType { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public string Knitter { get; set; } = string.Empty;
    public string KnitterName { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}