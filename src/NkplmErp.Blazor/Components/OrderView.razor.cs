using Microsoft.AspNetCore.Components;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Components;

public partial class OrderView : ComponentBase
{
    [Inject]
    private IBuyerOrderSummaryService BuyerOrderSummaryService { get; set; } = default!;

    [Parameter]
    public IEnumerable<OrderViewHeaderDto> Data { get; set; } = Array.Empty<OrderViewHeaderDto>();

    [Parameter]
    public bool IsLoading { get; set; }

    [Parameter]
    public EventCallback<string> OnStyleSelected { get; set; }

    private List<string> SizeHeaders { get; set; } = new();
    
    private void ShowStyleDetails(string styleNo)
    {
        OnStyleSelected.InvokeAsync(styleNo);
    }

    protected override async Task OnParametersSetAsync()
    {
        await base.OnParametersSetAsync();
        ExtractSizeHeaders();
        // Production history is now loaded on-demand when a style is clicked
    }



    private void ExtractSizeHeaders()
    {
        if (Data == null || !Data.Any())
        {
            SizeHeaders = new List<string>();
            return;
        }

        // Get all unique sizes where value > 0 across all items.
        // We use string compare to ignore case, then sort via custom logic.
        SizeHeaders = Data
            .SelectMany(x => x.Sizes)
            .Where(x => x.Value > 0)
            .Select(x => x.Key.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetSizeSortOrder)
            .ToList();
    }

    private static int GetSizeSortOrder(string size)
    {
        var fixedOrder = new List<string> { "XXXS", "XXS", "XS", "S", "M", "L", "XL", "XXL", "XXXL", "OSFA" };
        int idx = fixedOrder.IndexOf(size.ToUpper());
        return idx == -1 ? 999 : idx; // Unknown sizes go to the end
    }
}
