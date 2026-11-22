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
       and gauge. Used for machine-wise planning allocation. Deducts
       already-knitted/received and already-planned pieces per size so
       planning schedules only the true remaining balance.
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

            -- Size-wise remaining balance (Order Qty - Knitted Qty - Already Planned Qty)
            CASE WHEN (ISNULL(od.[xxxs], 0) - ISNULL(prod.knitted_xxxs, 0) - ISNULL(planned.planned_xxxs, 0)) < 0 THEN 0
                 ELSE (ISNULL(od.[xxxs], 0) - ISNULL(prod.knitted_xxxs, 0) - ISNULL(planned.planned_xxxs, 0)) END AS [xxxs],

            CASE WHEN (ISNULL(od.[xxs], 0) - ISNULL(prod.knitted_xxs, 0) - ISNULL(planned.planned_xxs, 0)) < 0 THEN 0
                 ELSE (ISNULL(od.[xxs], 0) - ISNULL(prod.knitted_xxs, 0) - ISNULL(planned.planned_xxs, 0)) END AS [xxs],

            CASE WHEN (ISNULL(od.[s], 0) - ISNULL(prod.knitted_s, 0) - ISNULL(planned.planned_s, 0)) < 0 THEN 0
                 ELSE (ISNULL(od.[s], 0) - ISNULL(prod.knitted_s, 0) - ISNULL(planned.planned_s, 0)) END AS [s],

            CASE WHEN (ISNULL(od.[m], 0) - ISNULL(prod.knitted_m, 0) - ISNULL(planned.planned_m, 0)) < 0 THEN 0
                 ELSE (ISNULL(od.[m], 0) - ISNULL(prod.knitted_m, 0) - ISNULL(planned.planned_m, 0)) END AS [m],

            CASE WHEN (ISNULL(od.[l], 0) - ISNULL(prod.knitted_l, 0) - ISNULL(planned.planned_l, 0)) < 0 THEN 0
                 ELSE (ISNULL(od.[l], 0) - ISNULL(prod.knitted_l, 0) - ISNULL(planned.planned_l, 0)) END AS [l],

            CASE WHEN (ISNULL(od.[xl], 0) - ISNULL(prod.knitted_xl, 0) - ISNULL(planned.planned_xl, 0)) < 0 THEN 0
                 ELSE (ISNULL(od.[xl], 0) - ISNULL(prod.knitted_xl, 0) - ISNULL(planned.planned_xl, 0)) END AS [xl],

            CASE WHEN (ISNULL(od.[xxl], 0) - ISNULL(prod.knitted_xxl, 0) - ISNULL(planned.planned_xxl, 0)) < 0 THEN 0
                 ELSE (ISNULL(od.[xxl], 0) - ISNULL(prod.knitted_xxl, 0) - ISNULL(planned.planned_xxl, 0)) END AS [xxl],

            CASE WHEN (ISNULL(od.[xxxl], 0) - ISNULL(prod.knitted_xxxl, 0) - ISNULL(planned.planned_xxxl, 0)) < 0 THEN 0
                 ELSE (ISNULL(od.[xxxl], 0) - ISNULL(prod.knitted_xxxl, 0) - ISNULL(planned.planned_xxxl, 0)) END AS [xxxl],

            CASE WHEN (ISNULL(od.[osfa], 0) - ISNULL(prod.knitted_osfa, 0) - ISNULL(planned.planned_osfa, 0)) < 0 THEN 0
                 ELSE (ISNULL(od.[osfa], 0) - ISNULL(prod.knitted_osfa, 0) - ISNULL(planned.planned_osfa, 0)) END AS [osfa]

        FROM tbl_order AS od
        INNER JOIN tbl_stylesheet AS ts
            ON od.product_name = ts.style_no
        LEFT JOIN (
            SELECT
                r.order_id,
                COUNT(c.item_no) AS total_received,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(r.size))) = 'xxxs' THEN 1 ELSE 0 END) AS knitted_xxxs,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(r.size))) = 'xxs'  THEN 1 ELSE 0 END) AS knitted_xxs,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(r.size))) = 's'    THEN 1 ELSE 0 END) AS knitted_s,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(r.size))) = 'm'    THEN 1 ELSE 0 END) AS knitted_m,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(r.size))) = 'l'    THEN 1 ELSE 0 END) AS knitted_l,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(r.size))) = 'xl'   THEN 1 ELSE 0 END) AS knitted_xl,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(r.size))) = 'xxl'  THEN 1 ELSE 0 END) AS knitted_xxl,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(r.size))) = 'xxxl' THEN 1 ELSE 0 END) AS knitted_xxxl,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(r.size))) = 'osfa' THEN 1 ELSE 0 END) AS knitted_osfa
            FROM tbl_knitter_record AS r
            INNER JOIN tbl_knitter_recieved AS c
                ON r.kr_id = c.item_id
            GROUP BY r.order_id
        ) AS prod
            ON od.order_id = prod.order_id
        LEFT JOIN (
            SELECT
                sz.order_id,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(sz.size))) = 'xxxs' THEN sz.qty ELSE 0 END) AS planned_xxxs,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(sz.size))) = 'xxs'  THEN sz.qty ELSE 0 END) AS planned_xxs,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(sz.size))) = 's'    THEN sz.qty ELSE 0 END) AS planned_s,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(sz.size))) = 'm'    THEN sz.qty ELSE 0 END) AS planned_m,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(sz.size))) = 'l'    THEN sz.qty ELSE 0 END) AS planned_l,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(sz.size))) = 'xl'   THEN sz.qty ELSE 0 END) AS planned_xl,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(sz.size))) = 'xxl'  THEN sz.qty ELSE 0 END) AS planned_xxl,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(sz.size))) = 'xxxl' THEN sz.qty ELSE 0 END) AS planned_xxxl,
                SUM(CASE WHEN LOWER(LTRIM(RTRIM(sz.size))) = 'osfa' THEN sz.qty ELSE 0 END) AS planned_osfa
            FROM dbo.MasterPlanDetailSize AS sz
            GROUP BY sz.order_id
        ) AS planned
            ON od.order_id = planned.order_id
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
