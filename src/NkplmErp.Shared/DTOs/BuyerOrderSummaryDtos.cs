namespace NkplmErp.Shared.DTOs;
public class BuyerOrderSummaryDto
{
     public long SN { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int NotStartedOrder { get; set; }
    public int RunningOrder { get; set; }
    public int TotalOrder { get; set; } 
    
    public DateTime? JoinedDate { get; set; }
    public DateTime? RecentDate { get; set; }
    public int? TotalQty { get; set; } 
    public double? TotalWeight { get; set; } 
}

public class BuyerOrderHistoryDto
{
    public int CustomerId { get; set; }
    public int Year { get; set; }
    public int Silk { get; set; } 
    public int Knit { get; set; }
    public int Weave { get; set; }
    public int Linen { get; set; } 
    public int Other { get; set; }
    public int TotalPcs { get; set; }
    public decimal NoofPos { get; set; }
    public decimal TotalWeight { get; set; }
    public decimal Knit_pct { get; set; }
     public decimal Weave_pct { get; set; }
      public decimal Silk_pct { get; set; }
       public decimal Linen_pct { get; set; }
        public decimal Other_pct { get; set; }
        public string MonthName { get; set; } = string.Empty;

    
}
public class BuyerProfile()
{
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public DateOnly? JoinedDate { get; set; }
    public DateOnly? RecentDate { get; set; }
    public int? TotalPcs { get; set; }
    public int? DistinctOrders { get; set; }
    public int? OrderYear { get; set; }

}
public class AbsentBuyer()
{
      public long SN { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public DateOnly? JoinedDate { get; set; }
    public DateOnly? RecentDate { get; set; }
    public int? TotalPcs { get; set; }
    public string Status { get; set; } = string.Empty;
}
public class OrderStatusDetailDto
{
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string OrderNo { get; set; } = string.Empty;
    public int OrderQty { get; set; }
    public int? KnPcs { get; set; }
    public DateOnly? LatestShippingDate { get; set; }
    public string? PoNo { get; set; }
    public DateOnly? OrderEntry { get; set; }
    public DateOnly? Packingdate { get; set; }
    public string Status { get; set; } = string.Empty;
    public decimal CoveragePercent { get; set; }
    public int? DaysRemaining { get; set; }
    public string DecisionRemark { get; set; } = string.Empty;
    public string RiskFlag { get; set; } = string.Empty;
}

/// <summary>
/// Tracks the flow of a buyer order through each production department stage.
/// </summary>
public class ProductionFlowDto
{
    // ── Order identification ───────────────────────────────────────────────
    public string? OrderNo { get; set; }
    public int? PCS { get; set; }
    public DateOnly? OrderEntryStart { get; set; }
    public DateOnly? OrderEntryFinish { get; set; }
    public DateOnly? IDDate { get; set; }
    public DateOnly? ShippingDate { get; set; }
    public int? ProductionDays { get; set; }

    // ── Progress / dispatch ────────────────────────────────────────────────
    public int? Ns { get; set; }
    public int? Nr { get; set; }
    public int? totalDispatched { get; set; }
    public string? status { get; set; }

    // ── Production stage quantities (all nullable – SQL may return NULL) ───
    /// <summary>P/M – Preparation / Marking</summary>
    public int? PLM { get; set; }
    /// <summary>CHK – Checking</summary>
    public int? CHK { get; set; }
    /// <summary>KCH – Knit Checking</summary>
    public int? KCH { get; set; }
    /// <summary>DYE – Dyeing</summary>
    public int? DYE { get; set; }
    /// <summary>HUB – Hubbard / Hubbing</summary>
    public int? HUB { get; set; }
    /// <summary>LNK – Linking</summary>
    public int? LNK { get; set; }
    /// <summary>MND – Mending</summary>
    public int? MND { get; set; }
    /// <summary>PRND – Printing / Pre-Needle Dry</summary>
    public int? PRND { get; set; }
    /// <summary>TLR – Tailoring</summary>
    public int? TLR { get; set; }
    /// <summary>WSH – Washing</summary>
    public int? WSH { get; set; }
    /// <summary>EMB – Embroidery</summary>
    public int? EMB { get; set; }
    /// <summary>PRS – Pressing / Ironing</summary>
    public int? PRS { get; set; }
    /// <summary>PCK – Packing</summary>
    public int? PCK { get; set; }

    // ── Totals ─────────────────────────────────────────────────────────────
    public int? totalPacked { get; set; }
    public int? Total_Dispatch { get; set; }

    // ── Max delivery / shipment dates ──────────────────────────────────────
    /// <summary>Knitting max date</summary>
    public DateOnly? KNT_maxDate { get; set; }
    /// <summary>Production max date</summary>
    public DateOnly? PRS_maxDate { get; set; }
    /// <summary>Packing max date</summary>
    public DateOnly? PCK_maxDate { get; set; }
    /// <summary>Dispatch max date</summary>
    public DateOnly? DSP_maxDate { get; set; }
    /// <summary>Shipment max date</summary>
    public DateOnly? SHP_maxDate { get; set; }
    public int  BuyerId { get; set; }
}
