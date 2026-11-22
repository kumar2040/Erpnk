/* =====================================================================
   sp_PoTask_CreateFromOrderReview — called when PLM (PHP) saves an order
   review, through the inbound third-party API. In ONE transaction:
     1. keeps the local copy of the review (dbo.tbl_order_review, same id as MySQL)
     2. Stage 1 "create production plan" task  (sp_ManagePoTask CREATE)
     3. Stage 2 "BOM" task                     (sp_PoTask_AttachOrCreateBom)
     4. in-app notifications for the assignees (dbo.PoTaskNotification)
   Any failure rolls back all of it (XACT_ABORT). Idempotent: re-sending the
   same review returns the existing tasks instead of duplicating them.
   Same task shape as the old background sweep (PoTaskReminderService).
   ===================================================================== */
CREATE OR ALTER PROCEDURE [dbo].[sp_PoTask_CreateFromOrderReview]
    @ReviewId           int,
    @OrderNo            nvarchar(50),
    @Remark             nvarchar(max)  = NULL,
    @ReviewDate         nvarchar(30)   = NULL,   -- 'yyyy-MM-dd HH:mm:ss' from PLM; NULL/bad = now
    @ReviewUser         int            = NULL,   -- PLM tbl_users.userId of the reviewer
    @Pc                 int            = NULL,
    @AssigneeUserIds    nvarchar(max)  = NULL,   -- pipe-delimited Production Manager user ids
    @PlanDueDays        int            = 3,
    @BomNotifyAfterDays int            = 2,
    @BomDetail          nvarchar(max)  = NULL,   -- BOM summary line (optional)
    @UserId             nvarchar(450)  = N'system'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    SET @OrderNo = NULLIF(LTRIM(RTRIM(@OrderNo)), N'');
    IF @OrderNo IS NULL
    BEGIN
        RAISERROR('Order number is required.', 16, 1);
        RETURN;
    END;
    IF ISNULL(@ReviewId, 0) <= 0
    BEGIN
        RAISERROR('Review id is required.', 16, 1);
        RETURN;
    END;

    DECLARE @started    datetime     = GETDATE();   -- anything created at/after this was made by this call
    DECLARE @reviewDt   datetime2(0) = ISNULL(TRY_CONVERT(datetime2(0), @ReviewDate, 120), SYSDATETIME());
    DECLARE @today      datetime     = CAST(CAST(GETDATE() AS date) AS datetime);
    DECLARE @planTitle  nvarchar(200) = @OrderNo + N' reviewed — create production plan';
    DECLARE @planDetail nvarchar(max) = N'Order reviewed on ' + CONVERT(nvarchar(10), @reviewDt, 23) + N'.'
                                      + CASE WHEN NULLIF(LTRIM(RTRIM(@Remark)), N'') IS NULL THEN N''
                                             ELSE N' Remark: ' + LTRIM(RTRIM(@Remark)) END;
    DECLARE @planDue    datetime = DATEADD(DAY, ISNULL(@PlanDueDays, 3), @today);
    DECLARE @bomNotify  datetime = DATEADD(DAY, ISNULL(@BomNotifyAfterDays, 2), GETDATE());
    SET @AssigneeUserIds = NULLIF(LTRIM(RTRIM(@AssigneeUserIds)), N'');

    DECLARE @plan TABLE ([PoTaskId] int);
    DECLARE @PlanPoTaskId int = NULL,
            @BomPoTaskId  int = NULL,
            @ReviewInserted bit = 0;

    BEGIN TRANSACTION;

    -- 1) Local copy of the review (same id as PLM's MySQL row).
    IF NOT EXISTS (SELECT 1 FROM [dbo].[tbl_order_review] WITH (UPDLOCK, HOLDLOCK) WHERE [id] = @ReviewId)
    BEGIN
        SET IDENTITY_INSERT [dbo].[tbl_order_review] ON;
        INSERT INTO [dbo].[tbl_order_review] ([id], [order_no], [remark], [date_], [user_], [meeting_dash], [pc])
        VALUES (@ReviewId, @OrderNo, ISNULL(@Remark, N''), @reviewDt, ISNULL(@ReviewUser, 0), 0, ISNULL(@Pc, 0));
        SET IDENTITY_INSERT [dbo].[tbl_order_review] OFF;
        SET @ReviewInserted = 1;
    END;

    -- 2) Planning task (Stage 1). CREATE returns the open one if it already exists.
    INSERT INTO @plan ([PoTaskId])
    EXEC [dbo].[sp_ManagePoTask]
         @Flag = N'CREATE', @OrderNo = @OrderNo, @Stage = 1,
         @Title = @planTitle, @Detail = @planDetail,
         @StartDate = @today, @DueDate = @planDue,
         @CompletionRule = 2,                       -- Any: one PM planning it completes it
         @AssigneeUserIds = @AssigneeUserIds,
         @UserId = @UserId;
    SELECT TOP (1) @PlanPoTaskId = [PoTaskId] FROM @plan;

    -- 3) BOM task (Stage 2), linked to this review. Attaches to the existing one if any.
    --    Remember whether the order was already on a BOM task, so a repeat call notifies nobody twice.
    DECLARE @orderAlreadyOnBom bit = CASE WHEN EXISTS (SELECT 1 FROM [dbo].[PoTaskOrder]
                                                       WHERE [OrderNo] = @OrderNo AND [IsActive] = 1)
                                          THEN 1 ELSE 0 END;
    EXEC [dbo].[sp_PoTask_AttachOrCreateBom]
         @OrderNo = @OrderNo,
         @ReviewId = @ReviewId,
         @Detail = @BomDetail,
         @NotificationDate = @bomNotify,
         @DueDate = @bomNotify,
         @AssigneeUserIds = @AssigneeUserIds,
         @UserId = @UserId,
         @ResultPoTaskId = @BomPoTaskId OUTPUT,
         @SuppressResult = 1;

    IF @PlanPoTaskId IS NULL OR @BomPoTaskId IS NULL
        THROW 50001, N'Planning / BOM task was not created for the reviewed order.', 1;

    -- 4) In-app notifications (dbo.PoTaskNotification), same transaction as the tasks.
    --    Creating a task with assignees already writes a generic "New task assigned" row; this adds
    --    what that misses and never duplicates a row:
    --      - Planning task: one notification per active assignee if they have none for it yet.
    --      - BOM task made by this call: same.
    --      - BOM task that already existed (the order was added to it): "Order added to BOM task".
    --    IsPushed stays 0; the API pushes pending rows over SignalR right after the commit.
    DECLARE @bomIsNew bit = CASE WHEN EXISTS (SELECT 1 FROM [dbo].[PoTask]
                                              WHERE [PoTaskId] = @BomPoTaskId AND [CreatedDate] >= @started)
                                 THEN 1 ELSE 0 END;

    INSERT INTO [dbo].[PoTaskNotification] ([UserId], [PoTaskId], [Kind], [Title], [Body])
    SELECT a.[UserId], @PlanPoTaskId, 'A', N'Order reviewed - create plan',
           LEFT(N'Order ' + @OrderNo + N' was reviewed. Create the production plan by '
                + CONVERT(nvarchar(10), @planDue, 23) + N'.', 400)
    FROM [dbo].[PoTaskAssignee] a
    WHERE a.[PoTaskId] = @PlanPoTaskId AND a.[IsActive] = 1
      AND NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskNotification] n
                      WHERE n.[PoTaskId] = @PlanPoTaskId AND n.[UserId] = a.[UserId]);

    IF @bomIsNew = 1
        INSERT INTO [dbo].[PoTaskNotification] ([UserId], [PoTaskId], [Kind], [Title], [Body])
        SELECT a.[UserId], @BomPoTaskId, 'A', N'Order reviewed - BOM needed',
               LEFT(N'BOM task for order ' + @OrderNo + N' is ready for calculation.', 400)
        FROM [dbo].[PoTaskAssignee] a
        WHERE a.[PoTaskId] = @BomPoTaskId AND a.[IsActive] = 1
          AND NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskNotification] n
                          WHERE n.[PoTaskId] = @BomPoTaskId AND n.[UserId] = a.[UserId]);
    ELSE IF @orderAlreadyOnBom = 0
        INSERT INTO [dbo].[PoTaskNotification] ([UserId], [PoTaskId], [Kind], [Title], [Body])
        SELECT a.[UserId], @BomPoTaskId, 'A', N'Order added to BOM task',
               LEFT(N'Order ' + @OrderNo + N' was added. Recalculate the combined BOM.', 400)
        FROM [dbo].[PoTaskAssignee] a
        WHERE a.[PoTaskId] = @BomPoTaskId AND a.[IsActive] = 1
          -- NatureKnit_test's BOM proc already writes this exact row on attach; don't double it.
          AND NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskNotification] n
                          WHERE n.[PoTaskId] = @BomPoTaskId AND n.[UserId] = a.[UserId]
                            AND n.[Title] = N'Order added to BOM task'
                            AND n.[Body] = LEFT(N'Order ' + @OrderNo + N' was added. Recalculate the combined BOM.', 400));

    COMMIT TRANSACTION;

    SELECT @PlanPoTaskId   AS [PlanPoTaskId],
           @BomPoTaskId    AS [BomPoTaskId],
           @ReviewInserted AS [ReviewInserted];
END;
GO
