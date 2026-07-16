namespace NkplmErp.Blazor.Services.TaskManagement.Manager.Route
{
    // Central place for the TaskManagement API routes.
    public static class TaskManagementEndpoint
    {
        public const string Base = "api/v1/TaskManagement";

        // Current user's factory scope (admin vs gauge-restricted + dropdown list).
        public const string Scope = Base + "/scope";

        // Incremental pull of new knitter rows from MySQL into SQL Server (POST).
        public const string Sync = Base + "/sync";

        // ---- Order return-detail modal (opened from a PO card's linked line) ----
        // KH: aggregated summary; KD: chart return series; KS: (style, colour, size) rows.
        public static string KnitterSummary(int taskId) => $"{Base}/knitter-summary?taskId={taskId}";
        public static string KnitterReturns(string rId) => $"{Base}/knitter-returns?rId={Uri.EscapeDataString(rId)}";
        public static string OrderStyles(int taskId) => $"{Base}/order-styles?taskId={taskId}";

        // Distinct gauge sub-categories for a factory within a date window (cascading options).
        public static string GetSubCategories(string? factoryType, DateTime? startDate = null, DateTime? endDate = null)
        {
            var url = Base + "/subcategories";
            var sep = '?';
            if (!string.IsNullOrWhiteSpace(factoryType)) { url += $"{sep}factoryType={Uri.EscapeDataString(factoryType)}"; sep = '&'; }
            if (startDate.HasValue) { url += $"{sep}startDate={startDate.Value:yyyy-MM-dd}"; sep = '&'; }
            if (endDate.HasValue) { url += $"{sep}endDate={endDate.Value:yyyy-MM-dd}"; sep = '&'; }
            return url;
        }

        // GET tasks for a column within a date range, optionally filtered by order no / factory / sub-category:
        // api/v1/TaskManagement?flag=S|P|C&startDate=2026-06-16&endDate=2026-06-16&orderNo=Nksh26&factoryType=knit&subCategories=general|T2
        public static string GetTasks(string flag, DateTime? startDate = null, DateTime? endDate = null, string? orderNo = null, string? factoryType = null, string? subCategories = null)
        {
            var url = $"{Base}?flag={flag}";
            if (startDate.HasValue) url += $"&startDate={startDate.Value:yyyy-MM-dd}";
            if (endDate.HasValue) url += $"&endDate={endDate.Value:yyyy-MM-dd}";
            if (!string.IsNullOrWhiteSpace(orderNo)) url += $"&orderNo={Uri.EscapeDataString(orderNo)}";
            if (!string.IsNullOrWhiteSpace(factoryType)) url += $"&factoryType={Uri.EscapeDataString(factoryType)}";
            if (!string.IsNullOrWhiteSpace(subCategories)) url += $"&subCategories={Uri.EscapeDataString(subCategories)}";
            return url;
        }
    }
}
