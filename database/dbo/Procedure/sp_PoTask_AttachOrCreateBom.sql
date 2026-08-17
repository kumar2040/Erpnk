CREATE OR ALTER PROCEDURE [dbo].[sp_PoTask_AttachOrCreateBom]
    @OrderNo          nvarchar(50),
    @ReviewId         int            = NULL,
    @FactoryType      nvarchar(100)  = NULL,
    @Detail           nvarchar(max)  = NULL,
    @NotificationDate datetime       = NULL,
    @DueDate          datetime       = NULL,
    @AssigneeUserIds  nvarchar(max)  = NULL,
    @GroupId          int            = NULL,
    @UserId           nvarchar(450)  = NULL,
    @MaxOrders        int            = 5
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @OrderNo = NULLIF(LTRIM(RTRIM(@OrderNo)), N'');
    SET @MaxOrders = CASE WHEN ISNULL(@MaxOrders, 0) < 1 THEN 5 ELSE @MaxOrders END;
    SET @Detail = ISNULL(NULLIF(LTRIM(RTRIM(@Detail)), N''), N'Auto-created from a reviewed order for BOM calculation.');

    IF @OrderNo IS NULL
    BEGIN
        RAISERROR('Order number is required.', 16, 1);
        RETURN;
    END;

    DECLARE @PoTaskId int = NULL,
            @WasCreated bit = 0,
            @WasAttached bit = 0;

    DECLARE @TargetAssignees TABLE
    (
        [UserId] nvarchar(450) NOT NULL PRIMARY KEY
    );

    INSERT INTO @TargetAssignees ([UserId])
    SELECT DISTINCT CONVERT(nvarchar(450), LTRIM(RTRIM([value])))
    FROM STRING_SPLIT(ISNULL(@AssigneeUserIds, N''), N'|')
    WHERE LEN(LTRIM(RTRIM([value]))) BETWEEN 1 AND 450;

    BEGIN TRANSACTION;

    -- Serialize membership for this order. The filtered unique index remains the
    -- final protection if two sweep workers reach this branch together.
    SELECT @PoTaskId = [PoTaskId]
    FROM [dbo].[PoTaskOrder] WITH (UPDLOCK, HOLDLOCK)
    WHERE [OrderNo] = @OrderNo AND [IsActive] = 1;

    IF @PoTaskId IS NULL
    BEGIN
        -- Newest active Stage-2 task that is stored Scheduled, has not started,
        -- and still has room. Overdue is only a display state, so it stays eligible.
        SELECT TOP (1) @PoTaskId = t.[PoTaskId]
        FROM [dbo].[PoTask] t WITH (UPDLOCK, HOLDLOCK)
        WHERE t.[Stage] = 2
          AND t.[IsActive] = 1
          AND t.[Status] = 'S'
          AND (@FactoryType IS NULL OR ISNULL(t.[FactoryType], N'') = ISNULL(@FactoryType, N''))
          AND NOT EXISTS
              (SELECT 1 FROM [dbo].[PoTaskAssignee] a WITH (HOLDLOCK)
               WHERE a.[PoTaskId] = t.[PoTaskId] AND a.[IsActive] = 1
                 AND (a.[Status] IN ('P','C') OR a.[StartDate] IS NOT NULL))
          AND (CASE WHEN EXISTS (SELECT 1 FROM [dbo].[PoTaskOrder] eo WITH (HOLDLOCK)
                                 WHERE eo.[PoTaskId] = t.[PoTaskId] AND eo.[IsActive] = 1)
                    THEN (SELECT COUNT(*) FROM [dbo].[PoTaskOrder] o WITH (HOLDLOCK)
                          WHERE o.[PoTaskId] = t.[PoTaskId] AND o.[IsActive] = 1)
                    WHEN t.[OrderNo] IS NULL THEN 0 ELSE 1 END) < @MaxOrders
        -- Fill the latest eligible BOM batch. Overdue is derived only for display,
        -- so a stored Scheduled task remains eligible until an assignee starts it.
        ORDER BY t.[PoTaskId] DESC;

        IF @PoTaskId IS NULL
        BEGIN
            DECLARE @created TABLE ([PoTaskId] int);
            DECLARE @TaskTitle nvarchar(200) = N'BOM - ' + @OrderNo;
            DECLARE @startDateLocal datetime = GETDATE();

            INSERT INTO @created ([PoTaskId])
            EXEC [dbo].[sp_ManagePoTask]
                 @Flag = N'CREATE', @OrderNo = @OrderNo, @Stage = 2,
                 @FactoryType = @FactoryType,
                 @Title = @TaskTitle,
                 @Detail = @Detail,
                 @NotificationDate = @NotificationDate,
                 @DueDate = @DueDate,
                 @StartDate = @startDateLocal,
                 @CompletionRule = 1,
                 @AssigneeUserIds = @AssigneeUserIds,
                 @GroupId = @GroupId,
                 @UserId = @UserId;

            SELECT TOP (1) @PoTaskId = [PoTaskId] FROM @created;
            SET @WasCreated = 1;
        END;

        -- Existing single-order tasks predate PoTaskOrder. Materialize their legacy
        -- primary order before adding the newly reviewed order so both are calculated.
        IF @WasCreated = 0
        BEGIN
            INSERT INTO [dbo].[PoTaskOrder]
                ([PoTaskId], [OrderNo], [Status], [AddedBy])
            SELECT t.[PoTaskId], LTRIM(RTRIM(t.[OrderNo])), 'S', ISNULL(@UserId, N'system')
            FROM [dbo].[PoTask] t
            WHERE t.[PoTaskId] = @PoTaskId
              AND NULLIF(LTRIM(RTRIM(t.[OrderNo])), N'') IS NOT NULL
              AND NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskOrder] x
                              WHERE x.[OrderNo] = LTRIM(RTRIM(t.[OrderNo])) AND x.[IsActive] = 1);
        END;

        IF NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskOrder]
                       WHERE [PoTaskId] = @PoTaskId AND [OrderNo] = @OrderNo AND [IsActive] = 1)
        BEGIN
            INSERT INTO [dbo].[PoTaskOrder]
                ([PoTaskId], [OrderNo], [Status], [ReviewId], [AddedBy])
            VALUES
                (@PoTaskId, @OrderNo, 'S', @ReviewId, ISNULL(@UserId, N'system'));
        END;
        SET @WasAttached = 1;

        -- Re-sync BOM task assignees: ensure ONLY current Production Managers (@TargetAssignees)
        -- remain active assignees on this BOM task, and deactivate legacy non-PM assignees (like Saksham).
        IF EXISTS (SELECT 1 FROM @TargetAssignees)
        BEGIN
            UPDATE [dbo].[PoTaskAssignee]
            SET [IsActive] = 0
            WHERE [PoTaskId] = @PoTaskId
              AND [IsActive] = 1
              AND [UserId] NOT IN (SELECT [UserId] FROM @TargetAssignees);

            UPDATE [dbo].[PoTaskAssignee]
            SET [IsActive] = 1,
                [Status] = 'S',
                [AssignedBy] = ISNULL(@UserId, N'system'),
                [AssignedDate] = GETDATE()
            WHERE [PoTaskId] = @PoTaskId
              AND [UserId] IN (SELECT [UserId] FROM @TargetAssignees);

            INSERT INTO [dbo].[PoTaskAssignee]
                ([PoTaskId], [UserId], [Status], [AssignedBy], [IsActive])
            SELECT @PoTaskId, target.[UserId], 'S', ISNULL(@UserId, N'system'), 1
            FROM @TargetAssignees target
            WHERE NOT EXISTS
                  (SELECT 1 FROM [dbo].[PoTaskAssignee] existing
                   WHERE existing.[PoTaskId] = @PoTaskId
                     AND existing.[UserId] = target.[UserId]);
        END;

        -- Notify ONLY the active Production Manager assignees (Bishnu)
        IF @WasCreated = 0
           AND OBJECT_ID(N'[dbo].[PoTaskNotification]', N'U') IS NOT NULL
        BEGIN
            INSERT INTO [dbo].[PoTaskNotification]
                ([UserId], [PoTaskId], [Kind], [Title], [Body])
            SELECT a.[UserId], @PoTaskId, 'A', N'Order added to BOM task',
                   N'Order ' + @OrderNo + N' was added. Recalculate the combined BOM.'
            FROM [dbo].[PoTaskAssignee] a
            WHERE a.[PoTaskId] = @PoTaskId AND a.[IsActive] = 1;
        END;
    END;

    COMMIT TRANSACTION;

    SELECT @PoTaskId AS [PoTaskId],
           @WasCreated AS [WasCreated],
           @WasAttached AS [WasAttached],
           (SELECT COUNT(*) FROM [dbo].[PoTaskOrder]
            WHERE [PoTaskId] = @PoTaskId AND [IsActive] = 1) AS [OrderCount],
           CASE WHEN @WasCreated = 1 THEN N'BOM task created.'
                WHEN @WasAttached = 1 THEN N'Order attached to existing BOM task.'
                ELSE N'Order already belongs to a BOM task.' END AS [Message];
END;
