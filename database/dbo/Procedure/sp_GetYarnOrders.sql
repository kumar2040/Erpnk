CREATE PROCEDURE [dbo].[sp_GetYarnOrders]
AS
BEGIN
    SET NOCOUNT ON;

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
    ORDER BY o.created_date DESC, o.yo_id DESC;
END
