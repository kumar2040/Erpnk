-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.sp_GetYarnVendorOrders  (SQL_STORED_PROCEDURE)

/* ---------------------------------------------------------------------
   sp_GetYarnVendorOrders — vendor sub-orders placed under a parent.
   --------------------------------------------------------------------- */
CREATE   PROCEDURE dbo.sp_GetYarnVendorOrders
    @YoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT vyo_id, yo_id, vyo_no, vendor, created_date, created_by,
           total_kg, line_count, departure_date, arrival_date, [status]
    FROM dbo.tbl_yarn_vendor_order
    WHERE yo_id = @YoId
    ORDER BY vyo_id;
END
