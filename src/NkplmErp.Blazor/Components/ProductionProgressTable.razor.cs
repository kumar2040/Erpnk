using Microsoft.AspNetCore.Components;
using NkplmErp.Shared.DTOs.OrderPlanningApi;

namespace NkplmErp.Blazor.Components;

// Per-style "what is made / what is left" table for the non-Knit product types (Weave, Silk, Linen, Other)
// on the Order Planning page. Purely presentational; the rows come from the third-party (PLM) API.
public partial class ProductionProgressTable : ComponentBase
{
    [Parameter] public List<ProductionProgressModel> Items { get; set; } = new();

    [Parameter] public string Title { get; set; } = "Production progress";
}
