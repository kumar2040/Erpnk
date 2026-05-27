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
}

public class PlannedDataDto
{
    public int MasterPlanChildId { get; set; }
    public DateTime StartDate { get; set; }
    public string Gauge { get; set; } = string.Empty;
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
}

public class WeaveAnalysisPlanDto
{
    public List<WeaveFactorySummaryDto> FactorySummaries { get; set; } = new();
    public List<WeaveYarnStatusDto> YarnStatuses { get; set; } = new();
    public List<WeavePrintEmbroiderySummaryDto> PrintEmbroiderySummaries { get; set; } = new();
}

