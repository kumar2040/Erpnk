-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.fn_knittednGiven  (SQL_SCALAR_FUNCTION)

CREATE FUNCTION [dbo].[fn_knittednGiven]
(
    @orderid INT,
    @typ     VARCHAR(20)
)
RETURNS INT
AS
BEGIN
    DECLARE @v_qty INT = 0;
      set @v_qty=@orderid;
    IF @typ = 'knitted'
    BEGIN
        -- Counts how many received/knitted items exist for this order
        SELECT @v_qty = COUNT(rr.item_no)
        FROM dbo.tbl_knitter_record AS rc
        INNER JOIN dbo.tbl_knitter_recieved AS rr
            ON rc.kr_id = rr.item_id
        WHERE rc.order_id = @orderid;
        
    END
    ELSE
    BEGIN
        -- Sums the 'pics' (pieces?) from record_data
        SELECT @v_qty = SUM(rd.pics-ret_pic)
        FROM dbo.tbl_knitter_record AS rc
        INNER JOIN dbo.tbl_knitter_record_data AS rd
            ON rd.r_id = rc.kr_id
        WHERE rc.order_id = @orderid;
    END
     
    RETURN @v_qty;   -- ← recommended: prevents NULL → 0 is safer
END
