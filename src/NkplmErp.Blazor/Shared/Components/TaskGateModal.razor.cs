using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NkplmErp.Blazor.Model.Task_Gate;
using NkplmErp.Blazor.Services.Task_Gate;
using NkplmErp.Blazor.Services.Task_Gate.Manager.Interface;
using NkplmErp.Blazor.Services.Toast;

namespace NkplmErp.Blazor.Shared.Components
{
    public partial class TaskGateModal : ComponentBase, IDisposable
    {
        // sessionStorage is per browser tab and clears when the tab closes. Login
        // POSTs to /auth/set-token and redirects — a full page load, but the same
        // tab and origin, so these keys survive it.
        private const string GateKey = "potask.gate.v1";
        private const string SkippedKey = "potask.gate.skipped.v1";

        // Beyond this many tasks the dot row stops being readable; the counter carries it.
        private const int MaxDots = 12;

        [Inject] private ITaskGateManager TaskGate { get; set; } = default!;
        [Inject] private TaskGateState State { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;
        [Inject] private ToastService Toast { get; set; } = default!;

        protected override void OnInitialized() => State.Changed += OnStateChanged;

        // Must be OnAfterRenderAsync, not OnInitialized: sessionStorage is only
        // reachable through JS interop and there is no JS during prerender.
        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (!firstRender || State.HasLoaded) return;
            await InitialiseAsync();
        }

        // Fail open on every path. A task popup must never be the thing that stops
        // someone using the ERP, so a 403, a timeout or a malformed payload all end
        // with the app usable and the gate simply not shown.
        private async Task InitialiseAsync()
        {
            try
            {
                var result = await TaskGate.GetQueueAsync();

                // Deliberately do NOT set the gate flag here — leaving it unset lets
                // a later reload retry after a transient failure.
                if (!result.Succeeded || result.Data is null) return;

                var skipped = await ReadIntCsvAsync(SkippedKey);
                State.Load(result.Data, skipped);

                // Already ran in this tab session. The badge still shows the count.
                if (await ReadFlagAsync(GateKey)) return;

                if (State.OpenRun()) return;

                // Nothing pending — record that so we don't re-check all session.
                await WriteAsync(GateKey, "1");
            }
            catch
            {
                // Swallow: the gate is never worth breaking the app over.
            }
        }

        private async Task StartAsync()
        {
            var task = State.Current;
            if (task?.TaskId is null || State.IsBusy) return;

            var taskId = task.TaskId.Value;

            State.IsBusy = true;
            State.Notify();

            IResponseResult result;
            try
            {
                var response = await TaskGate.StartTaskAsync(
                    new TaskGateRequestModel { TaskId = taskId.ToString() });

                result = new IResponseResult(response.Succeeded, response.Messages);
            }
            catch (Exception ex)
            {
                result = new IResponseResult(false, ex.Message);
            }
            finally
            {
                // Must be in a finally. Both buttons are disabled while IsBusy, so
                // leaking a true here would freeze a modal that has no close button.
                State.IsBusy = false;
            }

            if (!result.Succeeded)
            {
                // Show what the server actually said and keep the task on screen —
                // advancing here would silently lose the transition. Skip stays
                // available, so the user is never trapped.
                Toast.ShowError(result.Message ?? "Could not start this task.");
                State.Notify();
                return;
            }

            State.MarkStarted(taskId);
            await AfterAdvanceAsync();
        }

        private async Task SkipAsync()
        {
            var task = State.Current;
            if (task?.TaskId is null || State.IsBusy) return;

            // Skip writes nothing to the server: the task stays Scheduled and comes
            // back at the user's next login.
            State.MarkSkipped(task.TaskId.Value);
            await WriteAsync(SkippedKey, State.SkippedCsv);
            await AfterAdvanceAsync();
        }

        private async Task AfterAdvanceAsync()
        {
            if (State.IsOpen) return;

            await WriteAsync(GateKey, "1");

            if (State.StartedCount > 0)
                Toast.ShowSuccess($"{State.StartedCount} task(s) started.");
        }

        // Local carrier so the try/catch/finally above has one shape to hand on.
        private readonly record struct IResponseResult(bool Succeeded, string? Message);

        // ---------------------------------------------------------------- display --

        private int ProgressPercent =>
            State.Total == 0 ? 0 : (int)Math.Round((State.Position - 1) * 100.0 / State.Total);

        private string DotClass(int index) =>
            index < State.Position - 1 ? "is-done"
            : index == State.Position - 1 ? "is-current"
            : string.Empty;

        // Stage byte -> chip colour. Values match the StageName map in sp_ManageTaskGate.
        private static string StageClass(byte? stage) => stage switch
        {
            2 => "tg-chip-bom",
            3 => "tg-chip-planning",
            10 => "tg-chip-yarn",
            11 => "tg-chip-return",
            _ => "tg-chip-manual"
        };

        // ------------------------------------------------------------ sessionStorage --

        private async Task<bool> ReadFlagAsync(string key) =>
            await JS.InvokeAsync<string?>("sessionStorage.getItem", key) == "1";

        private Task WriteAsync(string key, string value) =>
            JS.InvokeVoidAsync("sessionStorage.setItem", key, value).AsTask();

        private async Task<List<int>> ReadIntCsvAsync(string key)
        {
            var raw = await JS.InvokeAsync<string?>("sessionStorage.getItem", key);
            var ids = new List<int>();

            if (string.IsNullOrWhiteSpace(raw)) return ids;

            foreach (var part in raw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                if (int.TryParse(part, out var id)) ids.Add(id);

            return ids;
        }

        private void OnStateChanged() => InvokeAsync(StateHasChanged);

        public void Dispose() => State.Changed -= OnStateChanged;
    }
}
