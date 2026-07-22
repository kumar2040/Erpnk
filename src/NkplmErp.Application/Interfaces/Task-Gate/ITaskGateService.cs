using NkplmErp.Shared.DTOs.Task_Gate;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Application.Interfaces.Task_Gate
{
    public interface ITaskGateService
    {
        // sp_ManageTaskGate 'Q' — the caller's not-yet-started assignments, oldest first.
        Task<IResponse<List<TaskGateResponseModel>>> GetQueueAsync(string userId);

        // sp_ManageTaskGate 'S' — the caller's own assignee row moves Scheduled -> In progress.
        Task<IResponse<TaskGateResponseModel>> StartTaskAsync(TaskGateRequestModel request, string userId);
    }
}
