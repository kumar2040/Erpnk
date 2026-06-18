namespace NkplmErp.Blazor.Services.TaskManagement.Manager.Route
{
    // Central place for the TaskManagement API routes.
    public static class TaskManagementEndpoint
    {
        public const string Base = "api/v1/TaskManagement";

        // Current user's factory scope (admin vs gauge-restricted + dropdown list).
        public const string Scope = Base + "/scope";

        // GET tasks for a column within a date range, optionally filtered by order no / factory type:
        // api/v1/TaskManagement?flag=S|P|C&startDate=2026-06-16&endDate=2026-06-16&orderNo=Nksh26&factoryType=knit
        public static string GetTasks(string flag, DateTime? startDate = null, DateTime? endDate = null, string? orderNo = null, string? factoryType = null)
        {
            var url = $"{Base}?flag={flag}";
            if (startDate.HasValue) url += $"&startDate={startDate.Value:yyyy-MM-dd}";
            if (endDate.HasValue) url += $"&endDate={endDate.Value:yyyy-MM-dd}";
            if (!string.IsNullOrWhiteSpace(orderNo)) url += $"&orderNo={Uri.EscapeDataString(orderNo)}";
            if (!string.IsNullOrWhiteSpace(factoryType)) url += $"&factoryType={Uri.EscapeDataString(factoryType)}";
            return url;
        }
    }
}
