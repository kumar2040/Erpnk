namespace NkplmErp.Shared.DTOs.Yarn_Orders
{
    public class YarnOrderRequestModel
    {
        public string? DepartureDate { get; set; }
        public string? ArrivalDate { get; set; }
        public string? YarnId { get; set; }

        /// <summary>
        /// Vendor invoice number — the "yarn arrived and is ready for use" marker.
        /// Null or blank is meaningful, not missing: it clears the invoice and puts
        /// the vendor order back to pending.
        /// </summary>
        public string? InvoiceNo { get; set; }

        /// <summary>Arrived weight (kg), travels as a raw string — the proc does the TRY_CONVERT.</summary>
        public string? Weight { get; set; }

        /// <summary>Pragyapan no, captured together with the invoice on arrival.</summary>
        public string? PragyapanNo { get; set; }

        /// <summary>LC / TT no, captured together with the invoice on arrival.</summary>
        public string? LcTtNo { get; set; }
    }
}
