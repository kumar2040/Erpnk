using System.Net.Http.Json;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Services.PoTask;

/// <summary>Typed HTTP client for the PO lifecycle task board (/tasks page).</summary>
public class PoTaskApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PoTaskApiClient> _logger;
    private const string Base = "api/v1/PoTask";

    public PoTaskApiClient(HttpClient httpClient, ILogger<PoTaskApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    // ------------------------------------------------------------------ reads ----

    public async Task<List<PoTaskCardDto>> GetBoardAsync(
        string statusFlag, byte? stage = null, DateTime? startDate = null, DateTime? endDate = null,
        string? orderNo = null, string? factoryType = null)
        => await GetCardsAsync(BuildCardUrl($"{Base}/board", statusFlag, stage, startDate, endDate, orderNo, factoryType), statusFlag);

    public async Task<List<PoTaskCardDto>> GetMyTasksAsync(
        string statusFlag, byte? stage = null, DateTime? startDate = null, DateTime? endDate = null,
        string? orderNo = null, string? factoryType = null)
        => await GetCardsAsync(BuildCardUrl($"{Base}/my", statusFlag, stage, startDate, endDate, orderNo, factoryType), statusFlag);

    public async Task<PoTaskDetailResult?> GetDetailAsync(int poTaskId)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/{poTaskId}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<PoTaskDetailResult>();
            return null;
        }
        catch (Exception ex) { _logger.LogError(ex, "GetDetailAsync({Id}) failed", poTaskId); return null; }
    }

    public async Task<List<PoTaskGroupDto>> GetGroupsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/groups");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<PoTaskGroupDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetGroupsAsync failed"); return new(); }
    }

    // ----------------------------------------------------------------- writes ----

    public async Task<int?> CreateAsync(CreatePoTaskRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(Base, request);
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<CreatePoTaskResult>())?.PoTaskId;
            _logger.LogWarning("CreateAsync returned {Status}", response.StatusCode);
            return null;
        }
        catch (Exception ex) { _logger.LogError(ex, "CreateAsync failed"); return null; }
    }

    public Task<bool> AssignAsync(AssignPoTaskRequest request) => PostOkAsync($"{Base}/assign", request);

    public Task<bool> MyUpdateAsync(MyUpdatePoTaskRequest request) => PostOkAsync($"{Base}/my-update", request);

    public Task<bool> TransitionAsync(TransitionPoTaskRequest request) => PostOkAsync($"{Base}/transition", request);

    public Task<bool> HoldAsync(HoldPoTaskRequest request) => PostOkAsync($"{Base}/hold", request);

    public Task<bool> ResolveAsync(int poTaskId) => PostOkAsync($"{Base}/{poTaskId}/resolve", null);

    public Task<bool> CancelAsync(int poTaskId, string? note = null)
        => PostOkAsync($"{Base}/{poTaskId}/cancel{(string.IsNullOrWhiteSpace(note) ? "" : $"?note={Uri.EscapeDataString(note)}")}", null);

    public async Task<int?> RaiseExceptionAsync(RaiseExceptionRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/exception", request);
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<CreatePoTaskResult>())?.PoTaskId;
            return null;
        }
        catch (Exception ex) { _logger.LogError(ex, "RaiseExceptionAsync failed"); return null; }
    }

    public Task<bool> ToggleChecklistAsync(int checklistId)
        => PostOkAsync($"{Base}/checklist/toggle?checklistId={checklistId}", null);

    public async Task<bool> AlertCheckAsync(PoPlanParamRequest request)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{Base}/alert-check", request);
            if (response.IsSuccessStatusCode)
                return (await response.Content.ReadFromJsonAsync<AlertCheckResult>())?.Changed ?? false;
            return false;
        }
        catch (Exception ex) { _logger.LogError(ex, "AlertCheckAsync failed"); return false; }
    }

    // --------------------------------------------------------- notifications ----

    public async Task<List<PoTaskNotificationDto>> GetNotificationsAsync(int top = 30)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/notifications?top={top}");
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<PoTaskNotificationDto>>() ?? new();
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetNotificationsAsync failed"); return new(); }
    }

    public async Task<int> GetUnreadCountAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{Base}/notifications/unread-count");
            if (response.IsSuccessStatusCode)
            {
                var dto = await response.Content.ReadFromJsonAsync<UnreadCountResponse>();
                return dto?.UnreadCount ?? 0;
            }
            return 0;
        }
        catch (Exception ex) { _logger.LogError(ex, "GetUnreadCountAsync failed"); return 0; }
    }

    public Task<bool> MarkReadAsync(int notificationId) => PostOkAsync($"{Base}/notifications/{notificationId}/read", null);

    public Task<bool> MarkAllReadAsync() => PostOkAsync($"{Base}/notifications/read-all", null);

    private sealed class UnreadCountResponse { public int UnreadCount { get; set; } }

    // ---------------------------------------------------------------- helpers ----

    private async Task<List<PoTaskCardDto>> GetCardsAsync(string url, string statusFlag)
    {
        try
        {
            var response = await _httpClient.GetAsync(url);
            if (response.IsSuccessStatusCode)
                return await response.Content.ReadFromJsonAsync<List<PoTaskCardDto>>() ?? new();
            _logger.LogWarning("GetCards({Flag}) returned {Status}", statusFlag, response.StatusCode);
            return new();
        }
        catch (Exception ex) { _logger.LogError(ex, "GetCards({Flag}) failed", statusFlag); return new(); }
    }

    private async Task<bool> PostOkAsync(string url, object? body)
    {
        try
        {
            var response = body is null
                ? await _httpClient.PostAsync(url, null)
                : await _httpClient.PostAsJsonAsync(url, body);
            if (!response.IsSuccessStatusCode)
                _logger.LogWarning("POST {Url} returned {Status}", url, response.StatusCode);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex) { _logger.LogError(ex, "POST {Url} failed", url); return false; }
    }

    private static string BuildCardUrl(string baseUrl, string statusFlag, byte? stage,
        DateTime? startDate, DateTime? endDate, string? orderNo, string? factoryType)
    {
        var url = $"{baseUrl}?statusFlag={statusFlag}";
        if (stage.HasValue) url += $"&stage={stage.Value}";
        if (startDate.HasValue) url += $"&startDate={startDate.Value:yyyy-MM-dd}";
        if (endDate.HasValue) url += $"&endDate={endDate.Value:yyyy-MM-dd}";
        if (!string.IsNullOrWhiteSpace(orderNo)) url += $"&orderNo={Uri.EscapeDataString(orderNo)}";
        if (!string.IsNullOrWhiteSpace(factoryType)) url += $"&factoryType={Uri.EscapeDataString(factoryType)}";
        return url;
    }
}
