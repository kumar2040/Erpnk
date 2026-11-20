namespace NkplmErp.Shared.DTOs.OrderPlanningApi;

/// <summary>One machine and the pieces given to it.</summary>
public class PlanRowMachineQtyModel
{
    public string MachineNo { get; set; } = string.Empty;
    public decimal Qty { get; set; }
}

/// <summary>
/// Edit of one open PLM plan row's machines (request) and the rows it ended up as (response, Id = the edited row).
/// An empty Machines list un-assigns the row; otherwise the row's qty is split over the machines.
/// </summary>
public class PlanRowMachineModel
{
    public int Id { get; set; }
    public List<PlanRowMachineQtyModel> Machines { get; set; } = new();
}