using System.Net.Http.Json;
using NkplmErp.Blazor.Services.TaskManagement.Manager.Interface;
using NkplmErp.Blazor.Services.TaskManagement.Manager.Route;
using NkplmErp.Blazor.Services.TaskManagement.Model;

namespace NkplmErp.Blazor.Services.TaskManagement.Manager.Implementation
{
    public class TaskManagementManager : ITaskManagementManager
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<TaskManagementManager> _logger;

        public TaskManagementManager(HttpClient httpClient, ILogger<TaskManagementManager> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public async Task<List<TaskManagementResponseModel>> GetTasksAsync(
            string flag, DateTime? startDate = null, DateTime? endDate = null, string? orderNo = null, string? factoryType = null, string? subCategories = null)
        {
            try
            {
                var url = TaskManagementEndpoint.GetTasks(flag, startDate, endDate, orderNo, factoryType, subCategories);
                var response = await _httpClient.GetAsync(url);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content
                        .ReadFromJsonAsync<List<TaskManagementResponseModel>>();
                    return data ?? new List<TaskManagementResponseModel>();
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("GetTasksAsync({Flag}) returned {Status}: {Error}",
                    flag, response.StatusCode, error);
                return new List<TaskManagementResponseModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTasksAsync({Flag}) failed", flag);
                return new List<TaskManagementResponseModel>();
            }
        }

        public async Task<TaskScopeResponseModel> GetScopeAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(TaskManagementEndpoint.Scope);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<TaskScopeResponseModel>();
                    return data ?? new TaskScopeResponseModel();
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("GetScopeAsync returned {Status}: {Error}", response.StatusCode, error);
                return new TaskScopeResponseModel();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetScopeAsync failed");
                return new TaskScopeResponseModel();
            }
        }

        public async Task<List<string>> GetSubCategoriesAsync(string? factoryType, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var response = await _httpClient.GetAsync(TaskManagementEndpoint.GetSubCategories(factoryType, startDate, endDate));

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<List<string>>();
                    return data ?? new List<string>();
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("GetSubCategoriesAsync({Factory}) returned {Status}: {Error}", factoryType, response.StatusCode, error);
                return new List<string>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSubCategoriesAsync({Factory}) failed", factoryType);
                return new List<string>();
            }
        }

        public async Task<List<KnitterReturnPointResponseModel>> GetKnitterReturnSeriesAsync(string? rId)
        {
            if (string.IsNullOrWhiteSpace(rId)) return new List<KnitterReturnPointResponseModel>();

            try
            {
                var response = await _httpClient.GetAsync(TaskManagementEndpoint.KnitterReturns(rId));

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content
                        .ReadFromJsonAsync<List<KnitterReturnPointResponseModel>>();
                    return data ?? new List<KnitterReturnPointResponseModel>();
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("GetKnitterReturnSeriesAsync({RId}) returned {Status}: {Error}",
                    rId, response.StatusCode, error);
                return new List<KnitterReturnPointResponseModel>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetKnitterReturnSeriesAsync({RId}) failed", rId);
                return new List<KnitterReturnPointResponseModel>();
            }
        }
    }
}
