using NkplmErp.Shared.DTOs;

namespace NkplmErp.Application.Interfaces;

public interface IBuyerOrderSummaryService
{
    Task<IEnumerable<BuyerOrderSummaryDto>> GetBuyerOrderSummaryAsync(int year, string type);
}
