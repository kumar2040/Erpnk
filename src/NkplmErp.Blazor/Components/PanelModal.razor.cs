using Microsoft.AspNetCore.Components;

namespace NkplmErp.Blazor.Components;

// Panel-style modal: a floating white card with a navy header bar (optional icon + title +
// close), modeled on the order-planning "Machine Allocation" popup. Reusable and
// self-contained (styles live in PanelModal.razor). Currently used by the Task Management
// return-detail modal.
public partial class PanelModal : ComponentBase
{
    [Parameter] public bool IsVisible { get; set; }
    [Parameter] public string Title { get; set; } = string.Empty;

    // Optional Font Awesome class for the header icon (e.g. "fa-solid fa-clipboard-list").
    [Parameter] public string? Icon { get; set; }

    // CSS max-width of the card (e.g. "960px").
    [Parameter] public string MaxWidth { get; set; } = "1000px";

    [Parameter] public bool ShowClose { get; set; } = true;
    [Parameter] public bool CloseOnBackdrop { get; set; } = true;

    // Extra classes for the body (e.g. to override the default padding).
    [Parameter] public string BodyClass { get; set; } = string.Empty;

    [Parameter] public RenderFragment? ChildContent { get; set; }
    [Parameter] public EventCallback OnClose { get; set; }

    private async Task CloseAsync() => await OnClose.InvokeAsync();

    private async Task OnBackdropClick()
    {
        if (CloseOnBackdrop) await CloseAsync();
    }
}
