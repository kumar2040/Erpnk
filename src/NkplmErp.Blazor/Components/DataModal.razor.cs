using Microsoft.AspNetCore.Components;

namespace NkplmErp.Blazor.Components
{
    public partial class DataModal : ComponentBase
    {
        private static int _globalZIndex = 3000;
        private int _currentZIndex;
        private bool _wasVisible = false;

        [Parameter]
        public bool IsVisible { get; set; }

        [Parameter]
        public EventCallback<bool> IsVisibleChanged { get; set; }

        [Parameter]
        public string Title { get; set; } = "Modal Title";
        
        [Parameter]
        public string? BadgeText { get; set; }

        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        [Parameter]
        public RenderFragment? HeaderExtra { get; set; }

        [Parameter]
        public EventCallback OnModalOpened { get; set; }

        [Parameter]
        public bool IsLoading { get; set; }

        [Parameter]
        public bool FullScreen { get; set; }

        [Parameter]
        public string? MaxWidth { get; set; }

        [Parameter]
        public int ZIndex { get; set; } = 2000;

        [Parameter]
        public bool BringToFrontOnClick { get; set; } = true;

        [Parameter(CaptureUnmatchedValues = true)]
        public IDictionary<string, object>? AdditionalAttributes { get; set; }

        protected override void OnInitialized()
        {
            _currentZIndex = ZIndex;
        }

        protected override async Task OnParametersSetAsync()
        {
            if (IsVisible && !_wasVisible)
            {
                BringToFront();
                if (OnModalOpened.HasDelegate)
                {
                    await OnModalOpened.InvokeAsync();
                }
            }
            _wasVisible = IsVisible;
        }

        public void BringToFront()
        {
            _currentZIndex = System.Threading.Interlocked.Increment(ref _globalZIndex);
        }

        public void HandleClick()
        {
            if (BringToFrontOnClick && !FullScreen)
            {
                BringToFront();
            }
        }

        private async Task CloseModal()
        {
            IsVisible = false;
            await IsVisibleChanged.InvokeAsync(IsVisible);
        }
    }
}
