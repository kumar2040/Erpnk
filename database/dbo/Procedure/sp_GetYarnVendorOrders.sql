-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.sp_GetYarnVendorOrders  (SQL_STORED_PROCEDURE)

/* ---------------------------------------------------------------------
   sp_GetYarnVendorOrders — vendor sub-orders placed under a parent.

   invoice_no / invoice_date drive the card's Pending-vs-Completed badge and
   seed the invoice input, so the page can tell "not yet invoiced" from
   "invoiced" without a second round trip.
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_GetYarnVendorOrders
    @YoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT vyo_id, yo_id, vyo_no, vendor, created_date, created_by,
           total_kg, line_count, departure_date, arrival_date,
           invoice_no, invoice_date, invoice_by,
           [status]
    FROM dbo.tbl_yarn_vendor_order
    WHERE yo_id = @YoId
    ORDER BY vyo_id;
END
