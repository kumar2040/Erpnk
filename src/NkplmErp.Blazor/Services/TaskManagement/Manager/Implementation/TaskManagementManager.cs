using NkplmErp.Blazor.Services.TaskManagement.Manager.Interface;
using NkplmErp.Blazor.Services.TaskManagement.Manager.Route;
using NkplmErp.Blazor.Services.TaskManagement.Model;
using NkplmErp.Shared.Wrapper;
using System.Net.Http.Json;
using static System.Runtime.InteropServices.JavaScript.JSType;

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

        public async Task<IResponse<List<TaskManagementResponseModel>>> GetTasksAsync(
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
                    return Response<List<TaskManagementResponseModel>>.Success(data!);
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("GetTasksAsync({Flag}) returned {Status}: {Error}",
                    flag, response.StatusCode, error);
                return Response<List<TaskManagementResponseModel>>.Fail(error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetTasksAsync({Flag}) failed", flag);
                return Response<List<TaskManagementResponseModel>>.Fail(ex.Message);
            }
        }

        public async Task<IResponse<TaskScopeResponseModel>> GetScopeAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync(TaskManagementEndpoint.Scope);

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<TaskScopeResponseModel>();
                    return Response<TaskScopeResponseModel>.Success(data!);
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("GetScopeAsync returned {Status}: {Error}", response.StatusCode, error);
                return Response<TaskScopeResponseModel>.Fail(error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetScopeAsync failed");
                return Response<TaskScopeResponseModel>.Fail(ex.Message);
            }
        }

        public async Task<IResponse<List<string>>> GetSubCategoriesAsync(string? factoryType, DateTime? startDate = null, DateTime? endDate = null)
        {
            try
            {
                var response = await _httpClient.GetAsync(TaskManagementEndpoint.GetSubCategories(factoryType, startDate, endDate));

                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<List<string>>();
                    return Response<List<string>>.Success(data ?? new List<string>());
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("GetSubCategoriesAsync({Factory}) returned {Status}: {Error}", factoryType, response.StatusCode, error);
                return Response<List<string>>.Fail(error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetSubCategoriesAsync({Factory}) failed", factoryType);
                return Response<List<string>>.Fail(ex.Message);
            }
        }

        public async Task<IResponse<SyncResultModel>> SyncAsync()
        {
            try
            {
                var response = await _httpClient.PostAsync(TaskManagementEndpoint.Sync, null);
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<SyncResultModel>();
                    return Response<SyncResultModel>.Success(data ?? new SyncResultModel { Message = "No response." });
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("SyncAsync returned {Status}: {Error}", response.StatusCode, error);
                return Response<SyncResultModel>.Fail(error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SyncAsync failed");
                return Response<SyncResultModel>.Fail(ex.Message);
            }
        }

        // ---- Order return-detail modal (KH / KD / KS) ----

        public async Task<IResponse<KnitterSummaryResponseModel?>> GetKnitterSummaryAsync(int taskId)
        {
            if (taskId <= 0)
                return Response<KnitterSummaryResponseModel?>.Fail("Invalid TaskId.");

            try
            {
                var response = await _httpClient.GetAsync(TaskManagementEndpoint.KnitterSummary(taskId));
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<KnitterSummaryResponseModel>();
                    return Response<KnitterSummaryResponseModel?>.Success(data);
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("GetKnitterSummaryAsync({TaskId}) returned {Status}: {Error}", taskId, response.StatusCode, error);
                return Response<KnitterSummaryResponseModel?>.Fail(error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetKnitterSummaryAsync({TaskId}) failed", taskId);
                return Response<KnitterSummaryResponseModel?>.Fail(ex.Message);
            }
        }

        public async Task<IResponse<List<KnitterReturnPointResponseModel>>> GetKnitterReturnSeriesAsync(string? rId)
        {
            if (string.IsNullOrWhiteSpace(rId)) 
                return Response<List<KnitterReturnPointResponseModel>>.Fail("RId is null");

            try
            {
                var response = await _httpClient.GetAsync(TaskManagementEndpoint.KnitterReturns(rId));
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<List<KnitterReturnPointResponseModel>>();
                    return Response<List<KnitterReturnPointResponseModel>>.Success(data ?? new List<KnitterReturnPointResponseModel>());
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("GetKnitterReturnSeriesAsync({RId}) returned {Status}: {Error}", rId, response.StatusCode, error);
                return Response<List<KnitterReturnPointResponseModel>>.Fail(error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetKnitterReturnSeriesAsync({RId}) failed", rId);
                return Response<List<KnitterReturnPointResponseModel>>.Fail(ex.Message);
            }
        }

        public async Task<IResponse<List<OrderStyleResponseModel>>> GetOrderStylesAsync(int taskId)
        {
            if (taskId <= 0) 
                return Response<List<OrderStyleResponseModel>>.Fail("Invalid TaskId.");

            try
            {
                var response = await _httpClient.GetAsync(TaskManagementEndpoint.OrderStyles(taskId));
                if (response.IsSuccessStatusCode)
                {
                    var data = await response.Content.ReadFromJsonAsync<List<OrderStyleResponseModel>>();
                    return Response<List<OrderStyleResponseModel>>.Success(data ?? new List<OrderStyleResponseModel>());
                }

                var error = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("GetOrderStylesAsync({TaskId}) returned {Status}: {Error}", taskId, response.StatusCode, error);
                return Response<List<OrderStyleResponseModel>>.Fail(error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetOrderStylesAsync({TaskId}) failed", taskId);
                return Response<List<OrderStyleResponseModel>>.Fail(ex.Message);
            }
        }
    }
}
