-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.sp_SaveYarnOrder  (SQL_STORED_PROCEDURE)

/* ---------------------------------------------------------------------
   Atomically create a yarn order, or append its lines to the newest active
   Stage 12 task whose active assignees have not started it.
   @LinesJson: [{ "productId","yarnName","color","ply","orderNo","importKg" }, ...]
   --------------------------------------------------------------------- */
CREATE OR ALTER PROCEDURE dbo.sp_SaveYarnOrder
    @CreatedBy       VARCHAR(50)   = NULL,
    @LinesJson       NVARCHAR(MAX),
    @AssigneeUserIds NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Raw TABLE
    (
        [product_id] NVARCHAR(MAX),
        [yarn_name] NVARCHAR(MAX),
        [color] NVARCHAR(MAX),
        [ply] NVARCHAR(MAX),
        [order_no] NVARCHAR(MAX),
        [import_kg_text] NVARCHAR(MAX)
    );

    DECLARE @Incoming TABLE
    (
        [product_id] VARCHAR(100) NOT NULL,
        [yarn_name]  VARCHAR(200) NULL,
        [color]      VARCHAR(100) NOT NULL,
        [ply]        VARCHAR(20) NULL,
        [order_no]   VARCHAR(50) NOT NULL,
        [import_kg]  DECIMAL(18,3) NOT NULL
    );

    DECLARE @AssigneeIds TABLE
    (
        [UserId] NVARCHAR(450) NOT NULL PRIMARY KEY
    );

    DECLARE @CandidateTasks TABLE
    (
        [PoTaskId] INT NOT NULL PRIMARY KEY
    );

    DECLARE @LockedAssignees TABLE
    (
        [AssigneeId] INT NOT NULL PRIMARY KEY,
        [Status] CHAR(1) NOT NULL,
        [StartDate] DATETIME NULL,
        [IsActive] BIT NOT NULL
    );

    IF @LinesJson IS NULL OR ISJSON(@LinesJson) <> 1
    BEGIN
        SELECT CAST(NULL AS VARCHAR(30)) AS [YoNo],
               -1 AS [YoId],
               CAST(0 AS DECIMAL(18,3)) AS [TotalKg],
               -1 AS [PoTaskId],
               CAST(0 AS BIT) AS [WasAppended],
               0 AS [OrderCount],
               0 AS [LineCount],
               'Invalid or empty line data.' AS [Message],
               CAST(0 AS BIT) AS [IsSuccess];
        RETURN;
    END;

    INSERT INTO @Raw
        ([product_id], [yarn_name], [color], [ply], [order_no], [import_kg_text])
    SELECT NULLIF(LTRIM(RTRIM([productId])), ''),
           NULLIF(LTRIM(RTRIM([yarnName])), ''),
           NULLIF(LTRIM(RTRIM([color])), ''),
           NULLIF(LTRIM(RTRIM([ply])), ''),
           NULLIF(LTRIM(RTRIM([orderNo])), ''),
           [importKg]
    FROM OPENJSON(@LinesJson)
    WITH
    (
        [productId] NVARCHAR(MAX) '$.productId',
        [yarnName] NVARCHAR(MAX) '$.yarnName',
        [color] NVARCHAR(MAX) '$.color',
        [ply] NVARCHAR(MAX) '$.ply',
        [orderNo] NVARCHAR(MAX) '$.orderNo',
        [importKg] NVARCHAR(MAX) '$.importKg'
    );

    INSERT INTO @AssigneeIds ([UserId])
    SELECT DISTINCT CONVERT(NVARCHAR(450), LTRIM(RTRIM([value])))
    FROM STRING_SPLIT(ISNULL(@AssigneeUserIds, N''), N'|')
    WHERE LEN(LTRIM(RTRIM([value]))) BETWEEN 1 AND 450;

    DECLARE @normalizedAssigneeUserIds NVARCHAR(MAX);

    SELECT @normalizedAssigneeUserIds =
        STRING_AGG(CONVERT(NVARCHAR(MAX), [UserId]), N'|')
        WITHIN GROUP (ORDER BY [UserId])
    FROM @AssigneeIds;

    IF NOT EXISTS (SELECT 1 FROM @Raw)
       OR EXISTS
          (
              SELECT 1
              FROM @Raw
              WHERE [product_id] IS NULL
                 OR [color] IS NULL
                 OR [order_no] IS NULL
                 OR LEN([product_id]) > 100
                 OR LEN([yarn_name]) > 200
                 OR LEN([color]) > 100
                 OR LEN([ply]) > 20
                 OR LEN([order_no]) > 50
                 OR TRY_CONVERT(DECIMAL(18,3), [import_kg_text]) IS NULL
                 OR TRY_CONVERT(DECIMAL(18,3), [import_kg_text]) <= 0
          )
       OR NOT EXISTS (SELECT 1 FROM @AssigneeIds)
    BEGIN
        SELECT CAST(NULL AS VARCHAR(30)) AS [YoNo],
               -1 AS [YoId],
               CAST(0 AS DECIMAL(18,3)) AS [TotalKg],
               -1 AS [PoTaskId],
               CAST(0 AS BIT) AS [WasAppended],
               0 AS [OrderCount],
               0 AS [LineCount],
               'Valid yarn lines and at least one Yarn-role assignee are required.' AS [Message],
               CAST(0 AS BIT) AS [IsSuccess];
        RETURN;
    END;

    INSERT INTO @Incoming
        ([product_id], [yarn_name], [color], [ply], [order_no], [import_kg])
    SELECT CONVERT(VARCHAR(100), [product_id]),
           MAX(CONVERT(VARCHAR(200), [yarn_name])),
           CONVERT(VARCHAR(100), [color]),
           CONVERT(VARCHAR(20), [ply]),
           CONVERT(VARCHAR(50), [order_no]),
           SUM(TRY_CONVERT(DECIMAL(18,3), [import_kg_text]))
    FROM @Raw
    GROUP BY CONVERT(VARCHAR(100), [product_id]),
             CONVERT(VARCHAR(100), [color]),
             CONVERT(VARCHAR(20), [ply]),
             CONVERT(VARCHAR(50), [order_no]);

    DECLARE @lockResult INT,
            @yoId INT,
            @poTaskId INT,
            @yoNo VARCHAR(30),
            @wasAppended BIT = 0,
            @firstOrder VARCHAR(50),
            @incomingOrders NVARCHAR(MAX);

    SELECT TOP (1) @firstOrder = [order_no]
    FROM @Incoming
    ORDER BY [order_no];

    SELECT @incomingOrders = STRING_AGG(CONVERT(NVARCHAR(MAX), x.[order_no]), N', ')
                             WITHIN GROUP (ORDER BY x.[order_no])
    FROM (SELECT DISTINCT [order_no] FROM @Incoming) x;

    BEGIN TRANSACTION;

    EXEC @lockResult = sys.sp_getapplock
        @Resource = N'NkplmErp.YarnOrder.Request',
        @LockMode = 'Exclusive',
        @LockOwner = 'Transaction',
        @LockTimeout = 15000;

    IF @lockResult < 0
        THROW 50001, 'Could not acquire the Yarn Order request lock.', 1;

    /* Candidate discovery is advisory and deliberately uses a statement-scoped
       READ COMMITTED parent read. The retained eligibility lock order is:
       (1) every child assignee row/key range, (2) the parent task row, then
       (3) the linked Yarn Order header. sp_ManagePoTask MYUPDATE changes the
       child before sp_PoTask_Recompute reaches the parent, so matching that order
       avoids a parent/child deadlock. If this save wins the child lock, append is
       linearized before the start; if MYUPDATE wins, the locked recheck observes
       its StartDate/status and this save creates a new task. The application lock
       above continues to serialize save against save. */
    INSERT INTO @CandidateTasks ([PoTaskId])
    SELECT t.[PoTaskId]
    FROM dbo.[PoTask] AS t WITH (READCOMMITTED)
    WHERE t.[Stage] = 12
      AND t.[Status] = 'S'
      AND t.[IsActive] = 1
      AND t.[RefId] IS NOT NULL
      AND EXISTS
          (
              SELECT 1
              FROM dbo.[PoTaskAssignee] AS a
              WHERE a.[PoTaskId] = t.[PoTaskId]
                AND a.[IsActive] = 1
          )
      AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.[PoTaskAssignee] AS a
              WHERE a.[PoTaskId] = t.[PoTaskId]
                AND a.[IsActive] = 1
                AND (a.[StartDate] IS NOT NULL OR a.[Status] <> 'S')
          );

    DECLARE @candidatePoTaskId INT;

    WHILE EXISTS (SELECT 1 FROM @CandidateTasks)
    BEGIN
        SELECT @candidatePoTaskId = MAX([PoTaskId])
        FROM @CandidateTasks;

        DELETE FROM @CandidateTasks
        WHERE [PoTaskId] = @candidatePoTaskId;

        DELETE FROM @LockedAssignees;

        INSERT INTO @LockedAssignees ([AssigneeId], [Status], [StartDate], [IsActive])
        SELECT a.[AssigneeId], a.[Status], a.[StartDate], a.[IsActive]
        FROM dbo.[PoTaskAssignee] AS a WITH (UPDLOCK, HOLDLOCK, INDEX([IX_PoTaskAssignee_Task]))
        WHERE a.[PoTaskId] = @candidatePoTaskId;

        SET @poTaskId = NULL;
        SET @yoId = NULL;
        SET @yoNo = NULL;

        SELECT @poTaskId = t.[PoTaskId],
               @yoId = t.[RefId]
        FROM dbo.[PoTask] AS t WITH (UPDLOCK, HOLDLOCK)
        WHERE t.[PoTaskId] = @candidatePoTaskId
          AND t.[Stage] = 12
          AND t.[Status] = 'S'
          AND t.[IsActive] = 1
          AND t.[RefId] IS NOT NULL
          AND EXISTS (SELECT 1 FROM @LockedAssignees WHERE [IsActive] = 1)
          AND NOT EXISTS
              (
                  SELECT 1
                  FROM @LockedAssignees
                  WHERE [IsActive] = 1
                    AND ([StartDate] IS NOT NULL OR [Status] <> 'S')
              );

        IF @poTaskId IS NOT NULL
        BEGIN
            SELECT @yoNo = y.[yo_no]
            FROM dbo.[tbl_yarn_order] AS y WITH (UPDLOCK, HOLDLOCK)
            WHERE y.[yo_id] = @yoId;

            IF @yoNo IS NOT NULL
                BREAK;

            SET @poTaskId = NULL;
            SET @yoId = NULL;
        END;
    END;

    IF @poTaskId IS NOT NULL
        SET @wasAppended = 1;
    ELSE
    BEGIN
        DECLARE @nextNo INT =
            ISNULL
            (
                (
                    SELECT MAX
                    (
                        TRY_CONVERT
                        (
                            INT,
                            SUBSTRING([yo_no], LEN('Natureknit Yarn-') + 1, 30)
                        )
                    )
                    FROM dbo.[tbl_yarn_order] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [yo_no] LIKE 'Natureknit Yarn-%'
                ),
                0
            ) + 1;

        SET @yoNo = 'Natureknit Yarn-'
                  + CASE
                        WHEN @nextNo < 1000
                            THEN RIGHT('000' + CAST(@nextNo AS VARCHAR(10)), 3)
                        ELSE CAST(@nextNo AS VARCHAR(10))
                    END;

        INSERT INTO dbo.[tbl_yarn_order]
            ([yo_no], [created_by], [total_kg], [order_count], [line_count], [status])
        VALUES
            (@yoNo, @CreatedBy, 0, 0, 0, 'Ready for Approval');

        SET @yoId = CAST(SCOPE_IDENTITY() AS INT);
    END;

    UPDATE d
       SET d.[yarn_name] = i.[yarn_name],
           d.[import_kg] = i.[import_kg],
           d.[is_dropped] = 0,
           d.[drop_date] = NULL,
           d.[drop_by] = NULL,
           d.[drop_note] = NULL
    FROM dbo.[tbl_yarn_order_detail] d
    INNER JOIN @Incoming i
        ON i.[product_id] = d.[product_id]
       AND i.[color] = d.[color]
       AND ISNULL(i.[ply], '') = ISNULL(d.[ply], '')
       AND i.[order_no] = d.[order_no]
    WHERE d.[yo_id] = @yoId;

    INSERT INTO dbo.[tbl_yarn_order_detail]
        ([yo_id], [product_id], [yarn_name], [color], [ply], [order_no], [import_kg])
    SELECT @yoId,
           i.[product_id],
           i.[yarn_name],
           i.[color],
           i.[ply],
           i.[order_no],
           i.[import_kg]
    FROM @Incoming i
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.[tbl_yarn_order_detail] d WITH (UPDLOCK, HOLDLOCK)
        WHERE d.[yo_id] = @yoId
          AND d.[product_id] = i.[product_id]
          AND d.[color] = i.[color]
          AND ISNULL(d.[ply], '') = ISNULL(i.[ply], '')
          AND d.[order_no] = i.[order_no]
    );

    DECLARE @total DECIMAL(18,3),
            @orderCnt INT,
            @lineCnt INT;

    SELECT @total = ISNULL(SUM([import_kg]), 0),
           @orderCnt = COUNT(DISTINCT [order_no]),
           @lineCnt = COUNT(*)
    FROM dbo.[tbl_yarn_order_detail]
    WHERE [yo_id] = @yoId
      AND [is_dropped] = 0;

    UPDATE dbo.[tbl_yarn_order]
       SET [total_kg] = @total,
           [order_count] = @orderCnt,
           [line_count] = @lineCnt
    WHERE [yo_id] = @yoId;

    IF @wasAppended = 0
    BEGIN
        DECLARE @CreatedTask TABLE ([PoTaskId] INT);
        DECLARE @newTaskTitle NVARCHAR(200) = N'Yarn import request ready - ' + @yoNo;
        DECLARE @newTaskDetail NVARCHAR(MAX) = N'Review yarn import request ' + @yoNo
                                              + N' and send it to YarnControl. Production orders: ' + @incomingOrders;
        DECLARE @newTaskStartDate DATETIME = GETDATE();

        INSERT INTO @CreatedTask ([PoTaskId])
        EXEC dbo.[sp_ManagePoTask]
            @Flag = 'CREATE',
            @OrderNo = @firstOrder,
            @Stage = 12,
            @Title = @newTaskTitle,
            @Detail = @newTaskDetail,
            @RefId = @yoId,
            @PriorityId = 2,
            @CompletionRule = 2,
            @StartDate = @newTaskStartDate,
            @AssigneeUserIds = @normalizedAssigneeUserIds,
            @UserId = @CreatedBy;

        SELECT @poTaskId = [PoTaskId]
        FROM @CreatedTask;
    END
    ELSE
    BEGIN
        UPDATE dbo.[PoTaskAssignee]
        SET [IsActive] = 0
        WHERE [PoTaskId] = @poTaskId
          AND [IsActive] = 1;

        UPDATE dbo.[PoTaskAssignee]
        SET [Status] = 'S',
            [StartDate] = NULL,
            [CompletedDate] = NULL,
            [Note] = NULL,
            [AssignedBy] = @CreatedBy,
            [AssignedDate] = GETDATE(),
            [IsActive] = 1
        WHERE [PoTaskId] = @poTaskId
          AND [UserId] IN (SELECT [UserId] FROM @AssigneeIds);

        INSERT INTO dbo.[PoTaskAssignee]
            ([PoTaskId], [UserId], [Status], [AssignedBy])
        SELECT @poTaskId, [UserId], 'S', @CreatedBy
        FROM @AssigneeIds AS wanted
        WHERE NOT EXISTS
              (
                  SELECT 1
                  FROM dbo.[PoTaskAssignee] AS existing
                  WHERE existing.[PoTaskId] = @poTaskId
                    AND existing.[UserId] = wanted.[UserId]
              );

        UPDATE dbo.[PoTask]
           SET [ModifiedBy] = @CreatedBy,
               [ModifiedDate] = GETDATE(),
               [Title] = N'Yarn import request ready - ' + @yoNo,
               [Detail] = N'Review yarn import request ' + @yoNo
                        + N' and send it to YarnControl. Production orders: '
                        +
                          (
                              SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), x.[order_no]), N', ')
                                     WITHIN GROUP (ORDER BY x.[order_no])
                              FROM
                              (
                                  SELECT DISTINCT [order_no]
                                  FROM dbo.[tbl_yarn_order_detail]
                                  WHERE [yo_id] = @yoId
                              ) x
                          )
        WHERE [PoTaskId] = @poTaskId;

        INSERT INTO dbo.[PoTaskNotification]
            ([UserId], [PoTaskId], [Kind], [Title], [Body])
        SELECT a.[UserId],
               @poTaskId,
               'U',
               N'Yarn order updated',
               LEFT(CONVERT(NVARCHAR(MAX), @yoNo) + N' now includes ' + @incomingOrders, 400)
        FROM dbo.[PoTaskAssignee] a
        WHERE a.[PoTaskId] = @poTaskId
          AND a.[IsActive] = 1;
    END;

    COMMIT TRANSACTION;

    SELECT @yoNo AS [YoNo],
           @yoId AS [YoId],
           @total AS [TotalKg],
           @poTaskId AS [PoTaskId],
           @wasAppended AS [WasAppended],
           @orderCnt AS [OrderCount],
           @lineCnt AS [LineCount],
           CASE
               WHEN @wasAppended = 1
                   THEN 'Added request to ' + @yoNo + '. Yarn Order task reused.'
               ELSE @yoNo + ' created. Yarn Order task created.'
           END AS [Message],
           CAST(1 AS BIT) AS [IsSuccess];
END;
