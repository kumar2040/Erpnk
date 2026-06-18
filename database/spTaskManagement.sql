USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   Task Management board (Knitting).
   One row per DETAIL LINE (MasterPlanDetail.MasterPlanChildId) -- a single
   machine/stage of an order. The rows are NOT grouped, so an order
   (MaterID / OrderNo) that has several lines yields several cards, each
   showing its own gauge, machine, dates, qty and status. The same OrderNo
   therefore appears on more than one card.

   Per-line columns:
        TaskId       = MasterPlanChildId  (unique per line)
        OrderNo      = the order this line belongs to (repeats across lines)
        Machine      = mpd.Machine        (e.g. KN-27)
        Qty          = mpd.Qty
        StartDate    = mpd.StartDate
        EndDate      = mpd.EndDate
        MachineCount = per-ORDER count of knit machines (MasterPlanDetail
                       rows for the MaterID with factory_type='knit' AND
                       Machine <> '1'), but sent ONLY on a card whose own
                       Machine <> '1'. A card whose line has Machine = '1'
                       gets NULL (no count shown).

   Guage column (display): NULL for a knit line (knit gauge numbers are
   never shown); for a non-knit line it is the gauge value, resolved to the
   tbl_tailor name when the gauge holds a tailor code (tid), otherwise the
   raw gauge value. Each line has exactly one factory_type, so there is no
   aggregation/assumption involved.

   Date filter (OVERLAP): most flags show a line when its active period
   [StartDate, EndDate] overlaps the selected window [@StartDate, @EndDate]
   -- i.e. StartDate <= window-end AND EndDate >= window-start. When both
   dates are NULL no date filter is applied. EXCEPTION: In Progress is
   filtered on the START DATE only (StartDate <= window-end), ignoring the
   end date -- see its block.

     @Flag = 'S'  Scheduled    -> line NOT started, NOT overdue
     @Flag = 'P'  In Progress  -> line started & outstanding, from its start date on
     @Flag = 'C'  Completed    -> line has a returned piece (pics = ret_pic)
     @Flag = 'O'  Overdue      -> line NOT started AND end date already passed

   Status is per LINE, from the knitter records tied to that line's sizes
   (MasterPlanDetailSize.MasterPlanDetailId = mpd.MasterPlanChildId):
        "started"     = some size of the line has a knitter row with pics
        "outstanding" = some started piece is not fully returned (ret_pic
                        NULL or pics <> ret_pic)
        "returned"    = some piece is fully returned (pics = ret_pic)
   NOTE: knitter records exist for KNITTING; a non-knit line (weave/tailor)
   has none, so it reads as NOT started -> it lands in Scheduled or Overdue
   by date.

   S = NOT started & not past due ; O = NOT started & past due ;
   P = outstanding ; C = a returned piece. Overdue is not-started only, so it
   never overlaps In Progress/Completed; C and P can overlap for a line that
   has both a returned and an outstanding piece (both use EXISTS). Overdue
   honours the window by overlap with a ONE-DAY GRACE at the window start so
   a line that ended the day before the window (e.g. yesterday, on today's
   view) still surfaces instead of vanishing the moment it falls due.

   Columns are explicitly aliased (no SELECT *) so Dapper maps them by name.
   ============================================================ */
