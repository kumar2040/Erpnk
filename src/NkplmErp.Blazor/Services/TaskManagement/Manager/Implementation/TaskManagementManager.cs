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
            string flag, DateTime? startDate = null, DateTime? endDate = null, string? orderNo = null)
        {
            try
            {
                var url = TaskManagementEndpoint.GetTasks(flag, startDate, endDate, orderNo);
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
    }
}
