using NkplmErp.Shared.DTOs.Task_Gate;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Blazor.Services.Task_Gate.Manager.Interface
{
    public interface ITaskGateManager
    {
        Task<IResponse<List<TaskGateResponseModel>>> GetQueueAsync();

        Task<IResponse<TaskGateResponseModel>> StartTaskAsync(TaskGateRequestModel request);
    }
}
