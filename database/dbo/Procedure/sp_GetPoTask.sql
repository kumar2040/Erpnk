/*==============================================================================
  sp_GetPoTask  —  reads for the PO task board (/tasks)

  Flags
    'BOARD'      Org-wide cards for one status bucket (@StatusFlag).
    'MYTASKS'    Same cards, narrowed to the caller's own active assignments;
                 the bucket is applied to the caller's OWN assignee row and the
                 caller's status comes back as [MyStatus].
    'DETAIL'     One task + its assignees, checklist and attachments (4 sets).
    'GROUPS'     Assignable groups, for the Add Task form.
    'ASSIGNEES'  The assignee rows of one task.

  Board filters (BOARD + MYTASKS) — every one is optional; NULL = no filter,
  so the board is unfiltered until the page sends something.

    @StartDate / @EndDate   Date window, matched by OVERLAP against the task's
                            [StartDate, DueDate] span rather than containment,
                            so a task running THROUGH the window still shows up.
                            A task with a NULL end of the span skips that half
                            of the test instead of dropping out. Overdue gets a
                            one-day grace at the window start so a task that
                            ended just before the window (e.g. yesterday, on a
                            today-only view) is still surfaced.
    @OrderNo                Contains-match on the production order number.
    @FactoryType            Facility. ZERO TRUST: a user with an AssignedGauge
                            is pinned to it and @FactoryType is ignored — see
                            @EffFactory below. Only an unrestricted user (no
                            AssignedGauge) may choose, and NULL = all facilities.
    @Stage                  Optional lifecycle stage.

  Cancelled ('X') and inactive rows are never returned.
==============================================================================*/
CREATE OR ALTER PROCEDURE [dbo].[sp_GetPoTask]
    @Flag        NVARCHAR(20),               -- BOARD | MYTASKS | DETAIL | GROUPS | ASSIGNEES
    @StatusFlag  CHAR(1)       = NULL,        -- BOARD/MYTASKS: S/P/C/O/H
    @Stage       TINYINT       = NULL,        -- optional stage filter
    @StartDate   DATETIME      = NULL,
    @EndDate     DATETIME      = NULL,
    @OrderNo     NVARCHAR(50)  = NULL,
    @FactoryType NVARCHAR(100) = NULL,
    @PoTaskId    INT           = NULL,
    @UserId      NVARCHAR(450) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @op NVARCHAR(20) = UPPER(LTRIM(RTRIM(@Flag)));
    DECLARE @today DATE = CAST(GETDATE() AS DATE);

    -- Resolve factory scope from the caller (NULL = super admin = all factories),
    -- mirroring spTaskManagement's zero-trust rule. The caller's own AssignedGauge
    -- WINS over whatever @FactoryType the page sent, so a restricted user cannot
    -- widen their own scope by forging the request.
    DECLARE @UserGauge NVARCHAR(100) = NULL;
    IF (@UserId IS NOT NULL AND @UserId <> '')
        SELECT @UserGauge = NULLIF(LTRIM(RTRIM(u.[AssignedGauge])), '')
        FROM [identity].[Users] u WITH (NOLOCK)
        WHERE u.[Id] = @UserId;
    DECLARE @EffFactory NVARCHAR(100) = COALESCE(@UserGauge, NULLIF(LTRIM(RTRIM(@FactoryType)), ''));

    -- Normalise the remaining board filters once, so the WHERE clause below stays
    -- a plain NULL check and an empty string behaves the same as "not supplied".
    DECLARE @SearchOrderNo NVARCHAR(50) = NULLIF(LTRIM(RTRIM(@OrderNo)), '');
    DECLARE @WindowStart DATE = CAST(@StartDate AS DATE);
    DECLARE @WindowEnd   DATE = CAST(@EndDate   AS DATE);

    -- Shared display-name helpers reused by BOARD / MYTASKS / DETAIL.
    -- (Stage / Status / Priority -> friendly text.)

    /* ------------------------------------------------------------- GROUPS */
    IF (@op = 'GROUPS')
    BEGIN
        SELECT g.[GroupId], g.[GroupName], g.[FactoryType],
               (SELECT COUNT(*) FROM [dbo].[PoTaskGroupMember] m
                WHERE m.[GroupId] = g.[GroupId] AND m.[IsActive] = 1) AS [MemberCount]
        FROM [dbo].[PoTaskGroup] g
        WHERE g.[IsActive] = 1
        ORDER BY g.[GroupName];
        RETURN;
    END

    /* ---------------------------------------------------------- ASSIGNEES */
    IF (@op = 'ASSIGNEES')
    BEGIN
        SELECT a.[AssigneeId], a.[PoTaskId], a.[UserId],
               u.[UserName] AS [UserName], a.[Status],
               a.[StartDate], a.[CompletedDate], a.[Note], a.[AssignedDate]
        FROM [dbo].[PoTaskAssignee] a
        LEFT JOIN [identity].[Users] u WITH (NOLOCK) ON u.[Id] = a.[UserId]
        WHERE a.[PoTaskId] = @PoTaskId AND a.[IsActive] = 1
        ORDER BY u.[UserName];
        RETURN;
    END

    /* ------------------------------------------------------------- DETAIL
       Four result sets: the task, its assignees, its checklist, its attachments. */
    IF (@op = 'DETAIL')
    BEGIN
        SELECT t.[PoTaskId], t.[OrderNo], t.[Stage],
               CASE t.[Stage] WHEN 1 THEN 'PO entry' WHEN 2 THEN 'BOM task' WHEN 3 THEN 'Planning'
                              WHEN 10 THEN 'Yarn issue' WHEN 11 THEN 'Product return' WHEN 12 THEN 'Yarn order'
                              WHEN 20 THEN 'Manual' ELSE 'Task' END AS [StageName],
               t.[Status],
               CASE t.[Status] WHEN 'S' THEN 'Scheduled' WHEN 'P' THEN 'In progress'
                               WHEN 'C' THEN 'Completed' WHEN 'H' THEN 'On hold'
                               WHEN 'X' THEN 'Cancelled' END AS [StatusName],
               t.[FactoryType], t.[Guage], t.[Title], t.[Detail], t.[RefId],
               t.[PriorityId],
               CASE t.[PriorityId] WHEN 1 THEN 'Low' WHEN 2 THEN 'Medium' WHEN 3 THEN 'High' WHEN 4 THEN 'Urgent' END AS [PriorityName],
               t.[NotificationDate], t.[UpdateFrequency], t.[PlanningAction],
               t.[CompletionRule], t.[QuorumCount], t.[BlockedReason],
               t.[StartDate], t.[DueDate], t.[CompletedDate], t.[CreatedBy], t.[CreatedDate]
        FROM [dbo].[PoTask] t
        WHERE t.[PoTaskId] = @PoTaskId AND t.[IsActive] = 1;

        SELECT a.[AssigneeId], a.[UserId], u.[UserName] AS [UserName], a.[Status],
               a.[StartDate], a.[CompletedDate], a.[Note]
        FROM [dbo].[PoTaskAssignee] a
        LEFT JOIN [identity].[Users] u WITH (NOLOCK) ON u.[Id] = a.[UserId]
        WHERE a.[PoTaskId] = @PoTaskId AND a.[IsActive] = 1
        ORDER BY u.[UserName];

        SELECT [ChecklistId], [Text], [IsDone], [SortOrder]
        FROM [dbo].[PoTaskChecklist]
        WHERE [PoTaskId] = @PoTaskId
        ORDER BY [SortOrder];

        SELECT [AttachmentId], [FileName], [ContentType], [SizeBytes], [UploadedBy], [UploadedDate]
        FROM [dbo].[PoTaskAttachment]
        WHERE [PoTaskId] = @PoTaskId
        ORDER BY [UploadedDate] DESC;
        RETURN;
    END

    /* ----------------------------------------------------- BOARD / MYTASKS
       Both return cards; MYTASKS additionally scopes to the caller's own active
       assignments and reports their OWN status as [MyStatus]. The status bucket
       (@StatusFlag) is applied to the relevant status: the parent for BOARD, the
       caller's own assignee row for MYTASKS. */
    DECLARE @safe CHAR(1) = CASE WHEN @StatusFlag IN ('S','P','C','O','H') THEN @StatusFlag ELSE 'S' END;
    DECLARE @isMine BIT = CASE WHEN @op = 'MYTASKS' THEN 1 ELSE 0 END;

    SELECT
        t.[PoTaskId]                                   AS [TaskId],
        t.[OrderNo],
        t.[Stage],
        -- The URL this card's title opens, built per stage. Derived per read, so it covers
        -- tasks raised before this existed and follows the data if it moves. NULL = not
        -- clickable (no page for this stage, or the row is missing the key its page needs).
        -- This CASE is the ONE place a linkable stage is added -- the API and the board
        -- navigate whatever string lands here and never learn a route themselves.
        CASE t.[Stage]
            -- PO entry (1) -> the production plan for this order. No RefId gate here (unlike
            -- stage 3): the plan line does not exist yet -- creating it IS the task -- so the
            -- page opens on the order and the planner builds the plan from there.
            WHEN 1  THEN CASE WHEN q.[OrderNo] IS NOT NULL
                              THEN N'/order-planning?orderNo=' + q.[OrderNo]
                                   + ISNULL(N'&gauge=' + q.[Guage], N'')
                                   + ISNULL(om.[MonthParam], N'') END

            -- BOM (2) -> the bill of materials for its production order, keyed by order no.
            WHEN 2  THEN CASE WHEN q.[OrderNo] IS NOT NULL
                              THEN N'/bom?orderNo=' + q.[OrderNo]
                                   + ISNULL(om.[MonthParam], N'') END

            -- Planning (3) -> opens by order (+ gauge when present). RefId (the plan line,
            -- MasterPlanChildId) must exist, matching the old "no plan line, no link" gate.
            WHEN 3  THEN CASE WHEN t.[RefId] > 0 AND q.[OrderNo] IS NOT NULL
                              THEN N'/order-planning?orderNo=' + q.[OrderNo]
                                   + ISNULL(N'&gauge=' + q.[Guage], N'')
                                   + ISNULL(om.[MonthParam], N'') END

            -- Yarn order lifecycle (12: placed / departure / arrival) -> the placed order on
            -- /yarn-orders, keyed by yo_id. A production order belongs to exactly one yarn
            -- order; TOP 1 DESC stays deterministic if a PO is re-ordered. No match -> NULL.
            WHEN 12 THEN (SELECT TOP (1) N'/yarn-orders/' + CAST(od.[yo_id] AS nvarchar(20))
                          FROM   [dbo].[tbl_yarn_order_detail] od WITH (NOLOCK)
                          WHERE  od.[order_no] = t.[OrderNo]
                          ORDER BY od.[yo_id] DESC)

            -- Manual (20) -> the yarn-orders list (not tied to one placed order).
            WHEN 20 THEN N'/yarn-orders'

            ELSE NULL
        END                                            AS [LinkUrl],
        CASE t.[Stage] WHEN 1 THEN 'PO entry' WHEN 2 THEN 'BOM task' WHEN 3 THEN 'Planning'
                       WHEN 10 THEN 'Yarn issue' WHEN 11 THEN 'Product return' WHEN 12 THEN 'Yarn order'
                       WHEN 20 THEN 'Manual' ELSE 'Task' END AS [StageName],
        t.[Title],
        t.[FactoryType],
        t.[Guage],
        t.[PriorityId],
        CASE t.[PriorityId] WHEN 1 THEN 'Low' WHEN 2 THEN 'Medium' WHEN 3 THEN 'High' WHEN 4 THEN 'Urgent' END AS [PriorityName],
        t.[StartDate],
        t.[DueDate],
        t.[CompletedDate],
        t.[CompletionRule],
        t.[Status]                                     AS [Status],
        -- rollup counts for the "1/3 done" badge
        (SELECT COUNT(*) FROM [dbo].[PoTaskAssignee] a WHERE a.[PoTaskId] = t.[PoTaskId] AND a.[IsActive] = 1) AS [AssigneeTotal],
        (SELECT COUNT(*) FROM [dbo].[PoTaskAssignee] a WHERE a.[PoTaskId] = t.[PoTaskId] AND a.[IsActive] = 1 AND a.[Status] = 'C') AS [AssigneeDone],
        -- the caller's own status (MYTASKS); NULL on the org board
        CASE WHEN @isMine = 1
             THEN (SELECT TOP (1) a.[Status] FROM [dbo].[PoTaskAssignee] a
                   WHERE a.[PoTaskId] = t.[PoTaskId] AND a.[UserId] = @UserId AND a.[IsActive] = 1)
             ELSE NULL END                             AS [MyStatus],
        -- display flag (computes Overdue from the relevant status)
        CASE
            WHEN @isMine = 1 THEN
                (SELECT CASE WHEN a.[Status] = 'C' THEN 'C' WHEN a.[Status] = 'P' THEN 'P' WHEN a.[Status] = 'H' THEN 'H'
                             WHEN a.[Status] = 'S' AND t.[DueDate] < @today THEN 'O' ELSE 'S' END
                 FROM [dbo].[PoTaskAssignee] a
                 WHERE a.[PoTaskId] = t.[PoTaskId] AND a.[UserId] = @UserId AND a.[IsActive] = 1)
            ELSE
                CASE WHEN t.[Status] = 'C' THEN 'C' WHEN t.[Status] = 'P' THEN 'P' WHEN t.[Status] = 'H' THEN 'H'
                     WHEN t.[Status] = 'S' AND t.[DueDate] < @today THEN 'O' ELSE 'S' END
        END                                            AS [DisplayFlag]
    INTO #cards
    FROM [dbo].[PoTask] t
    -- The order's ship month -- first of the month of the earliest order_ldate across its
    -- lines -- pre-formatted as the &month value /order-planning and /bom read. Both pages
    -- load one month at a time, so a link without it lands on today's month and the order
    -- is simply absent from the list. Computed once here, used by every link branch above.
    -- No matching order row -> NULL -> the branches append nothing and the page keeps today.
    OUTER APPLY (
        SELECT N'&month=' + CONVERT(nvarchar(10),
                   DATEADD(DAY, 1 - DAY(MIN(o.[order_ldate])), MIN(o.[order_ldate])), 23) AS [MonthParam]
        FROM   [dbo].[tbl_order] o WITH (NOLOCK)
        WHERE  o.[order_no] = t.[OrderNo]
    ) om
    -- The link's query values, trimmed once so every branch can share them and test presence
    -- with a plain NULL check. They go out as-is; the board percent-encodes them right before
    -- it navigates (GoToUrl in PoTasks.razor.cs), so free text holding a space or '&' survives.
    CROSS APPLY (VALUES (
        NULLIF(LTRIM(RTRIM(t.[OrderNo])), ''),
        NULLIF(LTRIM(RTRIM(t.[Guage])),   '')
    )) q([OrderNo], [Guage])
    WHERE t.[IsActive] = 1
      AND t.[Status] <> 'X'
      AND (@Stage IS NULL OR t.[Stage] = @Stage)
      -- order-no search (contains)
      AND (@SearchOrderNo IS NULL OR t.[OrderNo] LIKE '%' + @SearchOrderNo + '%')
      -- facility: NULL = all facilities; a restricted user is already pinned above
      AND (@EffFactory IS NULL OR LOWER(t.[FactoryType]) = LOWER(@EffFactory))
      -- date window: overlap on [StartDate, DueDate]; NULL dates skip the filter
      AND (@WindowEnd   IS NULL OR t.[StartDate] IS NULL OR t.[StartDate] < DATEADD(DAY, 1, @WindowEnd))
      AND (@WindowStart IS NULL OR t.[DueDate]   IS NULL OR t.[DueDate]   >= DATEADD(DAY, -1, @WindowStart))
      -- MYTASKS: only tasks the caller is actively assigned to
      AND (@isMine = 0 OR EXISTS (SELECT 1 FROM [dbo].[PoTaskAssignee] a
                                  WHERE a.[PoTaskId] = t.[PoTaskId] AND a.[UserId] = @UserId AND a.[IsActive] = 1));

    SELECT * FROM #cards
    WHERE [DisplayFlag] = @safe
    ORDER BY [DueDate], [TaskId];

    DROP TABLE #cards;
END
