using NkplmErp.API.Controllers.TaskManagement.Model;

namespace NkplmErp.API.Controllers.TaskManagement.Service.Interface
{
    public interface ITaskManagementService
    {
        // Returns the plan lines for one board column, limited to the date range
        // and (optionally) an order-number search.
        // flag: "S" Scheduled, "P" In Progress, "C" Completed.
        // startDate/endDate: the selected period (null = no date filter).
        // orderNo: contains-match on OrderNo (null/empty = all orders).
        Task<IEnumerable<TaskManagementResponseModel>> GetTasksAsync(
            string flag, DateTime? startDate, DateTime? endDate, string? orderNo);
    }
}
