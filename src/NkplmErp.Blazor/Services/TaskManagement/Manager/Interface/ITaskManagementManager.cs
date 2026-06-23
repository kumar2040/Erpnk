using NkplmErp.Blazor.Services.TaskManagement.Model;

namespace NkplmErp.Blazor.Services.TaskManagement.Manager.Interface
{
    public interface ITaskManagementManager
    {
        // Calls the API for one board column within a date range, optionally
        // filtered by order number.
        // flag: "S" Scheduled, "P" In Progress, "C" Completed, "O" Overdue.
        // startDate/endDate: the selected period (null = no date filter; the "O"
        //   flag overlaps this window like S/P/C, +1-day grace at the start).
        // orderNo: contains-match on OrderNo (null/empty = all orders).
        // factoryType: factory scope (null/empty = all). Server forces this to a
        //   restricted user's gauge regardless of what is sent.
        // subCategories: pipe-delimited gauge sub-methods (null/empty = all).
        Task<List<TaskManagementResponseModel>> GetTasksAsync(
            string flag, DateTime? startDate = null, DateTime? endDate = null, string? orderNo = null, string? factoryType = null, string? subCategories = null);

        // Returns the current user's factory scope (admin vs gauge-restricted + the dropdown list).
        Task<TaskScopeResponseModel> GetScopeAsync();

        // Distinct gauge sub-categories for the given factory within the date window
        // (cascading sub-filter options; tailor codes resolve to names).
        Task<List<string>> GetSubCategoriesAsync(string? factoryType, DateTime? startDate = null, DateTime? endDate = null);

        // Daily returned-piece counts for one In Progress knitter card (chart data).
        // rId comes from the card; null/blank returns an empty list without a call.
        Task<List<KnitterReturnPointResponseModel>> GetKnitterReturnSeriesAsync(string? rId);
    }
}
