namespace NkplmErp.Shared.DTOs.OrderPlanningApi;

/// <summary>
/// Mirror of a PLM plan-row machine edit into MSSQL: the MasterPlanDetail row that owns the
/// (order, style, colour, size) line gets its Machine / MachineID updated. MachineNo is the
/// comma-joined machine numbers; MachineId is the first machine (null when un-assigned).
/// </summary>
public class MasterPlanMachineModel
{
    public int OrderId { get; set; }
    public string StyleNo { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public string MachineNo { get; set; } = string.Empty;
    public int? MachineId { get; set; }
}
