/* =====================================================================
   sp_PoTask_MakeOrder — called when PLM (PHP) submits a new order on
   "Make order" (OrderDraft::submit), through the inbound third-party API.
   One @Flag per page / feature:
     MAKE_ORDER : in ONE transaction
        1. a task for the new order on the /tasks board (dbo.PoTask, manual Stage 20)
           assigned to the Production Manager(s) in @AssigneeUserIds
        2. one in-app notification per active assignee (dbo.PoTaskNotification)
   Any failure rolls back all of it (XACT_ABORT). Idempotent: re-sending the same
   order returns the existing open task and writes no second notification.
   The task detail is a summary built here from the order header values PLM sent.
   ===================================================================== */
CREATE OR ALTER PROCEDURE [dbo].[sp_PoTask_MakeOrder]
    @Flag            nvarchar(20),                 -- 'MAKE_ORDER'
    @OrderNo         nvarchar(50),
    @BuyerName       nvarchar(200)  = NULL,
    @Collection      nvarchar(200)  = NULL,
    @OrderType       nvarchar(10)   = NULL,        -- 'p' production / 's' sample (PLM prodtype)
    @ShippingDate    nvarchar(30)   = NULL,        -- 'yyyy-MM-dd' from PLM; bad/NULL = none
    @TotalPieces     int            = NULL,
    @StyleCount      int            = NULL,
    @AssigneeUserIds nvarchar(max)  = NULL,        -- pipe-delimited Production Manager user ids
    @DueDays         int            = 3,
    @UserId          nvarchar(450)  = N'system'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @Flag    = UPPER(LTRIM(RTRIM(ISNULL(@Flag, N''))));
    SET @OrderNo = NULLIF(LTRIM(RTRIM(@OrderNo)), N'');

    IF @Flag <> N'MAKE_ORDER'
    BEGIN
        RAISERROR('Unknown flag for sp_PoTask_MakeOrder.', 16, 1);
        RETURN;
    END;
    IF @OrderNo IS NULL
    BEGIN
        RAISERROR('Order number is required.', 16, 1);
        RETURN;
    END;

    DECLARE @today    datetime = CAST(CAST(GETDATE() AS date) AS datetime);
    DECLARE @due      datetime = DATEADD(DAY, ISNULL(@DueDays, 3), @today);
    DECLARE @ship     date     = TRY_CONVERT(date, @ShippingDate, 23);
    DECLARE @typeText nvarchar(20) = CASE LOWER(LTRIM(RTRIM(ISNULL(@OrderType, N''))))
                                          WHEN N'p' THEN N'Production'
                                          WHEN N's' THEN N'Sample'
                                          ELSE NULL END;
    DECLARE @title    nvarchar(200) = @OrderNo + N' placed - new order';
    DECLARE @detail   nvarchar(max) =
          N'New order ' + @OrderNo + N' was placed in PLM.'
        + CASE WHEN NULLIF(LTRIM(RTRIM(@BuyerName)), N'')  IS NULL THEN N'' ELSE N' Buyer: ' + LTRIM(RTRIM(@BuyerName)) + N'.' END
        + CASE WHEN NULLIF(LTRIM(RTRIM(@Collection)), N'') IS NULL THEN N'' ELSE N' Collection: ' + LTRIM(RTRIM(@Collection)) + N'.' END
        + CASE WHEN @typeText IS NULL THEN N'' ELSE N' Type: ' + @typeText + N'.' END
        + CASE WHEN @StyleCount  IS NULL THEN N'' ELSE N' Styles: ' + CAST(@StyleCount AS nvarchar(12)) + N'.' END
        + CASE WHEN @TotalPieces IS NULL THEN N'' ELSE N' Pieces: ' + CAST(@TotalPieces AS nvarchar(12)) + N'.' END
        + CASE WHEN @ship IS NULL THEN N'' ELSE N' Shipping: ' + CONVERT(nvarchar(10), @ship, 23) + N'.' END;
    SET @AssigneeUserIds = NULLIF(LTRIM(RTRIM(@AssigneeUserIds)), N'');

    DECLARE @made TABLE ([PoTaskId] int);
    DECLARE @PoTaskId int = NULL, @WasCreated bit = 0;

    BEGIN TRANSACTION;

    -- 1) The task. Manual tasks are never deduped by sp_ManagePoTask, so do it here per (order, title).
    SELECT TOP (1) @PoTaskId = [PoTaskId]
    FROM [dbo].[PoTask] WITH (UPDLOCK, HOLDLOCK)
    WHERE [OrderNo] = @OrderNo AND [Title] = @title AND [IsActive] = 1 AND [Status] NOT IN ('C','X')
    ORDER BY [PoTaskId] DESC;

    IF @PoTaskId IS NULL
    BEGIN
        INSERT INTO @made ([PoTaskId])
        EXEC [dbo].[sp_ManagePoTask]
             @Flag = N'CREATE', @OrderNo = @OrderNo, @Stage = NULL,        -- NULL => Manual (20)
             @Title = @title, @Detail = @detail,
             @StartDate = @today, @DueDate = @due,
             @CompletionRule = 2,                                          -- Any: one PM acknowledging completes it
             @AssigneeUserIds = @AssigneeUserIds,
             @UserId = @UserId;
        SELECT TOP (1) @PoTaskId = [PoTaskId] FROM @made;
        SET @WasCreated = 1;
    END;

    IF @PoTaskId IS NULL
        THROW 50001, N'Task was not created for the new order.', 1;

    -- 2) Notifications. Creating a task with assignees already wrote a generic "New task assigned" row per
    --    assignee; make that row say what happened instead of adding a second one, then add a row for any
    --    assignee that still has none (a repeat call adds nothing).
    --    IsPushed stays 0; the API pushes pending rows over SignalR right after the commit.
    DECLARE @nTitle nvarchar(200) = N'New order placed';
    DECLARE @nBody  nvarchar(400) =
           LEFT(N'Order ' + @OrderNo + N' was placed'
                + CASE WHEN @typeText IS NULL THEN N'' ELSE N' (' + @typeText + N')' END
                + CASE WHEN NULLIF(LTRIM(RTRIM(@BuyerName)), N'') IS NULL THEN N'' ELSE N' for ' + LTRIM(RTRIM(@BuyerName)) END
                + CASE WHEN @TotalPieces IS NULL THEN N'' ELSE N', ' + CAST(@TotalPieces AS nvarchar(12)) + N' pcs' END
                + CASE WHEN @ship IS NULL THEN N'' ELSE N', ship ' + CONVERT(nvarchar(10), @ship, 23) END
                + N'.', 400);

    IF @WasCreated = 1
        UPDATE [dbo].[PoTaskNotification]
        SET [Title] = @nTitle, [Body] = @nBody
        WHERE [PoTaskId] = @PoTaskId AND [Title] = N'New task assigned';

    INSERT INTO [dbo].[PoTaskNotification] ([UserId], [PoTaskId], [Kind], [Title], [Body])
    SELECT a.[UserId], @PoTaskId, 'A', @nTitle, @nBody
    FROM [dbo].[PoTaskAssignee] a
    WHERE a.[PoTaskId] = @PoTaskId AND a.[IsActive] = 1
      AND NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskNotification] n
                      WHERE n.[PoTaskId] = @PoTaskId AND n.[UserId] = a.[UserId]);

    COMMIT TRANSACTION;

    SELECT @PoTaskId AS [PoTaskId], @WasCreated AS [WasCreated];
END;
GO
