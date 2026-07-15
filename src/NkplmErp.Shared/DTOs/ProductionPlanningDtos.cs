namespace NkplmErp.Shared.DTOs;

public class MonthlyOrderSummaryDto
{
    public int Year { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int MonthNum { get; set; }
    public DateTime MonthStartDate { get; set; }
    public decimal TotalPieces { get; set; }
}

public class MonthlyOrderDetailDto
{
    public string OrderNo { get; set; } = string.Empty;
    public decimal TotalPieces { get; set; }
    public string MonthName { get; set; } = string.Empty;
    public int Year { get; set; }
    public DateTime MonthStartDate { get; set; }
    public DateTime OrderLDate { get; set; }
    // Order entry date (mapped when the proc returns it); used for the 65% knit-deadline check.
    public DateTime? OrderEntryDate { get; set; }
    // Tagged client-side from order-collection types: does this order have Sample / Production work?
    public bool IsSample { get; set; }
    public bool IsProduction { get; set; }
}

// Order -> collection type flags (tbl_order_collection: type 's' = Sample, else Production).
public class OrderCollectionTypeDto
{
    public string OrderNo { get; set; } = string.Empty;
    public bool IsSample { get; set; }
    public bool IsProduction { get; set; }
}

// Result of updating a saved size line (server clamps over-allocation).
public class SizeLineUpdateResultDto
{
    public bool Success { get; set; }
    public decimal FinalQty { get; set; }
    public bool WasClamped { get; set; }
    public decimal MaxAllowed { get; set; }
}

// One editable size line of a saved machine plan.
public class PlanSizeLineEditDto
{
    public int SizeLineId { get; set; }
    public int? OrderId { get; set; }
    public string StyleNo { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public decimal Qty { get; set; }
}
public class OrderProductionStatusDto
{
    public string OrderNo { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int ProducedQuantity { get; set; }
    public int RemainingQuantity { get; set; }
    public List<YarnColorStatusDto> YarnColorStatus { get; set; } = new();
    public List<GuageWorkloadStatusDto> GuageWorkloadStatus { get; set; } = new();
}

public class YarnColorStatusDto
{
    public int ProductId { get; set; }
    public string Yarn { get; set; } = string.Empty;
    public string OrderColor { get; set; } = string.Empty;
    public decimal ReqWt { get; set; }
    public decimal OtherRunningWt { get; set; }
    public decimal StockQty { get; set; }
}

public class GuageWorkloadStatusDto
{
    public string StyleGuage { get; set; } = string.Empty;
    public decimal ExistingWorkload { get; set; }
    public decimal ExistingWorkloadDays { get; set; }
    public decimal NewOrderWorkLoad { get; set; }
    public decimal NewOrderWorkDays { get; set; }
    public decimal TotalDays { get; set; }
    public string ProductionRemark { get; set; } = string.Empty;
}
public class OrderDeptCompletionDto
{
    public string OrderNo { get; set; } = string.Empty;
    public DateTime OrderLDate { get; set; }
    public DateTime? EntryDate { get; set; }
    public DateTime? DateEntry { get; set; }
    public DateTime? DeptCompletionDate { get; set; }
}
public class GaugeUtilizationDto
{
    public double Gauge { get; set; }
    public int TotalMachines { get; set; }
    public int AvailableKnitters { get; set; }
    public int ActiveCapacity { get; set; }
    public decimal Utilization { get; set; }
    public string CompanyImpactAnalysis { get; set; } = string.Empty;
}

public class OrderPlanningDetailDto
{
    public List<YarnPlanningStatusDto> YarnStatus { get; set; } = new();
    public List<MachinePlanningStatusDto> MachineStatus { get; set; } = new();
    public List<ForwardTimelineDto> ForwardTimeline { get; set; } = new();
}

public class YarnPlanningStatusDto
{
    public int ProductId { get; set; }
    public string Yarn { get; set; } = string.Empty;
    public string StyleGuage { get; set; } = string.Empty;
    public string StylePly { get; set; } = string.Empty;
    public int ColorCount { get; set; }
    public int StyleCount { get; set; }
    public string OrderColor { get; set; } = string.Empty;
    public string StyleNo { get; set; } = string.Empty;
    public decimal RequiredKgs { get; set; }
    public decimal OtherRunningKgs { get; set; }
    public decimal StockAvailable { get; set; }
    public string StockStatus { get; set; } = string.Empty;
}

public class MachinePlanningStatusDto
{
    public string Gauge { get; set; } = string.Empty;
    public decimal BacklogDays { get; set; }
    public decimal NewOrderDays { get; set; }
    public decimal BacklogQty { get; set; }
    public decimal NewOrderQty { get; set; }
    public int TrueGaugeLimit { get; set; }
    public int SuggestedBacklogMachines { get; set; }
    public int SuggestedNewOrderMachines { get; set; }
    public string EfficiencyNote { get; set; } = string.Empty;
    public string NewOrderType { get; set; } = string.Empty;
    public string BacklogType { get; set; } = string.Empty;
    public string YarnStatus { get; set; } = string.Empty;
    public DateTime? FreeDate { get; set; }
}

public class OrderDetailByGuageDto
{
    public string OrderNo { get; set; } = string.Empty;
    public int OrderId { get; set; }
    public DateTime ShippingDate { get; set; }
    public string StyleNo { get; set; } = string.Empty;
    public string OrderColor { get; set; } = string.Empty;
    public decimal OrderPics { get; set; }
    public decimal TotalReceived { get; set; }
    public double StyleTarget { get; set; }
    public decimal BalanceQty { get; set; }
    public double RequireDays { get; set; }
    public string PrintStatus { get; set; } = string.Empty;
    public string EmbdStatus { get; set; } = string.Empty;

