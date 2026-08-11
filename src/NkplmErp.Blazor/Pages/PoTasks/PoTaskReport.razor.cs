using Microsoft.AspNetCore.Components;
using NkplmErp.Blazor.Services.PoTask;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Pages.PoTasks
{
    public partial class PoTaskReport
    {
        [Inject] private PoTaskApiClient Api { get; set; } = default!;
        [Inject] private NkplmErp.Blazor.Services.RoleManagement.PermissionService PermSvc { get; set; } = default!;

        // Same permission as the board — the report is just another view of the tasks.
        private const string PageKey = "PoTask";

        private bool AccessDenied;
        private bool loading = true;

        // Default window: the last 30 days of created tasks.
        private DateTime? startDate = DateTime.Today.AddDays(-30);
        private DateTime? endDate = DateTime.Today;

        private PoTaskAgingReportResult? report;

        protected override async Task OnInitializedAsync()
        {
            if (!PermSvc.IsLoaded)
                await PermSvc.LoadPermissionsAsync();
            if (!PermSvc.CanView(PageKey))
            {
                AccessDenied = true;
                return;
            }

            await LoadAsync();
        }

        private async Task LoadAsync()
        {
            loading = true;
            StateHasChanged();

            report = await Api.GetAgingReportAsync(startDate, endDate);

            loading = false;
            StateHasChanged();
        }

        // ---- display helpers ----

        private static string Fmt(decimal? d) => d is null ? "—" : d.Value.ToString("0.#");

        // Weighted overall average cycle across stages (by completed count).
        private decimal? OverallAvgCycle
        {
            get
            {
                var done = report?.Stages.Where(s => s.AvgCycleDays is not null && s.CompletedCount > 0).ToList();
                if (done is null || done.Count == 0) return null;
                var totalCount = done.Sum(s => s.CompletedCount);
                return totalCount == 0 ? null : done.Sum(s => s.AvgCycleDays!.Value * s.CompletedCount) / totalCount;
            }
        }

        private PoTaskAgingStageDto? SlowestStage =>
            report?.Stages.Where(s => s.AvgCycleDays is not null)
                   .OrderByDescending(s => s.AvgCycleDays).FirstOrDefault();

        private string SlowestStageLabel =>
            SlowestStage is null ? "—" : $"{SlowestStage.StageName} · {Fmt(SlowestStage.AvgCycleDays)} d";

        private bool IsSlowest(PoTaskAgingStageDto s) => SlowestStage?.Stage == s.Stage;

        // Bar width relative to the slowest stage's cycle (the slowest is 100%).
        private int BarWidth(PoTaskAgingStageDto s)
        {
            var max = SlowestStage?.AvgCycleDays;
            if (max is null or <= 0 || s.AvgCycleDays is null) return 0;
            return (int)Math.Round(Math.Clamp((double)(s.AvgCycleDays.Value / max.Value) * 100, 4, 100));
        }
    }
}
