-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.sp_GetYarnVendorOrder  (SQL_STORED_PROCEDURE)

/* ---------------------------------------------------------------------
   sp_GetYarnVendorOrder — single vendor sub-order: header (result 1) +
   lines (result 2). Used by the Excel PO export.
   --------------------------------------------------------------------- */
CREATE   PROCEDURE dbo.sp_GetYarnVendorOrder
    @VyoId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT vyo_id, yo_id, vyo_no, vendor, created_date, created_by,
           total_kg, line_count, departure_date, arrival_date, [status]
    FROM dbo.tbl_yarn_vendor_order
    WHERE vyo_id = @VyoId;

    SELECT vyod_id, vyo_id, product_id, yarn_name, color, ply, order_no, import_kg
    FROM dbo.tbl_yarn_vendor_order_detail
    WHERE vyo_id = @VyoId
    ORDER BY yarn_name, color, order_no;
END
