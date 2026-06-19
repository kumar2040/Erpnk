using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Infrastructure.Services;

public class ProductionPlanningService : IProductionPlanningService
{
    private readonly string _connectionString;

    public ProductionPlanningService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public async Task<IEnumerable<MonthlyOrderSummaryDto>> GetMonthlySummaryAsync(DateTime inputDate)
    {
        var result = new List<MonthlyOrderSummaryDto>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("sp_GetMonthlyOrderReport", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@InputDate", inputDate);
                    command.Parameters.AddWithValue("@Flag", "months");

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new MonthlyOrderSummaryDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                if (col == "monthstartdate") dto.MonthStartDate = Convert.ToDateTime(reader[i]);
                                else if (col == "monthname") dto.MonthName = reader[i].ToString() ?? string.Empty;
                                else if (col.Contains("totalpieces") || col.Contains("totalquantity")) dto.TotalPieces = Convert.ToDecimal(reader[i]);
                                else if (col == "monthnum") dto.MonthNum = Convert.ToInt32(reader[i]);
                                else if (col == "year") dto.Year = Convert.ToInt32(reader[i]);
                            }
                            result.Add(dto);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetMonthlySummaryAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<IEnumerable<MonthlyOrderDetailDto>> GetMonthlyOrderDetailsAsync(DateTime inputDate)
    {
        var result = new List<MonthlyOrderDetailDto>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("sp_GetMonthlyOrderReport", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@InputDate", inputDate);
                    command.Parameters.AddWithValue("@Flag", "monthsorders");

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new MonthlyOrderDetailDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                if (col == "orderno" || col == "order_no") dto.OrderNo = reader[i].ToString() ?? string.Empty;
                                else if (col.Contains("totalpieces") || col.Contains("total_pieces")) dto.TotalPieces = Convert.ToDecimal(reader[i]);
                                else if (col == "monthstartdate") dto.MonthStartDate = Convert.ToDateTime(reader[i]);
                                else if (col == "orderldate" || col == "order_ldate" || col == "orderdate") dto.OrderLDate = Convert.ToDateTime(reader[i]);
                                else if (col == "monthname") dto.MonthName = reader[i].ToString() ?? string.Empty;
                                else if (col == "year") dto.Year = Convert.ToInt32(reader[i]);
                                // Order entry date (any common naming) - used for the 65% knit-deadline rule.
                                else if (col == "orderedate" || col == "orderentrydate" || col == "entrydate" || col == "ordersdate") dto.OrderEntryDate = Convert.ToDateTime(reader[i]);
                            }
                            result.Add(dto);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetMonthlyOrderDetailsAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<IEnumerable<OrderCollectionTypeDto>> GetOrderCollectionTypesAsync()
    {
        var result = new List<OrderCollectionTypeDto>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("sp_GetOrderCollectionTypes", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new OrderCollectionTypeDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                if (col == "orderno") dto.OrderNo = reader[i].ToString() ?? string.Empty;
                                else if (col == "issample") dto.IsSample = Convert.ToInt32(reader[i]) != 0;
                                else if (col == "isproduction") dto.IsProduction = Convert.ToInt32(reader[i]) != 0;
                            }
                            if (!string.IsNullOrEmpty(dto.OrderNo)) result.Add(dto);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetOrderCollectionTypesAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<OrderProductionStatusDto> GetOrderProductionStatusAsync(string orderNo, int flag)
    {
        var result = new OrderProductionStatusDto();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("sp_GetOrderProductionStatus_plan", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@orderNo", orderNo);
                    command.Parameters.AddWithValue("@flag", flag);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).ToLower();
                                if (reader.IsDBNull(i)) continue;

                                if (col == "order_no") result.OrderNo = reader[i].ToString() ?? string.Empty;
                                else if (col == "order_pics" || col == "total_pics") result.TotalQuantity = Convert.ToInt32(reader[i]);
                                else if (col == "knittedpc" || col == "produced_qty") result.ProducedQuantity = Convert.ToInt32(reader[i]);
                                else if (col == "rempc" || col == "remaining_qty") result.RemainingQuantity = Convert.ToInt32(reader[i]);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetOrderProductionStatusAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<OrderDeptCompletionDto?> GetOrderDeptCompletionDateAsync(string orderNo, string deptName)
    {
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("orderDepCompletionDate", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@OrderNo", orderNo);
                    command.Parameters.AddWithValue("@DeptName", deptName);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new OrderDeptCompletionDto
                            {
                                OrderNo = reader["order_no"].ToString() ?? string.Empty,
                                OrderLDate = Convert.ToDateTime(reader["order_ldate"]),
                                DeptCompletionDate = reader["DeptCompletionDate"] != DBNull.Value ? Convert.ToDateTime(reader["DeptCompletionDate"]) : null
                            };
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetOrderDeptCompletionDateAsync Error: {ex.Message}");
        }
        return null;
    }


    public async Task<IEnumerable<GaugeUtilizationDto>> GetGaugeUtilizationReportAsync(double? targetGauge)
    {
        var result = new List<GaugeUtilizationDto>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("GetGaugeUtilizationReport", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    if (targetGauge.HasValue)
                    {
                        command.Parameters.AddWithValue("@targetGauge", targetGauge.Value);
                    }

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new GaugeUtilizationDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                if (col == "gauge") dto.Gauge = Convert.ToDouble(reader[i]);
                                else if (col == "totalmachines" || col == "machinecount") dto.TotalMachines = Convert.ToInt32(reader[i]);
                                else if (col == "availableknitters" || col == "knittercount") dto.AvailableKnitters = Convert.ToInt32(reader[i]);
                                else if (col == "activecapacity") dto.ActiveCapacity = Convert.ToInt32(reader[i]);
                                else if (col == "utilization" || col == "utilizationpercent") dto.Utilization = Convert.ToDecimal(reader[i]);
                                else if (col == "companyimpactanalysis") dto.CompanyImpactAnalysis = reader[i].ToString() ?? string.Empty;
                            }
                            result.Add(dto);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetGaugeUtilizationReportAsync Error: {ex.Message}");
        }
        return result;
    }

    public async Task<OrderPlanningDetailDto> GetOrderPlanningDetailAsync(string orderNo, int flag, string? gauge = null, string? ply = null)
    {
        var result = new OrderPlanningDetailDto();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("sp_GetOrderProductionStatus_plan", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@orderNo", orderNo);
                    command.Parameters.AddWithValue("@flag", flag);
                    command.Parameters.AddWithValue("@gauge", (object?)gauge ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ply", (object?)ply ?? DBNull.Value);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // First Result Set: Yarn Status
                        while (await reader.ReadAsync())
                        {
                            result.YarnStatus.Add(MapYarn(reader));
                        }

                        // Second Result Set: Machine Status
                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var machine = MapMachine(reader);
                                // Optional: Keep the logic for auto-suggesting machine count if it's too aggressive
                                if (machine.NewOrderDays > 0 && machine.SuggestedNewOrderMachines > 1)
                                {
                                    decimal actualDays = machine.NewOrderDays / machine.SuggestedNewOrderMachines;
                                    if (actualDays < 6)
                                    {
                                        decimal capacityPerDay = machine.NewOrderQty / machine.NewOrderDays;
                                        int betterMachineCount = (int)Math.Floor((double)(machine.NewOrderQty / (capacityPerDay * 6)));
                                        machine.SuggestedNewOrderMachines = Math.Max(1, betterMachineCount);
                                    }
                                }
                                result.MachineStatus.Add(machine);
                            }
                        }

                        // Third Result Set: Forward Timeline
                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                result.ForwardTimeline.Add(MapForwardTimeline(reader));
                            }
                        }

                        // Compute FreeDate for each machine status item
                        foreach (var machine in result.MachineStatus)
                        {
                            DateTime? freeDate = null;
                            var timelineForGauge = result.ForwardTimeline
                                .Where(t => string.Equals(t.Gauge?.Trim(), machine.Gauge?.Trim(), StringComparison.OrdinalIgnoreCase))
                                .OrderBy(t => t.PlanSnapshotDate)
                                .ToList();

                            if (timelineForGauge.Any())
                            {
                                var firstEntry = timelineForGauge.First();
                                if (firstEntry.FreeMachinesDate != DateTime.MinValue)
                                {
                                    freeDate = firstEntry.FreeMachinesDate;
                                }
                                else if (firstEntry.ImmediateFreeMachines > 0)
                                {
                                    freeDate = firstEntry.TodayDate != DateTime.MinValue ? firstEntry.TodayDate : DateTime.Today;
                                }
                                else
                                {
                                    // Find the first snapshot date where engaged machines is less than the total capacity limit
                                    foreach (var t in timelineForGauge)
                                    {
                                        int freeMachines = t.TotalActiveCapacityLimit - t.EngagedMachines;
                                        if (freeMachines > 0)
                                        {
                                            freeDate = t.PlanSnapshotDate;
                                            break;
                                        }
                                    }
                                }

                                // Fallback: if all machines are fully engaged throughout the timeline,
                                // the first machine will free up at the earliest EngagedMachinesReleaseDate
                                if (freeDate == null)
                                {
                                    var validReleaseDates = timelineForGauge
                                        .Where(t => t.EngagedMachinesReleaseDate > DateTime.Today)
                                        .Select(t => t.EngagedMachinesReleaseDate)
                                        .ToList();

                                    if (validReleaseDates.Any())
                                    {
                                        freeDate = validReleaseDates.Min();
                                    }
                                    else
                                    {
                                        freeDate = timelineForGauge.Last().PlanSnapshotDate;
                                    }
                                }
                            }
                            machine.FreeDate = freeDate ?? DateTime.Today;
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetOrderPlanningDetailAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<IEnumerable<OrderDetailByGuageDto>> GetOrderDetailByGuageAsync(string orderNo, string guage, string? flag = null)
    {
        var result = new List<OrderDetailByGuageDto>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                
                bool isSilkOrOther = string.Equals(flag, "silk", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(flag, "other", StringComparison.OrdinalIgnoreCase) ||
                                     string.Equals(flag, "linen", StringComparison.OrdinalIgnoreCase);
                                     
                using (var command = isSilkOrOther 
                    ? new SqlCommand(@"
    SELECT 
        od.order_no,
        od.order_ldate AS ShippingDate,
        ts.style_no,
        od.order_color,
        od.order_pics, 
        ISNULL(prod.total_received, 0) AS TotalReceived,
        ts.style_target,
        (od.order_pics - ISNULL(prod.total_received, 0)) AS BalanceQty,
        ROUND(
            (od.order_pics - ISNULL(prod.total_received, 0)) / 
            NULLIF(CAST(ts.style_target AS FLOAT), 0), 
        2) AS RequireDays,
        CASE WHEN ts.style_print = 1 THEN 'OK' ELSE '' END AS PrintStatus,
        CASE WHEN ts.style_embd = 1 THEN 'OK' ELSE '' END AS EmbdStatus
    FROM tbl_order AS od 
    INNER JOIN tbl_stylesheet AS ts ON od.product_name = ts.style_no
    INNER JOIN (
        SELECT r.style_no, t.tid AS masterId
        FROM tbl_tailoring AS tl
        INNER JOIN tbl_tailoring_list AS tli ON tl.tlr_list = tli.id
        INNER JOIN tbl_knitter_record AS r ON r.kr_id = tl.item_id
        INNER JOIN tbl_tailor AS t ON t.tid = tli.master_t
        GROUP BY r.style_no, t.tid
    ) AS ms ON ms.style_no = ts.style_no
    LEFT JOIN (
        SELECT 
            r.order_id, 
            COUNT(c.item_no) AS total_received 
        FROM tbl_knitter_record AS r 
        INNER JOIN tbl_knitter_recieved AS c ON r.kr_id = c.item_id 
        GROUP BY r.order_id
    ) AS prod ON od.order_id = prod.order_id
    WHERE od.order_ldate > DATEADD(DAY, -45, GETDATE()) 
      AND (CAST(ms.masterId AS VARCHAR(50)) = @guage OR @guage = '')
      AND ts.style_tailor = 1
      AND (od.order_pics - ISNULL(prod.total_received, 0)) > 0
      AND od.order_no = @orderNo
    ORDER BY od.order_ldate, style_no ASC;", connection)
                    : new SqlCommand("sp_getOrdersdateByGuage", connection))
                {
                    if (isSilkOrOther)
                    {
                        command.CommandType = CommandType.Text;
                        command.Parameters.AddWithValue("@orderNo", orderNo);
                        command.Parameters.AddWithValue("@guage", guage);
                    }
                    else
                    {
                        command.CommandType = CommandType.StoredProcedure;
                        command.Parameters.AddWithValue("@orderNo", orderNo);
                        command.Parameters.AddWithValue("@guage", guage);
                        command.Parameters.AddWithValue("@flag", flag ?? "");
                    }

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new OrderDetailByGuageDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                if (col == "orderno") dto.OrderNo = reader[i].ToString() ?? string.Empty;
                                else if (col == "orderid") dto.OrderId = Convert.ToInt32(reader[i]);
                                else if (col == "shippingdate") dto.ShippingDate = Convert.ToDateTime(reader[i]);
                                else if (col == "styleno") dto.StyleNo = reader[i].ToString() ?? string.Empty;
                                else if (col == "ordercolor") dto.OrderColor = reader[i].ToString() ?? string.Empty;
                                else if (col == "orderpics") dto.OrderPics = Convert.ToDecimal(reader[i]);
                                else if (col == "totalreceived") dto.TotalReceived = Convert.ToDecimal(reader[i]);
                                else if (col == "styletarget") dto.StyleTarget = Convert.ToDouble(reader[i]);
                                else if (col == "balanceqty") dto.BalanceQty = Convert.ToDecimal(reader[i]);
                                else if (col == "requiredays") dto.RequireDays = Convert.ToDouble(reader[i]);
                                else if (col == "printstatus") dto.PrintStatus = reader[i].ToString() ?? string.Empty;
                                else if (col == "embdstatus") dto.EmbdStatus = reader[i].ToString() ?? string.Empty;
                                else if (col == "xxxs") dto.XXXS = Convert.ToDecimal(reader[i]);
                                else if (col == "xxs") dto.XXS = Convert.ToDecimal(reader[i]);
                                else if (col == "s") dto.S = Convert.ToDecimal(reader[i]);
                                else if (col == "m") dto.M = Convert.ToDecimal(reader[i]);
                                else if (col == "l") dto.L = Convert.ToDecimal(reader[i]);
                                else if (col == "xl") dto.XL = Convert.ToDecimal(reader[i]);
                                else if (col == "xxl") dto.XXL = Convert.ToDecimal(reader[i]);
                                else if (col == "xxxl") dto.XXXL = Convert.ToDecimal(reader[i]);
                                else if (col == "osfa") dto.OSFA = Convert.ToDecimal(reader[i]);
                            }
                            result.Add(dto);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetOrderDetailByGuageAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<OrderAnalysisResultDto> GetOrderAnalysisAsync(string orderNo, string? knitType, int mode)
    {
        var result = new OrderAnalysisResultDto();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("spOrderAnalysisWhilePlaning", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@OrderNo", orderNo);
                    command.Parameters.AddWithValue("@KnitType", (object?)knitType ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Mode", mode);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (mode == 1)
                        {
                            result.DetailedAnalysis = new List<OrderAnalysisDetailedDto>();
                            while (await reader.ReadAsync())
                            {
                                var dto = new OrderAnalysisDetailedDto();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                    if (reader.IsDBNull(i)) continue;

                                    if (col == "knittype") dto.KnitType = reader[i].ToString() ?? string.Empty;
                                    else if (col == "totalqty") dto.TotalQty = Convert.ToDecimal(reader[i]);
                                    else if (col == "totalweight") dto.TotalWeight = Convert.ToDecimal(reader[i]);
                                    else if (col == "stylecount") dto.StyleCount = Convert.ToInt32(reader[i]);
                                }
                                result.DetailedAnalysis.Add(dto);
                            }
                        }
                        else if (mode == 2)
                        {
                            result.SummaryAnalysis = new List<OrderAnalysisSummaryDto>();
                            while (await reader.ReadAsync())
                            {
                                var dto = new OrderAnalysisSummaryDto();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                    if (reader.IsDBNull(i)) continue;

                                    if (col == "style") dto.Style = reader[i].ToString() ?? string.Empty;
                                    else if (col == "print") dto.Print = Convert.ToInt32(reader[i]);
                                    else if (col == "emb") dto.Emb = Convert.ToInt32(reader[i]);
                                    else if (col == "totalqty") dto.TotalQty = Convert.ToDecimal(reader[i]);
                                }
                                result.SummaryAnalysis.Add(dto);
                            }
                        }
                        else if (mode == 3)
                        {
                            result.WorkTypeAnalysis = new List<OrderAnalysisWorkTypeDto>();
                            while (await reader.ReadAsync())
                            {
                                var dto = new OrderAnalysisWorkTypeDto();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                    if (reader.IsDBNull(i)) continue;

                                    if (col == "worktype") dto.WorkType = reader[i].ToString() ?? string.Empty;
                                    else if (col == "qty") dto.Qty = Convert.ToDecimal(reader[i]);
                                }
                                result.WorkTypeAnalysis.Add(dto);
                            }
                        }
                    }

                    if (mode == 1 && result.DetailedAnalysis != null && result.DetailedAnalysis.Any())
                    {
                        DateTime? maxEndDate = null;
                        using (var cmdMax = new SqlCommand(@"
                            SELECT MAX(mpd.EndDate) 
                            FROM dbo.MasterPlan mp 
                            JOIN dbo.MasterPlanDetail mpd ON mp.MaterID = mpd.MaterID 
                            WHERE mp.OrderNo = @OrderNo", connection))
                        {
                            cmdMax.Parameters.AddWithValue("@OrderNo", orderNo);
                            var maxVal = await cmdMax.ExecuteScalarAsync();
                            if (maxVal != null && maxVal != DBNull.Value)
                            {
                                maxEndDate = Convert.ToDateTime(maxVal);
                            }
                        }

                        foreach (var item in result.DetailedAnalysis)
                        {
                            if (string.Equals(item.KnitType?.Trim(), "Knit", StringComparison.OrdinalIgnoreCase))
                            {
                                item.EstEndDate = maxEndDate;
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetOrderAnalysisAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<FabricAnalysisPlanDto> GetFabricAnalysisPlanAsync(string orderNo, string fabricType, int flag)
    {
        var result = new FabricAnalysisPlanDto();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("fabricAnalysisPlan", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@orderNo", orderNo);
                    command.Parameters.AddWithValue("@fabricType", fabricType);
                    command.Parameters.AddWithValue("@flag", flag);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // First Result Set: MasterWorkload
                        while (await reader.ReadAsync())
                        {
                            var dto = new FabricMasterWorkloadDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                if (col == "mastername") dto.MasterName = reader[i].ToString() ?? string.Empty;
                                else if (col == "backlogqty") dto.BacklogQty = Convert.ToDecimal(reader[i]);
                                else if (col == "neworderqty") dto.NewOrderQty = Convert.ToDecimal(reader[i]);
                                else if (col == "backlogdaysbycapacity") dto.BacklogDaysByCapacity = Convert.ToDecimal(reader[i]);
                                else if (col == "neworderdaysbycapacity") dto.NewOrderDaysByCapacity = Convert.ToDecimal(reader[i]);
                                else if (col == "masterid") dto.MasterId = reader[i].ToString()?.Trim();
                                else if (col == "activeplanqty") dto.ActivePlanQty = Convert.ToDecimal(reader[i]);
                                else if (col == "runningmachines") dto.RunningMachines = Convert.ToInt32(reader[i]);
                                else if (col == "masterfreedate") dto.MasterFreeDate = Convert.ToDateTime(reader[i]);
                            }
                            result.MasterWorkload.Add(dto);
                        }

                        // Second Result Set: FabricBalances
                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var dto = new FabricBalanceDto();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                    if (reader.IsDBNull(i)) continue;

                                    if (col == "odno") dto.Odno = reader[i].ToString() ?? string.Empty;
                                    else if (col == "productid") dto.Product_Id = Convert.ToInt32(reader[i]);
                                    else if (col == "pr") dto.Pr = reader[i].ToString() ?? string.Empty;
                                    else if (col == "rql") dto.Rql = Convert.ToDecimal(reader[i]);
                                    else if (col == "rb") dto.Rb = Convert.ToDecimal(reader[i]);
                                    else if (col == "balance") dto.Balance = Convert.ToDecimal(reader[i]);
                                    else if (col == "color") dto.Color = reader[i].ToString() ?? string.Empty;
                                    else if (col == "totalstocklen") dto.Total_Stock_Len = Convert.ToDecimal(reader[i]);
                                    else if (col == "totalbooklength") dto.Total_BookLength = Convert.ToDecimal(reader[i]);
                                    else if (col == "availablelen") dto.Available_Len = Convert.ToDecimal(reader[i]);
                                }
                                result.FabricBalances.Add(dto);
                            }
                        }

                        // Third Result Set: Embroidery and Print Requirements
                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var dto = new FabricEmbroideryPrintDto();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                    if (reader.IsDBNull(i)) continue;

                                    if (col == "styleno") dto.StyleNo = reader[i].ToString() ?? string.Empty;
                                    else if (col == "totalorderpics") dto.TotalOrderPics = Convert.ToDecimal(reader[i]);
                                    else if (col == "isprintrequired") dto.IsPrintRequired = Convert.ToInt32(reader[i]);
                                    else if (col == "isembdrequired") dto.IsEmbdRequired = Convert.ToInt32(reader[i]);
                                }
                                result.EmbroideryPrintRequirements.Add(dto);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetFabricAnalysisPlanAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<WeaveAnalysisPlanDto> GetWeaveAnalysisPlanAsync(string orderNo, string? factoryName, int flag)
    {
        var result = new WeaveAnalysisPlanDto();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("weaveAnalysisforPlaning", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@OrderNo", orderNo);
                    command.Parameters.AddWithValue("@FactoryName", (object?)factoryName ?? DBNull.Value);
                    command.Parameters.AddWithValue("@Flag", flag);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        // First Result Set: FactorySummaries
                        while (await reader.ReadAsync())
                        {
                            var dto = new WeaveFactorySummaryDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                if (col == "weavefactory") dto.WeaveFactory = reader[i].ToString() ?? string.Empty;
                                else if (col == "qty") dto.Qty = Convert.ToInt32(reader[i]);
                                else if (col == "totalreceived") dto.TotalReceived = Convert.ToInt32(reader[i]);
                                else if (col == "totalmachineloadqty") dto.TotalMachineLoadQty = Convert.ToDecimal(reader[i]);
                                else if (col == "totalmachinesallocated") dto.TotalMachinesAllocated = Convert.ToInt32(reader[i]);
                                else if (col == "reqmachinedays") dto.ReqMachineDays = Convert.ToDouble(reader[i]);
                                else if (col == "freedate") dto.FreeDate = Convert.ToDateTime(reader[i]);
                                else if (col == "yarnstatus") dto.YarnStatus = reader[i].ToString() ?? string.Empty;
                            }
                            result.FactorySummaries.Add(dto);
                        }

                        // Second Result Set: YarnStatuses
                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var dto = new WeaveYarnStatusDto();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                    if (reader.IsDBNull(i)) continue;

                                    if (col == "productid") dto.ProductId = reader[i].ToString() ?? string.Empty;
                                    else if (col == "ordercolor") dto.OrderColor = reader[i].ToString() ?? string.Empty;
                                    else if (col == "styleguage") dto.StyleGuage = reader[i].ToString() ?? string.Empty;
                                    else if (col == "styleply") dto.StylePly = reader[i].ToString() ?? string.Empty;
                                    else if (col == "itemqty") dto.ItemQty = Convert.ToDecimal(reader[i]);
                                    else if (col == "selfwt") dto.SelfWt = Convert.ToDecimal(reader[i]);
                                    else if (col == "othwt") dto.OthWt = Convert.ToDecimal(reader[i]);
                                    else if (col == "stockqty") dto.StockQty = Convert.ToDecimal(reader[i]);
                                    else if (col == "yarnstatus") dto.YarnStatus = reader[i].ToString() ?? string.Empty;
                                }
                                result.YarnStatuses.Add(dto);
                            }
                        }

                        // Third Result Set: PrintEmbroiderySummaries
                        if (await reader.NextResultAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var dto = new WeavePrintEmbroiderySummaryDto();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                    if (reader.IsDBNull(i)) continue;

                                    if (col == "styleno") dto.StyleNo = reader[i].ToString() ?? string.Empty;
                                    else if (col == "qty") dto.Qty = Convert.ToInt32(reader[i]);
                                    else if (col == "totalreceived") dto.TotalReceived = Convert.ToInt32(reader[i]);
                                    else if (col == "styletarget") dto.StyleTarget = Convert.ToDouble(reader[i]);
                                    else if (col == "styleprintstatus") dto.StylePrintStatus = reader[i].ToString() ?? string.Empty;
                                    else if (col == "styleembdstatus") dto.StyleEmbdStatus = reader[i].ToString() ?? string.Empty;
                                    else if (col == "stylereqmachinedays") dto.StyleReqMachineDays = Convert.ToDouble(reader[i]);
                                }
                                result.PrintEmbroiderySummaries.Add(dto);
                            }
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetWeaveAnalysisPlanAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }


    private YarnPlanningStatusDto MapYarn(IDataReader reader)
    {
        var yarn = new YarnPlanningStatusDto();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
            if (reader.IsDBNull(i)) continue;

            if (col == "productid") yarn.ProductId = Convert.ToInt32(reader[i]);
            else if (col == "yarn") yarn.Yarn = reader[i].ToString() ?? string.Empty;
            else if (col == "styleguage" || col == "stylegauge") yarn.StyleGuage = reader[i].ToString() ?? string.Empty;
            else if (col == "styleply") yarn.StylePly = reader[i].ToString() ?? string.Empty;
            else if (col == "colorcount") yarn.ColorCount = Convert.ToInt32(reader[i]);
            else if (col == "stylecount") yarn.StyleCount = Convert.ToInt32(reader[i]);
            else if (col == "ordercolor" || col == "color") yarn.OrderColor = reader[i].ToString() ?? string.Empty;
            else if (col == "styleno") yarn.StyleNo = reader[i].ToString() ?? string.Empty;
            else if (col == "requiredkgs") yarn.RequiredKgs = Convert.ToDecimal(reader[i]);
            else if (col == "otherrunningkgs") yarn.OtherRunningKgs = Convert.ToDecimal(reader[i]);
            else if (col == "stockavailable" || col == "stockqty") yarn.StockAvailable = Convert.ToDecimal(reader[i]);
            else if (col == "stockstatus") yarn.StockStatus = reader[i].ToString() ?? string.Empty;
        }
        return yarn;
    }

    private MachinePlanningStatusDto MapMachine(IDataReader reader)
    {
        var machine = new MachinePlanningStatusDto();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
            if (reader.IsDBNull(i)) continue;

            if (col == "gauge" || col == "stylegauge" || col == "styleguage") machine.Gauge = reader[i].ToString() ?? string.Empty;
            else if (col == "backlogdays") machine.BacklogDays = Convert.ToDecimal(reader[i]);
            else if (col == "neworderdays") machine.NewOrderDays = Convert.ToDecimal(reader[i]);
            else if (col == "backlogqty") machine.BacklogQty = Convert.ToDecimal(reader[i]);
            else if (col == "neworderqty") machine.NewOrderQty = Convert.ToDecimal(reader[i]);
            else if (col == "truegaugelimit") machine.TrueGaugeLimit = Convert.ToInt32(reader[i]);
            else if (col == "suggestedbacklogmachines") machine.SuggestedBacklogMachines = Convert.ToInt32(reader[i]);
            else if (col == "suggestednewordermachines") machine.SuggestedNewOrderMachines = Convert.ToInt32(reader[i]);
            else if (col == "efficiencynote") machine.EfficiencyNote = reader[i].ToString() ?? string.Empty;
            else if (col == "newordertype") machine.NewOrderType = reader[i].ToString() ?? string.Empty;
            else if (col == "backlogtype") machine.BacklogType = reader[i].ToString() ?? string.Empty;
            else if (col == "yarnstatus") machine.YarnStatus = reader[i].ToString() ?? string.Empty;
        }
        return machine;
    }

    private ForwardTimelineDto MapForwardTimeline(IDataReader reader)
    {
        var timeline = new ForwardTimelineDto();
        for (int i = 0; i < reader.FieldCount; i++)
        {
            string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
            if (reader.IsDBNull(i)) continue;

            if (col == "gauge") timeline.Gauge = reader[i].ToString() ?? string.Empty;
            else if (col == "plansnapshotdate") timeline.PlanSnapshotDate = Convert.ToDateTime(reader[i]);
            else if (col == "plannedqtyload") timeline.PlannedQtyLoad = Convert.ToDecimal(reader[i]);
            else if (col == "engagedmachines") timeline.EngagedMachines = Convert.ToInt32(reader[i]);
            else if (col == "immediatefreemachines") timeline.ImmediateFreeMachines = Convert.ToInt32(reader[i]);
            else if (col == "totalactivecapacitylimit") timeline.TotalActiveCapacityLimit = Convert.ToInt32(reader[i]);
            else if (col == "freemachinesavailabletoday") timeline.FreeMachinesAvailableToday = Convert.ToInt32(reader[i]);
            else if (col == "todaydate") timeline.TodayDate = Convert.ToDateTime(reader[i]);
            else if (col == "engagedmachinesreleasedate") timeline.EngagedMachinesReleaseDate = Convert.ToDateTime(reader[i]);
            else if (col == "freemachinesdate") timeline.FreeMachinesDate = Convert.ToDateTime(reader[i]);
        }
        return timeline;
    }

    public async Task<int> SavePlanAsync(string orderNo, string guage, DateTime startDate, DateTime endDate, decimal qty, int machine, string orderType, string knitType, string userId, DateTime createdDate, List<PlanSizeLineDto>? sizeLines = null, string? machineNo = null, int? machineId = null, bool isOvertime = false, decimal overtimeHours = 0, bool workSaturday = false)
    {
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();

                // All-or-nothing: the plan row and its size lines commit together.
                using (var transaction = connection.BeginTransaction())
                {
                    try
                    {
                        int childId;
                        using (var command = new SqlCommand("doPlan", connection, transaction))
                        {
                            command.CommandType = CommandType.StoredProcedure;
                            command.Parameters.AddWithValue("@orderNo", orderNo);
                            command.Parameters.AddWithValue("@guage", guage);
                            command.Parameters.AddWithValue("@startDate", startDate);
                            command.Parameters.AddWithValue("@endDate", endDate);
                            command.Parameters.AddWithValue("@qty", qty);
                            command.Parameters.AddWithValue("@machine", machine);
                            command.Parameters.AddWithValue("@orderType", (object?)orderType ?? DBNull.Value);
                            command.Parameters.AddWithValue("@knitType", (object?)knitType ?? DBNull.Value);
                            command.Parameters.AddWithValue("@userId", (object?)userId ?? DBNull.Value);
                            command.Parameters.AddWithValue("@createdDate", createdDate);
                            command.Parameters.AddWithValue("@machineNo", (object?)machineNo ?? DBNull.Value);
                            command.Parameters.AddWithValue("@machineId", (object?)machineId ?? DBNull.Value);
                            command.Parameters.AddWithValue("@isOvertime", isOvertime);
                            command.Parameters.AddWithValue("@overtimeHours", overtimeHours);
                            command.Parameters.AddWithValue("@workSaturday", workSaturday);

                            // doPlan now returns MasterPlanChildId as the first column.
                            var result = await command.ExecuteScalarAsync();
                            childId = result != null && result != DBNull.Value ? Convert.ToInt32(result) : 0;
                        }

                        // Persist the style/color/size breakdown lines for this machine plan row.
                        if (childId > 0 && sizeLines != null && sizeLines.Count > 0)
                        {
                            foreach (var line in sizeLines)
                            {
                                if (line == null || line.Qty <= 0) continue;

                                using (var sizeCmd = new SqlCommand("saveMasterPlanDetailSize", connection, transaction))
                                {
                                    sizeCmd.CommandType = CommandType.StoredProcedure;
                                    sizeCmd.Parameters.AddWithValue("@masterPlanDetailId", childId);
                                    sizeCmd.Parameters.AddWithValue("@orderId", line.OrderId);
                                    sizeCmd.Parameters.AddWithValue("@styleNo", (object?)line.StyleNo ?? DBNull.Value);
                                    sizeCmd.Parameters.AddWithValue("@color", (object?)line.Color ?? DBNull.Value);
                                    sizeCmd.Parameters.AddWithValue("@size", (object?)line.Size ?? DBNull.Value);
                                    sizeCmd.Parameters.AddWithValue("@qty", line.Qty);
                                    await sizeCmd.ExecuteNonQueryAsync();
                                }
                            }
                        }

                        transaction.Commit();
                        return childId;
                    }
                    catch
                    {
                        try { transaction.Rollback(); } catch { /* connection already broken */ }
                        throw;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SavePlanAsync Error: {ex.Message}");
            throw;
        }
    }

    public async Task<IEnumerable<PlannedDataDto>> GetPlannedDataByOrderAsync(string orderNo, string? gauge = null, decimal? qty = null)
    {
        var list = new List<PlannedDataDto>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("listPlanedDatabyOrder", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@orderNo", orderNo);
                    command.Parameters.AddWithValue("@gauge", (object?)gauge ?? DBNull.Value);
                    command.Parameters.AddWithValue("@qty", (object?)qty ?? DBNull.Value);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var item = new PlannedDataDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                if (col == "masterplanchildid") item.MasterPlanChildId = Convert.ToInt32(reader[i]);
                                else if (col == "orderid") item.OrderId = Convert.ToInt32(reader[i]);
                                else if (col == "startdate") item.StartDate = Convert.ToDateTime(reader[i]);
                                else if (col == "gauge") item.Gauge = reader[i].ToString() ?? string.Empty;
                                else if (col == "mc") item.Mc = reader[i].ToString() ?? string.Empty;
                                else if (col == "quantity") item.Quantity = Convert.ToDecimal(reader[i]);
                                else if (col == "estenddate") item.EstEndDate = Convert.ToDateTime(reader[i]);
                                else if (col == "knittype") item.KnitType = reader[i].ToString();
                            }
                            list.Add(item);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetPlannedDataByOrderAsync Error: {ex.Message}");
            throw;
        }
        return list;
    }

    public async Task<bool> DeletePlanDetailAsync(int planDetailId)
    {
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("DELETE FROM dbo.MasterPlanDetail WHERE MasterPlanChildId = @PlanDetailId", connection))
                {
                    command.Parameters.AddWithValue("@PlanDetailId", planDetailId);
                    int affected = await command.ExecuteNonQueryAsync();
                    return affected > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DeletePlanDetailAsync Error: {ex.Message}");
            throw;
        }
    }

