using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Infrastructure.Services;

/// <summary>
/// Bill of Materials / yarn requirement service. All data access goes
/// through the parameterized stored procedure knitYarnRequirement.
/// </summary>
public class BomService : IBomService
{
    private readonly string _connectionString;

    public BomService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
    }

    public async Task<List<BomYarnLineDto>> GetYarnRequirementAsync(string orderNo, int flag = 1)
    {
        var result = new List<BomYarnLineDto>();
        if (string.IsNullOrWhiteSpace(orderNo)) return result;

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("knitYarnRequirement", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@OrderNo", orderNo.Trim());
        cmd.Parameters.AddWithValue("@Flag", flag);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var dto = new BomYarnLineDto();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                string col = reader.GetName(i).Replace("_", "").Replace(" ", "").ToLower();
                if (reader.IsDBNull(i)) continue;

                switch (col)
                {
                    case "productid": dto.ProductId = reader[i].ToString()?.Trim() ?? string.Empty; break;
                    case "yarnname": dto.YarnName = reader[i].ToString()?.Trim() ?? string.Empty; break;
                    case "ordercolor": dto.OrderColor = reader[i].ToString()?.Trim() ?? string.Empty; break;
                    case "styleguage": dto.StyleGuage = reader[i].ToString()?.Trim() ?? string.Empty; break;
                    case "styleply": dto.StylePly = reader[i].ToString()?.Trim() ?? string.Empty; break;
                    case "itemqty": dto.ItemQty = ToDecimalSafe(reader[i]); break;
                    case "selfwt": dto.SelfWt = ToDecimalSafe(reader[i]); break;
                    case "othwt": dto.OthWt = ToDecimalSafe(reader[i]); break;
                    case "mainqty": dto.MainQty = ToDecimalSafe(reader[i]); break;
                    case "plmqty": dto.PlmQty = ToDecimalSafe(reader[i]); break;
                    case "knitterqty": dto.KnitterQty = ToDecimalSafe(reader[i]); break;
                    case "stockqty": dto.StockQty = ToDecimalSafe(reader[i]); break;
                    case "shortfallkg": dto.ShortfallKg = ToDecimalSafe(reader[i]); break;
                    case "decision": dto.Decision = reader[i].ToString()?.Trim() ?? string.Empty; break;
                }
            }
            result.Add(dto);
        }
        return result;
    }

    public async Task<PlaceYarnOrderResult> PlaceYarnOrderAsync(PlaceYarnOrderRequest request, string? createdBy)
    {
        if (request?.Lines == null || request.Lines.Count == 0)
            return new PlaceYarnOrderResult { YoId = -1, Message = "No lines to place." };

        // Project to the exact JSON shape the proc reads (camelCase keys).
        var payload = request.Lines.Select(l => new
        {
            productId = l.ProductId,
            yarnName = l.YarnName,
            color = l.Color,
            ply = l.Ply,
            orderNo = l.OrderNo,
            importKg = l.ImportKg
        });
        var json = JsonSerializer.Serialize(payload);

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_SaveYarnOrder", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@CreatedBy", (object?)createdBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LinesJson", json);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new PlaceYarnOrderResult
            {
                YoNo = reader["yo_no"] != DBNull.Value ? reader["yo_no"].ToString() : null,
                YoId = reader["yo_id"] != DBNull.Value ? Convert.ToInt32(reader["yo_id"]) : -1,
                TotalKg = reader["total_kg"] != DBNull.Value ? Convert.ToDecimal(reader["total_kg"]) : 0m,
                Message = reader["message"]?.ToString() ?? string.Empty
            };
        }
        return new PlaceYarnOrderResult { YoId = -1, Message = "No response from procedure." };
    }

    public async Task<List<YarnOrderHeaderDto>> GetYarnOrdersAsync()
    {
        var result = new List<YarnOrderHeaderDto>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_GetYarnOrders", connection) { CommandType = CommandType.StoredProcedure };

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new YarnOrderHeaderDto
            {
                YoId = reader["yo_id"] != DBNull.Value ? Convert.ToInt32(reader["yo_id"]) : 0,
                YoNo = reader["yo_no"]?.ToString() ?? string.Empty,
                CreatedDate = reader["created_date"] != DBNull.Value ? Convert.ToDateTime(reader["created_date"]) : default,
                CreatedBy = reader["created_by"]?.ToString(),
                TotalKg = reader["total_kg"] != DBNull.Value ? Convert.ToDecimal(reader["total_kg"]) : 0m,
                OrderCount = reader["order_count"] != DBNull.Value ? Convert.ToInt32(reader["order_count"]) : 0,
                LineCount = reader["line_count"] != DBNull.Value ? Convert.ToInt32(reader["line_count"]) : 0,
                Status = reader["status"]?.ToString() ?? string.Empty,
                order_no = reader["order_no"]?.ToString() ?? string.Empty
            });
        }
        return result;
    }

    public async Task<List<YarnOrderDetailLineDto>> GetYarnOrderDetailAsync(int yoId)
    {
        var result = new List<YarnOrderDetailLineDto>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_GetYarnOrderDetail", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@YoId", yoId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new YarnOrderDetailLineDto
            {
                YodId = reader["yod_id"] != DBNull.Value ? Convert.ToInt32(reader["yod_id"]) : 0,
                YoId = reader["yo_id"] != DBNull.Value ? Convert.ToInt32(reader["yo_id"]) : 0,
                ProductId = reader["product_id"]?.ToString() ?? string.Empty,
                YarnName = reader["yarn_name"]?.ToString() ?? string.Empty,
                Color = reader["color"]?.ToString() ?? string.Empty,
                Ply = reader["ply"]?.ToString() ?? string.Empty,
                OrderNo = reader["order_no"]?.ToString() ?? string.Empty,
                ImportKg = reader["import_kg"] != DBNull.Value ? Convert.ToDecimal(reader["import_kg"]) : 0m,
                Vendor = reader["vendor_id"] != DBNull.Value ? reader["vendor_id"].ToString() : null
            });
        }
        return result;
    }

    public async Task<List<string>> GetYarnOrderedOrdersAsync()
    {
        var result = new List<string>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_GetYarnOrderedOrders", connection) { CommandType = CommandType.StoredProcedure };

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            if (!reader.IsDBNull(0))
            {
                var no = reader[0].ToString();
                if (!string.IsNullOrWhiteSpace(no)) result.Add(no.Trim());
            }
        }
        return result;
    }

    public async Task<SaveYarnVendorOrderResult> PlaceYarnVendorOrderAsync(SaveYarnVendorOrderRequest request, string? createdBy)
    {
        if (request?.Lines == null || request.Lines.Count == 0)
            return new SaveYarnVendorOrderResult { VyoId = -1, Message = "No lines to place." };

        var payload = request.Lines.Select(l => new
        {
            productId = l.ProductId,
            yarnName = l.YarnName,
            color = l.Color,
            ply = l.Ply,
            orderNo = l.OrderNo,
            importKg = l.ImportKg
        });
        var json = JsonSerializer.Serialize(payload);

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_SaveYarnVendorOrder", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@YoId", request.YoId);
        cmd.Parameters.AddWithValue("@Vendor", (object?)request.Vendor ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CreatedBy", (object?)createdBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@LinesJson", json);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new SaveYarnVendorOrderResult
            {
                VyoNo = reader["vyo_no"] != DBNull.Value ? reader["vyo_no"].ToString() : null,
                VyoId = reader["vyo_id"] != DBNull.Value ? Convert.ToInt32(reader["vyo_id"]) : -1,
                TotalKg = reader["total_kg"] != DBNull.Value ? Convert.ToDecimal(reader["total_kg"]) : 0m,
                Message = reader["message"]?.ToString() ?? string.Empty
            };
        }
        return new SaveYarnVendorOrderResult { VyoId = -1, Message = "No response from procedure." };
    }

    public async Task<List<YarnVendorOrderDto>> GetYarnVendorOrdersAsync(int yoId)
    {
        var result = new List<YarnVendorOrderDto>();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_GetYarnVendorOrders", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@YoId", yoId);

        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            result.Add(new YarnVendorOrderDto
            {
                VyoId = reader["vyo_id"] != DBNull.Value ? Convert.ToInt32(reader["vyo_id"]) : 0,
                YoId = reader["yo_id"] != DBNull.Value ? Convert.ToInt32(reader["yo_id"]) : 0,
                VyoNo = reader["vyo_no"]?.ToString() ?? string.Empty,
                Vendor = reader["vendor"]?.ToString(),
                CreatedDate = reader["created_date"] != DBNull.Value ? Convert.ToDateTime(reader["created_date"]) : default,
                CreatedBy = reader["created_by"]?.ToString(),
                TotalKg = reader["total_kg"] != DBNull.Value ? Convert.ToDecimal(reader["total_kg"]) : 0m,
                LineCount = reader["line_count"] != DBNull.Value ? Convert.ToInt32(reader["line_count"]) : 0,
                DepartureDate = reader["departure_date"] != DBNull.Value ? Convert.ToDateTime(reader["departure_date"]) : null,
                ArrivalDate = reader["arrival_date"] != DBNull.Value ? Convert.ToDateTime(reader["arrival_date"]) : null,
                Status = reader["status"]?.ToString() ?? string.Empty
            });
        }
        return result;
    }

    public async Task<YarnVendorOrderExport> GetYarnVendorOrderAsync(int vyoId)
    {
        var export = new YarnVendorOrderExport();
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_GetYarnVendorOrder", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@VyoId", vyoId);

        using var reader = await cmd.ExecuteReaderAsync();

        // Result 1: header
        if (await reader.ReadAsync())
        {
            export.Header = new YarnVendorOrderDto
            {
                VyoId = reader["vyo_id"] != DBNull.Value ? Convert.ToInt32(reader["vyo_id"]) : 0,
                YoId = reader["yo_id"] != DBNull.Value ? Convert.ToInt32(reader["yo_id"]) : 0,
                VyoNo = reader["vyo_no"]?.ToString() ?? string.Empty,
                Vendor = reader["vendor"]?.ToString(),
                CreatedDate = reader["created_date"] != DBNull.Value ? Convert.ToDateTime(reader["created_date"]) : default,
                CreatedBy = reader["created_by"]?.ToString(),
                TotalKg = reader["total_kg"] != DBNull.Value ? Convert.ToDecimal(reader["total_kg"]) : 0m,
                LineCount = reader["line_count"] != DBNull.Value ? Convert.ToInt32(reader["line_count"]) : 0,
                DepartureDate = reader["departure_date"] != DBNull.Value ? Convert.ToDateTime(reader["departure_date"]) : null,
                ArrivalDate = reader["arrival_date"] != DBNull.Value ? Convert.ToDateTime(reader["arrival_date"]) : null,
                Status = reader["status"]?.ToString() ?? string.Empty
            };
        }

        // Result 2: lines
        if (await reader.NextResultAsync())
        {
            while (await reader.ReadAsync())
            {
                export.Lines.Add(new YarnVendorOrderLineDto
                {
                    ProductId = reader["product_id"]?.ToString() ?? string.Empty,
                    YarnName = reader["yarn_name"]?.ToString() ?? string.Empty,
                    Color = reader["color"]?.ToString() ?? string.Empty,
                    Ply = reader["ply"]?.ToString() ?? string.Empty,
                    OrderNo = reader["order_no"]?.ToString() ?? string.Empty,
                    ImportKg = reader["import_kg"] != DBNull.Value ? Convert.ToDecimal(reader["import_kg"]) : 0m
                });
            }
        }
        return export;
    }

    public async Task<DropColorResult> DropYarnColorsAsync(int vyoId, List<string> colors, string? note, string? droppedBy)
    {
        if (colors == null || colors.Count == 0)
            return new DropColorResult { Succeeded = false, Message = "No colors supplied." };

        var json = JsonSerializer.Serialize(colors);

        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_ManageYarnOrder", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@Flag", "D");
        cmd.Parameters.AddWithValue("@VyoId", vyoId);
        cmd.Parameters.AddWithValue("@ColorsJson", json);
        cmd.Parameters.AddWithValue("@DropBy", (object?)droppedBy ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DropNote", (object?)note ?? DBNull.Value);

        using var reader = await cmd.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            var dropped = reader["dropped_count"] != DBNull.Value ? Convert.ToInt32(reader["dropped_count"]) : 0;
            return new DropColorResult
            {
                Succeeded = dropped > 0,
                DroppedCount = dropped,
                MailCount = reader["mail_count"] != DBNull.Value ? Convert.ToInt32(reader["mail_count"]) : 0,
                NotifyCount = reader["notify_count"] != DBNull.Value ? Convert.ToInt32(reader["notify_count"]) : 0,
                Message = reader["message"]?.ToString() ?? string.Empty
            };
        }
        return new DropColorResult { Succeeded = false, Message = "No response from procedure." };
    }

    public async Task<bool> SetYarnVendorOrderDateAsync(int vyoId, string kind, DateTime date)
    {
        using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        using var cmd = new SqlCommand("sp_SetYarnVendorOrderDate", connection) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@VyoId", vyoId);
        cmd.Parameters.AddWithValue("@Kind", kind);
        cmd.Parameters.AddWithValue("@Date", date.Date);

        var affected = await cmd.ExecuteScalarAsync();
        return affected != null && affected != DBNull.Value && Convert.ToInt32(affected) > 0;
    }

    private static decimal ToDecimalSafe(object value)
    {
        if (value == null || value == DBNull.Value) return 0m;
        if (value is decimal d) return d;
        return decimal.TryParse(value.ToString(), out var n) ? n : 0m;
    }
}
