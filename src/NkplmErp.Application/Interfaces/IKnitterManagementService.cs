using NkplmErp.Shared.DTOs;

namespace NkplmErp.Application.Interfaces;

/// <summary>
/// CRUD for knitters: list, create, edit (incl. gauge assignment),
/// delete, and activate/deactivate.
/// </summary>
public interface IKnitterManagementService
{
    Task<List<KnitterManagementDto>> GetAllKnittersAsync();
    Task<List<string>> GetGaugeOptionsAsync();
    Task<KnitterOperationResult> SaveKnitterAsync(SaveKnitterRequest request);
    Task<KnitterOperationResult> DeleteKnitterAsync(int cardNo);
    Task<KnitterOperationResult> SetActiveAsync(int cardNo, bool isActive);
}
