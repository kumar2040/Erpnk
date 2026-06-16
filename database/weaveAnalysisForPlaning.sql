USE [NatureKnit]
GO

/****** Object:  StoredProcedure [dbo].[weaveAnalysisForPlaning]    Script Date: 6/8/2026 4:30:14 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO


ALTER   PROCEDURE [dbo].[weaveAnalysisForPlaning]
    @OrderNo VARCHAR(50),
    @FactoryName VARCHAR(100) = NULL,
    @Flag INT = 1
AS
BEGIN
    SET NOCOUNT ON;

    -- Clean parameters of trailing/leading whitespaces and handle empty strings
    DECLARE @Today DATE = CAST(GETDATE() AS DATE);
    SET @OrderNo = LTRIM(RTRIM(@OrderNo));
    SET @FactoryName = LTRIM(RTRIM(@FactoryName));
    IF @FactoryName = '' SET @FactoryName = NULL;

    -- Clean up temporary tables safely if they exist in the session context
    IF OBJECT_ID('tempdb..#FinalResults') IS NOT NULL DROP TABLE #FinalResults;
    IF OBJECT_ID('tempdb..#Guage_color_stock') IS NOT NULL DROP TABLE #Guage_color_stock;

    /* ================================================================= */
    /* 1. COMPUTE AND INSERT INTO TEMPORARY RESULTS                      */
    /* ================================================================= */
    SELECT * INTO #FinalResults FROM (
        -- Block 1: Non-tailored standard styles
        SELECT order_id, order_no, order_color, CAST(style_product_id AS VARCHAR(100)) AS product_id, style_guage, style_ply, style_no, wt, order_pics, knittedPc, ToknitPc, rempc, weave_factory
        FROM (
            SELECT *, CASE WHEN ToknitPc IS NULL THEN order_pics ELSE order_pics - (ToknitPc + knittedPc) END AS rempc
            FROM (
                SELECT od.order_id, od.order_no, COALESCE(cv.color, od.order_color) AS order_color, ts.style_product_id, ts.style_guage, ts.style_no, style_ply,
                       COALESCE(cv.weight, ts.net_wet) AS wt, od.order_pics,
                       CASE WHEN LTRIM(RTRIM(ts.weave_factory)) = '0' THEN 'Pashminalooms'
                            ELSE COALESCE(LTRIM(RTRIM(ts.weave_factory)), 'No Factory Assigned')
                       END AS weave_factory,
                       dbo.fn_knittednGiven(od.order_id,'knitted') as knittedPc, dbo.fn_knittednGiven(od.order_id,'Given') as ToknitPc
                FROM tbl_order as od
                INNER JOIN tbl_stylesheet as ts on od.product_name = ts.style_no
                LEFT JOIN tbl_color_var as cv on cv.style_id = ts.style_id and cv.[var] = od.order_color and cv.weight <> 1
                WHERE ts.style_tailor <> 1
                  AND od.order_ldate > DATEADD(day, -45, CAST(GETDATE() AS DATE))
            ) as dd
        ) as mm WHERE rempc > 0

        UNION ALL

        -- Block 2: Extra yarn styles
        SELECT order_id, order_no, order_color, CAST(style_product_id AS VARCHAR(100)) AS product_id, style_guage, style_ply, style_no, wt, order_pics, knittedPc, ToknitPc, rempc, weave_factory
        FROM (
            SELECT *, CASE WHEN ToknitPc IS NULL THEN order_pics ELSE order_pics - (ToknitPc + knittedPc) END AS rempc
            FROM (
                SELECT od.order_id, od.order_no, COALESCE(cv.color, od.order_color) AS order_color, ts.style_product_id, ts.style_guage, ts.style_no, style_ply,
                       COALESCE(ex.wt, cv.weight) as wt, od.order_pics,
                       CASE WHEN LTRIM(RTRIM(ts.weave_factory)) = '0' THEN 'Pashminalooms'
                            ELSE COALESCE(LTRIM(RTRIM(ts.weave_factory)), 'No Factory Assigned')
                       END AS weave_factory,
                       dbo.fn_knittednGiven(od.order_id,'knitted') as knittedPc, dbo.fn_knittednGiven(od.order_id,'Given') as ToknitPc
                FROM tbl_order as od
                INNER JOIN tbl_stylesheet as ts on od.product_name = ts.style_no
                INNER JOIN tbl_stylesheet_extrayarn AS ex ON ts.style_id = ex.style_id
                LEFT JOIN tbl_color_var AS cv on cv.style_id = ts.style_id and cv.[var] = od.order_color and cv.weight <> 1
                WHERE od.order_ldate > DATEADD(day, -45, CAST(GETDATE() AS DATE))
            ) as dd
        ) as mm WHERE rempc > 0

        UNION ALL

        -- Block 3: Weave Styles (Targets type = 'wv')
        SELECT weav.order_id, weav.order_no, dbo.yarn_auto_calc_color(wy.product_id, weav.vcolor) as order_color,
               CAST(wy.product_id AS VARCHAR(100)) AS product_id, weave_factory as style_guage, style_ply, weav.style_no, wy.weight_y AS wt, weav.order_pics, weav.knittedPc, weav.ToknitPc, weav.rempc, weave_factory
        FROM (
            SELECT *, CASE WHEN ToknitPc IS NULL THEN order_pics ELSE order_pics - (knittedPc + ToknitPc) END AS rempc
            FROM (
                SELECT COALESCE(vr.color, od.order_color) AS vcolor, ts.style_id, od.order_pics, ts.style_no,
                       dbo.fn_knittednGiven(od.order_id,'knitted') as knittedPc, dbo.fn_knittednGiven(od.order_id,'Given') as ToknitPc,
                       od.order_color, COALESCE(ts.net_wet, vr.weight) AS wt, od.order_id, od.order_no, style_ply,
                       CASE WHEN LTRIM(RTRIM(ts.weave_factory)) = '0' THEN 'Pashminalooms'
                            ELSE COALESCE(LTRIM(RTRIM(ts.weave_factory)), 'No Factory Assigned')
                       END AS weave_factory
                FROM tbl_order AS od
                INNER JOIN tbl_stylesheet AS ts ON od.product_name = ts.style_no AND ts.type = 'wv'
                LEFT JOIN tbl_color_var AS vr ON vr.style_id = ts.style_id AND vr.[var] = od.order_color AND vr.weight > 7
                WHERE od.order_ldate > DATEADD(day, -45, CAST(GETDATE() AS DATE))
            ) AS mmd
        ) AS weav
        INNER JOIN tbl_stylesheet_wapweft_yarn AS wy ON weav.style_id = wy.style_id
        WHERE weav.rempc > 0
    ) AS CombinedData
    WHERE (LTRIM(RTRIM(weave_factory)) = @FactoryName OR @FactoryName IS NULL);


    /* ================================================================= */
    /* 2. AGGREGATION, PHYSICAL STOCK MAPPING & STATUS LOGIC             */
    /* ================================================================= */
    ;WITH AggregatedRequirements AS (
        SELECT
            fr.product_id,
            fr.order_color,
            fr.weave_factory,
            MAX(fr.style_guage) AS style_guage,
            fr.style_ply,

            -- Compounded Calculation: (wt * (1 + wastage%)) * 1.10 (working wt buffer)
            SUM(CASE WHEN LTRIM(RTRIM(fr.order_no)) = @OrderNo
                     THEN (fr.rempc * (
                            CASE WHEN ISNULL(p.wastage, 0) > 0
                                 THEN (fr.wt * (1.0 + (p.wastage / 100.0))) * 1.10
                                 ELSE fr.wt * 1.10
                            END) / 1000.0)
                     ELSE 0
                END) AS selfwt,

            SUM(CASE WHEN LTRIM(RTRIM(fr.order_no)) <> @OrderNo
                     THEN (fr.rempc * (
                            CASE WHEN ISNULL(p.wastage, 0) > 0
                                 THEN (fr.wt * (1.0 + (p.wastage / 100.0))) * 1.10
                                 ELSE fr.wt * 1.10
                            END) / 1000.0)
                     ELSE 0
                END) AS othWt,

            COUNT(DISTINCT CASE WHEN LTRIM(RTRIM(fr.order_no)) = @OrderNo THEN fr.style_no END) as StyleCount,
            SUM(CASE WHEN LTRIM(RTRIM(fr.order_no)) = @OrderNo THEN fr.rempc ELSE 0 END) AS qty
        FROM #FinalResults fr
        LEFT JOIN tblproduct AS p ON fr.product_id = CAST(p.product_id AS VARCHAR(100))
        -- Exclude corrupted or blank product identifiers
        WHERE fr.product_id IS NOT NULL
          AND LTRIM(RTRIM(fr.product_id)) <> ''
          AND LTRIM(RTRIM(fr.product_id)) <> '0'
        GROUP BY fr.product_id, fr.order_color, fr.style_ply, fr.weave_factory
    ),
    StockData AS (
        SELECT CAST(op.product_id AS VARCHAR(100)) AS product_id, bp.p_color, SUM(CAST(bp.p_wt AS FLOAT) / 1000.0) AS StockQty
        FROM tblopeningproductstock AS op
        INNER JOIN tbl_no_box AS bx ON op.id = bx.product_id
        INNER JOIN tbl_cone_stock AS bp ON bp.box_id = bx.b_id
        WHERE bp.active = '1' AND bp.for_use = 1
        GROUP BY op.product_id, bp.p_color
    )
    SELECT
        r.product_id, r.order_color, r.weave_factory, r.style_guage, r.style_ply, r.selfwt, r.othWt, r.StyleCount, r.qty, ISNULL(s.StockQty, 0) AS StockQty,
        CASE WHEN (r.selfwt + r.othWt) > ISNULL(s.StockQty, 0) THEN 'Shortage' ELSE 'Available' END AS YarnStatus
    INTO #Guage_color_stock
    FROM AggregatedRequirements r
    LEFT JOIN StockData s ON r.product_id = s.product_id AND r.order_color = s.p_color;


    /* ================================================================= */
    /* 3. FLAGGED OUTPUT CONTROLLER                                      */
    /* ================================================================= */
    IF @Flag = 1
    BEGIN

        -- -------------------------------------------------------------
        -- RESULT SET 1: Summary Dashboard Metrics + Linked Yarn Status
        -- -------------------------------------------------------------
        ;WITH ActivePlans AS (
            SELECT
                guage,
                SUM(qty) AS total_plan_qty,
                SUM(MachineCount) AS total_machines,
                MAX(EndDate) AS FreeDate
            FROM dbo.MasterPlanDetail
            WHERE EndDate >= @Today AND factory_type = 'weave'
            GROUP BY guage
        ),
        FactoryYarnStatus AS (
            SELECT
                weave_factory,
                CASE WHEN MIN(CASE WHEN YarnStatus = 'Shortage' THEN 0 ELSE 1 END) = 0
                     THEN 'Shortage'
                     ELSE 'Available'
                END AS OverallYarnStatus
            FROM #Guage_color_stock
            WHERE qty > 0  -- Filters status checking strictly to the active target order requirements
            GROUP BY weave_factory
        )
        SELECT
            CASE WHEN LTRIM(RTRIM(ts.weave_factory)) = '0' THEN 'Pashminalooms'
                 ELSE COALESCE(LTRIM(RTRIM(ts.weave_factory)), 'No Factory Assigned')
            END AS weave_factory,
            SUM(od.order_pics) AS qty,
            SUM(COALESCE(prod.total_received, 0)) AS total_received,

            SUM(CAST(od.order_pics AS FLOAT) * CASE WHEN ISNULL(ts.style_target, 0) = 0 THEN 1 ELSE ts.style_target END) AS req_machine_days,

            ISNULL(MAX(ap.total_plan_qty), 0) AS total_machine_load_qty,
            ISNULL(MAX(ap.total_machines), 0) AS total_machines_allocated,
            MAX(ap.FreeDate) AS FreeDate,

            ISNULL(fys.OverallYarnStatus, 'Available') AS YarnStatus
        FROM tbl_order AS od
        LEFT JOIN tbl_stylesheet AS ts ON od.product_name = ts.style_no
        LEFT JOIN (
            SELECT r.order_id, COUNT(c.item_no) AS total_received
            FROM tbl_knitter_record AS r
            INNER JOIN tbl_knitter_recieved AS c ON r.kr_id = c.item_id AND c.cancell = 0
            GROUP BY r.order_id
        ) AS prod ON od.order_id = prod.order_id
        LEFT JOIN ActivePlans ap ON ap.guage = (
            CASE WHEN LTRIM(RTRIM(ts.weave_factory)) = '0' THEN 'Pashminalooms'
                 ELSE COALESCE(LTRIM(RTRIM(ts.weave_factory)), 'No Factory Assigned')
            END
        )
        LEFT JOIN FactoryYarnStatus fys ON fys.weave_factory = (
            CASE WHEN LTRIM(RTRIM(ts.weave_factory)) = '0' THEN 'Pashminalooms'
                 ELSE COALESCE(LTRIM(RTRIM(ts.weave_factory)), 'No Factory Assigned')
            END
        )
        WHERE
            LTRIM(RTRIM(od.order_no)) = @OrderNo and ts.type='wv'
            AND (
                LTRIM(RTRIM(ts.weave_factory)) = @FactoryName
                OR (LTRIM(RTRIM(ts.weave_factory)) = '0' AND @FactoryName = 'Pashminalooms')
                OR @FactoryName IS NULL
            )
        GROUP BY
            CASE WHEN LTRIM(RTRIM(ts.weave_factory)) = '0' THEN 'Pashminalooms'
                 ELSE COALESCE(LTRIM(RTRIM(ts.weave_factory)), 'No Factory Assigned')
            END,
            fys.OverallYarnStatus;

        -- -------------------------------------------------------------
        -- RESULT SET 2: Detailed Yarn Status
        -- -------------------------------------------------------------
        SELECT
            product_id,
            order_color,
            CASE WHEN LTRIM(RTRIM(style_guage)) = '0' OR ISNULL(style_guage, '') = ''
                 THEN 'Pashminalooms'
                 ELSE style_guage
            END AS style_guage,
            style_ply,
            qty AS item_qty,
            selfwt,
            othWt,
            StockQty,
            YarnStatus
        FROM #Guage_color_stock
        WHERE qty > 0;

        -- -------------------------------------------------------------
        -- RESULT SET 3: Print & Embroidery Summary
        -- -------------------------------------------------------------
       SELECT
    ts.style_no,
    MAX(CASE WHEN ISNULL(ts.style_target, 0) = 0 THEN 1 ELSE ts.style_target END) AS style_target,
    SUM(od.order_pics) AS qty,
    SUM(COALESCE(prod.total_received, 0)) AS total_received,

    -- Your new conditional columns
    MAX(CASE WHEN ts.style_print = 1 THEN 'P' ELSE '0' END) AS style_print_status,
    MAX(CASE WHEN ts.style_embd = 1 THEN 'E' ELSE '0' END) AS style_embd_status,

    SUM(CAST(od.order_pics AS FLOAT) * CASE WHEN ISNULL(ts.style_target, 0) = 0 THEN 1 ELSE ts.style_target END) AS style_req_machine_days
