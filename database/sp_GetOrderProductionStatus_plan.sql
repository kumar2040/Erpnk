USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER PROCEDURE [dbo].[sp_GetOrderProductionStatus_plan]
    @orderNo NVARCHAR(50),
    @flag INT = 0,
    @gauge NVARCHAR(50) = NULL,
    @ply NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    /* 1. CLEANUP PREVIOUS TEMPORARY TABLES */
    IF OBJECT_ID('tempdb..#FinalResults') IS NOT NULL DROP TABLE #FinalResults;
    IF OBJECT_ID('tempdb..#Guage_color_stock') IS NOT NULL DROP TABLE #Guage_color_stock;
    IF OBJECT_ID('tempdb..#CapacityPlanning') IS NOT NULL DROP TABLE #CapacityPlanning;
    IF OBJECT_ID('tempdb..#ActiveGauges') IS NOT NULL DROP TABLE #ActiveGauges;

    /* 2. DATA GATHERING (YARN REQS & BACKLOGS) */
    SELECT * INTO #FinalResults FROM (
        SELECT order_id, order_no, order_color, style_product_id AS product_id, style_guage, style_ply, style_no, wt, order_pics, knittedPc, ToknitPc, rempc
        FROM (
            SELECT *, CASE WHEN ToknitPc IS NULL THEN order_pics ELSE order_pics - (ToknitPc + knittedPc) END AS rempc
            FROM (
                SELECT od.order_id, od.order_no, COALESCE(cv.color, od.order_color) AS order_color, ts.style_product_id, ts.style_guage, ts.style_no, style_ply,
                       COALESCE(cv.weight, ts.net_wet) AS wt, od.order_pics, 
                       dbo.fn_knittednGiven(od.order_id,'knitted') as knittedPc, dbo.fn_knittednGiven(od.order_id,'Given') as ToknitPc  
                FROM tbl_order as od 
                INNER JOIN tbl_stylesheet as ts on od.product_name = ts.style_no
                LEFT JOIN tbl_color_var as cv on cv.style_id = ts.style_id and cv.[var] = od.order_color and cv.weight <> 1
                WHERE ts.style_tailor<>1 and od.order_ldate > DATEADD(day, -45, CAST(GETDATE() AS DATE))
            ) as dd
        ) as mm WHERE rempc > 0    

        UNION ALL

        SELECT order_id, order_no, order_color, style_product_id AS product_id, style_guage, style_ply, style_no, wt, order_pics, knittedPc, ToknitPc, rempc
        FROM (
            SELECT *, CASE WHEN ToknitPc IS NULL THEN order_pics ELSE order_pics - (ToknitPc + knittedPc) END AS rempc
            FROM (
                SELECT od.order_id, od.order_no, COALESCE(cv.color, od.order_color) AS order_color, ts.style_product_id, ts.style_guage, ts.style_no, style_ply,
                       COALESCE(ex.wt, cv.weight) as wt, od.order_pics, 
                       dbo.fn_knittednGiven(od.order_id,'knitted') as knittedPc, dbo.fn_knittednGiven(od.order_id,'Given') as ToknitPc  
                FROM tbl_order as od 
                INNER JOIN tbl_stylesheet as ts on od.product_name = ts.style_no
                INNER JOIN tbl_stylesheet_extrayarn AS ex ON ts.style_id = ex.style_id
                LEFT JOIN tbl_color_var AS cv on cv.style_id = ts.style_id and cv.[var] = od.order_color and cv.weight <> 1
                WHERE od.order_ldate > DATEADD(day, -45, CAST(GETDATE() AS DATE))
            ) as dd
        ) as mm WHERE rempc > 0

        UNION ALL

        SELECT weav.order_id, weav.order_no, dbo.yarn_auto_calc_color(wy.product_id, weav.vcolor) as order_color, 
                wy.product_id, '' as style_guage, style_ply, weav.style_no, wy.weight_y AS wt, weav.order_pics, weav.knittedPc, weav.ToknitPc, weav.rempc
        FROM (
            SELECT *, CASE WHEN ToknitPc IS NULL THEN order_pics ELSE order_pics - (knittedPc + ToknitPc) END AS rempc 
            FROM (
                SELECT COALESCE(vr.color, od.order_color) AS vcolor, ts.style_id, od.order_pics, ts.style_no,
                       dbo.fn_knittednGiven(od.order_id,'knitted') as knittedPc, dbo.fn_knittednGiven(od.order_id,'Given') as ToknitPc,
                       od.order_color, COALESCE(ts.net_wet, vr.weight) AS wt, od.order_id, od.order_no, style_ply
                FROM tbl_order AS od 
                INNER JOIN tbl_stylesheet AS ts ON od.product_name = ts.style_no AND ts.type = 'wv'
                LEFT JOIN tbl_color_var AS vr ON vr.style_id = ts.style_id AND vr.[var] = od.order_color AND vr.weight > 7 
                WHERE od.order_ldate > DATEADD(day, -45, CAST(GETDATE() AS DATE))
            ) AS mmd  
        ) AS weav 
        INNER JOIN tbl_stylesheet_wapweft_yarn AS wy ON weav.style_id = wy.style_id
        WHERE weav.rempc > 0
    ) AS CombinedData;

    /* 3. AGGREGATION & PHYSICAL STOCK MAPPING */
    ;WITH AggregatedRequirements AS (
        SELECT  
            product_id,  
            order_color,  
            MAX(style_guage) AS style_guage, style_ply,
            SUM(CASE WHEN order_no = @orderNo THEN (rempc * (wt * 1.1) / 1000.0) ELSE 0 END) AS selfwt,
            SUM(CASE WHEN order_no <> @orderNo THEN (rempc * (wt * 1.1) / 1000.0) ELSE 0 END) AS othWt,
            COUNT(DISTINCT CASE WHEN order_no = @orderNo THEN style_no END) as StyleCount
        FROM #FinalResults  
        GROUP BY product_id, order_color, style_ply
    ),
    StockData AS (
        SELECT op.product_id, bp.p_color, SUM(CAST(bp.p_wt AS FLOAT) / 1000.0) AS StockQty
        FROM tblopeningproductstock AS op  
        INNER JOIN tbl_no_box AS bx ON op.id = bx.product_id
        INNER JOIN tbl_cone_stock AS bp ON bp.box_id = bx.b_id  
        WHERE bp.active = '1' AND bp.for_use = 1  
        GROUP BY op.product_id, bp.p_color
    )
    SELECT r.*, ISNULL(s.StockQty, 0) AS StockQty  
    INTO #Guage_color_stock  
    FROM AggregatedRequirements r
    LEFT JOIN StockData s ON r.product_id = s.product_id AND r.order_color = s.p_color;

    /* 4. CAPACITY PLANNING */
    SELECT  
        ISNULL(M.Gauge, K.Gauge) AS Gauge,
        ISNULL(M.TotalMachines, 0) AS MachineLimit,
        ISNULL(K.AvailableKnitters, 0) AS LaborLimit,
        CASE  
            WHEN ISNULL(M.TotalMachines, 0) < ISNULL(K.AvailableKnitters, 0) THEN ISNULL(M.TotalMachines, 0)
            ELSE ISNULL(K.AvailableKnitters, 0)
        END AS ActiveCapacity
    INTO #CapacityPlanning
    FROM (
        SELECT 
            TRY_CAST(REPLACE(REPLACE(REPLACE(Gauge, 'GG', ''), 'G', ''), ' ', '') AS DECIMAL(10,2)) as Gauge, 
            COUNT(MachineNo) as TotalMachines 
        FROM KnitMachine 
        WHERE Gauge IS NOT NULL 
        GROUP BY TRY_CAST(REPLACE(REPLACE(REPLACE(Gauge, 'GG', ''), 'G', ''), ' ', '') AS DECIMAL(10,2))
    ) M
    FULL OUTER JOIN (
        SELECT 
            TRY_CAST(REPLACE(REPLACE(REPLACE(Gauge, 'GG', ''), 'G', ''), ' ', '') AS DECIMAL(10,2)) as Gauge, 
            COUNT(DISTINCT CardNo) as AvailableKnitters 
        FROM KnittersGauges 
        WHERE Gauge IS NOT NULL 
        GROUP BY TRY_CAST(REPLACE(REPLACE(REPLACE(Gauge, 'GG', ''), 'G', ''), ' ', '') AS DECIMAL(10,2))
    ) K ON M.Gauge = K.Gauge;

    /* 5. RESULT 1: YARN STATUS */
    SELECT  
        p.product_id,  
        CONCAT(p.product_name, ':', p.count1) AS Yarn,
        sg.style_guage, sg.style_ply,
        COUNT(DISTINCT sg.order_color) AS ColorCount,
        SUM(sg.StyleCount) AS StyleCount,
        ROUND(SUM(sg.selfwt), 2) AS Required_Kgs,
        ROUND(SUM(sg.othWt), 2) AS Other_Running_Kgs, 
        ROUND(SUM(sg.StockQty), 2) AS Stock_Available,
        CASE WHEN @flag = 1 THEN sg.order_color ELSE '' END AS color,
        '' as styleno,
        CASE  
            WHEN SUM(sg.StockQty) < SUM(sg.selfwt + sg.othWt)  
            THEN 'SHORTAGE: ' + CAST(ROUND(SUM(sg.StockQty) - SUM(sg.selfwt + sg.othWt), 2) AS VARCHAR(50))  
            ELSE 'READY'  
        END AS Stock_Status
    FROM #Guage_color_stock AS sg  
    INNER JOIN tblproduct AS p ON sg.product_id = p.product_id  
    WHERE sg.selfwt > 0  
      AND (
            @flag = 0
            OR (
                @flag = 1
                AND @gauge IS NOT NULL
                AND sg.style_guage = @gauge and sg.style_ply=@ply
            )
          )
    GROUP BY
    GROUPING SETS (
        (p.product_id, p.product_name, p.count1, sg.style_guage, sg.style_ply),
        (p.product_id, p.product_name, p.count1, sg.style_guage, sg.order_color, sg.style_ply)
    )
    HAVING (
        (@flag = 0 AND GROUPING(sg.order_color) = 1)
        OR
        (@flag = 1 AND GROUPING(sg.order_color) = 0)
    )
    ORDER BY p.product_name, sg.style_guage, sg.style_ply;

    /* 6. RESULT 2: AUTOMATED CAPACITY CALCULATIONS */
    DECLARE @RefDate DATE; 
    SELECT TOP 1 @RefDate = order_ldate FROM tbl_order WHERE order_no = @orderNo;
    DECLARE @DaysGoal FLOAT = 3.0;

    WITH CurrentOrderYarnStatus AS (
        SELECT  
            style_guage,
            CASE  
                WHEN SUM(StockQty) < SUM(selfwt + othWt)  
                THEN 'SHORTAGE'  
                ELSE 'READY'  
            END AS Yarn_Summary
        FROM #Guage_color_stock
        WHERE selfwt > 0  
        GROUP BY style_guage
    ),
    WorkloadData AS (
        SELECT  
            ts.style_guage,
            CASE  
                WHEN MAX(CASE WHEN od.order_no = @orderNo AND ts.style_print = 1 AND ts.style_embd = 1 THEN 1 ELSE 0 END) = 1 THEN 'P*E'
                WHEN MAX(CASE WHEN od.order_no = @orderNo AND ts.style_print = 1 THEN 1 ELSE 0 END) = 1  
                     AND MAX(CASE WHEN od.order_no = @orderNo AND ts.style_embd = 1 THEN 1 ELSE 0 END) = 1 THEN 'P*E'
                WHEN MAX(CASE WHEN od.order_no = @orderNo AND ts.style_print = 1 THEN 1 ELSE 0 END) = 1 THEN 'P'
                WHEN MAX(CASE WHEN od.order_no = @orderNo AND ts.style_embd = 1 THEN 1 ELSE 0 END) = 1 THEN 'E'
                ELSE ''  
            END AS NewOrderType,
            CASE  
                WHEN MAX(CASE WHEN od.order_no <> @orderNo AND ts.style_print = 1 AND ts.style_embd = 1 THEN 1 ELSE 0 END) = 1 THEN 'P*E'
                WHEN MAX(CASE WHEN od.order_no <> @orderNo AND ts.style_print = 1 THEN 1 ELSE 0 END) = 1  
                     AND MAX(CASE WHEN od.order_no <> @orderNo AND ts.style_embd = 1 THEN 1 ELSE 0 END) = 1 THEN 'P*E'
                WHEN MAX(CASE WHEN od.order_no <> @orderNo AND ts.style_print = 1 THEN 1 ELSE 0 END) = 1 THEN 'P'
                WHEN MAX(CASE WHEN od.order_no <> @orderNo AND ts.style_embd = 1 THEN 1 ELSE 0 END) = 1 THEN 'E'
                ELSE ''  
            END AS BacklogType,
            SUM(CASE WHEN od.order_ldate < @RefDate THEN od.order_pics - COALESCE(prod.total_received, 0) ELSE 0 END) AS BacklogQty,
            ROUND(SUM(CASE WHEN od.order_ldate < @RefDate THEN (od.order_pics - COALESCE(prod.total_received, 0)) / NULLIF(ts.style_target, 0) ELSE 0 END), 2) AS BacklogDays,
            SUM(CASE WHEN od.order_no = @orderNo THEN od.order_pics - COALESCE(prod.total_received, 0) ELSE 0 END) AS NewOrderQty,
            ROUND(SUM(CASE WHEN od.order_no = @orderNo THEN (od.order_pics - COALESCE(prod.total_received, 0)) / NULLIF(ts.style_target, 0) ELSE 0 END), 2) AS NewOrderDays
        FROM tbl_order AS od 
        INNER JOIN tbl_stylesheet AS ts ON od.product_name = ts.style_no
        LEFT JOIN (
            SELECT r.order_id, COUNT(c.item_no) AS total_received 
            FROM tbl_knitter_record AS r 
            INNER JOIN tbl_knitter_recieved AS c ON r.kr_id = c.item_id 
            GROUP BY r.order_id
        ) AS prod ON od.order_id = prod.order_id
        WHERE ts.type = ''  
          AND ts.style_tailor <> 1  
          AND od.order_ldate > DATEADD(DAY, -45, GETDATE())  
        GROUP BY ts.style_guage
    ),
    FinalAllocation AS (
        SELECT wd.*, cp.ActiveCapacity, cp.MachineLimit, cp.LaborLimit,
            CEILING(wd.BacklogDays / NULLIF(@DaysGoal, 0)) AS Backlog_Req,
            ISNULL(yc.Yarn_Summary, 'READY') AS Yarn_Status
        FROM WorkloadData wd 
        LEFT JOIN #CapacityPlanning cp ON TRY_CAST(REPLACE(REPLACE(REPLACE(wd.style_guage, 'GG', ''), 'G', ''), ' ', '') AS DECIMAL(10,2)) = cp.Gauge
        LEFT JOIN CurrentOrderYarnStatus yc ON wd.style_guage = yc.style_guage
    )
    SELECT  
        style_guage AS Gauge, NewOrderType, BacklogType,
        BacklogDays, NewOrderDays,
        BacklogQty, NewOrderQty,
        ISNULL(ActiveCapacity, 0) AS [True_Gauge_Limit],
        CASE  
            WHEN BacklogQty <= 0 THEN 0
            WHEN Backlog_Req > ISNULL(ActiveCapacity, 0) THEN ISNULL(ActiveCapacity, 0)
            ELSE Backlog_Req
        END AS Suggested_Backlog_Machines,
        CASE  
            WHEN NewOrderQty <= 0 THEN 0
            WHEN ISNULL(ActiveCapacity, 0) - (CASE WHEN BacklogQty <= 0 THEN 0 WHEN Backlog_Req > ISNULL(ActiveCapacity, 0) THEN ISNULL(ActiveCapacity, 0) ELSE Backlog_Req END) <= 0 THEN 0
            ELSE ISNULL(ActiveCapacity, 0) - (CASE WHEN BacklogQty <= 0 THEN 0 WHEN Backlog_Req > ISNULL(ActiveCapacity, 0) THEN ISNULL(ActiveCapacity, 0) ELSE Backlog_Req END)
        END AS Suggested_NewOrder_Machines,
        Yarn_Status,
        CASE  
            WHEN ISNULL(MachineLimit, 0) > ISNULL(LaborLimit, 0) THEN 'IDLE ASSETS: Need ' + CAST(ISNULL(MachineLimit, 0) - ISNULL(LaborLimit, 0) AS VARCHAR(10)) + ' more knitters.'
            WHEN ISNULL(LaborLimit, 0) > ISNULL(MachineLimit, 0) THEN 'LABOR WASTE: Have ' + CAST(ISNULL(LaborLimit, 0) - ISNULL(MachineLimit, 0) AS VARCHAR(10)) + ' extra knitters.'
            ELSE 'OPTIMAL BALANCE'
        END AS Efficiency_Note
    INTO #ActiveGauges  
    FROM FinalAllocation
    WHERE (BacklogDays + NewOrderDays) > 0;

    /* OUTPUT RESULT 2 */
    SELECT Gauge, NewOrderType, BacklogType, BacklogDays, NewOrderDays, BacklogQty, NewOrderQty, [True_Gauge_Limit], Suggested_Backlog_Machines, Suggested_NewOrder_Machines, Yarn_Status, Efficiency_Note 
    FROM #ActiveGauges 
    ORDER BY TRY_CAST(REPLACE(REPLACE(REPLACE(Gauge, 'GG', ''), 'G', ''), ' ', '') AS DECIMAL(10,2)) DESC;


    /* 7. RESULT 3: TIMELINE QUERY WITH ACCURATE MACHINE TRACKING AND OVERLAPS */
    DECLARE @Today DATE = CAST(GETDATE() AS DATE);

    -- Step A: Get raw active plans
    WITH ActivePlans AS (
        SELECT 
            Guage AS Gauge,
            StartDate,
            EndDate,
            qty,
            MachineCount
        FROM dbo.MasterPlanDetail
        WHERE EndDate >= @Today
    ),
    
    -- Step B: Snapshot dates: @Today and the EndDate of each active plan
    SnapshotDates AS (
        SELECT DISTINCT Gauge, @Today AS SnapshotDate FROM ActivePlans
        UNION
        SELECT DISTINCT Gauge, EndDate AS SnapshotDate FROM ActivePlans
    ),
    
    -- Step C: Potential candidate dates when machine state could change
    CandidateDates AS (
        -- Today is a candidate
        SELECT DISTINCT Gauge, @Today AS CandidateDate FROM ActivePlans
        UNION
        -- The day after each plan ends is a candidate
        SELECT DISTINCT Gauge, DATEADD(day, 1, EndDate) AS CandidateDate FROM ActivePlans
    ),
    
    -- Step D: Calculate occupied machines on each candidate date
    CandidateEngaged AS (
        SELECT 
            c.Gauge,
            c.CandidateDate,
            ISNULL(cp.ActiveCapacity, 0) AS ActiveCapacity,
            ISNULL((
                SELECT SUM(p.MachineCount)
                FROM ActivePlans p
                WHERE p.Gauge = c.Gauge
                  AND c.CandidateDate BETWEEN p.StartDate AND p.EndDate
            ), 0) AS EngagedCount
        FROM CandidateDates c
        LEFT JOIN #CapacityPlanning cp ON 
            TRY_CAST(REPLACE(REPLACE(REPLACE(c.Gauge, 'GG', ''), 'G', ''), ' ', '') AS DECIMAL(10,2)) = cp.Gauge
        WHERE c.CandidateDate >= @Today
    ),
    
    -- Step E: Find the earliest date with available capacity
    EarliestFreeDate AS (
        SELECT 
            Gauge,
            MIN(CandidateDate) AS FreeDate
        FROM CandidateEngaged
        WHERE EngagedCount < ActiveCapacity
        GROUP BY Gauge
    ),

    -- Step F: Assemble snapshots for output
    DailyWorkloadSnapshot AS (
        SELECT 
            s.Gauge,
            s.SnapshotDate AS [Plan_Snapshot_Date],
            
            -- Planned Qty Load: sum of qty of plans ending on this date
            ISNULL((
                SELECT SUM(p.qty)
                FROM ActivePlans p
                WHERE p.Gauge = s.Gauge AND p.EndDate = s.SnapshotDate
            ), 0) AS [Planned_Qty_Load],
            
            -- Cumulative engaged machines on the snapshot date itself
            ISNULL((
                SELECT SUM(p.MachineCount)
                FROM ActivePlans p
                WHERE p.Gauge = s.Gauge
                  AND s.SnapshotDate BETWEEN p.StartDate AND p.EndDate
            ), 0) AS [Engaged_Machines],
            
            ISNULL(cp.ActiveCapacity, 0) AS [Total_Active_Capacity_Limit],
            
            -- Find the engaged count on @Today
            ISNULL((
                SELECT TOP 1 EngagedCount 
                FROM CandidateEngaged ce 
                WHERE ce.Gauge = s.Gauge AND ce.CandidateDate = @Today
            ), 0) AS [Today_Engaged_Count],
            
            -- Immediate Free Machines:
            -- For Today: Capacity - (engaged Today)
            -- For Future: Capacity - (engaged on SnapshotDate + 1)
            CASE 
                WHEN s.SnapshotDate = @Today THEN 
                    CASE 
                        WHEN ISNULL(cp.ActiveCapacity, 0) - ISNULL((
                            SELECT SUM(p.MachineCount)
                            FROM ActivePlans p
                            WHERE p.Gauge = s.Gauge
                              AND @Today BETWEEN p.StartDate AND p.EndDate
                        ), 0) < 0 THEN 0
                        ELSE ISNULL(cp.ActiveCapacity, 0) - ISNULL((
                            SELECT SUM(p.MachineCount)
                            FROM ActivePlans p
                            WHERE p.Gauge = s.Gauge
                              AND @Today BETWEEN p.StartDate AND p.EndDate
                        ), 0)
                    END
                ELSE
                    CASE 
                        WHEN ISNULL(cp.ActiveCapacity, 0) - ISNULL((
                            SELECT SUM(p.MachineCount)
                            FROM ActivePlans p
                            WHERE p.Gauge = s.Gauge
                              AND DATEADD(day, 1, s.SnapshotDate) BETWEEN p.StartDate AND p.EndDate
                        ), 0) < 0 THEN 0
                        ELSE ISNULL(cp.ActiveCapacity, 0) - ISNULL((
                            SELECT SUM(p.MachineCount)
                            FROM ActivePlans p
                            WHERE p.Gauge = s.Gauge
                              AND DATEADD(day, 1, s.SnapshotDate) BETWEEN p.StartDate AND p.EndDate
                        ), 0)
                    END
            END AS [Immediate_Free_Machines],
            
            -- Get the earliest free date calculated for this gauge
            ISNULL((
                SELECT TOP 1 FreeDate 
                FROM EarliestFreeDate ef 
                WHERE ef.Gauge = s.Gauge
            ), DATEADD(day, 1, (SELECT MAX(EndDate) FROM ActivePlans ap WHERE ap.Gauge = s.Gauge))) AS [Calculated_Free_Date],

            TRY_CAST(REPLACE(REPLACE(REPLACE(s.Gauge, 'GG', ''), 'G', ''), ' ', '') AS DECIMAL(10,2)) AS CleanGaugeSort
        FROM SnapshotDates s
        LEFT JOIN #CapacityPlanning cp ON 
            TRY_CAST(REPLACE(REPLACE(REPLACE(s.Gauge, 'GG', ''), 'G', ''), ' ', '') AS DECIMAL(10,2)) = cp.Gauge
    )

    /* OUTPUT RESULT 3 */
    SELECT 
        [Gauge],
        [Plan_Snapshot_Date],
        [Planned_Qty_Load],
        [Engaged_Machines],
        [Immediate_Free_Machines],
        [Total_Active_Capacity_Limit],
        
        -- Today's date to map correctly to timeline.TodayDate
        @Today AS [Today_Date],
        
        -- Free Machines Date: Shows exactly when a machine slot becomes available to accept a new plan
        CONVERT(VARCHAR(10), CAST([Calculated_Free_Date] AS DATE), 120) AS [Free_Machines_Date],
        
        -- Engaged Machines Release Date: Shows when the current line workload item on this row completely finishes
        CONVERT(VARCHAR(10), CAST([Plan_Snapshot_Date] AS DATE), 120) AS [Engaged_Machines_Release_Date]
        
    FROM DailyWorkloadSnapshot
    ORDER BY 
        CASE WHEN CleanGaugeSort IS NULL THEN 0 ELSE 1 END DESC,
        CleanGaugeSort DESC, 
        [Plan_Snapshot_Date] ASC;

    /* 8. HOUSEKEEPING CLEANUP */
    IF OBJECT_ID('tempdb..#FinalResults') IS NOT NULL DROP TABLE #FinalResults;
    IF OBJECT_ID('tempdb..#Guage_color_stock') IS NOT NULL DROP TABLE #Guage_color_stock;
    IF OBJECT_ID('tempdb..#CapacityPlanning') IS NOT NULL DROP TABLE #CapacityPlanning;
    IF OBJECT_ID('tempdb..#ActiveGauges') IS NOT NULL DROP TABLE #ActiveGauges;
END
GO
