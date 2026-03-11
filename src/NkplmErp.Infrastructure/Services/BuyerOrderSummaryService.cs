using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NkplmErp.Application.Interfaces;
using NkplmErp.Infrastructure.Persistence;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Infrastructure.Services;

public class BuyerOrderSummaryService(ApplicationDbContext context) : IBuyerOrderSummaryService
{
    private readonly ApplicationDbContext _context = context;

    public async Task<IEnumerable<BuyerOrderSummaryDto>> GetBuyerOrderSummaryAsync(int year, string type, int maxrec)
    {
        var yearParam = new SqlParameter("@Year", year);
        var typeParam = new SqlParameter("@Type", type ?? (object)DBNull.Value);
        var maxRecParam = new SqlParameter("@Limit", maxrec > 0 ? (object)maxrec : DBNull.Value);

        // Using explicit parameters with SqlQueryRaw for maximum compatibility
        var result = await _context.Database
            .SqlQueryRaw<BuyerOrderSummaryDto>("EXEC dbo.GetCustomerOrderStatusSummary @Year, @Type, @Limit", yearParam, typeParam, maxRecParam)
            .ToListAsync();

        return result;
    }

    public async Task<IEnumerable<int>> GetBuyerOrderYearsAsync(int? customerId)
    {
        try
        {
            var customerIdParam = new SqlParameter("@CustomerId", customerId ?? (object)DBNull.Value);

            // Log the call details
            Console.WriteLine($"DEBUG: Infrastructure - GetBuyerOrderYearsAsync called with CustomerId: {customerId}");

            // Using SqlQueryRaw<int?> to handle NULL values from DB
            var result = await _context.Database
                .SqlQueryRaw<int?>("EXEC GetBuyerOrderYears @CustomerId", customerIdParam)
                .ToListAsync();

            var years = result.Where(y => y.HasValue).Select(y => y.Value).ToList();

            Console.WriteLine($"DEBUG: Infrastructure - GetBuyerOrderYearsAsync returned {years.Count} years");
            return years;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Infrastructure - Error in GetBuyerOrderYearsAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<IEnumerable<BuyerOrderHistoryDto>> GetBuyerOrderHistoryAsync(int customerId, int? year = null)
    {
        try
        {
            var customerIdParam = new SqlParameter("@BuyerID", customerId);
            var yearParam = new SqlParameter("@Year", (object)year ?? DBNull.Value);

            Console.WriteLine($"DEBUG: Infrastructure - GetBuyerOrderHistoryAsync called for BuyerID: {customerId}, Year: {year}");

            var result = await _context.Database
                .SqlQueryRaw<BuyerOrderHistoryDto>("EXEC dbo.BuyerorderHistoryyearly @BuyerID, @Year", customerIdParam, yearParam)
                .ToListAsync();

            Console.WriteLine($"DEBUG: Infrastructure - GetBuyerOrderHistoryAsync returned {result?.Count ?? 0} records");
            return result ?? Enumerable.Empty<BuyerOrderHistoryDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Infrastructure - Error in GetBuyerOrderHistoryAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<IEnumerable<BuyerProfile>> GetBuyerProfileAsync(int customerId, int? year = null)
    {
        try
        {
            var customerIdParam = new SqlParameter("@BuyerID", customerId);
            var yearParam = new SqlParameter("@Year", (object)year ?? DBNull.Value);

            Console.WriteLine($"DEBUG: Infrastructure - GetBuyerProfileAsync called for BuyerID: {customerId}");

            var result = await _context.Database
                .SqlQueryRaw<BuyerProfile>("EXEC dbo.GetBuyerProfile @BuyerID, @Year", customerIdParam, yearParam)
                .ToListAsync();

            Console.WriteLine($"DEBUG: Infrastructure - GetBuyerProfileAsync returned {result?.Count ?? 0} records");
            return result ?? Enumerable.Empty<BuyerProfile>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Infrastructure - Error in GetBuyerProfileAsync: {ex.Message}");
            throw;
        }
    }
    public async Task<IEnumerable<AbsentBuyer>> GetAbsentBuyer()
    {
        try
        {
            var yearParam = new SqlParameter("@Year", DateTime.Now.Year);
            var result = await _context.Database
                .SqlQueryRaw<AbsentBuyer>("EXEC dbo.usp_GetAbsentCustomers @Year", yearParam)
                .ToListAsync();

            return result ?? Enumerable.Empty<AbsentBuyer>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Infrastructure - Error in GetAbsentBuyerAsync: {ex.Message}");
            throw;
        }
    }
    public async Task<IEnumerable<OrderStatusDetailDto>> GetOrderStatusDetailAsync(int year, string status)
    {
        try
        {
            var yearParam = new SqlParameter("@Year", year);
            var statusParam = new SqlParameter("@StatusFilter", (status == "All") ? (object)DBNull.Value : status);
            var result = await _context.Database
                .SqlQueryRaw<OrderStatusDetailDto>("EXEC dbo.usp_OrderMismatchReport @Year, @StatusFilter", yearParam, statusParam)
                .ToListAsync();

            return result ?? Enumerable.Empty<OrderStatusDetailDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Infrastructure - Error in GetOrderStatusDetailAsync: {ex.Message}");
            throw;
        }
    }
    public async Task<IEnumerable<ProductionFlowDto>> GetProductionFlowAsync(int buyerId, string? orderNo = null)
    {
        try
        {
            var buyerIdParam = new SqlParameter("@BuyerID", buyerId);
            var orderNoParam = new SqlParameter("@OrderNo", (object)orderNo ?? DBNull.Value);

            Console.WriteLine($"DEBUG: Infrastructure - GetProductionFlowAsync called for BuyerID: {buyerId}, OrderNo: {orderNo}");

            var result = await _context.Database
                .SqlQueryRaw<ProductionFlowDto>("EXEC dbo.id_productionFlow @BuyerID, @OrderNo", buyerIdParam, orderNoParam)
                .ToListAsync();

            Console.WriteLine($"DEBUG: Infrastructure - GetProductionFlowAsync returned {result?.Count ?? 0} records");
            return result ?? Enumerable.Empty<ProductionFlowDto>();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Infrastructure - Error in GetProductionFlowAsync: {ex.Message}");
            throw;
        }
    }
}
