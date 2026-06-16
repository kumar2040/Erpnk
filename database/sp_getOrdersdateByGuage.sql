USE [NatureKnit]
GO

/****** Object:  StoredProcedure [dbo].[sp_getOrdersdateByGuage]    Script Date: 6/5/2026 11:38:58 AM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO




ALTER PROCEDURE [dbo].[sp_getOrdersdateByGuage]
    @orderNo VARCHAR(50),
    @guage   VARCHAR(20),
    @flag    VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    /* ------------------------------------------------------------------
       FLAG 2: Style + Color + Size-wise breakdown for the SELECTED order
       and gauge. Used for machine-wise planning allocation so a plan can
       be split by style / color / size. Size figures come straight from
       the tbl_order size columns.
       ------------------------------------------------------------------ */
    IF (@flag = '2')
    BEGIN
        SELECT
            od.order_no,
            od.order_id,
            od.order_ldate AS ShippingDate,
            ts.style_no,
            od.order_color,
            od.order_pics,
            ISNULL(prod.total_received, 0) AS TotalReceived,
            ts.style_target,
            (od.order_pics - ISNULL(prod.total_received, 0)) AS BalanceQty,
            ROUND(
                (od.order_pics - ISNULL(prod.total_received, 0)) /
                NULLIF(CAST(ts.style_target AS FLOAT), 0),
            2) AS RequireDays,
            CASE WHEN ts.style_print = 1 THEN 'OK' ELSE '' END AS PrintStatus,
            CASE WHEN ts.style_embd = 1 THEN 'OK' ELSE '' END AS EmbdStatus,

            -- Size-wise quantities from tbl_order
            ISNULL(od.[xxxs], 0) AS [xxxs],
            ISNULL(od.[xxs],  0) AS [xxs],
            ISNULL(od.[s],    0) AS [s],
            ISNULL(od.[m],    0) AS [m],
            ISNULL(od.[l],    0) AS [l],
            ISNULL(od.[xl],   0) AS [xl],
            ISNULL(od.[xxl],  0) AS [xxl],
            ISNULL(od.[xxxl], 0) AS [xxxl],
            ISNULL(od.[osfa], 0) AS [osfa]

        FROM tbl_order AS od
        INNER JOIN tbl_stylesheet AS ts
            ON od.product_name = ts.style_no
        LEFT JOIN (
            SELECT
                r.order_id,
                COUNT(c.item_no) AS total_received
            FROM tbl_knitter_record AS r
            INNER JOIN tbl_knitter_recieved AS c
                ON r.kr_id = c.item_id
            GROUP BY r.order_id
        ) AS prod
            ON od.order_id = prod.order_id
        WHERE od.order_ldate > DATEADD(DAY, -25, GETDATE())
          AND ts.style_guage = @guage
          AND ts.style_tailor <> 1
          AND (od.order_pics - ISNULL(prod.total_received, 0)) > 0
          AND od.order_no = @orderNo
        ORDER BY od.order_ldate, style_no ASC;

        RETURN;
    END

    /* ------------------------------------------------------------------
       EXISTING LOGIC (flags 0, 1 and any other value) - unchanged
       ------------------------------------------------------------------ */
    SELECT
        od.order_no,
        od.order_ldate AS ShippingDate,
        ts.style_no,
        od.order_color,
        od.order_pics,
        ISNULL(prod.total_received, 0) AS TotalReceived,
        ts.style_target,
        (od.order_pics - ISNULL(prod.total_received, 0)) AS BalanceQty,
        ROUND(
            (od.order_pics - ISNULL(prod.total_received, 0)) /
            NULLIF(CAST(ts.style_target AS FLOAT), 0),
        2) AS RequireDays,

        -- Logic for Print and Embroidery signs
        CASE WHEN ts.style_print = 1 THEN 'OK' ELSE '' END AS PrintStatus,
        CASE WHEN ts.style_embd = 1 THEN 'OK' ELSE '' END AS EmbdStatus

    FROM tbl_order AS od
    INNER JOIN tbl_stylesheet AS ts
        ON od.product_name = ts.style_no
    LEFT JOIN (
        SELECT
            r.order_id,
            COUNT(c.item_no) AS total_received
        FROM tbl_knitter_record AS r
        INNER JOIN tbl_knitter_recieved AS c
            ON r.kr_id = c.item_id
        GROUP BY r.order_id
    ) AS prod
        ON od.order_id = prod.order_id
    WHERE od.order_ldate > DATEADD(DAY, -25, GETDATE())
      AND ts.style_guage = @guage
      AND ts.style_tailor <> 1
      AND (od.order_pics - ISNULL(prod.total_received, 0)) > 0
      AND (
            (@flag = '1' AND od.order_no = @orderNo)
         OR (@flag = '0' AND od.order_no <> @orderNo
             AND od.order_ldate < (SELECT MAX(order_ldate)
                                   FROM tbl_order
                                   WHERE order_no = @orderNo))
         OR (@flag NOT IN ('0','1'))
          )
    ORDER BY od.order_ldate,style_no ASC;
END
GO
