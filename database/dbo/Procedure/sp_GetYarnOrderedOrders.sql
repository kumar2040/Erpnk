-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.sp_GetYarnOrderedOrders  (SQL_STORED_PROCEDURE)

/* ---------------------------------------------------------------------
   sp_GetYarnOrderedOrders â€” distinct production order_no's that already
   have a yarn order placed. Used to drop them from the BOM pending list.
   --------------------------------------------------------------------- */
CREATE   PROCEDURE dbo.sp_GetYarnOrderedOrders
AS
BEGIN
    SET NOCOUNT ON;
    SELECT DISTINCT LTRIM(RTRIM(order_no)) AS order_no
    FROM dbo.tbl_yarn_order_detail
    WHERE order_no IS NOT NULL AND LTRIM(RTRIM(order_no)) <> '';
END
