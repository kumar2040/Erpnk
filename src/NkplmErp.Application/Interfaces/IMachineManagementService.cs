using NkplmErp.Shared.DTOs;

namespace NkplmErp.Application.Interfaces;

/// <summary>
/// CRUD for knit machines: list, create, edit (incl. gauge assignment),
/// delete, and activate/deactivate.
/// </summary>
public interface IMachineManagementService
{
    Task<List<MachineManagementDto>> GetAllMachinesAsync();
    Task<List<string>> GetGaugeOptionsAsync();
    Task<MachineOperationResult> SaveMachineAsync(SaveMachineRequest request);
    Task<MachineOperationResult> DeleteMachineAsync(int machineId);
    Task<MachineOperationResult> SetActiveAsync(int machineId, bool isActive);
}
