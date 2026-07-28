-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.sp_SetYarnVendorOrderDate  (SQL_STORED_PROCEDURE)

/* ---------------------------------------------------------------------
   sp_SetYarnVendorOrderDate — set the vendor-confirmed departure date or
   the arrival/ETA date. @Kind = 'departure' | 'arrival'.
   --------------------------------------------------------------------- */
CREATE   PROCEDURE dbo.sp_SetYarnVendorOrderDate
    @VyoId INT,
    @Kind  VARCHAR(10),
    @Date  DATE
AS
BEGIN
    SET NOCOUNT ON;
    IF @Kind = 'departure'
        UPDATE dbo.tbl_yarn_vendor_order SET departure_date = @Date WHERE vyo_id = @VyoId;
    ELSE IF @Kind = 'arrival'
        UPDATE dbo.tbl_yarn_vendor_order SET arrival_date = @Date WHERE vyo_id = @VyoId;
    SELECT @@ROWCOUNT AS affected;
END
