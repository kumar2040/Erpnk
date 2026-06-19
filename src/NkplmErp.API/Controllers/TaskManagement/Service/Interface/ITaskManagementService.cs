using NkplmErp.API.Controllers.TaskManagement.Model;

namespace NkplmErp.API.Controllers.TaskManagement.Service.Interface
{
    public interface ITaskManagementService
    {
        // Returns the plan lines for one board column, limited to the date range
        // and (optionally) an order-number search.
        // flag: "S" Scheduled, "P" In Progress, "C" Completed, "O" Overdue.
        // startDate/endDate: the selected period (null = no date filter). The "O"
        //   (Overdue) flag uses the same window overlap as S/P/C, with a one-day
        //   grace at the start so just-overdue tasks (ended yesterday) still surface.
        // orderNo: contains-match on OrderNo (null/empty = all orders).
        // factoryType: admin's chosen factory (null/empty = all). IGNORED by the SP for a
        //   restricted user — the SP locks the scope to that user's AssignedGauge.
        // userId: the current user. The SP reads identity.Users.AssignedGauge for this id and
        //   enforces the factory scope (null/blank gauge = super admin = all factories).
        // subCategories: pipe-delimited gauge sub-methods ('general'|text); null/empty/'all' = no
        //   sub-filter. A numeric gauge maps to 'general', any other gauge to its text.
        Task<IEnumerable<TaskManagementResponseModel>> GetTasksAsync(
            string flag, DateTime? startDate, DateTime? endDate, string? orderNo, string? factoryType, string? subCategories, string? userId);

        // Distinct factory_type values present in MasterPlanDetail (for the admin dropdown).
        Task<IEnumerable<string>> GetFactoryTypesAsync();

        // Distinct gauge sub-categories for the given factory within the date window
        // (numeric -> 'general', tailor code -> tailor name, else the gauge text). The SP
        // scopes this to the user's gauge for restricted users and to the [start,end] window.
        Task<IEnumerable<string>> GetSubCategoriesAsync(string? factoryType, DateTime? startDate, DateTime? endDate, string userId);

        // The current user's resolved gauge from identity.Users.AssignedGauge
        // (null/empty = super admin / unrestricted).
        Task<string?> GetUserAssignedGaugeAsync(string userId);

        // Incrementally pull new knitter rows from MySQL (linked server) into SQL Server
        // via sp_SyncKnitterRecords. Watermark-based, so already-synced rows are skipped.
        Task<SyncResultModel> SyncKnitterRecordsAsync();
    }
}