    // Size-wise quantities (populated by sp_getOrdersdateByGuage flag = 2)
    public decimal XXXS { get; set; }
    public decimal XXS { get; set; }
    public decimal S { get; set; }
    public decimal M { get; set; }
    public decimal L { get; set; }
    public decimal XL { get; set; }
    public decimal XXL { get; set; }
    public decimal XXXL { get; set; }
    public decimal OSFA { get; set; }
}

public class OrderAnalysisDetailedDto
{
    public string KnitType { get; set; } = string.Empty;
    public decimal TotalQty { get; set; }
    public decimal TotalWeight { get; set; }
    public int StyleCount { get; set; }
    public DateTime? EstEndDate { get; set; }
}

public class OrderAnalysisSummaryDto
{
    public string Style { get; set; } = string.Empty;
    public int Print { get; set; }
    public int Emb { get; set; }
    public decimal TotalQty { get; set; }
}

public class OrderAnalysisWorkTypeDto
{
    public string WorkType { get; set; } = string.Empty;
    public decimal Qty { get; set; }
}

public class OrderAnalysisResultDto
{
    public List<OrderAnalysisDetailedDto>? DetailedAnalysis { get; set; }
    public List<OrderAnalysisSummaryDto>? SummaryAnalysis { get; set; }
    public List<OrderAnalysisWorkTypeDto>? WorkTypeAnalysis { get; set; }
}

public class FabricAnalysisPlanDto
{
    public List<FabricMasterWorkloadDto> MasterWorkload { get; set; } = new();
    public List<FabricBalanceDto> FabricBalances { get; set; } = new();
    public List<FabricEmbroideryPrintDto> EmbroideryPrintRequirements { get; set; } = new();
}

public class FabricEmbroideryPrintDto
{
    public string StyleNo { get; set; } = string.Empty;
    public decimal TotalOrderPics { get; set; }
    public int IsPrintRequired { get; set; }
    public int IsEmbdRequired { get; set; }
}

public class FabricMasterWorkloadDto
{
    public string MasterName { get; set; } = string.Empty;
    public decimal BacklogQty { get; set; }
    public decimal NewOrderQty { get; set; }
    public decimal? BacklogDaysByCapacity { get; set; }
    public decimal? NewOrderDaysByCapacity { get; set; }
    
