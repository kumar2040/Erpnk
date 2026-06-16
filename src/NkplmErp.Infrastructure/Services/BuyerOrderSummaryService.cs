using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NkplmErp.Application.Interfaces;
using NkplmErp.Infrastructure.Persistence;
using NkplmErp.Shared.DTOs;
using System.Data;
using System.Data.Common;

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
            var yearParam = new SqlParameter("@Year", (object?)year ?? DBNull.Value);

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
            var yearParam = new SqlParameter("@Year", (object?)year ?? DBNull.Value);

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
        var results = new List<OrderStatusDetailDto>();
        try
        {
            var dbContext = _context;
            var connection = dbContext.Database.GetDbConnection();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "dbo.usp_OrderMismatchReport";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@Year", year));
                command.Parameters.Add(new SqlParameter("@StatusFilter", (status == "All") ? (object)DBNull.Value : status));

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var dto = new OrderStatusDetailDto();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string col = reader.GetName(i);
                            if (col.Equals("SN", StringComparison.OrdinalIgnoreCase)) dto.SN = Convert.ToInt64(reader[i]);
                            else if (col.Equals("CustomerId", StringComparison.OrdinalIgnoreCase)) dto.CustomerId = Convert.ToInt32(reader[i]);
                            else if (col.Equals("CustomerName", StringComparison.OrdinalIgnoreCase)) dto.CustomerName = reader[i]?.ToString() ?? string.Empty;
                            else if (col.Equals("OrderNo", StringComparison.OrdinalIgnoreCase)) dto.OrderNo = reader[i]?.ToString() ?? string.Empty;
                            else if (col.Equals("OrderQty", StringComparison.OrdinalIgnoreCase)) dto.OrderQty = Convert.ToInt32(reader[i]);
                            else if (col.Equals("KnPcs", StringComparison.OrdinalIgnoreCase)) dto.KnPcs = reader.IsDBNull(i) ? null : Convert.ToInt32(reader[i]);
                            else if (col.Equals("LatestShippingDate", StringComparison.OrdinalIgnoreCase)) dto.LatestShippingDate = reader.IsDBNull(i) ? null : DateOnly.FromDateTime(Convert.ToDateTime(reader[i]));
                            else if (col.Equals("PoNo", StringComparison.OrdinalIgnoreCase)) dto.PoNo = reader[i]?.ToString();
                            else if (col.Equals("OrderEntry", StringComparison.OrdinalIgnoreCase)) dto.OrderEntry = reader.IsDBNull(i) ? null : DateOnly.FromDateTime(Convert.ToDateTime(reader[i]));
                            else if (col.Equals("Packingdate", StringComparison.OrdinalIgnoreCase)) dto.Packingdate = reader.IsDBNull(i) ? null : DateOnly.FromDateTime(Convert.ToDateTime(reader[i]));
                            else if (col.Equals("Status", StringComparison.OrdinalIgnoreCase)) dto.Status = reader[i]?.ToString() ?? string.Empty;
                            else if (col.Equals("CoveragePercent", StringComparison.OrdinalIgnoreCase)) dto.CoveragePercent = Convert.ToDecimal(reader[i]);
                            else if (col.Equals("DaysRemaining", StringComparison.OrdinalIgnoreCase)) dto.DaysRemaining = reader.IsDBNull(i) ? null : Convert.ToInt32(reader[i]);
                            else if (col.Equals("DecisionRemark", StringComparison.OrdinalIgnoreCase)) dto.DecisionRemark = reader[i]?.ToString() ?? string.Empty;
                            else if (col.Equals("RiskFlag", StringComparison.OrdinalIgnoreCase)) dto.RiskFlag = reader[i]?.ToString() ?? string.Empty;
                            else if (col.Equals("Message", StringComparison.OrdinalIgnoreCase)) dto.Message = reader[i]?.ToString();
                        }
                        results.Add(dto);
                    }
                }
            }
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Infrastructure - Error in GetOrderStatusDetailAsync: {ex.Message}");
            throw;
        }
    }
    public async Task<IEnumerable<ProductionFlowDto>> GetProductionFlowAsync(int buyerId, string? orderNo = null)
    {
        // Actually, let's keep it simple and just use manual mapping for messages
        var results = new List<ProductionFlowDto>();
        try
        {
            var connection = _context.Database.GetDbConnection();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "dbo.id_productionFlow";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@BuyerID", buyerId));
                command.Parameters.Add(new SqlParameter("@OrderNo", (object?)orderNo ?? DBNull.Value));

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var dto = new ProductionFlowDto();
                        for (int j = 0; j < reader.FieldCount; j++)
                        {
                            string col = reader.GetName(j);
                            object val = reader.GetValue(j);
                            if (val == DBNull.Value) continue;

                            if (col.Equals("OrderNo", StringComparison.OrdinalIgnoreCase)) dto.OrderNo = val.ToString();
                            else if (col.Equals("PCS", StringComparison.OrdinalIgnoreCase)) dto.PCS = Convert.ToInt32(val);
                            else if (col.Equals("OrderEntryStart", StringComparison.OrdinalIgnoreCase)) dto.OrderEntryStart = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("OrderEntryFinish", StringComparison.OrdinalIgnoreCase)) dto.OrderEntryFinish = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("IDDate", StringComparison.OrdinalIgnoreCase)) dto.IDDate = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("ShippingDate", StringComparison.OrdinalIgnoreCase)) dto.ShippingDate = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("ProductionDays", StringComparison.OrdinalIgnoreCase)) dto.ProductionDays = Convert.ToInt32(val);
                            else if (col.Equals("Ns", StringComparison.OrdinalIgnoreCase)) dto.Ns = Convert.ToInt32(val);
                            else if (col.Equals("Nr", StringComparison.OrdinalIgnoreCase)) dto.Nr = Convert.ToInt32(val);
                            else if (col.Equals("totalDispatched", StringComparison.OrdinalIgnoreCase)) dto.totalDispatched = Convert.ToInt32(val);
                            else if (col.Equals("status", StringComparison.OrdinalIgnoreCase)) dto.status = val.ToString();
                            else if (col.Equals("PLM", StringComparison.OrdinalIgnoreCase)) dto.PLM = Convert.ToInt32(val);
                            else if (col.Equals("CHK", StringComparison.OrdinalIgnoreCase)) dto.CHK = Convert.ToInt32(val);
                            else if (col.Equals("KCH", StringComparison.OrdinalIgnoreCase)) dto.KCH = Convert.ToInt32(val);
                            else if (col.Equals("DYE", StringComparison.OrdinalIgnoreCase)) dto.DYE = Convert.ToInt32(val);
                            else if (col.Equals("HUB", StringComparison.OrdinalIgnoreCase)) dto.HUB = Convert.ToInt32(val);
                            else if (col.Equals("LNK", StringComparison.OrdinalIgnoreCase)) dto.LNK = Convert.ToInt32(val);
                            else if (col.Equals("MND", StringComparison.OrdinalIgnoreCase)) dto.MND = Convert.ToInt32(val);
                            else if (col.Equals("PRND", StringComparison.OrdinalIgnoreCase)) dto.PRND = Convert.ToInt32(val);
                            else if (col.Equals("TLR", StringComparison.OrdinalIgnoreCase)) dto.TLR = Convert.ToInt32(val);
                            else if (col.Equals("WSH", StringComparison.OrdinalIgnoreCase)) dto.WSH = Convert.ToInt32(val);
                            else if (col.Equals("EMB", StringComparison.OrdinalIgnoreCase)) dto.EMB = Convert.ToInt32(val);
                            else if (col.Equals("PRS", StringComparison.OrdinalIgnoreCase)) dto.PRS = Convert.ToInt32(val);
                            else if (col.Equals("PCK", StringComparison.OrdinalIgnoreCase)) dto.PCK = Convert.ToInt32(val);
                            else if (col.Equals("totalPacked", StringComparison.OrdinalIgnoreCase)) dto.totalPacked = Convert.ToInt32(val);
                            else if (col.Equals("Total_Dispatch", StringComparison.OrdinalIgnoreCase)) dto.Total_Dispatch = Convert.ToInt32(val);
                            else if (col.Equals("KNT_maxDate", StringComparison.OrdinalIgnoreCase)) dto.KNT_maxDate = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("KCH_maxDate", StringComparison.OrdinalIgnoreCase)) dto.KCH_maxDate = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("DYE_maxDate", StringComparison.OrdinalIgnoreCase)) dto.DYE_maxDate = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("HUB_maxDate", StringComparison.OrdinalIgnoreCase)) dto.HUB_maxDate = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("LNK_maxDate", StringComparison.OrdinalIgnoreCase)) dto.LNK_maxDate = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("MND_maxDate", StringComparison.OrdinalIgnoreCase)) dto.MND_maxDate = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("PRN_maxDate", StringComparison.OrdinalIgnoreCase)) dto.PRN_maxDate = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("WSH_maxDate", StringComparison.OrdinalIgnoreCase)) dto.WSH_maxDate = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("PRS_maxDate", StringComparison.OrdinalIgnoreCase)) dto.PRS_maxDate = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("PCK_maxDate", StringComparison.OrdinalIgnoreCase)) dto.PCK_maxDate = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("DSP_maxDate", StringComparison.OrdinalIgnoreCase)) dto.DSP_maxDate = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("SHP_maxDate", StringComparison.OrdinalIgnoreCase)) dto.SHP_maxDate = DateOnly.FromDateTime(Convert.ToDateTime(val));
                            else if (col.Equals("BuyerId", StringComparison.OrdinalIgnoreCase)) dto.BuyerId = Convert.ToInt32(val);
                            else if (col.Equals("Message", StringComparison.OrdinalIgnoreCase)) dto.Message = val.ToString();
                        }
                        results.Add(dto);
                    }
                }
            }
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Infrastructure - Error in GetProductionFlowAsync: {ex.Message}");
            throw;
        }
    }
    public async Task<IEnumerable<DepartmentStockDto>> GetdepartmentStockAsync(string? OrderNo, string Department)
    {
        var results = new List<DepartmentStockDto>();
        try
        {
            var dbContext = _context;
            var connection = dbContext.Database.GetDbConnection();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "dbo.flow_deparmentStock";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@order_no", (object?)OrderNo ?? DBNull.Value));
                command.Parameters.Add(new SqlParameter("@dep_n", (object?)Department ?? DBNull.Value));

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var dto = new DepartmentStockDto();
                        
                        // Map dynamic columns
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string columnName = reader.GetName(i).Trim();
                            
                            if (columnName.Equals("OrderId", StringComparison.OrdinalIgnoreCase))
                                dto.OrderId = reader[i]?.ToString() ?? string.Empty;
                            else if (columnName.Equals("StyleNo", StringComparison.OrdinalIgnoreCase))
                                dto.StyleNo = reader[i]?.ToString() ?? string.Empty;
                            else if (columnName.Equals("Color", StringComparison.OrdinalIgnoreCase))
                                dto.Color = reader[i]?.ToString() ?? string.Empty;
                            else if (columnName.Equals("Message", StringComparison.OrdinalIgnoreCase))
                                dto.Message = reader[i]?.ToString();
                            else
                            {
                                // Everything else is a potential size column
                                int val = 0;
                                if (!reader.IsDBNull(i))
                                {
                                    val = Convert.ToInt32(reader.GetValue(i));
                                }
                                dto.Sizes[columnName] = val;
                            }
                        }
                        results.Add(dto);
                    }
                }
            }
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Infrastructure - Error in GetdepartmentStockAsync: {ex.Message}");
            throw;
        }
    }
    public async Task<IEnumerable<OrderViewHeaderDto>> GetOrderViewDataAsync(string orderNo)
    {
        var results = new List<OrderViewHeaderDto>();
        try
        {
            var dbContext = _context;
            var connection = dbContext.Database.GetDbConnection();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "dbo.orderView";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@orderno", orderNo));
                command.Parameters.Add(new SqlParameter("@flag", "i"));

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var dto = new OrderViewHeaderDto();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string col = reader.GetName(i);
                            if (reader.IsDBNull(i)) continue;

                            if (col.Equals("shippingDate", StringComparison.OrdinalIgnoreCase)) dto.ShippingDate = DateOnly.FromDateTime(Convert.ToDateTime(reader[i]));
                            else if (col.Equals("Guage", StringComparison.OrdinalIgnoreCase)) dto.Guage = reader[i].ToString();
                            else if (col.Equals("ply", StringComparison.OrdinalIgnoreCase)) dto.Ply = reader[i].ToString();
                            else if (col.Equals("styleTarget", StringComparison.OrdinalIgnoreCase)) dto.StyleTarget = Convert.ToInt32(reader[i]);
                            else if (col.Equals("StyleNo", StringComparison.OrdinalIgnoreCase)) dto.StyleNo = reader[i].ToString() ?? string.Empty;
                            else if (col.Equals("Color", StringComparison.OrdinalIgnoreCase)) dto.Color = reader[i].ToString() ?? string.Empty;
                            else if (col.Equals("Qty", StringComparison.OrdinalIgnoreCase)) dto.Qty = Convert.ToInt32(reader[i]);
                            else if (col.Equals("Yarn", StringComparison.OrdinalIgnoreCase)) dto.Yarn = reader[i].ToString();
                            else if (col.Equals("ProductName", StringComparison.OrdinalIgnoreCase)) dto.ProductName = reader[i].ToString();
                            else if (col.Equals("stylePrint", StringComparison.OrdinalIgnoreCase)) dto.StylePrint = reader[i].ToString();
                            else if (col.Equals("KnSl", StringComparison.OrdinalIgnoreCase)) dto.KnSl = reader[i].ToString();
                            else if (col.Equals("DaysRequired", StringComparison.OrdinalIgnoreCase))
                            {
                                // The SP might return double/float from CEILING(CAST(...))
                                dto.DaysRequired = Convert.ToInt32(Math.Ceiling(Convert.ToDouble(reader[i])));
                            }
                            else
                            {
                                dto.Sizes[col] = Convert.ToInt32(reader[i]);
                            }
                        }
                        results.Add(dto);
                    }
                }
            }
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Infrastructure - Error in GetOrderViewDataAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<StyleDetailsDto> GetStyleDetailsAsync(string styleNo)
    {
        var result = new StyleDetailsDto();
        try
        {
            var connection = _context.Database.GetDbConnection();
            using (var command = connection.CreateCommand())
            {
                command.CommandText = "dbo.styleGeneralinfo";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@styleno", styleNo));

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    // Result Set 1: Style General Info
                    if (await reader.ReadAsync())
                    {
                        var info = new StyleGeneralInfoDto();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string col = reader.GetName(i);
                            if (reader.IsDBNull(i)) continue;

                            if (col.Equals("NetWet", StringComparison.OrdinalIgnoreCase) || col.Equals("net_wet", StringComparison.OrdinalIgnoreCase)) info.NetWet = Convert.ToDouble(reader[i]);
                            else if (col.Equals("styleNo", StringComparison.OrdinalIgnoreCase) || col.Equals("style_no", StringComparison.OrdinalIgnoreCase)) info.StyleNo = reader[i].ToString() ?? string.Empty;
                            else if (col.Equals("StylePrint", StringComparison.OrdinalIgnoreCase) || col.Equals("style_print", StringComparison.OrdinalIgnoreCase)) info.StylePrint = reader[i].ToString();
                            else if (col.Equals("styleDesc", StringComparison.OrdinalIgnoreCase) || col.Equals("style_discription", StringComparison.OrdinalIgnoreCase)) info.StyleDesc = reader[i].ToString();
                            else if (col.Equals("styleId", StringComparison.OrdinalIgnoreCase) || col.Equals("style_id", StringComparison.OrdinalIgnoreCase)) info.StyleId = Convert.ToInt32(reader[i]);
                            else if (col.Equals("stylePly", StringComparison.OrdinalIgnoreCase) || col.Equals("style_ply", StringComparison.OrdinalIgnoreCase)) info.StylePly = reader[i].ToString();
                            else if (col.Equals("styleGuage", StringComparison.OrdinalIgnoreCase) || col.Equals("style_guage", StringComparison.OrdinalIgnoreCase)) info.StyleGuage = reader[i].ToString();
                            else if (col.Equals("styleTarget", StringComparison.OrdinalIgnoreCase) || col.Equals("style_target", StringComparison.OrdinalIgnoreCase)) info.StyleTarget = Convert.ToInt32(reader[i]);
                            else if (col.Equals("Yarn", StringComparison.OrdinalIgnoreCase) || col.Equals("BaseYarn", StringComparison.OrdinalIgnoreCase)) info.Yarn = reader[i].ToString();
                            else if (col.Equals("Silks", StringComparison.OrdinalIgnoreCase)) info.Silks = reader[i].ToString();
                            else if (col.Equals("WarpWeftYarns", StringComparison.OrdinalIgnoreCase)) info.WarpWeftYarns = reader[i].ToString();
                        }
                        result.GeneralInfo = info;
                    }

                    // Result Set 2: Delivery Timeline
                    if (await reader.NextResultAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var timeline = new StyleDeliveryTimelineDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i);
                                if (reader.IsDBNull(i)) continue;

                                if (col.Equals("DeliveryYear", StringComparison.OrdinalIgnoreCase) || col.Equals("DeliveryDate", StringComparison.OrdinalIgnoreCase) || col.Equals("delivery_date", StringComparison.OrdinalIgnoreCase)) timeline.DeliveryYear = reader[i].ToString();
                                else if (col.Equals("QtyDeliveredThisYear", StringComparison.OrdinalIgnoreCase) || col.Equals("yearQty", StringComparison.OrdinalIgnoreCase) || col.Equals("QtyDeliveredThisDate", StringComparison.OrdinalIgnoreCase) || col.Equals("qty_delivered", StringComparison.OrdinalIgnoreCase)) timeline.QtyDeliveredThisYear = Convert.ToInt32(reader[i]);
                                else if (col.Equals("CumulativeQtyDelivered", StringComparison.OrdinalIgnoreCase) || col.Equals("cumulative_qty", StringComparison.OrdinalIgnoreCase)) timeline.CumulativeQtyDelivered = Convert.ToInt32(reader[i]);
                                else if (col.Equals("NumOrderLines", StringComparison.OrdinalIgnoreCase) || col.Equals("num_lines", StringComparison.OrdinalIgnoreCase)) timeline.NumOrderLines = Convert.ToInt32(reader[i]);
                            }
                            result.DeliveryTimeline.Add(timeline);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Infrastructure - Error in GetStyleDetailsAsync: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<IEnumerable<BuyerOrderDto>> GetBuyersOrdersAsync(int buyerId, int flag)
    {
        var results = new List<BuyerOrderDto>();
        try
        {
            var dbContext = _context;
            var connection = dbContext.Database.GetDbConnection();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "dbo.buyersOrders";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@buyerId", buyerId));
                command.Parameters.Add(new SqlParameter("@flag", flag));

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    long sn = 1;
                    while (await reader.ReadAsync())
                    {
                        var dto = new BuyerOrderDto { SN = sn++ };
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string col = reader.GetName(i);
                            if (reader.IsDBNull(i)) continue;

                            if (col.Equals("OrderNo", StringComparison.OrdinalIgnoreCase)) dto.OrderNo = reader[i]?.ToString() ?? string.Empty;
                            else if (col.Equals("Collection", StringComparison.OrdinalIgnoreCase)) dto.Collection = reader[i]?.ToString() ?? string.Empty;
                            else if (col.Equals("PoNo", StringComparison.OrdinalIgnoreCase)) dto.PoNo = reader[i]?.ToString();
                            else if (col.Equals("ShippingDate", StringComparison.OrdinalIgnoreCase)) dto.ShippingDate = Convert.ToDateTime(reader[i]);
                            else if (col.Equals("TotalOrderPics", StringComparison.OrdinalIgnoreCase)) dto.TotalOrderPics = Convert.ToInt32(reader[i]);
                            else if (col.Equals("TotalKnitterItems", StringComparison.OrdinalIgnoreCase)) dto.TotalKnitterItems = Convert.ToInt32(reader[i]);
                            else if (col.Equals("Difference", StringComparison.OrdinalIgnoreCase)) dto.Difference = Convert.ToInt32(reader[i]);
                            else if (col.Equals("SN", StringComparison.OrdinalIgnoreCase)) dto.SN = Convert.ToInt64(reader[i]);
                            else
                            {
                                // Everything else is a potential category column
                                dto.Categories[col] = Convert.ToInt32(reader[i]);
                            }
                        }
                        results.Add(dto);
                    }
                }
            }
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Infrastructure - Error in GetBuyersOrdersAsync: {ex.Message}");
            throw;
        }
    }

    public async Task<IEnumerable<OrderPriceAnalysisDto>> GetOrderPriceAnalysisAsync(string orderNo, decimal usdRate)
    {
        var results = new List<OrderPriceAnalysisDto>();
        try
        {
            var dbContext = _context;
            var connection = dbContext.Database.GetDbConnection();

            using (var command = connection.CreateCommand())
            {
                command.CommandText = "dbo.order_price_analysis";
                command.CommandType = CommandType.StoredProcedure;
                command.Parameters.Add(new SqlParameter("@orderno", orderNo));
                command.Parameters.Add(new SqlParameter("@usdrate", usdRate));

                if (connection.State != ConnectionState.Open)
                    await connection.OpenAsync();

                using (var reader = await command.ExecuteReaderAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        var dto = new OrderPriceAnalysisDto();
                        for (int i = 0; i < reader.FieldCount; i++)
                        {
                            string col = reader.GetName(i);
                            if (reader.IsDBNull(i)) continue;

                            if (col.Equals("SN", StringComparison.OrdinalIgnoreCase)) dto.SN = Convert.ToInt64(reader[i]);
                            else if (col.Equals("product_name", StringComparison.OrdinalIgnoreCase)) dto.ProductName = reader[i]?.ToString() ?? string.Empty;
                            else if (col.Equals("total_quantity", StringComparison.OrdinalIgnoreCase)) dto.TotalQuantity = Convert.ToDecimal(reader[i]);
                            else if (col.Equals("style_guage", StringComparison.OrdinalIgnoreCase)) dto.StyleGuage = reader[i].ToString();
                            else if (col.Equals("style_ply", StringComparison.OrdinalIgnoreCase)) dto.StylePly = reader[i].ToString();
                            else if (col.Equals("yarn_info", StringComparison.OrdinalIgnoreCase)) dto.YarnInfo = reader[i].ToString();
                            else if (col.Equals("net_wet", StringComparison.OrdinalIgnoreCase)) dto.NetWet = Convert.ToDecimal(reader[i]);
                            else if (col.Equals("overrate_per_pc_usd", StringComparison.OrdinalIgnoreCase)) dto.OverratePerPcUsd = Convert.ToDecimal(reader[i]);
                            else if (col.Equals("final_cost_per_pc_usd", StringComparison.OrdinalIgnoreCase)) dto.FinalCostPerPcUsd = Convert.ToDecimal(reader[i]);
                            else if (col.Equals("grand_total_production_cost_usd", StringComparison.OrdinalIgnoreCase)) dto.GrandTotalProductionCostUsd = Convert.ToDecimal(reader[i]);
                            else if (col.Equals("total_revenue_usd", StringComparison.OrdinalIgnoreCase)) dto.TotalRevenueUsd = Convert.ToDecimal(reader[i]);
                        }
                        results.Add(dto);
                    }
                }
            }
            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Infrastructure - Error in GetOrderPriceAnalysisAsync: {ex.Message}");
            throw;
        }
    }
}
