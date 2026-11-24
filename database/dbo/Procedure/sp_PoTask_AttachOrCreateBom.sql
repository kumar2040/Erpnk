-- Scripted from live DB [NatureKnit] on 2026-11-22, plus @ResultPoTaskId / @SuppressResult (deployed to NatureKnit + NatureKnit_test).
-- NOTE: NatureKnit_test runs an older variant of this proc; only the two new parameters were added there.
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
    @MaxOrders        int            = 5,
    -- For callers running this inside their own transaction (sp_PoTask_CreateFromOrderReview):
    -- the BOM task id comes back here; @SuppressResult = 1 skips the final result set.
    @ResultPoTaskId   int            = NULL OUTPUT,
    @SuppressResult   bit            = 0
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

    DECLARE @TargetAssignees TABLE ([UserId] nvarchar(450) NOT NULL PRIMARY KEY);
    INSERT INTO @TargetAssignees ([UserId])
    SELECT DISTINCT CONVERT(nvarchar(450), LTRIM(RTRIM([value])))
    FROM STRING_SPLIT(ISNULL(@AssigneeUserIds, N''), N'|')
    WHERE LEN(LTRIM(RTRIM([value]))) BETWEEN 1 AND 450;

    BEGIN TRANSACTION;

    -- Check if this order is already in an active BOM task
    SELECT @PoTaskId = [PoTaskId]
    FROM [dbo].[PoTaskOrder] WITH (UPDLOCK, HOLDLOCK)
    WHERE [OrderNo] = @OrderNo AND [IsActive] = 1;

    -- If not, find the active UNCALCULATED BOM Task (Status 'S', Stage 2) with capacity (< @MaxOrders)
    IF @PoTaskId IS NULL
    BEGIN
        SELECT TOP (1) @PoTaskId = t.[PoTaskId]
        FROM [dbo].[PoTask] t WITH (UPDLOCK, HOLDLOCK)
        WHERE t.[Stage] = 2 AND t.[Status] = 'S' AND t.[IsActive] = 1
          AND (
                SELECT COUNT(*) FROM [dbo].[PoTaskOrder] o WITH (UPDLOCK, HOLDLOCK)
                WHERE o.[PoTaskId] = t.[PoTaskId] AND o.[IsActive] = 1
              ) < @MaxOrders
        ORDER BY t.[PoTaskId] ASC;
    END;

    -- If uncalculated BOM task found -> JOIN it
    IF @PoTaskId IS NOT NULL
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskOrder] WHERE [PoTaskId] = @PoTaskId AND [OrderNo] = @OrderNo)
        BEGIN
            INSERT INTO [dbo].[PoTaskOrder]
                ([PoTaskId], [OrderNo], [Status], [ReviewId], [AddedBy])
            VALUES
                (@PoTaskId, @OrderNo, 'S', @ReviewId, ISNULL(@UserId, N'system'));

            DECLARE @CombinedOrders nvarchar(500);
            SELECT @CombinedOrders = STRING_AGG([OrderNo], ', ') 
            FROM [dbo].[PoTaskOrder]
            WHERE [PoTaskId] = @PoTaskId AND [IsActive] = 1;

            UPDATE [dbo].[PoTask]
            SET [Title] = N'BOM — ' + @CombinedOrders,
                [Detail] = ISNULL(@Detail, [Detail])
            WHERE [PoTaskId] = @PoTaskId;

            SET @WasAttached = 1;
        END;
    END
    ELSE
    -- If NO uncalculated BOM exists -> CREATE new
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

    -- Update assignees
    IF @PoTaskId IS NOT NULL AND EXISTS (SELECT 1 FROM @TargetAssignees)
    BEGIN
        UPDATE [dbo].[PoTaskAssignee]
        SET [IsActive] = 0
        WHERE [PoTaskId] = @PoTaskId AND [IsActive] = 1
          AND [UserId] NOT IN (SELECT [UserId] FROM @TargetAssignees);

        UPDATE [dbo].[PoTaskAssignee]
        SET [IsActive] = 1, [Status] = 'S', [AssignedBy] = ISNULL(@UserId, N'system'), [AssignedDate] = GETDATE()
        WHERE [PoTaskId] = @PoTaskId AND [UserId] IN (SELECT [UserId] FROM @TargetAssignees);

        INSERT INTO [dbo].[PoTaskAssignee] ([PoTaskId], [UserId], [Status], [AssignedBy])
        SELECT @PoTaskId, t.[UserId], 'S', ISNULL(@UserId, N'system')
        FROM @TargetAssignees t
        WHERE NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskAssignee] a WHERE a.[PoTaskId] = @PoTaskId AND a.[UserId] = t.[UserId]);
    END;

    COMMIT TRANSACTION;

    SET @ResultPoTaskId = @PoTaskId;
    IF ISNULL(@SuppressResult, 0) = 1
        RETURN;

    SELECT @PoTaskId AS PoTaskId, @WasCreated AS WasCreated, @WasAttached AS WasAttached;
END;
GO
