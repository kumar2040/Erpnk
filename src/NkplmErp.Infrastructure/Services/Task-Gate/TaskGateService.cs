using System.Data;
using NkplmErp.Shared.DTOs.Task_Gate;
using NkplmErp.Application.Interfaces.Task_Gate;
using NkplmErp.Shared.DataAccess.GenericRepository;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Infrastructure.Services.Task_Gate
{
    public class TaskGateService : ITaskGateService
    {
        private readonly IGenericRepository _genericRepository;

        public TaskGateService(IGenericRepository genericRepository)
        {
            _genericRepository = genericRepository;
        }

        // sp_ManageTaskGate flag 'Q'. The proc owns the FIFO ordering, the stage
        // and priority display names, and the overdue flag.
        public async Task<IResponse<List<TaskGateResponseModel>>> GetQueueAsync(string userId)
        {
            try
            {
                var rows = await _genericRepository.GetQueryResultAsync<TaskGateResponseModel>(
                    "sp_ManageTaskGate",
                    new
                    {
                        Flag = "Q",
                        UserId = userId
                    },
                    CommandType.StoredProcedure);

                // An empty queue is a successful answer, not a failure — the gate
                // simply has nothing to show.
                return Response<List<TaskGateResponseModel>>.Success(
                    rows ?? new List<TaskGateResponseModel>());
            }
            catch (Exception ex)
            {
                return Response<List<TaskGateResponseModel>>.Fail(ex.Message);
            }
        }

        // sp_ManageTaskGate flag 'S'. userId is the server-derived acting user, not
        // anything the client sent. The proc decides what happened and supplies the
        // message, so the only logic here is success vs fail.
        public async Task<IResponse<TaskGateResponseModel>> StartTaskAsync(TaskGateRequestModel request, string userId)
        {
            try
            {
                var row = await _genericRepository.GetQueryFirstOrDefaultResultAsync<TaskGateResponseModel>(
                    "sp_ManageTaskGate",
                    new
                    {
                        Flag = "S",
                        UserId = userId,
                        request.TaskId
                    },
                    CommandType.StoredProcedure);

                if (row is null)
                    return Response<TaskGateResponseModel>.Fail("No response from procedure.");

                return row.UpdatedCount > 0
                    ? Response<TaskGateResponseModel>.Success(row, row.Message)
                    : Response<TaskGateResponseModel>.Fail(row.Message);
            }
            catch (Exception ex)
            {
                return Response<TaskGateResponseModel>.Fail(ex.Message);
            }
        }
    }
}
