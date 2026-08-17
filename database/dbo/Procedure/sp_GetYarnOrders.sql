CREATE OR ALTER PROCEDURE [dbo].[sp_GetYarnOrders]
    -- Order state filter, codes supplied by spDropdown 'YarnOrderStatus'.
    --   'S'  Ready for Approval -> awaiting Yarn submission to YarnControl
    --   'A'  Pending Approval -> awaiting YarnControl approval
    --   'V'  Approved         -> approved by YarnControl, ready for vendor order placement
    --   'N'  Not ordered      -> backward compatibility alias for Approved
    --   'P'  Ordered          -> vendor order(s) placed, at least one NOT yet invoiced
    --   'C'  Completed        -> vendor order(s) placed and EVERY one of them invoiced
    --   'R'  Rejected         -> rejected by YarnControl
    --   NULL/'' (default)     -> no filter, every header
    @Status CHAR(1) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @st CHAR(1) = NULLIF(LTRIM(RTRIM(@Status)), '');

    SELECT  o.yo_id,
            o.yo_no,
            o.created_date,
            o.created_by,
            o.total_kg,
            o.order_count,
            o.line_count,
            -- Derived lifecycle status:
            -- 1. If vendor sub-orders exist and are all invoiced -> 'Completed'
            -- 2. If vendor sub-orders exist and any uninvoiced -> 'Ordered'
            -- 3. If explicitly Approved -> 'Approved'
            -- 4. If explicitly Rejected -> 'Rejected'
            -- 5. Explicitly submitted -> 'Pending Approval'
            -- 6. Default initial/legacy state -> 'Ready for Approval'
            [status] = CASE
                WHEN     EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK) WHERE v.yo_id = o.yo_id)
                     AND NOT EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK) WHERE v.yo_id = o.yo_id AND NULLIF(LTRIM(RTRIM(ISNULL(v.invoice_no, ''))), '') IS NULL)
                     THEN 'Completed'
                WHEN     EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK) WHERE v.yo_id = o.yo_id)
                     THEN 'Ordered'
                WHEN o.[status] = 'Approved' THEN 'Approved'
                WHEN o.[status] = 'Rejected' THEN 'Rejected'
                WHEN o.[status] = 'Pending Approval' THEN 'Pending Approval'
                ELSE 'Ready for Approval'
            END,
            order_no = (SELECT STRING_AGG(CONVERT(nvarchar(max), x.order_no), ', ')
                                   WITHIN GROUP (ORDER BY x.order_no)
                        FROM (SELECT DISTINCT od.order_no
                              FROM dbo.tbl_yarn_order_detail od WITH (NOLOCK)
                              WHERE od.yo_id = o.yo_id
                                AND NULLIF(LTRIM(RTRIM(od.order_no)), '') IS NOT NULL) x)
    FROM dbo.tbl_yarn_order o WITH (NOLOCK)
    WHERE @st IS NULL
       OR (@st = 'S' AND (o.[status] = 'Ready for Approval' OR o.[status] IS NULL OR o.[status] = 'Placed') AND NOT EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK) WHERE v.yo_id = o.yo_id))
       OR (@st = 'A' AND o.[status] = 'Pending Approval' AND NOT EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK) WHERE v.yo_id = o.yo_id))
       OR (@st IN ('V', 'N') AND o.[status] = 'Approved' AND NOT EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK) WHERE v.yo_id = o.yo_id))
       OR (@st = 'R' AND o.[status] = 'Rejected')
       OR (@st = 'O' AND EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK) WHERE v.yo_id = o.yo_id))
       OR (@st = 'P' AND EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK) WHERE v.yo_id = o.yo_id AND NULLIF(LTRIM(RTRIM(ISNULL(v.invoice_no, ''))), '') IS NULL))
       OR (@st = 'C' AND EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK) WHERE v.yo_id = o.yo_id)
                      AND NOT EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order v WITH (NOLOCK) WHERE v.yo_id = o.yo_id AND NULLIF(LTRIM(RTRIM(ISNULL(v.invoice_no, ''))), '') IS NULL))
    ORDER BY o.created_date DESC, o.yo_id DESC;
END;