FROM tbl_order AS od
LEFT JOIN tbl_stylesheet AS ts ON od.product_name = ts.style_no
LEFT JOIN (
    SELECT r.order_id, COUNT(c.item_no) AS total_received
    FROM tbl_knitter_record AS r
    INNER JOIN tbl_knitter_recieved AS c ON r.kr_id = c.item_id AND c.cancell = 0
    GROUP BY r.order_id
) AS prod ON od.order_id = prod.order_id
WHERE
    LTRIM(RTRIM(od.order_no)) = @OrderNo AND ts.type = 'wv'
    AND (
        LTRIM(RTRIM(ts.weave_factory)) = @FactoryName
        OR (LTRIM(RTRIM(ts.weave_factory)) = '0' AND @FactoryName = 'Pashminalooms')
        OR @FactoryName IS NULL
    )
GROUP BY
    ts.style_no;

    END
    ELSE IF @Flag = 2
    BEGIN
        SELECT
            product_id,
            order_color,
            CASE WHEN LTRIM(RTRIM(style_guage)) = '0' OR ISNULL(style_guage, '') = ''
                 THEN 'Pashminalooms'
                 ELSE style_guage
            END AS style_guage,
            style_ply, selfwt, othWt, StyleCount, qty, StockQty, YarnStatus
        FROM #Guage_color_stock;
    END
    ELSE
    BEGIN
        SELECT * FROM #FinalResults WHERE LTRIM(RTRIM(order_no)) = @OrderNo;
    END

    -- Cleanup temp structures safely
    IF OBJECT_ID('tempdb..#FinalResults') IS NOT NULL DROP TABLE #FinalResults;
    IF OBJECT_ID('tempdb..#Guage_color_stock') IS NOT NULL DROP TABLE #Guage_color_stock;

END
GO
