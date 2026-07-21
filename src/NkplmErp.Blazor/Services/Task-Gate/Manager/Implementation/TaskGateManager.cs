using NkplmErp.Blazor.Model.Task_Gate;
using NkplmErp.Blazor.Services.Task_Gate.Manager.Interface;
using NkplmErp.Blazor.Services.Task_Gate.Manager.Route;
using NkplmErp.Blazor.Shared.Http;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Services.Task_Gate.Manager.Implementation
{
    public class TaskGateManager : ITaskGateManager
    {
        private readonly IHttpServices _http;

        public TaskGateManager(IHttpServices http)
        {
            _http = http;
        }

        public async Task<IResponse<List<TaskGateResponseModel>>> GetQueueAsync()
        {
            return await _http.GetAsync<List<TaskGateResponseModel>>(TaskGateEndpoint.Queue);
        }

        public async Task<IResponse<TaskGateResponseModel>> StartTaskAsync(TaskGateRequestModel request)
        { 
            return await _http.PostAsJsonAsync<TaskGateResponseModel>(TaskGateEndpoint.Start, request); 
        }
    }
}
