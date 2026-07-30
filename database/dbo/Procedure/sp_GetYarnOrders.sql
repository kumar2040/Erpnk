CREATE OR ALTER PROCEDURE [dbo].[sp_GetYarnOrders]
    -- Order state filter, codes supplied by spDropdown 'YarnOrderStatus'.
    --   'N'  not ordered -> no vendor order placed yet
    --   'P'  ordered     -> vendor order(s) placed, at least one NOT yet invoiced
    --   'C'  completed   -> vendor order(s) placed and EVERY one of them invoiced
    --   'O'  legacy      -> P + C combined, kept so an older cached client
    --                       still returns something sane instead of nothing
    --   NULL/'' (default)-> no filter, every header
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
            -- The header's real state, DERIVED the same way the filter below derives it.
            -- o.[status] is deliberately not returned: sp_SaveYarnOrder stamps it 'Placed'
            -- once and nothing ever updates it, so it would show "Placed" on an order whose
            -- yarn has long since arrived. Same column name, so the DTO binds unchanged.
            [status] = CASE
                WHEN NOT EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK)
                                  WHERE v.yo_id = o.yo_id)                     THEN 'Not ordered'
                WHEN     EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK)
                                  WHERE v.yo_id = o.yo_id
                                    AND NULLIF(LTRIM(RTRIM(ISNULL(v.invoice_no, ''))), '') IS NULL)
                                                                               THEN 'Ordered'
                ELSE 'Completed' END,
            order_no = (SELECT STRING_AGG(CONVERT(nvarchar(max), x.order_no), ', ')
                                   WITHIN GROUP (ORDER BY x.order_no)
                        FROM (SELECT DISTINCT od.order_no
                              FROM dbo.tbl_yarn_order_detail od WITH (NOLOCK)
                              WHERE od.yo_id = o.yo_id
                                AND NULLIF(LTRIM(RTRIM(od.order_no)), '') IS NOT NULL) x)
    FROM dbo.tbl_yarn_order o WITH (NOLOCK)
    -- Every state is derived, nothing is stored: a header becomes ordered the moment a vendor
    -- sub-order exists for it, and completed the moment the LAST of those sub-orders is given
    -- an invoice number (= the yarn arrived and is ready for use). Pending is the gap between
    -- the two, so the three buckets never overlap and every header lands in exactly one.
    -- EXISTS rather than a JOIN for the same fan-out reason as order_no above -- a header can
    -- have several vendor orders.
    WHERE @st IS NULL
       OR (@st = 'N' AND NOT EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK)
                                      WHERE v.yo_id = o.yo_id))
       OR (@st = 'O' AND     EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK)
                                      WHERE v.yo_id = o.yo_id))
       OR (@st = 'P' AND     EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK)
                                      WHERE v.yo_id = o.yo_id
                                        AND NULLIF(LTRIM(RTRIM(ISNULL(v.invoice_no, ''))), '') IS NULL))
       OR (@st = 'C' AND     EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK)
                                      WHERE v.yo_id = o.yo_id)
                      AND NOT EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK)
                                      WHERE v.yo_id = o.yo_id
                                        AND NULLIF(LTRIM(RTRIM(ISNULL(v.invoice_no, ''))), '') IS NULL))
    ORDER BY o.created_date DESC, o.yo_id DESC;
END
