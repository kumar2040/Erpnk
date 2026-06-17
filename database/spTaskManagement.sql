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

   Date filter (OVERLAP): most flags show an order when its overall
   active period [MIN StartDate, MAX EndDate] overlaps the selected
   window [@StartDate, @EndDate] -- i.e. MIN(StartDate) <= window-end
   AND MAX(EndDate) >= window-start. When both dates are NULL no date
   filter is applied (returns everything). EXCEPTION: In Progress is
   filtered on the START DATE only (MIN(StartDate) <= window-end),
   ignoring the end date -- see its block.

     @Flag = 'S'  Scheduled    -> in range, NOT started, NOT overdue
     @Flag = 'P'  In Progress  -> started & outstanding, from its start date on
     @Flag = 'C'  Completed    -> started AND a returned piece (pics = ret_pic)
     @Flag = 'O'  Overdue      -> NOT started AND end date already passed

   Status is evaluated across ALL of the order's detail lines:
        "started"     = some line has a knitter row with pics recorded
        "outstanding" = some started piece is not yet fully returned
   S = NOT started & not past due ; O = NOT started & past due ;
   P = started AND outstanding ; C = started AND a returned piece (pics = ret_pic).

   OVERDUE: an order is overdue when it was NEVER STARTED (no knitter
   row with pics recorded on any line) AND its latest end date is before
   today (MAX(EndDate) < CAST(GETDATE() AS DATE)). It is the past-due
   half of the not-started orders -- the same not-started test as
   Scheduled, split by date. A STARTED order is never overdue (it lives
   in In Progress or Completed), so Overdue overlaps neither.
   The Scheduled query ALSO requires the order to be NOT overdue
   (MAX(EndDate) >= today): it shows not-started orders that are not yet
   past due, while past-due not-started orders go to Overdue. In Progress
   is filtered on its start date only and shows started & outstanding
   orders from their start date on, past due or not. Overdue is not-started
   only, so it does NOT overlap In Progress or Completed. (The one residual
   overlap is Completed vs In Progress for a MIXED order -- some pieces
   returned, some still outstanding -- because both use EXISTS.) The Overdue flag honours the selected date window
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
    -- Started AND at least one piece outstanding (across the order).
    -- Filtered on the START DATE only -- see the HAVING below -- and NOT
    -- gated on the end date, so an outstanding order keeps showing here
    -- from its start date onward, even once it is past due.
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
        -- Pending is filtered on the START DATE ONLY: show the order once the
        -- selected window reaches its start (MIN StartDate <= window end) and
        -- keep showing it regardless of the end date. A started-but-outstanding
        -- order stays In Progress even after its end date passes (so past-due
        -- outstanding orders appear in BOTH In Progress and Overdue).
        HAVING (@EndDate IS NULL OR MIN(mpd.[StartDate]) < DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
        ORDER BY MIN(mpd.[StartDate]) ASC;
    END

    --=========================== Completed ===========================
    -- Started AND at least one piece fully returned (a knitter row with
    -- pics = ret_pic). Date filter is overlap (no end-date cut-off), so a
    -- completed order stays here even past its end date. NOTE: this is
    -- EXISTS(returned) rather than "all returned", so an order with some
    -- returned and some outstanding pieces appears in BOTH Completed and
    -- In Progress.
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
          AND EXISTS (
                SELECT 1
                FROM [dbo].[MasterPlanDetail] d WITH (NOLOCK)
                INNER JOIN [dbo].[MasterPlanDetailSize] mpds WITH (NOLOCK)
                    ON mpds.[MasterPlanDetailId] = d.[MasterPlanChildId]
                INNER JOIN [dbo].[tbl_knitter_record_data] tkrd WITH (NOLOCK)
                    ON tkrd.[plan_id] = mpds.[id]
                WHERE d.[MaterID] = mp.[MaterID]
                  AND tkrd.[pics] IS NOT NULL
                  AND tkrd.[pics] = tkrd.[ret_pic])   -- a fully-returned piece (pics = ret_pic)
        GROUP BY mp.[MaterID], mp.[OrderNo], mp.[OrderType], mp.[ProductionType],
                 mp.[OrderStatus], mp.[PlanWorkingStatus]
        HAVING (@EndDate   IS NULL OR MIN(mpd.[StartDate]) <  DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
           AND (@StartDate IS NULL OR MAX(mpd.[EndDate])   >= CAST(@StartDate AS DATE))
        ORDER BY MIN(mpd.[StartDate]) ASC;
    END

    --============================ Overdue ============================
    -- NOT started and past due: no detail line of the order has a knitter
    -- row with pics recorded, AND the order's latest end date is before
    -- today (MAX(EndDate) < today). This is the past-due half of the
    -- not-started orders (Scheduled is the not-yet-due half); a started
    -- order -- In Progress or Completed -- is never overdue, so Overdue
    -- does not overlap those columns.
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
          -- NOT started: no detail line of the order has a knitter row with
          -- pics recorded. (Overdue is the past-due half of the not-started
          -- orders; a started order -- In Progress or Completed -- is never
          -- overdue, so Overdue never overlaps those columns.)
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
