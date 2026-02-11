using Microsoft.AspNetCore.Components;

namespace NkplmErp.Blazor.Components
{
    public partial class Modal : ComponentBase
    {
        /// <summary>
        /// Controls the visibility of the modal
        /// </summary>
        [Parameter]
        public bool IsVisible { get; set; }

        /// <summary>
        /// Modal title displayed in the header
        /// </summary>
        [Parameter]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Main content of the modal
        /// </summary>
        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        /// <summary>
        /// Footer content (typically buttons)
        /// </summary>
        [Parameter]
        public RenderFragment? FooterContent { get; set; }

        /// <summary>
        /// Size of the modal: sm, md, lg, xl, 2xl, 3xl, 4xl, full
        /// </summary>
        [Parameter]
        public string Size { get; set; } = "md";

        /// <summary>
        /// Show close button in header
        /// </summary>
        [Parameter]
        public bool ShowCloseButton { get; set; } = true;

        /// <summary>
        /// Close modal when clicking outside
        /// </summary>
        [Parameter]
        public bool CloseOnOverlayClick { get; set; } = true;

        /// <summary>
        /// Event callback when modal is closed
        /// </summary>
        [Parameter]
        public EventCallback OnClose { get; set; }

        /// <summary>
        /// Additional CSS classes for the body
        /// </summary>
        [Parameter]
        public string BodyClass { get; set; } = "p-6";

        /// <summary>
        /// Additional CSS classes for the footer
        /// </summary>
        [Parameter]
        public string FooterClass { get; set; } = "flex justify-end gap-3";

        /// <summary>
        /// Additional CSS classes for the overlay
        /// </summary>
        [Parameter]
        public string OverlayClass { get; set; } = string.Empty;

        /// <summary>
        /// Custom max height (e.g., "max-h-[80vh]")
        /// </summary>
        [Parameter]
        public string? MaxHeight { get; set; }

        private string SizeClass => Size switch
        {
            "sm" => "max-w-sm",
            "md" => "max-w-md",
            "lg" => "max-w-lg",
            "xl" => "max-w-xl",
            "2xl" => "max-w-2xl",
            "3xl" => "max-w-3xl",
            "4xl" => "max-w-4xl",
            "full" => "max-w-full",
            _ => "max-w-md"
        };

        private async Task HandleOverlayClick()
        {
            if (CloseOnOverlayClick)
            {
                await Close();
            }
        }

        private async Task Close()
        {
            IsVisible = false;
            await OnClose.InvokeAsync();
        }
    }
}
