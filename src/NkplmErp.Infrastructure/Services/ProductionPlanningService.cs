using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.DTOs.OrderPlanningApi;
using NkplmErp.Shared.DataAccess.GenericRepository;
using NkplmErp.Shared.Wrapper;
using NkplmErp.ThirdPartyApi.Plm.Outbound.Manager.Interface;
using NkplmErp.ThirdPartyApi.Plm.Outbound.Route;

namespace NkplmErp.Infrastructure.Services;

public class ProductionPlanningService : IProductionPlanningService
{
    private readonly string _connectionString;
    private readonly IThirdPartyManager _thirdParty;
    private readonly IGenericRepository _repo;

    public ProductionPlanningService(IConfiguration configuration, IThirdPartyManager thirdParty, IGenericRepository repo)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
        _thirdParty = thirdParty;
        _repo = repo;
    }

    // ---- Replan, MSSQL side (sp_Replan). The procedure decides what can be re-planned and owns the message. ----

    public async Task<IResponse<List<PlanRowModel>>> GetReplanPlansAsync()
    {
        try
        {
            var rows = await _repo.GetQueryResultAsync<PlanRowModel>("sp_Replan", new { Flag = "L" }, CommandType.StoredProcedure);
            return Response<List<PlanRowModel>>.Success(rows ?? new List<PlanRowModel>());
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetReplanPlansAsync Error: {ex.Message}");
            return Response<List<PlanRowModel>>.Fail("Saved plans could not be loaded.");
        }
    }

    public async Task<IResponse<bool>> SaveReplanMachineAsync(ReplanMachineModel request, string userId)
    {
        try
        {
            var result = await _repo.GetQueryFirstOrDefaultResultAsync<ReplanResultModel>("sp_Replan", new
            {
                Flag = "M",
                request.PlanId,
                request.Machine,
                request.MachineId,
                request.StartDate,
                request.EndDate,
                UserId = userId
            }, CommandType.StoredProcedure);

            if (result is null) return Response<bool>.Fail("The plan could not be saved.");
            return result.Succeeded
                ? Response<bool>.Success(true, result.Message)
                : Response<bool>.Fail(result.Message);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"SaveReplanMachineAsync Error: {ex.Message}");
            return Response<bool>.Fail("The plan could not be saved.");
        }
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

    // Mirrors a PLM plan-row machine edit: sets MasterPlanDetail.Machine / MachineID for the plan row(s)
    // that own the (order, style, colour, size) line.
    public async Task<bool> UpdateMasterPlanMachineAsync(NkplmErp.Shared.DTOs.OrderPlanningApi.MasterPlanMachineModel request, string userId)
    {
        try
        {
            using (var connection = new SqlConnection(_connectionString))
            {
                await connection.OpenAsync();
                using (var command = new SqlCommand(@"
                    UPDATE mpd
                    SET Machine = @Machine,
                        MachineID = @MachineID,
                        ModifyDate = @ModifyDate,
                        ModifiedBy = @ModifiedBy
                    FROM dbo.MasterPlanDetail mpd
                    WHERE mpd.MasterPlanChildId IN (
                        SELECT sz.MasterPlanDetailId
                        FROM dbo.MasterPlanDetailSize sz
                        WHERE sz.order_id = @OrderId
                          AND sz.style_no = @StyleNo
                          AND sz.color = @Color
                          AND sz.size = @Size)", connection))
                {
                    var machineNo = (request.MachineNo ?? string.Empty).Trim();
                    if (machineNo.Length > 50) machineNo = machineNo.Substring(0, 50);

                    command.Parameters.AddWithValue("@OrderId", request.OrderId);
                    command.Parameters.AddWithValue("@StyleNo", request.StyleNo ?? string.Empty);
                    command.Parameters.AddWithValue("@Color", request.Color ?? string.Empty);
                    command.Parameters.AddWithValue("@Size", request.Size ?? string.Empty);
                    command.Parameters.AddWithValue("@Machine", machineNo);
                    command.Parameters.AddWithValue("@MachineID", (object?)request.MachineId ?? DBNull.Value);
                    command.Parameters.AddWithValue("@ModifyDate", DateTime.Now);
                    command.Parameters.AddWithValue("@ModifiedBy", (object?)userId ?? DBNull.Value);

                    return await command.ExecuteNonQueryAsync() > 0;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UpdateMasterPlanMachineAsync Error: {ex.Message}");
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

    // Normalize a gauge value so the skill map and plan rows match (e.g. "2.5" == 2.5).
    private static string NormGauge(string? g)
    {
        var t = (g ?? string.Empty).Trim();
        return decimal.TryParse(t, out var d) ? d.ToString(System.Globalization.CultureInfo.InvariantCulture) : t.ToLowerInvariant();
    }

    // Skill-aware knitter staffing feasibility per day in [from,to].
    // Per day, machines occupied (each needing a knitter skilled in its gauge) are
    // matched to distinct skilled knitters via bipartite maximum matching (Kuhn's).
    // A day is staffable iff every running machine can be matched.
    public async Task<List<KnitterStaffingDayDto>> GetKnitterStaffingAsync(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var result = new List<KnitterStaffingDayDto>();
        var from = (fromDate ?? DateTime.Today).Date;
        var to = (toDate ?? from.AddDays(41)).Date;

        // gauge -> set of knitter cards skilled in it
        var skillByGauge = new Dictionary<string, List<string>>();
        // machines running: (machineId, gaugeKey, start, end)
        var machines = new List<(int MachineId, string Gauge, DateTime Start, DateTime End)>();

        try
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();
            using var command = new SqlCommand("sp_GetKnitterStaffingData", connection) { CommandType = CommandType.StoredProcedure };
            command.Parameters.AddWithValue("@fromDate", from);
            command.Parameters.AddWithValue("@toDate", to);

            using var reader = await command.ExecuteReaderAsync();

            // Result 1: skill map
            while (await reader.ReadAsync())
            {
                var card = reader["CardNo"]?.ToString()?.Trim();
                var gauge = NormGauge(reader["Gauge"]?.ToString());
                if (string.IsNullOrEmpty(card) || string.IsNullOrEmpty(gauge)) continue;
                if (!skillByGauge.TryGetValue(gauge, out var list)) { list = new List<string>(); skillByGauge[gauge] = list; }
                if (!list.Contains(card)) list.Add(card);
            }

            // Result 2: running knit machines
            if (await reader.NextResultAsync())
            {
                while (await reader.ReadAsync())
                {
                    if (reader["MachineID"] == DBNull.Value) continue;
                    int mid = Convert.ToInt32(reader["MachineID"]);
                    var gauge = NormGauge(reader["Guage"]?.ToString());
                    var s = Convert.ToDateTime(reader["StartDate"]).Date;
                    var e = Convert.ToDateTime(reader["EndDate"]).Date;
                    machines.Add((mid, gauge, s, e));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"GetKnitterStaffingAsync Error: {ex.Message}");
            throw;
        }

        for (var d = from; d <= to; d = d.AddDays(1))
        {
            // One slot per distinct machine running this day (Saturdays carry no work
            // unless a plan spans them - the plan's own start/end already reflect that).
            var slots = machines
                .Where(m => d >= m.Start && d <= m.End)
                .GroupBy(m => m.MachineId)
                .Select(g => g.First().Gauge)
                .ToList();
            if (slots.Count == 0) continue;

            var (matched, unmatchedGauges) = MaxMatch(slots, skillByGauge);
            result.Add(new KnitterStaffingDayDto
            {
                Date = d,
                MachinesRunning = slots.Count,
                KnittersMatched = matched,
                ShortBy = slots.Count - matched,
                BottleneckGauges = string.Join(", ", unmatchedGauges.Distinct())
            });
        }

        return result;
    }

    // Kuhn's bipartite maximum matching: slots (machines) -> knitters skilled in the slot's gauge.
    private static (int Matched, List<string> UnmatchedGauges) MaxMatch(
        List<string> slots, Dictionary<string, List<string>> skillByGauge)
    {
        // Index knitters.
        var knitterIndex = new Dictionary<string, int>();
        foreach (var kv in skillByGauge)
            foreach (var card in kv.Value)
                if (!knitterIndex.ContainsKey(card)) knitterIndex[card] = knitterIndex.Count;

        var matchKnitter = new int[knitterIndex.Count];
        for (int i = 0; i < matchKnitter.Length; i++) matchKnitter[i] = -1;

        bool TryAssign(int slot, bool[] visited)
        {
            var gauge = slots[slot];
            if (!skillByGauge.TryGetValue(gauge, out var cards)) return false;
            foreach (var card in cards)
            {
                int k = knitterIndex[card];
                if (visited[k]) continue;
                visited[k] = true;
                if (matchKnitter[k] == -1 || TryAssign(matchKnitter[k], visited))
                {
                    matchKnitter[k] = slot;
                    return true;
                }
            }
            return false;
        }

        int matched = 0;
        var unmatchedGauges = new List<string>();
        for (int s = 0; s < slots.Count; s++)
        {
            var visited = new bool[knitterIndex.Count];
            if (TryAssign(s, visited)) matched++;
            else unmatchedGauges.Add(slots[s]);
        }
        return (matched, unmatchedGauges);
    }

    /// <summary>
    /// Assigns a knitter to a machine plan in SQL Server AND PLM, all-or-nothing from the caller's view:
    /// the SQL Server save runs in a transaction, PLM (MySQL tbl_production_plan_detail) is updated through
    /// the third-party API while that transaction is still open, and the transaction commits only if PLM
    /// accepted it. Returns false when the knitter is double-booked (nothing saved, PLM not called);
    /// throws when PLM fails, after rolling the SQL Server save back.
    /// </summary>
    public async Task<bool> SaveKnitterAssignmentAsync(int masterPlanDetailId, string cardNo, string? knitterName, string? assignedBy)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync();
        try
        {
            int affected = 0;
            var lines = new List<KnitterAssignmentLineModel>();

            await using (var command = new SqlCommand("saveKnitterAssignment", connection, transaction))
            {
                command.CommandType = CommandType.StoredProcedure;
                command.CommandTimeout = 90;
                command.Parameters.AddWithValue("@masterPlanDetailId", masterPlanDetailId);
                command.Parameters.AddWithValue("@cardNo", (object?)cardNo ?? DBNull.Value);
                command.Parameters.AddWithValue("@knitterName", (object?)knitterName ?? DBNull.Value);
                command.Parameters.AddWithValue("@assignedBy", (object?)assignedBy ?? DBNull.Value);

                await using var reader = await command.ExecuteReaderAsync();
                if (await reader.ReadAsync() && !reader.IsDBNull(0))
                    affected = Convert.ToInt32(reader[0]);

                if (affected > 0 && await reader.NextResultAsync())
                {
                    while (await reader.ReadAsync())
                    {
                        lines.Add(new KnitterAssignmentLineModel
                        {
                            SizeLineId = Convert.ToInt32(reader["SizeLineId"]),
                            OrderId = reader["OrderId"] is DBNull ? null : Convert.ToInt32(reader["OrderId"]),
                            OrderNo = reader["OrderNo"]?.ToString() ?? string.Empty,
                            Size = reader["Size"]?.ToString() ?? string.Empty,
                            Qty = Convert.ToDecimal(reader["Qty"]),
                            Machine = reader["Machine"]?.ToString() ?? string.Empty,
                            FactoryType = reader["FactoryType"]?.ToString() ?? string.Empty,
                            StartDate = reader["StartDate"]?.ToString() ?? string.Empty,
                            EndDate = reader["EndDate"]?.ToString() ?? string.Empty
                        });
                    }
                }
            }

            if (affected <= 0 || lines.Count == 0)
            {
                await transaction.RollbackAsync();
                return false;   // -1 = double-booked, 0 = no size lines
            }

            var sync = await _thirdParty.CallAsync<KnitterAssignmentSyncResultModel>(
                ThirdPartyRouter.SaveKnitterAssignment,
                new
                {
                    cardNo,
                    lines = lines.Select(l => new
                    {
                        sizeLineId = l.SizeLineId,
                        orderId = l.OrderId,
                        orderNo = l.OrderNo,
                        size = l.Size,
                        qty = l.Qty,
                        machine = l.Machine,
                        factoryType = l.FactoryType,
                        startDate = l.StartDate,
                        endDate = l.EndDate
                    }).ToList()
                });

            if (!sync.Succeeded)
                throw new InvalidOperationException("The knitter could not be saved in PLM, so nothing was changed. Please try again.");

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            try { await transaction.RollbackAsync(); } catch { /* already rolled back or connection gone */ }
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
                                    case "knittername": dto.KnitterName = reader[i].ToString() ?? string.Empty; break;
                                    case "planid": dto.PlanId = Convert.ToInt32(reader[i]); break;
                                    case "gauge": dto.Gauge = reader[i].ToString() ?? string.Empty; break;
                                    case "machine": dto.Machine = reader[i].ToString() ?? string.Empty; break;
                                    case "orderid": dto.OrderId = reader[i].ToString() ?? string.Empty; break;
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

