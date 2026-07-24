-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.knitYarnRequirement  (SQL_STORED_PROCEDURE)

/* =====================================================================
   knitYarnRequirement
   ---------------------------------------------------------------------
   Purpose: When a new order arrives, decide whether yarn must be
            imported from the supplier or can be knit from the main store.

   Logic (per yarn product_id x color x ply):
       selfwt   = required weight (kg) for THIS order (@OrderNo)
       othWt    = weight (kg) already committed to OTHER open orders
                  (backlog within the last 45 days) for the same yarn
       StockQty = weight (kg) on hand in the main store

       Backlog has first claim on stock, so:
         ShortfallKg = (selfwt + othWt) - StockQty
         Decision    = 'Import'   when ShortfallKg > 0
                       'In-stock' otherwise

   Weight per garment = wt * (1 + tblproduct.wastage%) * 1.10 working
   buffer, then /1000 to convert grams -> kg.

   @Flag = 1  -> decision table for THIS order's yarn (qty > 0)
   @Flag = 2  -> full #Guage_color_stock (includes backlog-only rows)
   @Flag = 3  -> raw #FinalResults rows for THIS order
   ===================================================================== */
CREATE   PROCEDURE [dbo].[knitYarnRequirement]
    @OrderNo VARCHAR(50),
    @Flag    INT = 1
