USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   Task Management board (Knitting).
   One row per ORDER (MasterPlan.MaterID), shaped identically for
   every flag so the same DTO maps all three.

   Each MasterPlanDetail row is one machine line (Machine = KN-27,
   KN-47, ...). So the number of detail rows for a MaterID is the
   ORDER'S MACHINE COUNT -- the column mpd.MachineCount is NOT a
   count (the write path stores the machine id there), so we never
   use it. Machine count = COUNT(*) of the order's detail rows.

   Aggregates per order:
        MachineCount = COUNT(*) detail lines
        Qty          = SUM(mpd.Qty)
        StartDate    = MIN(mpd.StartDate)   (earliest line)
        EndDate      = MAX(mpd.EndDate)     (latest line)

   Date filter (OVERLAP): an order shows when its overall active
   period [MIN StartDate, MAX EndDate] overlaps the selected window
   [@StartDate, @EndDate] -- i.e. MIN(StartDate) <= window-end AND
   MAX(EndDate) >= window-start. When both dates are NULL no date
   filter is applied (returns everything).

     @Flag = 'S'  Scheduled    -> in range, not started
     @Flag = 'P'  In Progress  -> in range, started, pieces outstanding
     @Flag = 'C'  Completed    -> in range, started, all pieces returned

   Status is evaluated across ALL of the order's detail lines:
        "started"     = some line has a knitter row with pics recorded
        "outstanding" = some started piece is not yet fully returned
   S = NOT started ; P = started AND outstanding ; C = started AND NOT outstanding.

   Columns are explicitly aliased (no SELECT *) so Dapper maps
   them reliably by name.
   ============================================================ */
