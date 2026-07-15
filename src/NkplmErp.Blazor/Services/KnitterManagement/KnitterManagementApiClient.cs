using System.Net.Http.Json;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Services.KnitterManagement;

/// <summary>Typed HTTP client for the Knitter Management API.</summary>
public class KnitterManagementApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KnitterManagementApiClient> _logger;
    private const string Base = "api/v1/KnitterManagement";

    public KnitterManagementApiClient(HttpClient httpClient, ILogger<KnitterManagementApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<KnitterManagementDto>> GetAllKnittersAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/knitters");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<KnitterManagementDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAllKnittersAsync failed"); return new(); }
    }

    public async Task<List<string>> GetGaugeOptionsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/gauges");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<string>>() ?? new();
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetGaugeOptionsAsync failed"); return new(); }
    }

    public async Task<KnitterOperationResult?> SaveKnitterAsync(SaveKnitterRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/knitters", request);
            return await response.Content.ReadFromJsonAsync<KnitterOperationResult>();
        }
        catch (Exception ex) { _logger.LogError(ex, "SaveKnitterAsync failed"); return null; }
    }

    public async Task<KnitterOperationResult?> SetActiveAsync(int cardNo, bool isActive)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{Base}/knitters/{cardNo}/active?isActive={isActive}", null);
            return await response.Content.ReadFromJsonAsync<KnitterOperationResult>();
        }
        catch (Exception ex) { _logger.LogError(ex, "SetActiveAsync failed"); return null; }
    }

    public async Task<KnitterOperationResult?> DeleteKnitterAsync(int cardNo)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{Base}/knitters/{cardNo}");
            return await response.Content.ReadFromJsonAsync<KnitterOperationResult>();
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteKnitterAsync failed"); return null; }
    }
}