IF OBJECT_ID('[dbo].[spTaskManagement]', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[spTaskManagement] AS BEGIN SET NOCOUNT ON; END');
GO

alter PROCEDURE [dbo].[spTaskManagement]
    @Flag        NVARCHAR(50)  = NULL,
    @StartDate   DATETIME      = NULL,
    @EndDate     DATETIME      = NULL,
    @OrderNo     NVARCHAR(50)  = NULL,  -- optional: contains-match on OrderNo (NULL/'' = all)
    @FactoryType   NVARCHAR(100) = NULL,  -- admin's factory dropdown pick (ignored for restricted users)
    @UserId        NVARCHAR(450) = NULL,  -- current user; their identity.Users.AssignedGauge locks the scope
    @SubCategories NVARCHAR(MAX) = NULL   -- pipe-delimited gauge sub-methods ('general'|text); NULL/''/'all' = no sub-filter
AS
BEGIN
    SET NOCOUNT ON;

    -- ---- Resolve the caller's factory scope from the identity Users table ----
    -- The logged-in user's AssignedGauge decides what they may see:
    --   NULL / blank  => SUPER ADMIN: no restriction (may narrow by @FactoryType).
    --   has a value   => LOCKED to that factory_type, regardless of @FactoryType (zero trust).
    DECLARE @UserGauge NVARCHAR(100) = NULL;   -- matches identity.Users.AssignedGauge width
    IF (@UserId IS NOT NULL AND @UserId <> '')
        SELECT @UserGauge = NULLIF(LTRIM(RTRIM(u.[AssignedGauge])), '')
        FROM [identity].[Users] u WITH (NOLOCK)
        WHERE u.[Id] = @UserId;

    -- A restricted user's gauge always wins; an admin falls back to their dropdown pick.
    -- NULL here means "no factory filter" (show all factories).
    DECLARE @EffectiveFactory NVARCHAR(100) =
        COALESCE(@UserGauge, NULLIF(LTRIM(RTRIM(@FactoryType)), ''));

    -- ---- Cascading sub-category (gauge method) multi-select ----
    -- @SubCategories: pipe-delimited list (e.g. 'general|T2'). For each row a numeric
    -- gauge maps to 'general'; any other gauge maps to its trimmed text. NULL / '' /
    -- 'all' means no sub-filter. Split ONCE into a session-local table variable: no
    -- shared-lock/deadlock surface, and STRING_SPLIT is parameterised (no dynamic SQL).
    DECLARE @SubList TABLE (val NVARCHAR(100));
    IF (@SubCategories IS NOT NULL
        AND LTRIM(RTRIM(@SubCategories)) <> ''
        AND LOWER(LTRIM(RTRIM(@SubCategories))) <> 'all')
        INSERT INTO @SubList (val)
        SELECT DISTINCT LOWER(LTRIM(RTRIM(s.[value])))
        FROM STRING_SPLIT(@SubCategories, '|') s
        WHERE LTRIM(RTRIM(s.[value])) <> ''
          AND LOWER(LTRIM(RTRIM(s.[value]))) <> 'all';
    DECLARE @HasSub BIT = CASE WHEN EXISTS (SELECT 1 FROM @SubList) THEN 1 ELSE 0 END;

    --=========================== Scheduled ===========================
    -- One row per detail line that is NOT started and NOT overdue: the line
    -- has no knitter row with pics, and its end date has not yet passed
    -- (past-due not-started lines go to Overdue).
    IF (@Flag = 'S')
    BEGIN
        SELECT
            mpd.[MasterPlanChildId]   AS [TaskId],
            mp.[OrderNo]              AS [OrderNo],
            mp.[OrderType]            AS [OrderType],
            mp.[ProductionType]       AS [ProductionType],
            mpd.[factory_type]        AS [FactoryType],
            mpd.[Machine]             AS [Machine],
            CASE
                WHEN mpd.[factory_type] <> 'knit' AND tl.[name] IS NOT NULL
                    THEN tl.[name]                                  -- non-knit + tailor code (T1,T2,...) -> name
                WHEN mpd.[factory_type] <> 'knit'
                    THEN NULLIF(LTRIM(RTRIM(mpd.[Guage])), '')      -- non-knit, no tailor match -> raw gauge value
                ELSE NULL                                           -- knit -> hide (no gauge numbers)
            END                       AS [Guage],
            CAST(mpd.[Qty] AS INT)    AS [Qty],
            CASE WHEN mpd.[Machine] <> '1'
                 THEN (SELECT COUNT(*) FROM [dbo].[MasterPlanDetail] m WITH (NOLOCK)
                       WHERE m.[MaterID] = mpd.[MaterID]
                         AND m.[factory_type] = 'knit'
                         AND m.[Machine] <> '1')
                 ELSE NULL END
                                      AS [MachineCount],   -- knit-machine count for the ORDER, sent ONLY on a card whose own Machine<>'1' (Machine='1' -> NULL)
            mpd.[StartDate]           AS [StartDate],
            mpd.[EndDate]             AS [EndDate],
            mp.[OrderStatus]          AS [OrderStatus],
            mpd.[PlaningStatus]       AS [PlaningStatus],
            mp.[PlanWorkingStatus]    AS [PlanWorkingStatus]
        FROM [dbo].[MasterPlan] mp WITH (NOLOCK)
        INNER JOIN [dbo].[MasterPlanDetail] mpd WITH (NOLOCK)
            ON mpd.[MaterID] = mp.[MaterID]
        -- Resolve a tailor/factory code in mpd.Guage to its name (deduped on tid).
        LEFT JOIN (SELECT [tid], MAX([name]) AS [name]
                   FROM [dbo].[tbl_tailor] WITH (NOLOCK) GROUP BY [tid]) tl
            ON tl.[tid] = mpd.[Guage]
        WHERE (@OrderNo IS NULL OR @OrderNo = '' OR mp.[OrderNo] LIKE '%' + @OrderNo + '%')
          -- factory scope: a restricted user is locked to their AssignedGauge (resolved above);
          -- an admin may narrow by @FactoryType. NULL @EffectiveFactory = show all factories.
          AND (@EffectiveFactory IS NULL OR LOWER(mpd.[factory_type]) = LOWER(@EffectiveFactory))
          -- cascading sub-category filter: numeric gauge -> 'general', else the gauge text.
          AND (@HasSub = 0 OR
               (CASE WHEN TRY_CONVERT(DECIMAL(18,4), LTRIM(RTRIM(mpd.[Guage]))) IS NOT NULL
                     THEN 'general'                                  -- numeric gauge -> general
                     WHEN tl.[name] IS NOT NULL
                     THEN LOWER(LTRIM(RTRIM(tl.[name])))             -- tailor code (T2) -> name (Laxaman Jee)
                     ELSE LOWER(LTRIM(RTRIM(mpd.[Guage]))) END) IN (SELECT val FROM @SubList))
          AND mpd.[plan_status] = 0      -- exclude held lines (plan_status = 1 -> Hold column)
          -- this LINE is not started (none of its sizes has a knitter row with pics)
          AND NOT EXISTS (
                SELECT 1
                FROM [dbo].[MasterPlanDetailSize] mpds WITH (NOLOCK)
                INNER JOIN [dbo].[tbl_knitter_record_data] tkrd WITH (NOLOCK)
                    ON tkrd.[plan_id] = mpds.[id]
                WHERE mpds.[MasterPlanDetailId] = mpd.[MasterPlanChildId]
                  AND tkrd.[pics] IS NOT NULL)
          AND (@EndDate   IS NULL OR mpd.[StartDate] <  DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
          AND (@StartDate IS NULL OR mpd.[EndDate]   >= CAST(@StartDate AS DATE))
          AND mpd.[EndDate] >= CAST(GETDATE() AS DATE)   -- not overdue (past-due -> Overdue)
        ORDER BY mpd.[StartDate] ASC;
    END

    --========================== In Progress ==========================
    -- One row per detail line that is started AND still has an outstanding
    -- piece. Filtered on the START DATE only (not gated on the end date), so
    -- an outstanding line keeps showing from its start date on, even once it
    -- is past due.
    IF (@Flag = 'P')
    BEGIN
        SELECT
            mpd.[MasterPlanChildId]   AS [TaskId],
            mp.[OrderNo]              AS [OrderNo],
            mp.[OrderType]            AS [OrderType],
            mp.[ProductionType]       AS [ProductionType],
            mpd.[factory_type]        AS [FactoryType],
            mpd.[Machine]             AS [Machine],
            CASE
                WHEN mpd.[factory_type] <> 'knit' AND tl.[name] IS NOT NULL
                    THEN tl.[name]                                  -- non-knit + tailor code (T1,T2,...) -> name
                WHEN mpd.[factory_type] <> 'knit'
                    THEN NULLIF(LTRIM(RTRIM(mpd.[Guage])), '')      -- non-knit, no tailor match -> raw gauge value
                ELSE NULL                                           -- knit -> hide (no gauge numbers)
            END                       AS [Guage],
            CAST(mpd.[Qty] AS INT)    AS [Qty],
            CASE WHEN mpd.[Machine] <> '1'
                 THEN (SELECT COUNT(*) FROM [dbo].[MasterPlanDetail] m WITH (NOLOCK)
                       WHERE m.[MaterID] = mpd.[MaterID]
                         AND m.[factory_type] = 'knit'
                         AND m.[Machine] <> '1')
                 ELSE NULL END
                                      AS [MachineCount],   -- knit-machine count for the ORDER, sent ONLY on a card whose own Machine<>'1' (Machine='1' -> NULL)
            mpd.[StartDate]           AS [StartDate],
            mpd.[EndDate]             AS [EndDate],
            mp.[OrderStatus]          AS [OrderStatus],
            mpd.[PlaningStatus]       AS [PlaningStatus],
            mp.[PlanWorkingStatus]    AS [PlanWorkingStatus]
        FROM [dbo].[MasterPlan] mp WITH (NOLOCK)
        INNER JOIN [dbo].[MasterPlanDetail] mpd WITH (NOLOCK)
            ON mpd.[MaterID] = mp.[MaterID]
        LEFT JOIN (SELECT [tid], MAX([name]) AS [name]
                   FROM [dbo].[tbl_tailor] WITH (NOLOCK) GROUP BY [tid]) tl
            ON tl.[tid] = mpd.[Guage]
        WHERE (@OrderNo IS NULL OR @OrderNo = '' OR mp.[OrderNo] LIKE '%' + @OrderNo + '%')
          -- factory scope: a restricted user is locked to their AssignedGauge (resolved above);
          -- an admin may narrow by @FactoryType. NULL @EffectiveFactory = show all factories.
          AND (@EffectiveFactory IS NULL OR LOWER(mpd.[factory_type]) = LOWER(@EffectiveFactory))
          -- cascading sub-category filter: numeric gauge -> 'general', else the gauge text.
          AND (@HasSub = 0 OR
               (CASE WHEN TRY_CONVERT(DECIMAL(18,4), LTRIM(RTRIM(mpd.[Guage]))) IS NOT NULL
                     THEN 'general'                                  -- numeric gauge -> general
                     WHEN tl.[name] IS NOT NULL
                     THEN LOWER(LTRIM(RTRIM(tl.[name])))             -- tailor code (T2) -> name (Laxaman Jee)
                     ELSE LOWER(LTRIM(RTRIM(mpd.[Guage]))) END) IN (SELECT val FROM @SubList))
          AND mpd.[plan_status] = 0      -- exclude held lines (plan_status = 1 -> Hold column)
          -- this LINE is started AND has an outstanding piece (outstanding implies started)
          AND EXISTS (
                SELECT 1
                FROM [dbo].[MasterPlanDetailSize] mpds WITH (NOLOCK)
                INNER JOIN [dbo].[tbl_knitter_record_data] tkrd WITH (NOLOCK)
                    ON tkrd.[plan_id] = mpds.[id]
                WHERE mpds.[MasterPlanDetailId] = mpd.[MasterPlanChildId]
                  AND tkrd.[pics] IS NOT NULL
                  AND (tkrd.[ret_pic] IS NULL OR tkrd.[pics] <> tkrd.[ret_pic]))
          AND (@EndDate IS NULL OR mpd.[StartDate] < DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
        ORDER BY mpd.[StartDate] ASC;
    END

    --=========================== Completed ===========================
    -- One row per detail line that has at least one fully-returned piece
    -- (a knitter row with pics = ret_pic). Overlap date filter (no end-date
    -- cut-off), so a completed line stays even past its end date. A line
    -- with both a returned and an outstanding piece appears in BOTH
    -- Completed and In Progress.
    IF (@Flag = 'C')
    BEGIN
        SELECT
            mpd.[MasterPlanChildId]   AS [TaskId],
            mp.[OrderNo]              AS [OrderNo],
            mp.[OrderType]            AS [OrderType],
            mp.[ProductionType]       AS [ProductionType],
            mpd.[factory_type]        AS [FactoryType],
            mpd.[Machine]             AS [Machine],
            CASE
                WHEN mpd.[factory_type] <> 'knit' AND tl.[name] IS NOT NULL
                    THEN tl.[name]                                  -- non-knit + tailor code (T1,T2,...) -> name
                WHEN mpd.[factory_type] <> 'knit'
                    THEN NULLIF(LTRIM(RTRIM(mpd.[Guage])), '')      -- non-knit, no tailor match -> raw gauge value
                ELSE NULL                                           -- knit -> hide (no gauge numbers)
            END                       AS [Guage],
            CAST(mpd.[Qty] AS INT)    AS [Qty],
            CASE WHEN mpd.[Machine] <> '1'
                 THEN (SELECT COUNT(*) FROM [dbo].[MasterPlanDetail] m WITH (NOLOCK)
                       WHERE m.[MaterID] = mpd.[MaterID]
                         AND m.[factory_type] = 'knit'
                         AND m.[Machine] <> '1')
                 ELSE NULL END
                                      AS [MachineCount],   -- knit-machine count for the ORDER, sent ONLY on a card whose own Machine<>'1' (Machine='1' -> NULL)
            mpd.[StartDate]           AS [StartDate],
            mpd.[EndDate]             AS [EndDate],
            mp.[OrderStatus]          AS [OrderStatus],
            mpd.[PlaningStatus]       AS [PlaningStatus],
            mp.[PlanWorkingStatus]    AS [PlanWorkingStatus]
        FROM [dbo].[MasterPlan] mp WITH (NOLOCK)
        INNER JOIN [dbo].[MasterPlanDetail] mpd WITH (NOLOCK)
            ON mpd.[MaterID] = mp.[MaterID]
        LEFT JOIN (SELECT [tid], MAX([name]) AS [name]
                   FROM [dbo].[tbl_tailor] WITH (NOLOCK) GROUP BY [tid]) tl
            ON tl.[tid] = mpd.[Guage]
        WHERE (@OrderNo IS NULL OR @OrderNo = '' OR mp.[OrderNo] LIKE '%' + @OrderNo + '%')
          -- factory scope: a restricted user is locked to their AssignedGauge (resolved above);
          -- an admin may narrow by @FactoryType. NULL @EffectiveFactory = show all factories.
          AND (@EffectiveFactory IS NULL OR LOWER(mpd.[factory_type]) = LOWER(@EffectiveFactory))
          -- cascading sub-category filter: numeric gauge -> 'general', else the gauge text.
          AND (@HasSub = 0 OR
               (CASE WHEN TRY_CONVERT(DECIMAL(18,4), LTRIM(RTRIM(mpd.[Guage]))) IS NOT NULL
                     THEN 'general'                                  -- numeric gauge -> general
                     WHEN tl.[name] IS NOT NULL
                     THEN LOWER(LTRIM(RTRIM(tl.[name])))             -- tailor code (T2) -> name (Laxaman Jee)
                     ELSE LOWER(LTRIM(RTRIM(mpd.[Guage]))) END) IN (SELECT val FROM @SubList))
		  AND mpd.[plan_status] = 0
          -- this LINE has a fully-returned piece (pics = ret_pic)
          AND EXISTS (
                SELECT 1
                FROM [dbo].[MasterPlanDetailSize] mpds WITH (NOLOCK)
                INNER JOIN [dbo].[tbl_knitter_record_data] tkrd WITH (NOLOCK)
                    ON tkrd.[plan_id] = mpds.[id]
                WHERE mpds.[MasterPlanDetailId] = mpd.[MasterPlanChildId]
                  AND tkrd.[pics] IS NOT NULL
                  AND tkrd.[pics] = tkrd.[ret_pic])
          AND (@EndDate   IS NULL OR mpd.[StartDate] <  DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
          AND (@StartDate IS NULL OR mpd.[EndDate]   >= CAST(@StartDate AS DATE))
        ORDER BY mpd.[StartDate] ASC;
    END

    --============================ Overdue ============================
    -- One row per detail line that is NOT started AND past due: the line has
    -- no knitter row with pics, AND its end date is before today. The
    -- past-due half of the not-started lines (Scheduled is the not-yet-due
    -- half); a started line is never overdue, so Overdue does not overlap In
    -- Progress or Completed. The selected window applies by OVERLAP, with a
    -- ONE-DAY GRACE at the window start (>= @StartDate - 1 day) so a line
    -- that ended the day before the window -- e.g. yesterday, on today's
    -- daily view -- still surfaces. NULL dates => no window filter.
    IF (@Flag = 'O')
    BEGIN
        SELECT
            mpd.[MasterPlanChildId]   AS [TaskId],
            mp.[OrderNo]              AS [OrderNo],
            mp.[OrderType]            AS [OrderType],
            mp.[ProductionType]       AS [ProductionType],
            mpd.[factory_type]        AS [FactoryType],
            mpd.[Machine]             AS [Machine],
            CASE
                WHEN mpd.[factory_type] <> 'knit' AND tl.[name] IS NOT NULL
                    THEN tl.[name]                                  -- non-knit + tailor code (T1,T2,...) -> name
                WHEN mpd.[factory_type] <> 'knit'
                    THEN NULLIF(LTRIM(RTRIM(mpd.[Guage])), '')      -- non-knit, no tailor match -> raw gauge value
                ELSE NULL                                           -- knit -> hide (no gauge numbers)
            END                       AS [Guage],
            CAST(mpd.[Qty] AS INT)    AS [Qty],
            CASE WHEN mpd.[Machine] <> '1'
                 THEN (SELECT COUNT(*) FROM [dbo].[MasterPlanDetail] m WITH (NOLOCK)
                       WHERE m.[MaterID] = mpd.[MaterID]
                         AND m.[factory_type] = 'knit'
                         AND m.[Machine] <> '1')
                 ELSE NULL END
                                      AS [MachineCount],   -- knit-machine count for the ORDER, sent ONLY on a card whose own Machine<>'1' (Machine='1' -> NULL)
            mpd.[StartDate]           AS [StartDate],
            mpd.[EndDate]             AS [EndDate],
            mp.[OrderStatus]          AS [OrderStatus],
            mpd.[PlaningStatus]       AS [PlaningStatus],
            mp.[PlanWorkingStatus]    AS [PlanWorkingStatus]
        FROM [dbo].[MasterPlan] mp WITH (NOLOCK)
        INNER JOIN [dbo].[MasterPlanDetail] mpd WITH (NOLOCK)
            ON mpd.[MaterID] = mp.[MaterID]
        LEFT JOIN (SELECT [tid], MAX([name]) AS [name]
                   FROM [dbo].[tbl_tailor] WITH (NOLOCK) GROUP BY [tid]) tl
            ON tl.[tid] = mpd.[Guage]
        WHERE (@OrderNo IS NULL OR @OrderNo = '' OR mp.[OrderNo] LIKE '%' + @OrderNo + '%')
          -- factory scope: a restricted user is locked to their AssignedGauge (resolved above);
          -- an admin may narrow by @FactoryType. NULL @EffectiveFactory = show all factories.
          AND (@EffectiveFactory IS NULL OR LOWER(mpd.[factory_type]) = LOWER(@EffectiveFactory))
          -- cascading sub-category filter: numeric gauge -> 'general', else the gauge text.
          AND (@HasSub = 0 OR
               (CASE WHEN TRY_CONVERT(DECIMAL(18,4), LTRIM(RTRIM(mpd.[Guage]))) IS NOT NULL
                     THEN 'general'                                  -- numeric gauge -> general
                     WHEN tl.[name] IS NOT NULL
                     THEN LOWER(LTRIM(RTRIM(tl.[name])))             -- tailor code (T2) -> name (Laxaman Jee)
                     ELSE LOWER(LTRIM(RTRIM(mpd.[Guage]))) END) IN (SELECT val FROM @SubList))
          AND mpd.[plan_status] = 0      -- exclude held lines (plan_status = 1 -> Hold column)
          -- this LINE is not started
          AND NOT EXISTS (
                SELECT 1
                FROM [dbo].[MasterPlanDetailSize] mpds WITH (NOLOCK)
                INNER JOIN [dbo].[tbl_knitter_record_data] tkrd WITH (NOLOCK)
                    ON tkrd.[plan_id] = mpds.[id]
                WHERE mpds.[MasterPlanDetailId] = mpd.[MasterPlanChildId]
                  AND tkrd.[pics] IS NOT NULL)
          AND mpd.[EndDate] < CAST(GETDATE() AS DATE)   -- genuinely overdue (ended before today)
          AND (@EndDate   IS NULL OR mpd.[StartDate] <  DATEADD(DAY,  1, CAST(@EndDate   AS DATE)))
          AND (@StartDate IS NULL OR mpd.[EndDate]   >= DATEADD(DAY, -1, CAST(@StartDate AS DATE)))
        ORDER BY mpd.[EndDate] ASC;   -- most overdue first
    END

    --============================= On Hold ============================
    -- One row per detail line that is HELD (plan_status = 1). Held lines are
    -- pulled out of S/P/C/O (those filter plan_status = 0) and live only here.
    -- They IGNORE the end date and the started/returned logic entirely; the
    -- ONLY date filter is on the START DATE -- a held line shows once its start
    -- date is on or before the selected window end (StartDate <= window end)
    -- and then keeps showing (no end-date cut-off, so it never "expires").
    -- Order-number search and factory/gauge scope still apply.
    IF (@Flag = 'H')
    BEGIN
        SELECT
            mpd.[MasterPlanChildId]   AS [TaskId],
            mp.[OrderNo]              AS [OrderNo],
            mp.[OrderType]            AS [OrderType],
            mp.[ProductionType]       AS [ProductionType],
            mpd.[factory_type]        AS [FactoryType],
            mpd.[Machine]             AS [Machine],
            CASE
                WHEN mpd.[factory_type] <> 'knit' AND tl.[name] IS NOT NULL
                    THEN tl.[name]                                  -- non-knit + tailor code (T1,T2,...) -> name
                WHEN mpd.[factory_type] <> 'knit'
                    THEN NULLIF(LTRIM(RTRIM(mpd.[Guage])), '')      -- non-knit, no tailor match -> raw gauge value
                ELSE NULL                                           -- knit -> hide (no gauge numbers)
            END                       AS [Guage],
            CAST(mpd.[Qty] AS INT)    AS [Qty],
            CASE WHEN mpd.[Machine] <> '1'
                 THEN (SELECT COUNT(*) FROM [dbo].[MasterPlanDetail] m WITH (NOLOCK)
                       WHERE m.[MaterID] = mpd.[MaterID]
                         AND m.[factory_type] = 'knit'
                         AND m.[Machine] <> '1')
                 ELSE NULL END
                                      AS [MachineCount],   -- knit-machine count for the ORDER, sent ONLY on a card whose own Machine<>'1' (Machine='1' -> NULL)
            mpd.[StartDate]           AS [StartDate],
            mpd.[EndDate]             AS [EndDate],
            mp.[OrderStatus]          AS [OrderStatus],
            mpd.[PlaningStatus]       AS [PlaningStatus],
            mp.[PlanWorkingStatus]    AS [PlanWorkingStatus]
        FROM [dbo].[MasterPlan] mp WITH (NOLOCK)
        INNER JOIN [dbo].[MasterPlanDetail] mpd WITH (NOLOCK)
            ON mpd.[MaterID] = mp.[MaterID]
        LEFT JOIN (SELECT [tid], MAX([name]) AS [name]
                   FROM [dbo].[tbl_tailor] WITH (NOLOCK) GROUP BY [tid]) tl
            ON tl.[tid] = mpd.[Guage]
        WHERE (@OrderNo IS NULL OR @OrderNo = '' OR mp.[OrderNo] LIKE '%' + @OrderNo + '%')
          AND (@EffectiveFactory IS NULL OR LOWER(mpd.[factory_type]) = LOWER(@EffectiveFactory))
          -- cascading sub-category filter: numeric gauge -> 'general', else the gauge text.
          AND (@HasSub = 0 OR
               (CASE WHEN TRY_CONVERT(DECIMAL(18,4), LTRIM(RTRIM(mpd.[Guage]))) IS NOT NULL
                     THEN 'general'                                  -- numeric gauge -> general
                     WHEN tl.[name] IS NOT NULL
                     THEN LOWER(LTRIM(RTRIM(tl.[name])))             -- tailor code (T2) -> name (Laxaman Jee)
                     ELSE LOWER(LTRIM(RTRIM(mpd.[Guage]))) END) IN (SELECT val FROM @SubList))
          AND mpd.[plan_status] = 1   -- held lines only
          -- start-date-only window: show once StartDate is on/before the window end; no end-date cut-off.
          AND (@EndDate IS NULL OR mpd.[StartDate] < DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
        ORDER BY mpd.[StartDate] ASC;
    END

    --======================= Factory types ==========================
    -- Distinct factory_type values for the board's factory dropdown.
    -- Admin/unrestricted users pick from these; a gauge-restricted user is
    -- locked to their own value server-side and never needs this list.
    IF (@Flag = 'FT')
    BEGIN
        SELECT DISTINCT LTRIM(RTRIM(mpd.[factory_type])) AS [FactoryType]
        FROM [dbo].[MasterPlanDetail] mpd WITH (NOLOCK)
        WHERE mpd.[factory_type] IS NOT NULL
          AND LTRIM(RTRIM(mpd.[factory_type])) <> ''
        ORDER BY [FactoryType];
    END

    --======================== Sub-categories =========================
    -- Distinct gauge "methods" for the cascading sub-filter checkboxes:
    --   numeric gauge      -> 'general'
    --   tailor code (T1..) -> the tailor NAME from tbl_tailor (e.g. T2 -> 'Laxaman Jee')
    --   anything else      -> the raw gauge text (e.g. 'Pashminalooms')
    -- Scoped to @EffectiveFactory (NULL = all factories, e.g. admin on "All Factories";
    -- a restricted user is locked to their own gauge), AND to the selected date window so
    -- the options reflect only sub-categories that have rows overlapping the window.
    IF (@Flag = 'SUB')
    BEGIN
        SELECT DISTINCT
            CASE WHEN TRY_CONVERT(DECIMAL(18,4), LTRIM(RTRIM(mpd.[Guage]))) IS NOT NULL
                 THEN 'general'
                 WHEN tl.[name] IS NOT NULL
                 THEN LTRIM(RTRIM(tl.[name]))
                 ELSE LTRIM(RTRIM(mpd.[Guage])) END AS [SubCategory]
        FROM [dbo].[MasterPlanDetail] mpd WITH (NOLOCK)
        LEFT JOIN (SELECT [tid], MAX([name]) AS [name]
                   FROM [dbo].[tbl_tailor] WITH (NOLOCK) GROUP BY [tid]) tl
            ON tl.[tid] = mpd.[Guage]
        WHERE mpd.[Guage] IS NOT NULL
          AND LTRIM(RTRIM(mpd.[Guage])) <> ''
          AND (@EffectiveFactory IS NULL OR LOWER(mpd.[factory_type]) = LOWER(@EffectiveFactory))
          -- Match the board's LOOSEST date rule. All columns require StartDate <= window end,
          -- but In Progress and On Hold filter on the START date ONLY (no end-date cut-off).
          -- Using window OVERLAP here would drop the chips whenever the visible rows are
          -- In Progress / On Hold whose end date falls before the window start (e.g. June
          -- tasks shown in a September window). So filter on the start date only.
          AND (@EndDate IS NULL OR mpd.[StartDate] < DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
        ORDER BY [SubCategory];
    END

    --========================== Current gauge =========================
    -- The caller's resolved factory scope (NULL => super admin). The board's
    -- /scope endpoint uses this to choose an editable (admin) vs a fixed
    -- (restricted) factory dropdown.
    IF (@Flag = 'GAUGE')
    BEGIN
        SELECT @UserGauge AS [AssignedGauge];
    END
END
GO
