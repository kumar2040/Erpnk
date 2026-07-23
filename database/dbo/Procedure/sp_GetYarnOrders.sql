CREATE OR ALTER PROCEDURE [dbo].[sp_GetYarnOrders]
    -- Order state filter, codes supplied by spDropdown 'YarnOrderStatus'.
    --   'O'  ordered      -> a vendor order has been placed against this header
    --   'N'  not ordered  -> no vendor order placed yet
    --   NULL/'' (default) -> no filter, every header
    @Status CHAR(1) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @st CHAR(1) = NULLIF(LTRIM(RTRIM(@Status)), '');

    -- One row per yarn order header, always.
    -- order_no lives on tbl_yarn_order_detail, which holds MANY rows per header, so it is
    -- folded into a single comma-separated value with a correlated subquery. It must not be
    -- reached with a JOIN: a join to the detail table fans each header out into one row per
    -- detail line (8 orders -> 174 rows), which the /yarn-orders list renders as duplicate cards.
    SELECT  o.yo_id,
            o.yo_no,
            o.created_date,
            o.created_by,
            o.total_kg,
            o.order_count,
            o.line_count,
            o.[status],
            order_no = (SELECT STRING_AGG(CONVERT(nvarchar(max), x.order_no), ', ')
                                   WITHIN GROUP (ORDER BY x.order_no)
                        FROM (SELECT DISTINCT od.order_no
                              FROM dbo.tbl_yarn_order_detail od WITH (NOLOCK)
                              WHERE od.yo_id = o.yo_id
                                AND NULLIF(LTRIM(RTRIM(od.order_no)), '') IS NOT NULL) x)
    FROM dbo.tbl_yarn_order o WITH (NOLOCK)
    -- "Ordered" is derived, not stored: a header becomes ordered the moment a vendor
    -- sub-order exists for it. EXISTS rather than a JOIN for the same fan-out reason
    -- as order_no above -- a header can have several vendor orders.
    WHERE @st IS NULL
       OR (@st = 'O' AND     EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK)
                                     WHERE v.yo_id = o.yo_id))
       OR (@st = 'N' AND NOT EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK)
                                     WHERE v.yo_id = o.yo_id))
    ORDER BY o.created_date DESC, o.yo_id DESC;
END