IF OBJECT_ID('[dbo].[spTaskManagement]', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[spTaskManagement] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[spTaskManagement]
    @Flag      NVARCHAR(50) = NULL,
    @StartDate DATETIME     = NULL,
    @EndDate   DATETIME     = NULL,
    @OrderNo   NVARCHAR(50) = NULL   -- optional: contains-match on OrderNo (NULL/'' = all)
AS
BEGIN
    SET NOCOUNT ON;

    --=========================== Scheduled ===========================
    -- In range and not started: no detail line of the order has a
    -- knitter row with pics recorded.
    IF (@Flag = 'S')
    BEGIN
        SELECT
            mp.[MaterID]              AS [TaskId],
            mp.[OrderNo]              AS [OrderNo],
            mp.[OrderType]            AS [OrderType],
            mp.[ProductionType]       AS [ProductionType],
            MAX(mpd.[factory_type])   AS [FactoryType],
            MAX(mpd.[Machine])        AS [Machine],
            MAX(mpd.[Guage])          AS [Guage],
            CAST(SUM(mpd.[Qty]) AS INT) AS [Qty],
            COUNT(*)                  AS [MachineCount],   -- machines = detail lines
            MIN(mpd.[StartDate])      AS [StartDate],
            MAX(mpd.[EndDate])        AS [EndDate],
            mp.[OrderStatus]          AS [OrderStatus],
            MAX(mpd.[PlaningStatus])  AS [PlaningStatus],
            mp.[PlanWorkingStatus]    AS [PlanWorkingStatus]
        FROM [dbo].[MasterPlan] mp WITH (NOLOCK)
        INNER JOIN [dbo].[MasterPlanDetail] mpd WITH (NOLOCK)
            ON mpd.[MaterID] = mp.[MaterID]
        WHERE (@OrderNo IS NULL OR @OrderNo = '' OR mp.[OrderNo] LIKE '%' + @OrderNo + '%')
          AND NOT EXISTS (
                SELECT 1
                FROM [dbo].[MasterPlanDetail] d WITH (NOLOCK)
                INNER JOIN [dbo].[MasterPlanDetailSize] mpds WITH (NOLOCK)
                    ON mpds.[MasterPlanDetailId] = d.[MasterPlanChildId]
                INNER JOIN [dbo].[tbl_knitter_record_data] tkrd WITH (NOLOCK)
                    ON tkrd.[plan_id] = mpds.[id]
                WHERE d.[MaterID] = mp.[MaterID]
                  AND tkrd.[pics] IS NOT NULL)
        GROUP BY mp.[MaterID], mp.[OrderNo], mp.[OrderType], mp.[ProductionType],
                 mp.[OrderStatus], mp.[PlanWorkingStatus]
        HAVING (@EndDate   IS NULL OR MIN(mpd.[StartDate]) <  DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
           AND (@StartDate IS NULL OR MAX(mpd.[EndDate])   >= CAST(@StartDate AS DATE))
        ORDER BY MIN(mpd.[StartDate]) ASC;
    END

    --========================== In Progress ==========================
    -- In range, started AND at least one piece outstanding (across the order).
    IF (@Flag = 'P')
    BEGIN
        SELECT
            mp.[MaterID]              AS [TaskId],
            mp.[OrderNo]              AS [OrderNo],
            mp.[OrderType]            AS [OrderType],
            mp.[ProductionType]       AS [ProductionType],
            MAX(mpd.[factory_type])   AS [FactoryType],
            MAX(mpd.[Machine])        AS [Machine],
            MAX(mpd.[Guage])          AS [Guage],
            CAST(SUM(mpd.[Qty]) AS INT) AS [Qty],
            COUNT(*)                  AS [MachineCount],   -- machines = detail lines
            MIN(mpd.[StartDate])      AS [StartDate],
            MAX(mpd.[EndDate])        AS [EndDate],
            mp.[OrderStatus]          AS [OrderStatus],
            MAX(mpd.[PlaningStatus])  AS [PlaningStatus],
            mp.[PlanWorkingStatus]    AS [PlanWorkingStatus]
        FROM [dbo].[MasterPlan] mp WITH (NOLOCK)
        INNER JOIN [dbo].[MasterPlanDetail] mpd WITH (NOLOCK)
            ON mpd.[MaterID] = mp.[MaterID]
        WHERE (@OrderNo IS NULL OR @OrderNo = '' OR mp.[OrderNo] LIKE '%' + @OrderNo + '%')
          AND EXISTS (
                SELECT 1
                FROM [dbo].[MasterPlanDetail] d WITH (NOLOCK)
                INNER JOIN [dbo].[MasterPlanDetailSize] mpds WITH (NOLOCK)
                    ON mpds.[MasterPlanDetailId] = d.[MasterPlanChildId]
                INNER JOIN [dbo].[tbl_knitter_record_data] tkrd WITH (NOLOCK)
                    ON tkrd.[plan_id] = mpds.[id]
                WHERE d.[MaterID] = mp.[MaterID]
                  AND tkrd.[pics] IS NOT NULL)
          AND EXISTS (
                SELECT 1
                FROM [dbo].[MasterPlanDetail] d WITH (NOLOCK)
                INNER JOIN [dbo].[MasterPlanDetailSize] mpds WITH (NOLOCK)
                    ON mpds.[MasterPlanDetailId] = d.[MasterPlanChildId]
                INNER JOIN [dbo].[tbl_knitter_record_data] tkrd WITH (NOLOCK)
                    ON tkrd.[plan_id] = mpds.[id]
                WHERE d.[MaterID] = mp.[MaterID]
                  AND tkrd.[pics] IS NOT NULL
                  AND (tkrd.[ret_pic] IS NULL OR tkrd.[pics] <> tkrd.[ret_pic]))
        GROUP BY mp.[MaterID], mp.[OrderNo], mp.[OrderType], mp.[ProductionType],
                 mp.[OrderStatus], mp.[PlanWorkingStatus]
        HAVING (@EndDate   IS NULL OR MIN(mpd.[StartDate]) <  DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
           AND (@StartDate IS NULL OR MAX(mpd.[EndDate])   >= CAST(@StartDate AS DATE))
        ORDER BY MIN(mpd.[StartDate]) ASC;
    END

    --=========================== Completed ===========================
    -- In range, started AND nothing outstanding (all returned, across the order).
    IF (@Flag = 'C')
    BEGIN
        SELECT
            mp.[MaterID]              AS [TaskId],
            mp.[OrderNo]              AS [OrderNo],
            mp.[OrderType]            AS [OrderType],
            mp.[ProductionType]       AS [ProductionType],
            MAX(mpd.[factory_type])   AS [FactoryType],
            MAX(mpd.[Machine])        AS [Machine],
            MAX(mpd.[Guage])          AS [Guage],
            CAST(SUM(mpd.[Qty]) AS INT) AS [Qty],
            COUNT(*)                  AS [MachineCount],   -- machines = detail lines
            MIN(mpd.[StartDate])      AS [StartDate],
            MAX(mpd.[EndDate])        AS [EndDate],
            mp.[OrderStatus]          AS [OrderStatus],
            MAX(mpd.[PlaningStatus])  AS [PlaningStatus],
            mp.[PlanWorkingStatus]    AS [PlanWorkingStatus]
        FROM [dbo].[MasterPlan] mp WITH (NOLOCK)
        INNER JOIN [dbo].[MasterPlanDetail] mpd WITH (NOLOCK)
            ON mpd.[MaterID] = mp.[MaterID]
        WHERE (@OrderNo IS NULL OR @OrderNo = '' OR mp.[OrderNo] LIKE '%' + @OrderNo + '%')
          AND EXISTS (
                SELECT 1
                FROM [dbo].[MasterPlanDetail] d WITH (NOLOCK)
                INNER JOIN [dbo].[MasterPlanDetailSize] mpds WITH (NOLOCK)
                    ON mpds.[MasterPlanDetailId] = d.[MasterPlanChildId]
                INNER JOIN [dbo].[tbl_knitter_record_data] tkrd WITH (NOLOCK)
                    ON tkrd.[plan_id] = mpds.[id]
                WHERE d.[MaterID] = mp.[MaterID]
                  AND tkrd.[pics] IS NOT NULL)
          AND NOT EXISTS (
                SELECT 1
                FROM [dbo].[MasterPlanDetail] d WITH (NOLOCK)
                INNER JOIN [dbo].[MasterPlanDetailSize] mpds WITH (NOLOCK)
                    ON mpds.[MasterPlanDetailId] = d.[MasterPlanChildId]
                INNER JOIN [dbo].[tbl_knitter_record_data] tkrd WITH (NOLOCK)
                    ON tkrd.[plan_id] = mpds.[id]
                WHERE d.[MaterID] = mp.[MaterID]
                  AND tkrd.[pics] IS NOT NULL
                  AND (tkrd.[ret_pic] IS NULL OR tkrd.[pics] <> tkrd.[ret_pic]))
        GROUP BY mp.[MaterID], mp.[OrderNo], mp.[OrderType], mp.[ProductionType],
                 mp.[OrderStatus], mp.[PlanWorkingStatus]
        HAVING (@EndDate   IS NULL OR MIN(mpd.[StartDate]) <  DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
           AND (@StartDate IS NULL OR MAX(mpd.[EndDate])   >= CAST(@StartDate AS DATE))
        ORDER BY MIN(mpd.[StartDate]) ASC;
    END
END
GO
