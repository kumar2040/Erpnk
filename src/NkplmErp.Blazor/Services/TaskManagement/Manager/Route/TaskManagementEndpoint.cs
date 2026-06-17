namespace NkplmErp.Blazor.Services.TaskManagement.Manager.Route
{
    // Central place for the TaskManagement API routes.
    public static class TaskManagementEndpoint
    {
        public const string Base = "api/v1/TaskManagement";

        // GET tasks for a column within a date range, optionally filtered by order no:
        // api/v1/TaskManagement?flag=S|P|C&startDate=2026-06-16&endDate=2026-06-16&orderNo=Nksh26
        public static string GetTasks(string flag, DateTime? startDate = null, DateTime? endDate = null, string? orderNo = null)
        {
            var url = $"{Base}?flag={flag}";
            if (startDate.HasValue) url += $"&startDate={startDate.Value:yyyy-MM-dd}";
            if (endDate.HasValue) url += $"&endDate={endDate.Value:yyyy-MM-dd}";
            if (!string.IsNullOrWhiteSpace(orderNo)) url += $"&orderNo={Uri.EscapeDataString(orderNo)}";
            return url;
        }
    }
}
