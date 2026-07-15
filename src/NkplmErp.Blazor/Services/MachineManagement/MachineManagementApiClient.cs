using System.Net.Http.Json;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Services.MachineManagement;

/// <summary>Typed HTTP client for the Machine Management API.</summary>
public class MachineManagementApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MachineManagementApiClient> _logger;
    private const string Base = "api/v1/MachineManagement";

    public MachineManagementApiClient(HttpClient httpClient, ILogger<MachineManagementApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<List<MachineManagementDto>> GetAllMachinesAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/machines");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<MachineManagementDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetAllMachinesAsync failed"); return new(); }
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

    public async Task<MachineOperationResult?> SaveMachineAsync(SaveMachineRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/machines", request);
            return await response.Content.ReadFromJsonAsync<MachineOperationResult>();
        }
        catch (Exception ex) { _logger.LogError(ex, "SaveMachineAsync failed"); return null; }
    }

    public async Task<MachineOperationResult?> SetActiveAsync(int machineId, bool isActive)
    {
        try
        {
            var response = await _httpClient.PostAsync($"{Base}/machines/{machineId}/active?isActive={isActive}", null);
            return await response.Content.ReadFromJsonAsync<MachineOperationResult>();
        }
        catch (Exception ex) { _logger.LogError(ex, "SetActiveAsync failed"); return null; }
    }

    public async Task<MachineOperationResult?> DeleteMachineAsync(int machineId)
    {
        try
        {
            var response = await _httpClient.DeleteAsync($"{Base}/machines/{machineId}");
            return await response.Content.ReadFromJsonAsync<MachineOperationResult>();
        }
        catch (Exception ex) { _logger.LogError(ex, "DeleteMachineAsync failed"); return null; }
    }
}
