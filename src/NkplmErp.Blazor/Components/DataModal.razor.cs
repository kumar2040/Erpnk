using Microsoft.AspNetCore.Components;

namespace NkplmErp.Blazor.Components
{
    public partial class DataModal : ComponentBase
    {
        [Parameter]
        public bool IsVisible { get; set; }

        [Parameter]
        public EventCallback<bool> IsVisibleChanged { get; set; }

        [Parameter]
        public string Title { get; set; } = "Modal Title";

        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        [Parameter]
        public EventCallback OnModalOpened { get; set; }

        [Parameter]
        public bool IsLoading { get; set; }

        [Parameter]
        public bool FullScreen { get; set; }

        [Parameter]
        public string? MaxWidth { get; set; }

        [Parameter(CaptureUnmatchedValues = true)]
        public IDictionary<string, object>? AdditionalAttributes { get; set; }

        protected override async Task OnParametersSetAsync()
        {
            if (IsVisible && OnModalOpened.HasDelegate)
            {
                await OnModalOpened.InvokeAsync();
            }
        }

        private async Task CloseModal()
        {
            IsVisible = false;
            await IsVisibleChanged.InvokeAsync(IsVisible);
        }
    }
}
