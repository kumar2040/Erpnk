using NkplmErp.Blazor.Services.TaskManagement.Model;

namespace NkplmErp.Blazor.Services.TaskManagement.Manager.Interface
{
    public interface ITaskManagementManager
    {
        // Calls the API for one board column within a date range, optionally
        // filtered by order number.
        // flag: "S" Scheduled, "P" In Progress, "C" Completed.
        // startDate/endDate: the selected period (null = no date filter).
        // orderNo: contains-match on OrderNo (null/empty = all orders).
        Task<List<TaskManagementResponseModel>> GetTasksAsync(
            string flag, DateTime? startDate = null, DateTime? endDate = null, string? orderNo = null);
    }
}
