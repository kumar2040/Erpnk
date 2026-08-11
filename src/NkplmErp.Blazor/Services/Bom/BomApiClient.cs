using System.Net.Http.Json;
using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Services.Bom;

/// <summary>Typed HTTP client for the Bill of Materials (yarn requirement) API.</summary>
public class BomApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<BomApiClient> _logger;
    private const string Base = "api/v1/Bom";

    public BomApiClient(HttpClient httpClient, ILogger<BomApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>Yarn requirement / import decision for an order (flag 1 = this order's lines).</summary>
    public async Task<Response<List<BomYarnLineDto>>> GetYarnRequirementAsync(string orderNo, int flag = 1, int? poTaskId = null)
    {
        try
        {
            var taskPart = poTaskId.HasValue ? $"&poTaskId={poTaskId.Value}" : string.Empty;
            var response = await _httpClient.GetAsync($"{Base}/yarn-requirement?orderNo={Uri.EscapeDataString(orderNo)}&flag={flag}{taskPart}");
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<Response<List<BomYarnLineDto>>>()
                    ?? Response<List<BomYarnLineDto>>.Fail("The BOM API returned an empty response.");
            }
            var message = await response.Content.ReadAsStringAsync();
            return Response<List<BomYarnLineDto>>.Fail(
                string.IsNullOrWhiteSpace(message)
                    ? $"BOM request failed ({(int)response.StatusCode})."
                    : message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetYarnRequirementAsync failed");
            return Response<List<BomYarnLineDto>>.Fail(ex.Message);
        }
    }

    /// <summary>Place a yarn order; returns the generated reference (yo_no) or null on failure.</summary>
    public async Task<PlaceYarnOrderResult?> PlaceYarnOrderAsync(PlaceYarnOrderRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/yarn-order", request);
            return await response.Content.ReadFromJsonAsync<PlaceYarnOrderResult>();
        }
        catch (Exception ex) { _logger.LogError(ex, "PlaceYarnOrderAsync failed"); return null; }
    }

    /// <summary>All saved yarn orders (headers), newest first.</summary>
    /// <param name="status">Order-state filter from spDropdown 'YarnOrderStatus':
    /// 'O' ordered, 'N' not ordered, null/blank for every header.</param>
    public async Task<List<YarnOrderHeaderDto>> GetYarnOrdersAsync(string? status = null)
    {
        try
        {
            var url = $"{Base}/yarn-orders";
            if (!string.IsNullOrWhiteSpace(status))
                url += $"?status={Uri.EscapeDataString(status)}";

            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<YarnOrderHeaderDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetYarnOrdersAsync failed"); return new(); }
    }

    /// <summary>Detail lines of a saved yarn order.</summary>
    public async Task<List<YarnOrderDetailLineDto>> GetYarnOrderDetailAsync(int yoId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/yarn-orders/{yoId}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<YarnOrderDetailLineDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetYarnOrderDetailAsync failed"); return new(); }
    }

    /// <summary>Production order numbers that already have a yarn order placed.</summary>
    public async Task<List<string>> GetYarnOrderedOrdersAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/ordered-orders");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<string>>() ?? new();
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetYarnOrderedOrdersAsync failed"); return new(); }
    }

    /// <summary>Vendor sub-orders already placed under a parent yarn order.</summary>
    public async Task<List<YarnVendorOrderDto>> GetYarnVendorOrdersAsync(int yoId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/yarn-orders/{yoId}/vendor-orders");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<YarnVendorOrderDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetYarnVendorOrdersAsync failed"); return new(); }
    }

    /// <summary>Place a vendor sub-order under a parent yarn order.</summary>
    public async Task<SaveYarnVendorOrderResult?> PlaceYarnVendorOrderAsync(int yoId, SaveYarnVendorOrderRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/yarn-orders/{yoId}/vendor-orders", request);
            return await response.Content.ReadFromJsonAsync<SaveYarnVendorOrderResult>();
        }
        catch (Exception ex) { _logger.LogError(ex, "PlaceYarnVendorOrderAsync failed"); return null; }
    }

    /// <summary>Download a vendor sub-order Excel; returns (fileName, bytes) or null.</summary>
    public async Task<(string fileName, byte[] bytes)?> DownloadVendorOrderExcelAsync(int vyoId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/vendor-orders/{vyoId}/excel");
            if (!response.IsSuccessStatusCode) return null;

            var bytes = await response.Content.ReadAsByteArrayAsync();
            var fileName = response.Content.Headers.ContentDisposition?.FileNameStar
                ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                ?? $"yarn-order-{vyoId}.xlsx";
            return (fileName, bytes);
        }
        catch (Exception ex) { _logger.LogError(ex, "DownloadVendorOrderExcelAsync failed"); return null; }
    }

    /// <summary>Set the vendor-confirmed departure date.</summary>
    public async Task<bool> SetDepartureAsync(int vyoId, DateTime date)
        => await PostDateAsync($"{Base}/vendor-orders/{vyoId}/departure", date, "SetDepartureAsync");

    /// <summary>Set the arrival / ETA date.</summary>
    public async Task<bool> SetArrivalAsync(int vyoId, DateTime date)
        => await PostDateAsync($"{Base}/vendor-orders/{vyoId}/arrival", date, "SetArrivalAsync");

    private async Task<bool> PostDateAsync(string url, DateTime date, string label)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, new SetVendorOrderDateRequest { Date = date });
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { _logger.LogError(ex, "{Label} failed", label); return false; }
    }

    /// <summary>Flag one or more dropped colors on a vendor sub-order; returns the server result
    /// (Succeeded/counts/Message) or null. The body is read on failure too, so the proc's
    /// message (e.g. "No matching color lines") reaches the UI.</summary>
    public async Task<DropColorResult?> DropColorsAsync(int vyoId, DropColorRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/vendor-orders/{vyoId}/drop-color", request);
            return await response.Content.ReadFromJsonAsync<DropColorResult>();
        }
        catch (Exception ex) { _logger.LogError(ex, "DropColorsAsync failed"); return null; }
    }
}
