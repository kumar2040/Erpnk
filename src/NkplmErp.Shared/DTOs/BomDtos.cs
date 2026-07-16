namespace NkplmErp.Shared.DTOs;

/// <summary>
/// One yarn requirement line for an order — a single yarn (product) × color,
/// produced by the knitYarnRequirement stored procedure (flag = 1).
/// Drives the "import or knit-from-stock" decision.
/// </summary>
public class BomYarnLineDto
{
    /// <summary>Yarn product identifier (tblproduct.product_id, kept as text).</summary>
    public string ProductId { get; set; } = string.Empty;

    /// <summary>Display label: product_name + count1, e.g. "Merino Lambswool 2/28".</summary>
    public string YarnName { get; set; } = string.Empty;

    public string OrderColor { get; set; } = string.Empty;
    public string StyleGuage { get; set; } = string.Empty;
    public string StylePly { get; set; } = string.Empty;

    /// <summary>Remaining pieces of this order that drive the requirement.</summary>
    public decimal ItemQty { get; set; }

    /// <summary>Required weight (kg) for THIS order.</summary>
    public decimal SelfWt { get; set; }

    /// <summary>Weight (kg) already committed to OTHER open orders (backlog).</summary>
    public decimal OthWt { get; set; }

    /// <summary>Weight (kg) in the primary main store (tbl_cone_stock).</summary>
    public decimal MainQty { get; set; }

    /// <summary>Weight (kg) in the secondary PLM store (tbl_plm_stock).</summary>
    public decimal PlmQty { get; set; }

    /// <summary>Reusable leftover (kg) still with knitters (issued − consumed).</summary>
    public decimal KnitterQty { get; set; }

    /// <summary>Total available weight (kg) = main + PLM + with-knitter.</summary>
    public decimal StockQty { get; set; }

    /// <summary>(StockQty − OthWt) − SelfWt. Negative ⇒ shortage ⇒ must import.</summary>
    public decimal ShortfallKg { get; set; }

    /// <summary>Positive kg to import when short (absolute shortage), else 0.</summary>
    public decimal ImportKg => ShortfallKg < 0 ? -ShortfallKg : 0m;

    /// <summary>
    /// Suggested order quantity in cones: yarn is bought whole-cone (~1 kg),
    /// so a 0.1 kg need still means ordering 1 kg. Rounded UP to the next kg.
    /// </summary>
    public decimal OrderKg => ImportKg > 0 ? Math.Ceiling(ImportKg) : 0m;

    // User-editable order weight. Defaults to the cone suggestion (OrderKg);
    // the buyer can override it, and the override is what gets ordered/saved.
    private decimal? _orderQtyKg;
    public decimal OrderQtyKg
    {
        get => _orderQtyKg ?? OrderKg;
        set => _orderQtyKg = value < 0 ? 0 : value;
    }

    /// <summary>'Import' or 'In-stock'.</summary>
    public string Decision { get; set; } = string.Empty;

    /// <summary>Total requirement: this order plus backlog.</summary>
    public decimal TotalNeed => SelfWt + OthWt;

    public bool IsImport => string.Equals(Decision?.Trim(), "Import", StringComparison.OrdinalIgnoreCase);
}

/// <summary>One yarn-order line as sent to the save proc — per yarn × color × source order.</summary>
public class YarnOrderLineDto
{
    public string ProductId { get; set; } = string.Empty;
    public string YarnName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Ply { get; set; } = string.Empty;
    /// <summary>The production order this quantity is required for.</summary>
    public string OrderNo { get; set; } = string.Empty;
    public decimal ImportKg { get; set; }
}

/// <summary>Place-order request: the combined cart, expanded to per-order lines.</summary>
public class PlaceYarnOrderRequest
{
    public List<YarnOrderLineDto> Lines { get; set; } = new();
}

/// <summary>Result of saving a yarn order via sp_SaveYarnOrder.</summary>
public class PlaceYarnOrderResult
{
    public string? YoNo { get; set; }
    public int YoId { get; set; }
    public decimal TotalKg { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsSuccess => YoId > 0 && !string.IsNullOrEmpty(YoNo);
}

/// <summary>A saved yarn order header (list row).</summary>
public class YarnOrderHeaderDto
{
    public int YoId { get; set; }
    public string YoNo { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public decimal TotalKg { get; set; }
    public int OrderCount { get; set; }
    public int LineCount { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>One detail line of a saved yarn order (per yarn × color × source order).</summary>
public class YarnOrderDetailLineDto
{
    public int YodId { get; set; }
    public int YoId { get; set; }
    public string ProductId { get; set; } = string.Empty;
    public string YarnName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Ply { get; set; } = string.Empty;
    public string OrderNo { get; set; } = string.Empty;
    public decimal ImportKg { get; set; }
    /// <summary>Supplier name (vendor_id) from the most recent matching yarn import (may be null).</summary>
    public string? Vendor { get; set; }
    public string Display => string.IsNullOrWhiteSpace(YarnName) ? ProductId : YarnName;
}

/// <summary>A line to place on a vendor sub-order.</summary>
public class YarnVendorOrderLineDto
{
    public string ProductId { get; set; } = string.Empty;
    public string YarnName { get; set; } = string.Empty;
    public string Color { get; set; } = string.Empty;
    public string Ply { get; set; } = string.Empty;
    public string OrderNo { get; set; } = string.Empty;
    public decimal ImportKg { get; set; }
}

/// <summary>Place a vendor sub-order under a parent yarn order.</summary>
public class SaveYarnVendorOrderRequest
{
    public int YoId { get; set; }
    public string? Vendor { get; set; }
    public List<YarnVendorOrderLineDto> Lines { get; set; } = new();
}

/// <summary>Result of saving a vendor sub-order.</summary>
public class SaveYarnVendorOrderResult
{
    public string? VyoNo { get; set; }
    public int VyoId { get; set; }
    public decimal TotalKg { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsSuccess => VyoId > 0 && !string.IsNullOrEmpty(VyoNo);
}

/// <summary>A placed vendor sub-order (header).</summary>
public class YarnVendorOrderDto
{
    public int VyoId { get; set; }
    public int YoId { get; set; }
    public string VyoNo { get; set; } = string.Empty;
    public string? Vendor { get; set; }
    public DateTime CreatedDate { get; set; }
    public string? CreatedBy { get; set; }
    public decimal TotalKg { get; set; }
    public int LineCount { get; set; }
    public DateTime? DepartureDate { get; set; }
    public DateTime? ArrivalDate { get; set; }
    public string Status { get; set; } = string.Empty;
}

/// <summary>A vendor sub-order with its lines — used by the Excel PO export.</summary>
public class YarnVendorOrderExport
{
    public YarnVendorOrderDto? Header { get; set; }
    public List<YarnVendorOrderLineDto> Lines { get; set; } = new();
}

/// <summary>Set a vendor sub-order's departure or arrival date.</summary>
public class SetVendorOrderDateRequest
{
    public DateTime Date { get; set; }
}

/// <summary>Flag that the vendor dropped one or more colors on a vendor sub-order.</summary>
public class DropColorRequest
{
    /// <summary>Colors the vendor can't supply, as shown on the order line (e.g. "Eco Gravel 25579").</summary>
    public List<string> Colors { get; set; } = new();
    public string? Note { get; set; }
}

/// <summary>
/// Result of flagging dropped colors. NOTE: persistence is intentionally deferred for now —
/// the endpoint acknowledges success without writing to the DB (see BomController.DropColor).
/// </summary>
public class DropColorResult
{
    public bool Succeeded { get; set; }
    public string Message { get; set; } = string.Empty;
}
