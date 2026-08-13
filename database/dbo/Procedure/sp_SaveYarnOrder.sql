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
        [product_id]    VARCHAR(100),
        [yarn_name]     VARCHAR(200),
        [color]         VARCHAR(100),
        [ply]           VARCHAR(20),
        [order_no]      VARCHAR(50),
        [import_kg_text] NVARCHAR(50)
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
        [productId] VARCHAR(100) '$.productId',
        [yarnName]  VARCHAR(200) '$.yarnName',
        [color]     VARCHAR(100) '$.color',
        [ply]       VARCHAR(20)  '$.ply',
        [orderNo]   VARCHAR(50)  '$.orderNo',
        [importKg]  NVARCHAR(50) '$.importKg'
    );

    IF NOT EXISTS (SELECT 1 FROM @Raw)
       OR EXISTS
          (
              SELECT 1
              FROM @Raw
              WHERE [product_id] IS NULL
                 OR [color] IS NULL
                 OR [order_no] IS NULL
                 OR TRY_CONVERT(DECIMAL(18,3), [import_kg_text]) IS NULL
                 OR TRY_CONVERT(DECIMAL(18,3), [import_kg_text]) <= 0
          )
       OR NULLIF(LTRIM(RTRIM(@AssigneeUserIds)), '') IS NULL
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
    SELECT [product_id],
           MAX([yarn_name]),
           [color],
           [ply],
           [order_no],
           SUM(TRY_CONVERT(DECIMAL(18,3), [import_kg_text]))
    FROM @Raw
    GROUP BY [product_id], [color], [ply], [order_no];

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

    SELECT TOP (1)
        @poTaskId = t.[PoTaskId],
        @yoId = t.[RefId],
        @yoNo = y.[yo_no]
    FROM dbo.[PoTask] t WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.[tbl_yarn_order] y WITH (UPDLOCK, HOLDLOCK)
        ON y.[yo_id] = t.[RefId]
    WHERE t.[Stage] = 12
      AND t.[Status] = 'S'
      AND t.[IsActive] = 1
      AND EXISTS
          (
              SELECT 1
              FROM dbo.[PoTaskAssignee] a
              WHERE a.[PoTaskId] = t.[PoTaskId]
                AND a.[IsActive] = 1
          )
      AND NOT EXISTS
          (
              SELECT 1
              FROM dbo.[PoTaskAssignee] a
              WHERE a.[PoTaskId] = t.[PoTaskId]
                AND a.[IsActive] = 1
                AND (a.[StartDate] IS NOT NULL OR a.[Status] <> 'S')
          )
    ORDER BY t.[PoTaskId] DESC;

    IF @poTaskId IS NOT NULL
        SET @wasAppended = 1;
    ELSE
    BEGIN
        DECLARE @nextNo INT =
            ISNULL
            (
                (
                    SELECT MAX(TRY_CONVERT(INT, RIGHT([yo_no], 3)))
                    FROM dbo.[tbl_yarn_order] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [yo_no] LIKE 'Natureknit Yarn-%'
                ),
                0
            ) + 1;

        SET @yoNo = 'Natureknit Yarn-' + RIGHT('000' + CAST(@nextNo AS VARCHAR(10)), 3);

        INSERT INTO dbo.[tbl_yarn_order]
            ([yo_no], [created_by], [total_kg], [order_count], [line_count], [status])
        VALUES
            (@yoNo, @CreatedBy, 0, 0, 0, 'Placed');

        SET @yoId = CAST(SCOPE_IDENTITY() AS INT);
    END;

    UPDATE d
       SET d.[yarn_name] = i.[yarn_name],
           d.[import_kg] = i.[import_kg]
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
    WHERE [yo_id] = @yoId;

    UPDATE dbo.[tbl_yarn_order]
       SET [total_kg] = @total,
           [order_count] = @orderCnt,
           [line_count] = @lineCnt
    WHERE [yo_id] = @yoId;

    IF @wasAppended = 0
    BEGIN
        DECLARE @CreatedTask TABLE ([PoTaskId] INT);

        INSERT INTO @CreatedTask ([PoTaskId])
        EXEC dbo.[sp_ManagePoTask]
            @Flag = 'CREATE',
            @OrderNo = @firstOrder,
            @Stage = 12,
            @Title = N'Make yarn order - ' + @yoNo,
            @Detail = N'Place the vendor yarn order for ' + @yoNo
                    + N'. Production orders: ' + @incomingOrders,
            @RefId = @yoId,
            @PriorityId = 2,
            @CompletionRule = 2,
            @StartDate = GETDATE(),
            @AssigneeUserIds = @AssigneeUserIds,
            @UserId = @CreatedBy;

        SELECT @poTaskId = [PoTaskId]
        FROM @CreatedTask;
    END
    ELSE
    BEGIN
        UPDATE dbo.[PoTask]
           SET [ModifiedBy] = @CreatedBy,
               [ModifiedDate] = GETDATE(),
               [Detail] = N'Place the vendor yarn order for ' + @yoNo
                        + N'. Production orders: '
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
