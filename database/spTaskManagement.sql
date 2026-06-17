USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   Task Management board (Knitting).
   One row per ORDER (MasterPlan.MaterID), shaped identically for
   every flag so the same DTO maps all of them.

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

     @Flag = 'S'  Scheduled    -> in range, NOT started, NOT overdue
     @Flag = 'P'  In Progress  -> in range, started, outstanding, NOT overdue
     @Flag = 'C'  Completed    -> in range, started, all pieces returned
     @Flag = 'O'  Overdue      -> end date already passed AND not completed

   Status is evaluated across ALL of the order's detail lines:
        "started"     = some line has a knitter row with pics recorded
        "outstanding" = some started piece is not yet fully returned
   S = NOT started ; P = started AND outstanding ; C = started AND NOT outstanding.

   OVERDUE: an order is overdue once its latest end date is before
   today (MAX(EndDate) < CAST(GETDATE() AS DATE)) AND it is NOT
   completed -- i.e. it was never started, or it was started but still
   has outstanding pieces. A completed order is never overdue.
   To keep the columns mutually exclusive, the Scheduled and In
   Progress queries now ALSO require the order to be NOT overdue
   (MAX(EndDate) >= today); past-due work moves out of those columns
   and into Overdue. The Overdue flag honours the selected date window
   by OVERLAP, exactly like S/P/C (the order's [StartDate, EndDate]
   overlaps the window), with a ONE-DAY GRACE at the window start
   (MAX(EndDate) >= @StartDate - 1 day) so a task that ended the day
   before the window -- e.g. yesterday, on today's daily view -- still
   surfaces instead of vanishing the moment it falls due. When the dates
   are NULL no window filter is applied (returns every overdue order).
   See the Overdue block for the exact predicate.

   Columns are explicitly aliased (no SELECT *) so Dapper maps
   them reliably by name.
   ============================================================ */
IF OBJECT_ID('[dbo].[spTaskManagement]', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[spTaskManagement] AS BEGIN SET NOCOUNT ON; END');
GO

Create PROCEDURE [dbo].[spTaskManagement]
    @Flag      NVARCHAR(50) = NULL,
    @StartDate DATETIME     = NULL,
    @EndDate   DATETIME     = NULL,
    @OrderNo   NVARCHAR(50) = NULL   -- optional: contains-match on OrderNo (NULL/'' = all)
AS
BEGIN
    SET NOCOUNT ON;

    --=========================== Scheduled ===========================
    -- In range, not started AND not overdue: no detail line of the order
    -- has a knitter row with pics recorded, and the order's end date has
    -- not yet passed (past-due not-started orders go to Overdue).
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
           AND MAX(mpd.[EndDate]) >= CAST(GETDATE() AS DATE)   -- not overdue (past-due -> Overdue)
        ORDER BY MIN(mpd.[StartDate]) ASC;
    END

    --========================== In Progress ==========================
    -- In range, started AND at least one piece outstanding (across the
    -- order) AND not overdue (end date has not yet passed; past-due
    -- outstanding orders go to Overdue).
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
           AND MAX(mpd.[EndDate]) >= CAST(GETDATE() AS DATE)   -- not overdue (past-due -> Overdue)
        ORDER BY MIN(mpd.[StartDate]) ASC;
    END

    --=========================== Completed ===========================
    -- In range, started AND nothing outstanding (all returned, across the
    -- order). Completed orders are never overdue, so there is no end-date
    -- cut-off here -- a finished order stays Completed even past its date.
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

    --============================ Overdue ============================
    -- Past due and not finished: the order's latest end date is before
    -- today (MAX(EndDate) < today) AND the order is NOT completed -- it
    -- was either never started, or it was started but still has
    -- outstanding pieces. Completed orders are excluded (they stay in the
    -- Completed column).
    --
    -- The selected date window applies here too, by OVERLAP (same rule as
    -- S/P/C): the order's active period [MIN StartDate, MAX EndDate]
    -- overlaps [@StartDate, @EndDate], so picking any date between an
    -- order's start and end surfaces it. The window-start side gets a
    -- ONE-DAY GRACE (>= @StartDate - 1 day) so a task that ended the day
    -- before the window -- e.g. yesterday, on today's daily view -- still
    -- surfaces instead of vanishing the moment it is due. NULL dates =>
    -- no window filter (every overdue order).
    IF (@Flag = 'O')
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
          AND (
                -- NOT started: no detail line has a knitter row with pics ...
                NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[MasterPlanDetail] d WITH (NOLOCK)
                    INNER JOIN [dbo].[MasterPlanDetailSize] mpds WITH (NOLOCK)
                        ON mpds.[MasterPlanDetailId] = d.[MasterPlanChildId]
                    INNER JOIN [dbo].[tbl_knitter_record_data] tkrd WITH (NOLOCK)
                        ON tkrd.[plan_id] = mpds.[id]
                    WHERE d.[MaterID] = mp.[MaterID]
                      AND tkrd.[pics] IS NOT NULL)
                -- ... OR started but still has outstanding pieces.
                OR EXISTS (
                    SELECT 1
                    FROM [dbo].[MasterPlanDetail] d WITH (NOLOCK)
                    INNER JOIN [dbo].[MasterPlanDetailSize] mpds WITH (NOLOCK)
                        ON mpds.[MasterPlanDetailId] = d.[MasterPlanChildId]
                    INNER JOIN [dbo].[tbl_knitter_record_data] tkrd WITH (NOLOCK)
                        ON tkrd.[plan_id] = mpds.[id]
                    WHERE d.[MaterID] = mp.[MaterID]
                      AND tkrd.[pics] IS NOT NULL
                      AND (tkrd.[ret_pic] IS NULL OR tkrd.[pics] <> tkrd.[ret_pic]))
              )
        GROUP BY mp.[MaterID], mp.[OrderNo], mp.[OrderType], mp.[ProductionType],
                 mp.[OrderStatus], mp.[PlanWorkingStatus]
        HAVING MAX(mpd.[EndDate]) < CAST(GETDATE() AS DATE)   -- genuinely overdue (ended before today)
           -- ...and the order's active period [MIN StartDate, MAX EndDate]
           -- OVERLAPS the selected window (same overlap as S/P/C), so picking
           -- any date between an order's start and end surfaces it. The
           -- window-start side has a ONE-DAY GRACE so a task that ended the day
           -- before the window start (yesterday, for today's view) still shows.
           AND (@EndDate   IS NULL OR MIN(mpd.[StartDate]) <  DATEADD(DAY,  1, CAST(@EndDate   AS DATE)))
           AND (@StartDate IS NULL OR MAX(mpd.[EndDate])   >= DATEADD(DAY, -1, CAST(@StartDate AS DATE)))
        ORDER BY MAX(mpd.[EndDate]) ASC;                      -- most overdue first
    END
END
GO
