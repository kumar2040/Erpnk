using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NkplmErp.Application.Interfaces;
using NkplmErp.Infrastructure.Persistence;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Infrastructure.Services;

public class BuyerOrderSummaryService(ApplicationDbContext context) : IBuyerOrderSummaryService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<IEnumerable<BuyerOrderSummaryDto>> GetBuyerOrderSummaryAsync(int year, string type)
    {
        var yearParam = new SqlParameter("@Year", year);
        var typeParam = new SqlParameter("@Type", type ?? (object)DBNull.Value);

        // Using explicit parameters with SqlQueryRaw for maximum compatibility
        var result = await _context.Database
            .SqlQueryRaw<BuyerOrderSummaryDto>("EXEC dbo.GetCustomerOrderStatusSummary @Year, @Type", yearParam, typeParam)
            .ToListAsync();

        return result;
    }
}
