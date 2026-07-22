CREATE PROCEDURE [dbo].[sp_GetPoTask]
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
    -- mirroring spTaskManagement's zero-trust rule.
    DECLARE @UserGauge NVARCHAR(100) = NULL;
    IF (@UserId IS NOT NULL AND @UserId <> '')
        SELECT @UserGauge = NULLIF(LTRIM(RTRIM(u.[AssignedGauge])), '')
        FROM [identity].[Users] u WITH (NOLOCK)
        WHERE u.[Id] = @UserId;
    DECLARE @EffFactory NVARCHAR(100) = COALESCE(@UserGauge, NULLIF(LTRIM(RTRIM(@FactoryType)), ''));

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
                              WHEN 10 THEN 'Yarn issue' WHEN 11 THEN 'Product return'
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
        -- The record this card opens when its title is clicked. Derived per read rather
        -- than stored, so it covers tasks raised before this existed and follows the data
        -- if it ever moves. NULL = the stage has no page of its own, or nothing matched,
        -- and the card keeps its plain title.
        CASE t.[Stage]
            -- BOM: the yarn order holding this task's production order. A production order
            -- belongs to exactly one yarn order; TOP 1 DESC keeps it deterministic if a PO
            -- is ever re-ordered, by preferring the newest.
            WHEN 2 THEN (SELECT TOP (1) od.[yo_id]
                         FROM   [dbo].[tbl_yarn_order_detail] od WITH (NOLOCK)
                         WHERE  od.[order_no] = t.[OrderNo]
                         ORDER BY od.[yo_id] DESC)
            -- Planning: the plan line (MasterPlanChildId) is already stored on the task.
            WHEN 3 THEN t.[RefId]
            ELSE NULL
        END                                            AS [LinkId],
        CASE t.[Stage] WHEN 1 THEN 'PO entry' WHEN 2 THEN 'BOM task' WHEN 3 THEN 'Planning'
                       WHEN 10 THEN 'Yarn issue' WHEN 11 THEN 'Product return'
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
    WHERE t.[IsActive] = 1
      AND t.[Status] <> 'X'
      AND (@Stage IS NULL OR t.[Stage] = @Stage)
      AND (@OrderNo IS NULL OR @OrderNo = '' OR t.[OrderNo] LIKE '%' + @OrderNo + '%')
      AND (@EffFactory IS NULL OR LOWER(t.[FactoryType]) = LOWER(@EffFactory))
      -- window overlap on [StartDate, DueDate]; NULL dates skip the filter
      AND (@EndDate   IS NULL OR t.[StartDate] IS NULL OR t.[StartDate] < DATEADD(DAY, 1, CAST(@EndDate AS DATE)))
      AND (@StartDate IS NULL OR t.[DueDate]   IS NULL OR t.[DueDate]   >= DATEADD(DAY, -1, CAST(@StartDate AS DATE)))
      -- MYTASKS: only tasks the caller is actively assigned to
      AND (@isMine = 0 OR EXISTS (SELECT 1 FROM [dbo].[PoTaskAssignee] a
                                  WHERE a.[PoTaskId] = t.[PoTaskId] AND a.[UserId] = @UserId AND a.[IsActive] = 1));

    SELECT * FROM #cards
    WHERE [DisplayFlag] = @safe
    ORDER BY [DueDate], [TaskId];

    DROP TABLE #cards;
END

