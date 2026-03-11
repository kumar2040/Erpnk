using Microsoft.AspNetCore.Components;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Components;

public partial class ProductionFlow : ComponentBase
{
    /// <summary>
    /// The list of production flow records to display as cards.
    /// </summary>
    [Parameter]
    public IEnumerable<ProductionFlowDto> Items { get; set; } = Enumerable.Empty<ProductionFlowDto>();

    /// <summary>
    /// Raised when the user clicks the "Details →" link on a card.
    /// </summary>
    [Parameter]
    public EventCallback<ProductionFlowDto> OnOrderSelected { get; set; }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Calculates how many days remain until the shipping date.
    /// Returns null if ShippingDate is not set.
    /// </summary>
    private static int? DaysLeft(ProductionFlowDto order)
    {
        if (order.ShippingDate is null) return null;
        var today = DateOnly.FromDateTime(DateTime.Today);
        return order.ShippingDate.Value.DayNumber - today.DayNumber;
    }

    /// <summary>
    /// Returns the progress percentage based on packed vs total PCS.
    /// Clamps result between 0 and 100.
    /// </summary>
    private static int ProgressPercent(ProductionFlowDto order)
    {
        if (order.PCS is null or 0) return 0;
        var packed = (order.totalPacked is null or 0)
            ? (order.PCK ?? 0) + (order.Total_Dispatch ?? 0) + (order.totalDispatched ?? 0)
            : order.totalPacked.Value;
        var pct = (int)Math.Round(packed * 100m / order.PCS.Value);
        return Math.Clamp(pct, 0, 100);
    }

    /// <summary>
    /// Returns the Tailwind CSS class for the progress bar fill colour
    /// based on the current percentage.
    /// </summary>
    private static string ProgressBarColor(int pct) => pct switch
    {
        >= 80 => "bg-emerald-500",
        >= 50 => "bg-indigo-600",
        >= 25 => "bg-amber-500",
        _      => "bg-rose-500"
    };
}