    // New fields mapped from the updated fabricAnalysisPlan procedure
    public string? MasterId { get; set; }
    public decimal? ActivePlanQty { get; set; }
    public int? RunningMachines { get; set; }
    public DateTime? MasterFreeDate { get; set; }
}

public class FabricBalanceDto
{
    public string Odno { get; set; } = string.Empty;
    public int Product_Id { get; set; }
    public string Pr { get; set; } = string.Empty;
    public decimal Rql { get; set; }
    public decimal Rb { get; set; }
    public decimal Balance { get; set; }
    public string Color { get; set; } = string.Empty;
    public decimal Total_Stock_Len { get; set; }
    public decimal Total_BookLength { get; set; }
    public decimal Available_Len { get; set; }
}

public class ForwardTimelineDto
{
    public string Gauge { get; set; } = string.Empty;
    public DateTime PlanSnapshotDate { get; set; }
    public decimal PlannedQtyLoad { get; set; }
    public int EngagedMachines { get; set; }
    public int ImmediateFreeMachines { get; set; }
    public int TotalActiveCapacityLimit { get; set; }
    public int FreeMachinesAvailableToday { get; set; }
    public DateTime TodayDate { get; set; }
    public DateTime EngagedMachinesReleaseDate { get; set; }
    public DateTime FreeMachinesDate { get; set; }
}

public class SavePlanRequestDto
{
    public string OrderNo { get; set; } = string.Empty;
    public string Guage { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal Qty { get; set; }
    public int Machine { get; set; }
    public string OrderType { get; set; } = string.Empty;
    public string KnitType { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }

    // Optional style/color/size breakdown for this machine plan row (Knit).
    public List<PlanSizeLineDto>? SizeLines { get; set; }

    // Machine name (e.g. KN-56) and numeric id (e.g. 25) for the Machine / MachineID columns.
    public string? MachineNo { get; set; }
    public int? MachineId { get; set; }

    // Overtime / Saturday-working flags applied to this plan row.
    public bool IsOvertime { get; set; }
    public decimal OvertimeHours { get; set; }
    public bool WorkSaturday { get; set; }
}

// One style/color/size allocation line attached to a machine plan row.
public class PlanSizeLineDto
{
    public int OrderId { get; set; }
    public string StyleNo { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public decimal Qty { get; set; }
}

// A knitter available for a gauge (KnittersGauges joined to Knitters by CardNo).
public class KnitterDto
{
    public string CardNo { get; set; } = string.Empty;
    public string KnitterName { get; set; } = string.Empty;
    public string Gauge { get; set; } = string.Empty;
    public decimal? GaugeValue { get; set; }
}

// A busy window for a knitter (a plan they're already assigned to).
public class KnitterBusyDto
{
    public string CardNo { get; set; } = string.Empty;
    public string KnitterName { get; set; } = string.Empty;
    public int PlanId { get; set; }          // MasterPlanChildId of the busy plan
    public string Gauge { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string Status { get; set; } = "Assigned"; // Assigned / Completed
}

// One audit row of knitter assignment history (per machine plan + knitter).
public class KnitterAssignmentHistoryDto
{
    public int PlanId { get; set; }
    public int? OrderId { get; set; }
    public string Gauge { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
    public string CardNo { get; set; } = string.Empty;
    public string KnitterName { get; set; } = string.Empty;
    public decimal Qty { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string AssignedBy { get; set; } = string.Empty;
    public DateTime? AssignedDate { get; set; }
    public DateTime? CompletedDate { get; set; }
}

// Request to assign a knitter to a machine plan (fans out to its size lines).
public class SaveKnitterAssignmentRequestDto
{
    public int MasterPlanDetailId { get; set; }
    public string CardNo { get; set; } = string.Empty;
    public string? KnitterName { get; set; }
    public string? AssignedBy { get; set; }
}

// One day of the CEO Planing Report (factory-wide load vs capacity).
public class PlaningReportDayDto
{
    public DateTime Date { get; set; }
    public int BusyMachines { get; set; }
    public decimal LoadQty { get; set; }       // planned pcs/day (spread across working days)
    public int KnittedPC { get; set; }          // actual pcs knitted/received that day
    public int ShipCount { get; set; }          // orders shipping (order_ldate) that day
    public string ShipOrders { get; set; } = string.Empty; // comma-list of those order nos
    public int TotalMachines { get; set; }
    public int TotalKnitters { get; set; }
    public string DayName { get; set; } = string.Empty;
    public bool IsSaturday { get; set; }
}

// Skill-aware knitter staffing feasibility for a single day. Result of the
// bipartite matching (machine-needing-gauge <-> knitter-skilled-in-gauge).
public class KnitterStaffingDayDto
{
    public DateTime Date { get; set; }
    public int MachinesRunning { get; set; }   // knit machines occupied that day
    public int KnittersMatched { get; set; }   // machines that CAN be staffed (max matching)
    public bool Staffable => MachinesRunning <= KnittersMatched;
    public int ShortBy { get; set; }           // machines that cannot be staffed
    public string BottleneckGauges { get; set; } = string.Empty; // gauges left unstaffed
}

// One pivoted row for the Master Planning page (MasterPlan + MasterPlanDetail + MasterPlanDetailSize).
public class MasterPlanningRowDto
{
    public string OrderNo { get; set; } = string.Empty;
    public string Guage { get; set; } = string.Empty;
    public string? KnitType { get; set; }   // factory_type: department of this row

    public string Machine { get; set; } = string.Empty;
    public int? MachineID { get; set; }
    public string Style { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public decimal XXXS { get; set; }
    public decimal XXS { get; set; }
    public decimal XS { get; set; }
    public decimal S { get; set; }
    public decimal M { get; set; }
    public decimal L { get; set; }
    public decimal XL { get; set; }
    public decimal XXL { get; set; }
    public decimal XXXL { get; set; }
    public decimal OSFA { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int PlanID { get; set; }
    public int OrderRowId { get; set; }
}

public class PlannedDataDto
{
    public int MasterPlanChildId { get; set; }
    public int OrderId { get; set; }
    public DateTime StartDate { get; set; }
    public string Gauge { get; set; } = string.Empty;
    public string? KnitType { get; set; }   // factory_type: department of this row
    public string Mc { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public DateTime EstEndDate { get; set; }
}

public class WeaveFactorySummaryDto
{
    public string WeaveFactory { get; set; } = string.Empty;
    public int Qty { get; set; }
    public int TotalReceived { get; set; }
    public decimal TotalMachineLoadQty { get; set; }
    public int TotalMachinesAllocated { get; set; }
    public double ReqMachineDays { get; set; }
    public DateTime? FreeDate { get; set; }
    public string YarnStatus { get; set; } = string.Empty;
}

public class WeaveYarnStatusDto
{
    public string ProductId { get; set; } = string.Empty;
    public string OrderColor { get; set; } = string.Empty;
    public string StyleGuage { get; set; } = string.Empty;
    public string StylePly { get; set; } = string.Empty;
    public decimal ItemQty { get; set; }
    public decimal SelfWt { get; set; }
    public decimal OthWt { get; set; }
    public decimal StockQty { get; set; }
    public string YarnStatus { get; set; } = string.Empty;
}

public class WeavePrintEmbroiderySummaryDto
{
    public string StyleNo { get; set; } = string.Empty;
    public int Qty { get; set; }
    public int TotalReceived { get; set; }
    public double StyleTarget { get; set; }
    public string StylePrintStatus { get; set; } = string.Empty;
    public string StyleEmbdStatus { get; set; } = string.Empty;
    public double StyleReqMachineDays { get; set; }
}

public class WeaveAnalysisPlanDto
{
    public List<WeaveFactorySummaryDto> FactorySummaries { get; set; } = new();
    public List<WeaveYarnStatusDto> YarnStatuses { get; set; } = new();
    public List<WeavePrintEmbroiderySummaryDto> PrintEmbroiderySummaries { get; set; } = new();
}

public class KnitGanttChartDto
{
    public int MasterPlanChildId { get; set; }
    public string OrderNo { get; set; } = string.Empty;
    public string OrderType { get; set; } = string.Empty;
    public string ProductionType { get; set; } = string.Empty;
    public string OrderStatus { get; set; } = string.Empty;
    public string Guage { get; set; } = string.Empty;
    public string? KnitType { get; set; }   // factory_type: department of this row
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int MachineCount { get; set; }
    public int Qty { get; set; }
    public string PlaningStatus { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public string Machine { get; set; } = string.Empty;
    public int? MachineID { get; set; }
}

public class MachinePlaningDto
{
    public int Machine_ID { get; set; }
    public string MachineNo { get; set; } = string.Empty;
    public double? Gauge { get; set; }
    public string Size { get; set; } = string.Empty;
    public DateTime FreeDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string OrderNo { get; set; } = string.Empty;
    public int? PlannedQty { get; set; }
    public string PlaningStatus { get; set; } = string.Empty;
}


