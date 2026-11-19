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
    -- Check if a BOM task already exists for this specific order
    SELECT @PoTaskId = [PoTaskId]
    FROM [dbo].[PoTaskOrder] WITH (UPDLOCK, HOLDLOCK)
    WHERE [OrderNo] = @OrderNo AND [IsActive] = 1;

    IF @PoTaskId IS NULL
    BEGIN
        SELECT @PoTaskId = [PoTaskId]
        FROM [dbo].[PoTask] WITH (UPDLOCK, HOLDLOCK)
        WHERE [OrderNo] = @OrderNo AND [Stage] = 2 AND [IsActive] = 1;
    END;

    IF @PoTaskId IS NULL
    BEGIN
        DECLARE @created TABLE ([PoTaskId] int);
        DECLARE @TaskTitle nvarchar(200) = N'BOM — ' + @OrderNo;
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

        IF @PoTaskId IS NOT NULL
        BEGIN
            INSERT INTO [dbo].[PoTaskOrder]
                ([PoTaskId], [OrderNo], [Status], [ReviewId], [AddedBy])
            VALUES
                (@PoTaskId, @OrderNo, 'S', @ReviewId, ISNULL(@UserId, N'system'));
        END;
    END;

    -- Re-sync BOM task assignees: ensure ONLY current Production Managers (@TargetAssignees)
    -- remain active assignees on this BOM task, and deactivate legacy non-PM assignees.
    IF @PoTaskId IS NOT NULL AND EXISTS (SELECT 1 FROM @TargetAssignees)
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

    -- Notify the active Production Manager assignees
    IF @WasCreated = 1
       AND OBJECT_ID(N'[dbo].[PoTaskNotification]', N'U') IS NOT NULL
    BEGIN
        INSERT INTO [dbo].[PoTaskNotification]
            ([UserId], [PoTaskId], [Kind], [Title], [Body])
        SELECT a.[UserId], @PoTaskId, 'A', N'BOM Task Created',
               N'BOM task for order ' + @OrderNo + N' is ready for calculation.'
        FROM [dbo].[PoTaskAssignee] a
        WHERE a.[PoTaskId] = @PoTaskId AND a.[IsActive] = 1;
    END;

    COMMIT TRANSACTION;

    SELECT @PoTaskId AS [PoTaskId],
           @WasCreated AS [WasCreated],
           @WasAttached AS [WasAttached],
           1 AS [OrderCount],
           CASE WHEN @WasCreated = 1 THEN N'BOM task created.'
                ELSE N'Order already belongs to a BOM task.' END AS [Message];
END;
GO
