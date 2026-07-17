CREATE PROCEDURE [dbo].[sp_GetYarnOrderDetail]
    @YoId INT
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        yod.yod_id, yod.yo_id, yod.product_id, yod.yarn_name, yod.color,
        yod.ply, yod.order_no, yod.import_kg,
        v.vendor_id
    FROM dbo.tbl_yarn_order_detail AS yod
    -- Resolve the supplier from the most recent matching import of this
    -- yarn (product_id + color). TOP 1 avoids row multiplication when the
    -- same yarn was imported from several vendors over time.
    OUTER APPLY (
        SELECT TOP 1 yi.vendor_id
        FROM dbo.tbl_yarn_import_detail AS d
        INNER JOIN dbo.tbl_yarn_import AS yi ON d.imp_id = yi.id
        WHERE CAST(d.yarn AS VARCHAR(100)) = yod.product_id
          AND d.color = yod.color
        ORDER BY yi.entry_date DESC, yi.id DESC
    ) AS v
    WHERE yod.yo_id = @YoId
      AND yod.is_dropped = 0         
    ORDER BY yod.yarn_name, yod.color, yod.order_no;
END
