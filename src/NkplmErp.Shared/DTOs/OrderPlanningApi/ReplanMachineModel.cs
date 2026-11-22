namespace NkplmErp.Shared.DTOs.OrderPlanningApi;

/// <summary>
/// Replan an MSSQL plan (no knitter assigned yet) onto one machine with new dates (sp_Replan 'M').
/// Dates are yyyy-MM-dd text; the procedure converts and validates them.
/// </summary>
public class ReplanMachineModel
{
    public int PlanId { get; set; }
    public string Machine { get; set; } = string.Empty;
    public int MachineId { get; set; }
    public string StartDate { get; set; } = string.Empty;
    public string EndDate { get; set; } = string.Empty;
}

/// <summary>sp_Replan 'M' result.</summary>
public class ReplanResultModel
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
}