    public async Task<bool> UpdatePlanDetailAsync(int planDetailId, DateTime startDate, DateTime endDate, decimal qty, int machine, string userId)
    {
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(@"
                    UPDATE dbo.MasterPlanDetail 
                    SET StartDate = @StartDate, 
                        EndDate = @EndDate, 
                        Machine = CAST(@Machine AS NVARCHAR(50)), 
                        MachineCount = @Machine, 
                        Qty = @Qty, 
                        ModifyDate = @ModifyDate, 
                        ModifiedBy = @ModifiedBy 
                    WHERE MasterPlanChildId = @PlanDetailId", connection))
                {
                    command.Parameters.AddWithValue("@PlanDetailId", planDetailId);
                    command.Parameters.AddWithValue("@StartDate", startDate);
                    command.Parameters.AddWithValue("@EndDate", endDate);
                    command.Parameters.AddWithValue("@Machine", machine);
                    command.Parameters.AddWithValue("@Qty", qty);
                    command.Parameters.AddWithValue("@ModifyDate", DateTime.Now);
                    command.Parameters.AddWithValue("@ModifiedBy", (object?)userId ?? DBNull.Value);

                    int affected = await command.ExecuteNonQueryAsync();
                    return affected > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdatePlanDetailAsync Error: {ex.Message}");
            throw;
        }
    }

    public async Task<List<KnitGanttChartDto>> GetKnitGanttChartDataAsync(DateTime? startDate, DateTime? endDate, string? orderNo, string? gauge)
    {
        var result = new List<KnitGanttChartDto>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("sp_getKnitGanttChartData", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@startDateFilter", (object?)startDate ?? DBNull.Value);
                    command.Parameters.AddWithValue("@endDateFilter", (object?)endDate ?? DBNull.Value);
                    command.Parameters.AddWithValue("@orderNoFilter", (object?)orderNo ?? DBNull.Value);
                    command.Parameters.AddWithValue("@gaugeFilter", (object?)gauge ?? DBNull.Value);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new KnitGanttChartDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                if (col == "masterplanchildid") dto.MasterPlanChildId = Convert.ToInt32(reader[i]);
                                else if (col == "orderno") dto.OrderNo = reader[i].ToString() ?? string.Empty;
                                else if (col == "ordertype") dto.OrderType = reader[i].ToString() ?? string.Empty;
                                else if (col == "productiontype") dto.ProductionType = reader[i].ToString() ?? string.Empty;
                                else if (col == "orderstatus") dto.OrderStatus = reader[i].ToString() ?? string.Empty;
                                else if (col == "guage") dto.Guage = reader[i].ToString() ?? string.Empty;
                                else if (col == "startdate") dto.StartDate = Convert.ToDateTime(reader[i]);
                                else if (col == "enddate") dto.EndDate = Convert.ToDateTime(reader[i]);
                                else if (col == "machinecount") dto.MachineCount = Convert.ToInt32(reader[i]);
                                else if (col == "qty") dto.Qty = Convert.ToInt32(reader[i]);
                                else if (col == "planingstatus") dto.PlaningStatus = reader[i].ToString() ?? string.Empty;
                                else if (col == "entrydate") dto.EntryDate = Convert.ToDateTime(reader[i]);
                                else if (col == "createdby") dto.CreatedBy = reader[i].ToString() ?? string.Empty;
                                else if (col == "machine") dto.Machine = reader[i].ToString() ?? string.Empty;
                                else if (col == "machineid") dto.MachineID = Convert.ToInt32(reader[i]);
                                else if (col == "knittype") dto.KnitType = reader[i].ToString();
                            }
                            result.Add(dto);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetKnitGanttChartDataAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<List<MachinePlaningDto>> GetMachinePlaningAsync(string? targetGauge = null)
    {
        var result = new List<MachinePlaningDto>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("machinePlaning", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@TargetGauge", (object?)targetGauge ?? DBNull.Value);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new MachinePlaningDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                if (col == "machineid") dto.Machine_ID = Convert.ToInt32(reader[i]);
                                else if (col == "machineno") dto.MachineNo = reader[i].ToString() ?? string.Empty;
                                else if (col == "gauge") dto.Gauge = Convert.ToDouble(reader[i]);
                                else if (col == "size") dto.Size = reader[i].ToString() ?? string.Empty;
                                else if (col == "freedate") dto.FreeDate = Convert.ToDateTime(reader[i]);
                                else if (col == "status") dto.Status = reader[i].ToString() ?? string.Empty;
                                else if (col == "orderno") dto.OrderNo = reader[i].ToString() ?? string.Empty;
                                else if (col == "plannedqty") dto.PlannedQty = Convert.ToInt32(reader[i]);
                                else if (col == "planingstatus") dto.PlaningStatus = reader[i].ToString() ?? string.Empty;
                            }
                            result.Add(dto);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetMachinePlaningAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<List<MasterPlanningRowDto>> GetMasterPlanningAsync(string? orderNo = null, string? gauge = null)
    {
        var result = new List<MasterPlanningRowDto>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("sp_GetMasterPlanning", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@orderNo", (object?)orderNo ?? DBNull.Value);
                    command.Parameters.AddWithValue("@gauge", (object?)gauge ?? DBNull.Value);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new MasterPlanningRowDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                switch (col)
                                {
                                    case "order": dto.OrderNo = reader[i].ToString() ?? string.Empty; break;
                                    case "guage": dto.Guage = reader[i].ToString() ?? string.Empty; break;
                                    case "machine": dto.Machine = reader[i].ToString() ?? string.Empty; break;
                                    case "machineid": dto.MachineID = Convert.ToInt32(reader[i]); break;
                                    case "style": dto.Style = reader[i].ToString() ?? string.Empty; break;
                                    case "color": dto.Color = reader[i].ToString() ?? string.Empty; break;
                                    case "xxxs": dto.XXXS = Convert.ToDecimal(reader[i]); break;
                                    case "xxs": dto.XXS = Convert.ToDecimal(reader[i]); break;
                                    case "xs": dto.XS = Convert.ToDecimal(reader[i]); break;
                                    case "s": dto.S = Convert.ToDecimal(reader[i]); break;
                                    case "m": dto.M = Convert.ToDecimal(reader[i]); break;
                                    case "l": dto.L = Convert.ToDecimal(reader[i]); break;
                                    case "xl": dto.XL = Convert.ToDecimal(reader[i]); break;
                                    case "xxl": dto.XXL = Convert.ToDecimal(reader[i]); break;
                                    case "xxxl": dto.XXXL = Convert.ToDecimal(reader[i]); break;
                                    case "osfa": dto.OSFA = Convert.ToDecimal(reader[i]); break;
                                    case "startdate": dto.StartDate = Convert.ToDateTime(reader[i]); break;
                                    case "enddate": dto.EndDate = Convert.ToDateTime(reader[i]); break;
                                    case "planid": dto.PlanID = Convert.ToInt32(reader[i]); break;
                                    case "orderid": dto.OrderRowId = Convert.ToInt32(reader[i]); break;
                                    case "knittype": dto.KnitType = reader[i].ToString(); break;
                                }
                            }
                            result.Add(dto);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetMasterPlanningAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<List<KnitterDto>> GetKnittersByGaugeAsync(string? gauge = null)
    {
        var result = new List<KnitterDto>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("sp_GetKnittersByGauge", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@gauge", (object?)gauge ?? DBNull.Value);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new KnitterDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                switch (col)
                                {
                                    case "cardno": dto.CardNo = reader[i].ToString() ?? string.Empty; break;
                                    case "knittername": dto.KnitterName = reader[i].ToString() ?? string.Empty; break;
                                    case "gauge": dto.Gauge = reader[i].ToString() ?? string.Empty; break;
                                    case "gaugevalue": dto.GaugeValue = Convert.ToDecimal(reader[i]); break;
                                }
                            }
                            result.Add(dto);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetKnittersByGaugeAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<List<PlaningReportDayDto>> GetPlaningReportAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var result = new List<PlaningReportDayDto>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("sp_GetPlaningReport", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@fromDate", (object?)fromDate ?? DBNull.Value);
                    command.Parameters.AddWithValue("@toDate", (object?)toDate ?? DBNull.Value);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new PlaningReportDayDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                switch (col)
                                {
                                    case "date": dto.Date = Convert.ToDateTime(reader[i]); break;
                                    case "busymachines": dto.BusyMachines = Convert.ToInt32(reader[i]); break;
                                    case "loadqty": dto.LoadQty = Convert.ToDecimal(reader[i]); break;
                                    case "knittedpc": dto.KnittedPC = Convert.ToInt32(reader[i]); break;
                                    case "shipcount": dto.ShipCount = Convert.ToInt32(reader[i]); break;
                                    case "shiporders": dto.ShipOrders = reader[i].ToString() ?? string.Empty; break;
                                    case "totalmachines": dto.TotalMachines = Convert.ToInt32(reader[i]); break;
                                    case "totalknitters": dto.TotalKnitters = Convert.ToInt32(reader[i]); break;
                                    case "dayname": dto.DayName = reader[i].ToString() ?? string.Empty; break;
                                    case "issaturday": dto.IsSaturday = Convert.ToBoolean(reader[i]); break;
                                }
                            }
                            result.Add(dto);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetPlaningReportAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<bool> SaveKnitterAssignmentAsync(int masterPlanDetailId, string cardNo, string? knitterName, string? assignedBy)
    {
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("saveKnitterAssignment", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@masterPlanDetailId", masterPlanDetailId);
                    command.Parameters.AddWithValue("@cardNo", (object?)cardNo ?? DBNull.Value);
                    command.Parameters.AddWithValue("@knitterName", (object?)knitterName ?? DBNull.Value);
                    command.Parameters.AddWithValue("@assignedBy", (object?)assignedBy ?? DBNull.Value);

                    var result = await command.ExecuteScalarAsync();
                    return result != null && result != DBNull.Value && Convert.ToInt32(result) > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SaveKnitterAssignmentAsync Error: {ex.Message}");
            throw;
        }
    }

    public async Task<List<KnitterBusyDto>> GetKnitterBusyAsync()
    {
        var result = new List<KnitterBusyDto>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("getKnitterBusy", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new KnitterBusyDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                switch (col)
                                {
                                    case "cardno": dto.CardNo = reader[i].ToString() ?? string.Empty; break;
                                    case "planid": dto.PlanId = Convert.ToInt32(reader[i]); break;
                                    case "fromdate": dto.FromDate = Convert.ToDateTime(reader[i]); break;
                                    case "todate": dto.ToDate = Convert.ToDateTime(reader[i]); break;
                                    case "status": dto.Status = reader[i].ToString() ?? "Assigned"; break;
                                }
                            }
                            result.Add(dto);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetKnitterBusyAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<bool> ManageKnitterAssignmentAsync(int masterPlanDetailId, string action)
    {
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("manageKnitterAssignment", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@masterPlanDetailId", masterPlanDetailId);
                    command.Parameters.AddWithValue("@action", action);

                    var result = await command.ExecuteScalarAsync();
                    return result != null && result != DBNull.Value && Convert.ToInt32(result) > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ManageKnitterAssignmentAsync Error: {ex.Message}");
            throw;
        }
    }

    public async Task<List<KnitterAssignmentHistoryDto>> GetKnitterAssignmentHistoryAsync(int days = 30)
    {
        var result = new List<KnitterAssignmentHistoryDto>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("getKnitterAssignmentHistory", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@days", days);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new KnitterAssignmentHistoryDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                switch (col)
                                {
                                    case "planid": dto.PlanId = Convert.ToInt32(reader[i]); break;
                                    case "orderid": dto.OrderId = Convert.ToInt32(reader[i]); break;
                                    case "gauge": dto.Gauge = reader[i].ToString() ?? string.Empty; break;
                                    case "machine": dto.Machine = reader[i].ToString() ?? string.Empty; break;
                                    case "cardno": dto.CardNo = reader[i].ToString() ?? string.Empty; break;
                                    case "knittername": dto.KnitterName = reader[i].ToString() ?? string.Empty; break;
                                    case "qty": dto.Qty = Convert.ToDecimal(reader[i]); break;
                                    case "startdate": dto.StartDate = Convert.ToDateTime(reader[i]); break;
                                    case "enddate": dto.EndDate = Convert.ToDateTime(reader[i]); break;
                                    case "status": dto.Status = reader[i].ToString() ?? string.Empty; break;
                                    case "assignedby": dto.AssignedBy = reader[i].ToString() ?? string.Empty; break;
                                    case "assigneddate": dto.AssignedDate = Convert.ToDateTime(reader[i]); break;
                                    case "completeddate": dto.CompletedDate = Convert.ToDateTime(reader[i]); break;
                                }
                            }
                            result.Add(dto);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetKnitterAssignmentHistoryAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<List<PlanSizeLineEditDto>> GetPlanSizeLinesAsync(int masterPlanDetailId)
    {
        var result = new List<PlanSizeLineEditDto>();
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("getMasterPlanSizeLines", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@masterPlanDetailId", masterPlanDetailId);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        while (await reader.ReadAsync())
                        {
                            var dto = new PlanSizeLineEditDto();
                            for (int i = 0; i < reader.FieldCount; i++)
                            {
                                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                                if (reader.IsDBNull(i)) continue;

                                switch (col)
                                {
                                    case "sizelineid": dto.SizeLineId = Convert.ToInt32(reader[i]); break;
                                    case "orderid": dto.OrderId = Convert.ToInt32(reader[i]); break;
                                    case "styleno": dto.StyleNo = reader[i].ToString() ?? string.Empty; break;
                                    case "color": dto.Color = reader[i].ToString() ?? string.Empty; break;
                                    case "size": dto.Size = reader[i].ToString() ?? string.Empty; break;
                                    case "qty": dto.Qty = Convert.ToDecimal(reader[i]); break;
                                }
                            }
                            result.Add(dto);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetPlanSizeLinesAsync Error: {ex.Message}");
            throw;
        }
        return result;
    }

    public async Task<string?> GetPlanGaugeAsync(int planDetailId)
    {
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("SELECT Guage FROM dbo.MasterPlanDetail WHERE MasterPlanChildId = @id", connection))
                {
                    command.Parameters.AddWithValue("@id", planDetailId);
                    var result = await command.ExecuteScalarAsync();
                    return result == null || result == DBNull.Value ? null : result.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetPlanGaugeAsync Error: {ex.Message}");
            throw;
        }
    }

    public async Task<string?> GetPlanKnitTypeAsync(int planDetailId)
    {
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("SELECT factory_type FROM dbo.MasterPlanDetail WHERE MasterPlanChildId = @id", connection))
                {
                    command.Parameters.AddWithValue("@id", planDetailId);
                    var result = await command.ExecuteScalarAsync();
                    return result == null || result == DBNull.Value ? null : result.ToString();
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetPlanKnitTypeAsync Error: {ex.Message}");
            throw;
        }
    }

    public async Task<int> GetSizeLinePlanIdAsync(int sizeLineId)
    {
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("SELECT MasterPlanDetailId FROM dbo.MasterPlanDetailSize WHERE id = @id", connection))
                {
                    command.Parameters.AddWithValue("@id", sizeLineId);
                    var result = await command.ExecuteScalarAsync();
                    return result == null || result == DBNull.Value ? 0 : Convert.ToInt32(result);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetSizeLinePlanIdAsync Error: {ex.Message}");
            throw;
        }
    }

    public async Task<SizeLineUpdateResultDto> UpdatePlanSizeLineAsync(int sizeLineId, decimal qty)
    {
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand("updateMasterPlanDetailSize", connection))
                {
                    command.CommandType = CommandType.StoredProcedure;
                    command.Parameters.AddWithValue("@sizeLineId", sizeLineId);
                    command.Parameters.AddWithValue("@qty", qty);

                    using (var reader = await command.ExecuteReaderAsync())
                    {
                        if (await reader.ReadAsync())
                        {
                            return new SizeLineUpdateResultDto
                            {
                                Success = Convert.ToInt32(reader["Affected"]) > 0,
                                FinalQty = reader["FinalQty"] != DBNull.Value ? Convert.ToDecimal(reader["FinalQty"]) : 0,
                                WasClamped = reader["WasClamped"] != DBNull.Value && Convert.ToBoolean(reader["WasClamped"]),
                                MaxAllowed = reader["MaxAllowed"] != DBNull.Value ? Convert.ToDecimal(reader["MaxAllowed"]) : 0
                            };
                        }
                    }
                    return new SizeLineUpdateResultDto { Success = false };
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdatePlanSizeLineAsync Error: {ex.Message}");
            throw;
        }
    }
}

