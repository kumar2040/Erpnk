using System.Net.Http.Json;
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

    public async Task<IEnumerable<BuyerOrderSummaryDto>> GetBuyerOrderSummaryAsync(int year, string type)
    {
        var url = $"api/v1/BuyerOrderSummary?year={year}&type={type}";
        // Console.WriteLine($"DEBUG: BuyerOrderSummaryService.GetBuyerOrderSummaryAsync - Executing GET {url}");
        
        var response = await _httpClient.GetAsync(url);
        
        if (response.RequestMessage?.Headers.Authorization != null)
        {
            var authHeader = response.RequestMessage.Headers.Authorization;
            _logger.LogInformation("DEBUG: BuyerOrderSummaryService - REQUEST SUCCESSFUL ATTACHMENT: {Url} - Auth: {Scheme} {Parameter}...", url, authHeader.Scheme, authHeader.Parameter?[..10]);
        }
        else
        {
            _logger.LogWarning("DEBUG: BuyerOrderSummaryService - REQUEST FAILED ATTACHMENT: {Url} - Authorization header is MISSING from RequestMessage", url);
        }

        _logger.LogInformation("DEBUG: BuyerOrderSummaryService - RESPONSE: {Url} - Status: {StatusCode} ({Code})", url, response.StatusCode, (int)response.StatusCode);

        if (response.IsSuccessStatusCode)
        {
            var data = await response.Content.ReadFromJsonAsync<IEnumerable<BuyerOrderSummaryDto>>() ?? Enumerable.Empty<BuyerOrderSummaryDto>();
            _logger.LogInformation("API Data Received: {Count} items", data.Count());
            return data;
        }
        
        var errorContent = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogError("API Error: Unauthorized (401). Response: {Content}", errorContent);
        }
        else
        {
            _logger.LogError("API Error: {StatusCode}. Response: {Content}", response.StatusCode, errorContent);
        }

        return Enumerable.Empty<BuyerOrderSummaryDto>();
    }
}
