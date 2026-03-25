using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Services.BuyerOrderSummary;

public class BuyerOrderSummaryService : IBuyerOrderSummaryService
{
    private readonly HttpClient _httpClient;

    private readonly ILogger<BuyerOrderSummaryService> _logger;

    public BuyerOrderSummaryService(HttpClient httpClient, ILogger<BuyerOrderSummaryService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _logger.LogInformation("DEBUG: BuyerOrderSummaryService instantiated. HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
    }

    public async Task<IEnumerable<BuyerOrderSummaryDto>> GetBuyerOrderSummaryAsync(int year, string type,int maxrec)
    {
        var url = $"api/v1/BuyerOrderSummary?year={year}&type={type}&maxrec={maxrec}";
        
        try
        {
            _logger.LogInformation("DEBUG: Making request to {Url}", url);
            var response = await _httpClient.GetAsync(url);
            
            if (response.RequestMessage?.Headers.Authorization != null)
            {
                var authHeader = response.RequestMessage.Headers.Authorization;
                _logger.LogInformation("DEBUG: BuyerOrderSummaryService - Auth Header Present: {Scheme}", authHeader.Scheme);
            }
            else
            {
                _logger.LogWarning("DEBUG: BuyerOrderSummaryService - Authorization header is MISSING");
            }

            _logger.LogInformation("DEBUG: Response Status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IEnumerable<BuyerOrderSummaryDto>>() ?? Enumerable.Empty<BuyerOrderSummaryDto>();
                _logger.LogInformation("DEBUG: API Data Received: {Count} items", data.Count());
                return data;
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("DEBUG: API Error {StatusCode}: {Content}", response.StatusCode, errorContent);
            return Enumerable.Empty<BuyerOrderSummaryDto>();
        }
        catch (HttpRequestException httpEx)
        {
            _logger.LogError(httpEx, "DEBUG: HttpRequestException - Cannot reach API at {Url}", url);
            return Enumerable.Empty<BuyerOrderSummaryDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: Unexpected error in GetBuyerOrderSummaryAsync");
            return Enumerable.Empty<BuyerOrderSummaryDto>();
        }
    }

    public async Task<IEnumerable<int>> GetBuyerOrderYearsAsync(int? customerId)
    {
        var url = $"api/v1/BuyerOrderSummary/years{(customerId.HasValue ? $"?customerId={customerId}" : "")}";
        
        try
        {
            _logger.LogInformation("DEBUG: GetBuyerOrderYearsAsync - Requesting URL: {Url}", url);
            var response = await _httpClient.GetAsync(url);
            
            _logger.LogInformation("DEBUG: GetBuyerOrderYearsAsync - Response Status: {StatusCode}", response.StatusCode);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation("DEBUG: GetBuyerOrderYearsAsync - Raw Content: {Content}", content);
                var data = await response.Content.ReadFromJsonAsync<IEnumerable<int>>() ?? Enumerable.Empty<int>();
                _logger.LogInformation("DEBUG: GetBuyerOrderYearsAsync - Parsed Count: {Count}", data.Count());
                return data;
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("DEBUG: GetBuyerOrderYearsAsync - API Error {StatusCode}: {Content}", response.StatusCode, errorContent);
            return Enumerable.Empty<int>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: GetBuyerOrderYearsAsync - Unexpected error");
            return Enumerable.Empty<int>();
        }
    }

    public async Task<IEnumerable<BuyerOrderHistoryDto>> GetBuyerOrderHistoryAsync(int customerId, int? year = null)
    {
        var url = $"api/v1/BuyerOrderSummary/history/{customerId}";
        if (year.HasValue)
        {
            url += $"?year={year.Value}";
        }
        
        try
        {
            _logger.LogInformation("DEBUG: GetBuyerOrderHistoryAsync - Requesting URL: {Url}", url);
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IEnumerable<BuyerOrderHistoryDto>>() ?? Enumerable.Empty<BuyerOrderHistoryDto>();
                _logger.LogInformation("DEBUG: GetBuyerOrderHistoryAsync - Received {Count} records", data.Count());
                return data;
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("DEBUG: GetBuyerOrderHistoryAsync - API Error {StatusCode}: {Content}", response.StatusCode, errorContent);
            return Enumerable.Empty<BuyerOrderHistoryDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: GetBuyerOrderHistoryAsync - Unexpected error");
            return Enumerable.Empty<BuyerOrderHistoryDto>();
        }
    }

    public async Task<IEnumerable<BuyerProfile>> GetBuyerProfileAsync(int customerId,int? year=null)
    {
        var url = $"api/v1/BuyerOrderSummary/profile/{customerId}";
        if (year.HasValue)
        {
            url += $"?year={year.Value}";
        }

        
        try
        {
            _logger.LogInformation("DEBUG: GetBuyerProfileAsync - Requesting URL: {Url}", url);
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IEnumerable<BuyerProfile>>() ?? Enumerable.Empty<BuyerProfile>();
                _logger.LogInformation("DEBUG: GetBuyerProfileAsync - Received {Count} records", data.Count());
                return data;
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("DEBUG: GetBuyerProfileAsync - API Error {StatusCode}: {Content}", response.StatusCode, errorContent);
            return Enumerable.Empty<BuyerProfile>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: GetBuyerProfileAsync - Unexpected error");
            return Enumerable.Empty<BuyerProfile>();
        }
    }

    public async Task<IEnumerable<AbsentBuyer>> GetAbsentBuyerAsync()
    {
        var url = "api/v1/BuyerOrderSummary/absent-buyers";
        
        try
        {
            _logger.LogInformation("DEBUG: GetAbsentBuyer - Requesting URL: {Url}", url);
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IEnumerable<AbsentBuyer>>() ?? Enumerable.Empty<AbsentBuyer>();
                _logger.LogInformation("DEBUG: GetAbsentBuyer - Received {Count} records", data.Count());
                return data;
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("DEBUG: GetAbsentBuyer - API Error {StatusCode}: {Content}", response.StatusCode, errorContent);
            return Enumerable.Empty<AbsentBuyer>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: GetAbsentBuyer - Unexpected error");
            return Enumerable.Empty<AbsentBuyer>();
        }
    }

    // Interface requires asynchronous signature. Delegate to the async implementation.
    public Task<IEnumerable<AbsentBuyer>> GetAbsentBuyer()
    {
        return GetAbsentBuyerAsync();
    }

    public async Task<IEnumerable<OrderStatusDetailDto>> GetOrderStatusDetailAsync(int year, string status)
    {
        var url = $"api/v1/BuyerOrderSummary/order-status-detail?year={year}&status={status}";
        
        try
        {
            _logger.LogInformation("DEBUG: GetOrderStatusDetailAsync - Requesting URL: {Url}", url);
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IEnumerable<OrderStatusDetailDto>>() ?? Enumerable.Empty<OrderStatusDetailDto>();
                _logger.LogInformation("DEBUG: GetOrderStatusDetailAsync - Received {Count} records", data.Count());
                return data;
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("DEBUG: GetOrderStatusDetailAsync - API Error {StatusCode}: {Content}", response.StatusCode, errorContent);
            return Enumerable.Empty<OrderStatusDetailDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: GetOrderStatusDetailAsync - Unexpected error");
            return Enumerable.Empty<OrderStatusDetailDto>();
        }
    }

    public async Task<IEnumerable<ProductionFlowDto>> GetProductionFlowAsync(int buyerId, string? orderNo = null)
    {
        var url = $"api/v1/BuyerOrderSummary/productionflow/{buyerId}";
        if (!string.IsNullOrEmpty(orderNo))
        {
            url += $"?orderNo={Uri.EscapeDataString(orderNo)}";
        }

        try
        {
            _logger.LogInformation("DEBUG: GetProductionFlowAsync - Requesting URL: {Url}", url);
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IEnumerable<ProductionFlowDto>>() ?? Enumerable.Empty<ProductionFlowDto>();
                _logger.LogInformation("DEBUG: GetProductionFlowAsync - Received {Count} records", data.Count());
                return data;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("DEBUG: GetProductionFlowAsync - API Error {StatusCode}: {Content}", response.StatusCode, errorContent);
            return Enumerable.Empty<ProductionFlowDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: GetProductionFlowAsync - Unexpected error");
            return Enumerable.Empty<ProductionFlowDto>();
        }
    }

    public async Task<IEnumerable<DepartmentStockDto>> GetdepartmentStockAsync(string? OrderNo, string Department)
    {
        var url = $"api/v1/BuyerOrderSummary/departmentstock/{Uri.EscapeDataString(Department)}";
        if (!string.IsNullOrEmpty(OrderNo))
        {
            url += $"?OrderNo={Uri.EscapeDataString(OrderNo)}";
        }

        try
        {
            _logger.LogInformation("DEBUG: GetdepartmentStockAsync - Requesting URL: {Url}", url);
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IEnumerable<DepartmentStockDto>>() ?? Enumerable.Empty<DepartmentStockDto>();
                _logger.LogInformation("DEBUG: GetdepartmentStockAsync - Received {Count} records", data.Count());
                return data;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("DEBUG: GetdepartmentStockAsync - API Error {StatusCode}: {Content}", response.StatusCode, errorContent);
            return Enumerable.Empty<DepartmentStockDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: GetdepartmentStockAsync - Unexpected error");
            return Enumerable.Empty<DepartmentStockDto>();
        }
    }
    public async Task<IEnumerable<OrderViewHeaderDto>> GetOrderViewDataAsync(string orderNo)
    {
        var url = $"api/v1/BuyerOrderSummary/order-view/{Uri.EscapeDataString(orderNo)}";
        
        try
        {
            _logger.LogInformation("DEBUG: GetOrderViewDataAsync - Requesting URL: {Url}", url);
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IEnumerable<OrderViewHeaderDto>>() ?? Enumerable.Empty<OrderViewHeaderDto>();
                _logger.LogInformation("DEBUG: GetOrderViewDataAsync - Received {Count} records", data.Count());
                return data;
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("DEBUG: GetOrderViewDataAsync - API Error {StatusCode}: {Content}", response.StatusCode, errorContent);
            return Enumerable.Empty<OrderViewHeaderDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: GetOrderViewDataAsync - Unexpected error");
            return Enumerable.Empty<OrderViewHeaderDto>();
        }
    }
    public async Task<StyleDetailsDto> GetStyleDetailsAsync(string styleNo)
    {
        var url = $"api/v1/BuyerOrderSummary/style-details/{Uri.EscapeDataString(styleNo)}";
        
        try
        {
            _logger.LogInformation("DEBUG: GetStyleDetailsAsync - Requesting URL: {Url}", url);
            var response = await _httpClient.GetAsync(url);
            
            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<StyleDetailsDto>() ?? new StyleDetailsDto();
                _logger.LogInformation("DEBUG: GetStyleDetailsAsync - Success");
                return data;
            }
            
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("DEBUG: GetStyleDetailsAsync - API Error {StatusCode}: {Content}", response.StatusCode, errorContent);
            return new StyleDetailsDto();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: GetStyleDetailsAsync - Unexpected error");
            return new StyleDetailsDto();
        }
    }

    public async Task<IEnumerable<BuyerOrderDto>> GetBuyersOrdersAsync(int buyerId, int flag)
    {
        var url = $"api/v1/BuyerOrderSummary/buyers-orders/{buyerId}?flag={flag}";

        try
        {
            _logger.LogInformation("DEBUG: GetBuyersOrdersAsync - Requesting URL: {Url}", url);
            var response = await _httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                var data = await response.Content.ReadFromJsonAsync<IEnumerable<BuyerOrderDto>>() ?? Enumerable.Empty<BuyerOrderDto>();
                _logger.LogInformation("DEBUG: GetBuyersOrdersAsync - Received {Count} records", data.Count());
                return data;
            }

            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError("DEBUG: GetBuyersOrdersAsync - API Error {StatusCode}: {Content}", response.StatusCode, errorContent);
            return Enumerable.Empty<BuyerOrderDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DEBUG: GetBuyersOrdersAsync - Unexpected error");
            return Enumerable.Empty<BuyerOrderDto>();
        }
    }
}
