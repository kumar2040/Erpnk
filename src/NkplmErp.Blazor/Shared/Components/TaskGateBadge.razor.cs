using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NkplmErp.Blazor.Services.Task_Gate;

namespace NkplmErp.Blazor.Shared.Components
{
    public partial class TaskGateBadge : ComponentBase, IDisposable
    {
        private const string SkippedKey = "potask.gate.skipped.v1";

        [Inject] private TaskGateState State { get; set; } = default!;
        [Inject] private IJSRuntime JS { get; set; } = default!;

        protected override void OnInitialized() => State.Changed += OnStateChanged;

        // Reopen the queue over everything not yet started, including tasks skipped
        // earlier this session — so the session skip list is cleared here too.
        private async Task ReplayAsync()
        {
            await JS.InvokeVoidAsync("sessionStorage.removeItem", SkippedKey);
            State.Replay();
        }

        private void OnStateChanged() => InvokeAsync(StateHasChanged);

        public void Dispose() => State.Changed -= OnStateChanged;
    }
}