AS
BEGIN
    SET NOCOUNT ON;

    SET @OrderNo = LTRIM(RTRIM(@OrderNo));

    IF OBJECT_ID('tempdb..#FinalResults')     IS NOT NULL DROP TABLE #FinalResults;
    IF OBJECT_ID('tempdb..#Guage_color_stock') IS NOT NULL DROP TABLE #Guage_color_stock;

    /* =================================================================
       1. DATA GATHERING (YARN REQS & BACKLOGS)
       ================================================================= */
    SELECT * INTO #FinalResults FROM (

        -- Block 1: Non-tailored standard styles
        SELECT order_id, order_no, order_color, CAST(style_product_id AS VARCHAR(100)) AS product_id,
               style_guage, style_ply, style_no, wt, order_pics, knittedPc, ToknitPc, rempc
        FROM (
            SELECT *, CASE WHEN ToknitPc IS NULL THEN order_pics ELSE order_pics - (ToknitPc + knittedPc) END AS rempc
            FROM (
                SELECT od.order_id, od.order_no, COALESCE(cv.color, od.order_color) AS order_color,
                       ts.style_product_id, ts.style_guage, ts.style_no, style_ply,
                       COALESCE(cv.weight, ts.net_wet) AS wt, od.order_pics,
                       dbo.fn_knittednGiven(od.order_id,'knitted') AS knittedPc,
                       dbo.fn_knittednGiven(od.order_id,'Given')   AS ToknitPc
                FROM tbl_order AS od
                INNER JOIN tbl_stylesheet AS ts ON od.product_name = ts.style_no
                LEFT  JOIN tbl_color_var  AS cv ON cv.style_id = ts.style_id AND cv.[var] = od.order_color AND cv.weight <> 1
                WHERE ts.style_tailor <> 1
                  AND od.order_ldate > DATEADD(day, -45, CAST(GETDATE() AS DATE))
            ) AS dd
        ) AS mm WHERE rempc > 0

        UNION ALL

        -- Block 2: Extra-yarn styles
        SELECT order_id, order_no, order_color, CAST(style_product_id AS VARCHAR(100)) AS product_id,
               style_guage, style_ply, style_no, wt, order_pics, knittedPc, ToknitPc, rempc
        FROM (
            SELECT *, CASE WHEN ToknitPc IS NULL THEN order_pics ELSE order_pics - (ToknitPc + knittedPc) END AS rempc
            FROM (
                SELECT od.order_id, od.order_no, COALESCE(cv.color, od.order_color) AS order_color,
                       ts.style_product_id, ts.style_guage, ts.style_no, style_ply,
                       COALESCE(ex.wt, cv.weight) AS wt, od.order_pics,
                       dbo.fn_knittednGiven(od.order_id,'knitted') AS knittedPc,
                       dbo.fn_knittednGiven(od.order_id,'Given')   AS ToknitPc
                FROM tbl_order AS od
                INNER JOIN tbl_stylesheet           AS ts ON od.product_name = ts.style_no
                INNER JOIN tbl_stylesheet_extrayarn AS ex ON ts.style_id = ex.style_id
                LEFT  JOIN tbl_color_var            AS cv ON cv.style_id = ts.style_id AND cv.[var] = od.order_color AND cv.weight <> 1
                WHERE od.order_ldate > DATEADD(day, -45, CAST(GETDATE() AS DATE))
            ) AS dd
        ) AS mm WHERE rempc > 0

        UNION ALL

        -- Block 3: Weave styles (type = 'wv')
        SELECT weav.order_id, weav.order_no, dbo.yarn_auto_calc_color(wy.product_id, weav.vcolor) AS order_color,
               CAST(wy.product_id AS VARCHAR(100)) AS product_id, '' AS style_guage, style_ply,
               weav.style_no, wy.weight_y AS wt, weav.order_pics, weav.knittedPc, weav.ToknitPc, weav.rempc
        FROM (
            SELECT *, CASE WHEN ToknitPc IS NULL THEN order_pics ELSE order_pics - (knittedPc + ToknitPc) END AS rempc
            FROM (
                SELECT COALESCE(vr.color, od.order_color) AS vcolor, ts.style_id, od.order_pics, ts.style_no,
                       dbo.fn_knittednGiven(od.order_id,'knitted') AS knittedPc,
                       dbo.fn_knittednGiven(od.order_id,'Given')   AS ToknitPc,
                       od.order_color, COALESCE(ts.net_wet, vr.weight) AS wt, od.order_id, od.order_no, style_ply
                FROM tbl_order     AS od
                INNER JOIN tbl_stylesheet AS ts ON od.product_name = ts.style_no AND ts.type = 'wv'
                LEFT  JOIN tbl_color_var  AS vr ON vr.style_id = ts.style_id AND vr.[var] = od.order_color AND vr.weight > 7
                WHERE od.order_ldate > DATEADD(day, -45, CAST(GETDATE() AS DATE))
            ) AS mmd
        ) AS weav
        INNER JOIN tbl_stylesheet_wapweft_yarn AS wy ON weav.style_id = wy.style_id
        WHERE weav.rempc > 0

    ) AS CombinedData;


    /* =================================================================
       2. AGGREGATION, MAIN-STORE STOCK MAPPING & IMPORT DECISION
       ================================================================= */
    ;WITH AggregatedRequirements AS (
        SELECT
            fr.product_id,
            -- Yarn label: product name + count, e.g. "Merino Lambswool 2/28"
            MAX(LTRIM(RTRIM(
                ISNULL(p.product_name, '')
                + CASE WHEN NULLIF(LTRIM(RTRIM(CAST(p.count1 AS VARCHAR(50)))), '') IS NULL
                       THEN '' ELSE ' ' + LTRIM(RTRIM(CAST(p.count1 AS VARCHAR(50)))) END
            ))) AS YarnName,
            fr.order_color,
            MAX(fr.style_guage) AS style_guage,
            MAX(fr.style_ply)   AS style_ply,
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
            COUNT(DISTINCT CASE WHEN LTRIM(RTRIM(fr.order_no)) = @OrderNo THEN fr.style_no END) AS StyleCount,
            SUM(CASE WHEN LTRIM(RTRIM(fr.order_no)) = @OrderNo THEN fr.rempc ELSE 0 END)         AS qty
        FROM #FinalResults fr
        LEFT JOIN tblproduct AS p ON fr.product_id = CAST(p.product_id AS VARCHAR(100))
        WHERE fr.product_id IS NOT NULL
          AND LTRIM(RTRIM(fr.product_id)) NOT IN ('', '0')
        GROUP BY fr.product_id, fr.order_color
    ),
    -- ----------------------------------------------------------------
    -- Available yarn comes from THREE sources, in priority order:
    --   1. Main store  (tbl_cone_stock)                 â€” primary
    --   2. PLM store   (tbl_plm_stock)                   â€” secondary
    --   3. With knitter (tbl_yan_record leftover balance) â€” reusable
    --      e.g. issued 1 kg for 200 g of work => 800 g still usable.
    -- All amounts are kg. They are unioned then pivoted per yarn x color.
    -- ----------------------------------------------------------------
    AllStock AS (
        -- 1. MAIN STORE
        SELECT CAST(op.product_id AS VARCHAR(100)) AS product_id, bp.p_color AS color,
               SUM(CAST(bp.p_wt AS FLOAT) / 1000.0) AS qty, 'main' AS src
        FROM tblopeningproductstock AS op
        INNER JOIN tbl_no_box     AS bx ON op.id = bx.product_id
        INNER JOIN tbl_cone_stock AS bp ON bp.box_id = bx.b_id
        WHERE bp.active = '1' AND bp.for_use = 1
        GROUP BY op.product_id, bp.p_color

        UNION ALL

        -- 2. PLM STORE (secondary)
        SELECT CAST(py.product_id AS VARCHAR(100)) AS product_id, py.color,
               SUM(CAST(py.weight AS FLOAT) / 1000.0) AS qty, 'plm' AS src
        FROM tbl_plm_stock AS py
        WHERE py.[status] = 1 AND py.weight > 0
          AND py.cone NOT IN (SELECT cone FROM tbl_cone_split)
        GROUP BY py.product_id, py.color

        UNION ALL

        -- 3. WITH KNITTER â€” leftover balance = issued cone wt âˆ’ consumed (pics x net_wet+10%)
        SELECT product_id, color, SUM(Balance) / 1000.0 AS qty, 'knitter' AS src
        FROM (
            SELECT dd.product_id, dd.color, (dd.conewt - (dd.pics * dd.net_wet)) AS Balance
            FROM (
                SELECT r.kr_id, CAST(rc.product_id AS VARCHAR(100)) AS product_id, rc.color,
                       SUM(rc.cone_wt) AS conewt,
                       MAX(rd.pics)    AS pics,
                       MAX(ts.net_wet + (ts.net_wet * 10.0 / 100.0)) AS net_wet
                FROM tbl_yan_record           AS rc
                INNER JOIN tbl_knitter_record_data AS rd ON rc.r_id  = rd.r_id
                INNER JOIN tbl_knitter_record      AS r  ON r.kr_id  = rd.r_id
                INNER JOIN tbl_stylesheet          AS ts ON ts.style_no = r.style_no
                WHERE CAST(rc.i_date AS DATE) > '2025-10-01'
                  AND rc.ret_wt = 0 AND rd.pics > rd.ret_pic
                GROUP BY r.kr_id, rc.product_id, rc.color
            ) AS dd
        ) AS kn
        WHERE Balance > 0                       -- only positive leftovers are usable
        GROUP BY product_id, color
    ),
    StockData AS (
        SELECT
            product_id,
            color,
            SUM(CASE WHEN src = 'main'    THEN qty ELSE 0 END) AS MainQty,
            SUM(CASE WHEN src = 'plm'     THEN qty ELSE 0 END) AS PlmQty,
            SUM(CASE WHEN src = 'knitter' THEN qty ELSE 0 END) AS KnitterQty,
            -- Available stock for the decision = main + PLM only.
            -- With-knitter leftover (KnitterQty) is informational / view-only.
            SUM(CASE WHEN src IN ('main','plm') THEN qty ELSE 0 END) AS StockQty
        FROM AllStock
        GROUP BY product_id, color
    )
    SELECT
        r.product_id,
        r.YarnName,
        r.order_color,
        r.style_guage,
        r.style_ply,
        r.selfwt,
        r.othWt,
        r.StyleCount,
        r.qty,
        CAST(ISNULL(s.MainQty,    0) AS DECIMAL(18,3)) AS MainQty,
        CAST(ISNULL(s.PlmQty,     0) AS DECIMAL(18,3)) AS PlmQty,
        CAST(ISNULL(s.KnitterQty, 0) AS DECIMAL(18,3)) AS KnitterQty,
        CAST(ISNULL(s.StockQty,   0) AS DECIMAL(18,3)) AS StockQty,
        -- Stock left for THIS order after backlog has first claim, floored at 0:
        --   availForOrder = MAX(main+plm - backlog, 0)
        -- If stock can't even cover backlog, this order imports its full req
        -- (the backlog deficit belongs to those other orders, not this one).
        --   ShortfallKg = availForOrder - req   (negative => shortage => import)
        CAST(
            (CASE WHEN (ISNULL(s.StockQty, 0) - r.othWt) < 0 THEN 0
                  ELSE (ISNULL(s.StockQty, 0) - r.othWt) END)
            - r.selfwt
        AS DECIMAL(18,3)) AS ShortfallKg,
        CASE WHEN (CASE WHEN (ISNULL(s.StockQty, 0) - r.othWt) < 0 THEN 0
                        ELSE (ISNULL(s.StockQty, 0) - r.othWt) END) < r.selfwt
             THEN 'Import' ELSE 'In-stock' END AS Decision
    INTO #Guage_color_stock
    FROM AggregatedRequirements r
    LEFT JOIN StockData s ON r.product_id = s.product_id AND r.order_color = s.color;


    /* =================================================================
       3. FLAGGED OUTPUT
       ================================================================= */
    IF @Flag = 1
    BEGIN
        -- Import decision for the yarn required by THIS order
        SELECT
            product_id,
            YarnName,
            order_color,
            style_guage,
            style_ply,
            qty AS item_qty,
            CAST(selfwt    AS DECIMAL(18,3)) AS selfwt,
            CAST(othWt     AS DECIMAL(18,3)) AS othWt,
            MainQty,
            PlmQty,
            KnitterQty,
            CAST(StockQty  AS DECIMAL(18,3)) AS StockQty,
            ShortfallKg,
            Decision
        FROM #Guage_color_stock
        WHERE qty > 0
        ORDER BY product_id, order_color;
    END
    ELSE IF @Flag = 2
    BEGIN
        -- Full picture incl. backlog-only yarns (qty = 0)
        SELECT
            product_id, YarnName, order_color, style_guage, style_ply,
            CAST(selfwt   AS DECIMAL(18,3)) AS selfwt,
            CAST(othWt    AS DECIMAL(18,3)) AS othWt,
            StyleCount, qty,
            MainQty, PlmQty, KnitterQty,
            CAST(StockQty AS DECIMAL(18,3)) AS StockQty,
            ShortfallKg, Decision
        FROM #Guage_color_stock
        ORDER BY product_id, order_color;
    END
    ELSE
    BEGIN
        SELECT * FROM #FinalResults WHERE LTRIM(RTRIM(order_no)) = @OrderNo;
    END

    IF OBJECT_ID('tempdb..#FinalResults')      IS NOT NULL DROP TABLE #FinalResults;
    IF OBJECT_ID('tempdb..#Guage_color_stock') IS NOT NULL DROP TABLE #Guage_color_stock;
END
