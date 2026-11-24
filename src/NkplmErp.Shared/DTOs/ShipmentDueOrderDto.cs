namespace NkplmErp.Shared.DTOs;

/// <summary>One order due to ship in the chosen date range (PLM shipment/due-orders).</summary>
public class ShipmentDueOrderDto
{
    public string OrderNo { get; set; } = string.Empty;
    public string? Buyer { get; set; }

    /// <summary>Ship date as yyyy-MM-dd text (dates travel as strings).</summary>
    public string ShipDate { get; set; } = string.Empty;

    /// <summary>Pieces still to ship: ordered pieces minus dispatched pieces.</summary>
    public int Quantity { get; set; }

    /// <summary>Days from today to the ship date; negative = already past.</summary>
    public int DaysLeft { get; set; }

    /// <summary>True for open orders whose ship date is before the chosen range.</summary>
    public bool IsOverdue { get; set; }
}
