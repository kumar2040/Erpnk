-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.sp_ManagePoTask  (SQL_STORED_PROCEDURE)
CREATE PROCEDURE [dbo].[sp_ManagePoTask]
    @Flag             NVARCHAR(20),            -- see the dispatch below
    @PoTaskId         INT            = NULL,
    @OrderNo          NVARCHAR(50)   = NULL,
    @Stage            TINYINT        = NULL,
    @ToStatus         CHAR(1)        = NULL,
    @FactoryType      NVARCHAR(100)  = NULL,
    @Guage            NVARCHAR(100)  = NULL,
    @Title            NVARCHAR(200)  = NULL,
    @Detail           NVARCHAR(MAX)  = NULL,
    @RefId            INT            = NULL,
    @PriorityId       TINYINT        = NULL,
    @NotificationDate DATETIME       = NULL,
    @UpdateFrequency  TINYINT        = NULL,
    @PlanningAction   TINYINT        = NULL,
    @CompletionRule   TINYINT        = NULL,   -- 1=All 2=Any 3=Quorum (default 1)
    @QuorumCount      INT            = NULL,
    @StartDate        DATETIME       = NULL,
    @DueDate          DATETIME       = NULL,
    @AssigneeUserIds  NVARCHAR(MAX)  = NULL,   -- pipe-delimited user ids for ASSIGN (a|b|c)
    @GroupId          INT            = NULL,   -- ASSIGN: expand this group's members too
    @Note             NVARCHAR(400)  = NULL,
    @BlockedReason    NVARCHAR(400)  = NULL,
    @ChecklistId      INT            = NULL,
    @FileName         NVARCHAR(260)  = NULL,
    @ContentType      NVARCHAR(120)  = NULL,
    @SizeBytes        INT            = NULL,
    @Content          VARBINARY(MAX) = NULL,
    @ParamJson        NVARCHAR(MAX)  = NULL,   -- SNAPSHOT / ALERTCHECK: canonical production params
    @ReviewId         INT            = NULL,
    @NotifyAfterDays  INT            = NULL,
    @MaxOrders        INT            = NULL,
    @SourcePoTaskId   INT            = NULL,
    @TargetPoTaskId   INT            = NULL,
    @UserId           NVARCHAR(450)  = NULL    -- the ACTING user (whose own row MYUPDATE touches)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @op NVARCHAR(20) = UPPER(LTRIM(RTRIM(@Flag)));

    /* ---------------------------------------------------------------- CREATE
       Insert a task. If assignment params are supplied, fan them out in the
       same transaction (re-entrant call to ASSIGN). Returns the new PoTaskId. */
    IF (@op = 'CREATE')
    BEGIN
        -- Idempotent for the linear stages (PoEntry/BomTask/Planning): don't open a
        -- SECOND task for the same (OrderNo, Stage) while one is still open. This lets
        -- the plan/BOM auto-hooks fire freely without spawning duplicates. Manual
        -- (Stage 20) and exceptions are never deduped.
        -- When @RefId is given (e.g. Planning per gauge line = MasterPlanChildId), dedupe
        -- per (OrderNo, Stage, RefId) so 5G and 12G are SEPARATE tasks but re-saving the
        -- same line doesn't duplicate. With no @RefId (PoEntry/BOM) dedupe per (OrderNo, Stage).
        IF (@Stage IN (1,2,3) AND @OrderNo IS NOT NULL)
        BEGIN
            DECLARE @existingId INT;
            SELECT TOP (1) @existingId = [PoTaskId]
            FROM [dbo].[PoTask]
            WHERE [OrderNo] = @OrderNo AND [Stage] = @Stage
              AND [IsActive] = 1 AND [Status] NOT IN ('C','X')
              AND (@RefId IS NULL OR [RefId] = @RefId)
            ORDER BY [PoTaskId] DESC;
            IF (@existingId IS NOT NULL)
            BEGIN
                SELECT @existingId AS [PoTaskId];
                RETURN;
            END
        END

        BEGIN TRAN;
        INSERT INTO [dbo].[PoTask]
            ([OrderNo],[Stage],[Status],[FactoryType],[Guage],[Title],[Detail],[RefId],
             [PriorityId],[NotificationDate],[UpdateFrequency],[PlanningAction],
             [CompletionRule],[QuorumCount],[StartDate],[DueDate],[CreatedBy])
        VALUES
            (@OrderNo, ISNULL(@Stage,20), 'S', @FactoryType, @Guage, @Title, @Detail, @RefId,
             @PriorityId, @NotificationDate, @UpdateFrequency, @PlanningAction,
             ISNULL(@CompletionRule,1), @QuorumCount, @StartDate, @DueDate, @UserId);

        DECLARE @NewId INT = CAST(SCOPE_IDENTITY() AS INT);

        INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[FromStatus],[ToStatus],[Note],[ChangedBy])
        VALUES (@NewId, NULL, 'S', 'created', @UserId);

        IF (@GroupId IS NOT NULL OR (@AssigneeUserIds IS NOT NULL AND LTRIM(RTRIM(@AssigneeUserIds)) <> ''))
            EXEC [dbo].[sp_ManagePoTask] @Flag = 'ASSIGN',
                 @PoTaskId = @NewId, @AssigneeUserIds = @AssigneeUserIds, @GroupId = @GroupId, @UserId = @UserId;
        COMMIT;

        SELECT @NewId AS [PoTaskId];
        RETURN;
    END

    /* ---------------------------------------------------------------- ASSIGN
       Fan out @AssigneeUserIds (split on '|') and/or every member of @GroupId
       into PoTaskAssignee. Skips users already actively assigned, then rolls up. */
    IF (@op = 'ASSIGN')
    BEGIN
        DECLARE @ids TABLE ([UserId] NVARCHAR(450) PRIMARY KEY);

        IF (@AssigneeUserIds IS NOT NULL AND LTRIM(RTRIM(@AssigneeUserIds)) <> '')
            INSERT INTO @ids ([UserId])
            SELECT DISTINCT LTRIM(RTRIM(s.[value]))
            FROM STRING_SPLIT(@AssigneeUserIds, '|') s
            WHERE LTRIM(RTRIM(s.[value])) <> ''
              AND LTRIM(RTRIM(s.[value])) NOT IN (SELECT [UserId] FROM @ids);

        IF (@GroupId IS NOT NULL)
            INSERT INTO @ids ([UserId])
            SELECT DISTINCT m.[UserId]
            FROM [dbo].[PoTaskGroupMember] m
            WHERE m.[GroupId] = @GroupId AND m.[IsActive] = 1
              AND m.[UserId] NOT IN (SELECT [UserId] FROM @ids);

        INSERT INTO [dbo].[PoTaskAssignee] ([PoTaskId],[UserId],[Status],[AssignedBy])
        SELECT @PoTaskId, i.[UserId], 'S', @UserId
        FROM @ids i
        WHERE NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskAssignee] a
                          WHERE a.[PoTaskId] = @PoTaskId AND a.[UserId] = i.[UserId] AND a.[IsActive] = 1);

        -- "On assign" in-app notification: one per newly-assigned user (no duplicate
        -- 'A' per task/user). Guarded so this proc still runs if the notifications
        -- script (potask_notifications.sql) hasn't been applied yet.
        IF OBJECT_ID(N'[dbo].[PoTaskNotification]', N'U') IS NOT NULL
            INSERT INTO [dbo].[PoTaskNotification] ([UserId],[PoTaskId],[Kind],[Title],[Body])
            SELECT a.[UserId], @PoTaskId, 'A', 'New task assigned',
                   ISNULL((SELECT [Title] FROM [dbo].[PoTask] WHERE [PoTaskId] = @PoTaskId), 'A task was assigned to you')
            FROM [dbo].[PoTaskAssignee] a
            WHERE a.[PoTaskId] = @PoTaskId AND a.[IsActive] = 1
              AND a.[UserId] IN (SELECT [UserId] FROM @ids)
              AND NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskNotification] n
                              WHERE n.[PoTaskId] = @PoTaskId AND n.[UserId] = a.[UserId] AND n.[Kind] = 'A');

        EXEC [dbo].[sp_PoTask_Recompute] @PoTaskId = @PoTaskId, @ChangedBy = @UserId;
        RETURN;
    END

    /* -------------------------------------------------------------- UNASSIGN
       Soft-remove one user's assignment, then roll up. */
    IF (@op = 'UNASSIGN')
    BEGIN
        UPDATE [dbo].[PoTaskAssignee]
        SET [IsActive] = 0
        WHERE [PoTaskId] = @PoTaskId AND [UserId] = @UserId AND [IsActive] = 1;

        EXEC [dbo].[sp_PoTask_Recompute] @PoTaskId = @PoTaskId, @ChangedBy = @UserId;
        RETURN;
    END

    /* -------------------------------------------------------------- MYUPDATE
       "Update my side": move ONLY the acting user's own assignee row (zero
       trust — a user can never move someone else's), then roll the parent up. */
    IF (@op = 'MYUPDATE')
    BEGIN
        DECLARE @aId INT, @aCur CHAR(1);
        SELECT @aId = [AssigneeId], @aCur = [Status]
        FROM [dbo].[PoTaskAssignee]
        WHERE [PoTaskId] = @PoTaskId AND [UserId] = @UserId AND [IsActive] = 1;

        IF (@aId IS NULL)
        BEGIN
            RAISERROR('You are not assigned to this task.', 16, 1);
            RETURN;
        END
        IF (@ToStatus NOT IN ('S','P','C','H'))
        BEGIN
            RAISERROR('Invalid status for your update.', 16, 1);
            RETURN;
        END

        UPDATE [dbo].[PoTaskAssignee]
        SET [Status]        = @ToStatus,
            [StartDate]     = CASE WHEN @ToStatus IN ('P','C') AND [StartDate] IS NULL THEN GETDATE() ELSE [StartDate] END,
            [CompletedDate] = CASE WHEN @ToStatus = 'C' THEN GETDATE() ELSE NULL END,
            [Note]          = ISNULL(@Note, [Note])
        WHERE [AssigneeId] = @aId;

        INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[AssigneeId],[FromStatus],[ToStatus],[Note],[ChangedBy])
        VALUES (@PoTaskId, @aId, @aCur, @ToStatus, @Note, @UserId);

        EXEC [dbo].[sp_PoTask_Recompute] @PoTaskId = @PoTaskId, @ChangedBy = @UserId;
        RETURN;
    END

    /* ------------------------------------------------------------- SETSTATUS
       Admin / single-owner override: set the parent status directly. */
    IF (@op = 'SETSTATUS')
    BEGIN
        DECLARE @pCur CHAR(1);
        SELECT @pCur = [Status] FROM [dbo].[PoTask] WHERE [PoTaskId] = @PoTaskId;

        UPDATE [dbo].[PoTask]
        SET [Status]        = @ToStatus,
            [CompletedDate] = CASE WHEN @ToStatus = 'C' THEN GETDATE() ELSE [CompletedDate] END,
            [ModifiedBy]    = @UserId,
            [ModifiedDate]  = GETDATE()
        WHERE [PoTaskId] = @PoTaskId;

        INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[FromStatus],[ToStatus],[Note],[ChangedBy])
        VALUES (@PoTaskId, @pCur, @ToStatus, ISNULL(@Note,'override'), @UserId);
        RETURN;
    END

    /* ------------------------------------------------------------ HOLD / RESOLVE
       HOLD parks a task (and is not auto-overwritten by rollup). RESOLVE clears
       the hold and recomputes from the assignees. */
    IF (@op = 'HOLD')
    BEGIN
        UPDATE [dbo].[PoTask]
        SET [Status] = 'H', [BlockedReason] = @BlockedReason, [ModifiedBy] = @UserId, [ModifiedDate] = GETDATE()
        WHERE [PoTaskId] = @PoTaskId;

        INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[ToStatus],[Note],[ChangedBy])
        VALUES (@PoTaskId, 'H', ISNULL(@BlockedReason,'hold'), @UserId);
        RETURN;
    END

    IF (@op = 'RESOLVE')
    BEGIN
        UPDATE [dbo].[PoTask]
        SET [Status] = 'S', [BlockedReason] = NULL, [ModifiedBy] = @UserId, [ModifiedDate] = GETDATE()
        WHERE [PoTaskId] = @PoTaskId AND [Status] = 'H';

        EXEC [dbo].[sp_PoTask_Recompute] @PoTaskId = @PoTaskId, @ChangedBy = @UserId;
        INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[ToStatus],[Note],[ChangedBy])
        VALUES (@PoTaskId, 'S', 'resolved hold', @UserId);
        RETURN;
    END

    /* ---------------------------------------------------------------- CANCEL */
    IF (@op = 'CANCEL')
    BEGIN
        UPDATE [dbo].[PoTask]
        SET [Status] = 'X', [ModifiedBy] = @UserId, [ModifiedDate] = GETDATE()
        WHERE [PoTaskId] = @PoTaskId;

        INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[ToStatus],[Note],[ChangedBy])
        VALUES (@PoTaskId, 'X', ISNULL(@Note,'cancelled'), @UserId);
        RETURN;
    END

    /* ------------------------------------------------------------- EXCEPTION
       Raise a Yarn issue (Stage 10) or Product return (Stage 11) against a PO,
       and optionally hold the PO's current open linear task (@PoTaskId). */
    IF (@op = 'EXCEPTION')
    BEGIN
        BEGIN TRAN;
        INSERT INTO [dbo].[PoTask]
            ([OrderNo],[Stage],[Status],[FactoryType],[Guage],[Title],[Detail],[StartDate],[DueDate],[CreatedBy])
        VALUES
            (@OrderNo, ISNULL(@Stage,10), 'P', @FactoryType, @Guage,
             ISNULL(@Title, CASE WHEN @Stage = 11 THEN 'Product return' ELSE 'Yarn issue' END),
             @Detail, GETDATE(), @DueDate, @UserId);

        DECLARE @excId INT = CAST(SCOPE_IDENTITY() AS INT);
        INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[ToStatus],[Note],[ChangedBy])
        VALUES (@excId, 'P', 'exception raised', @UserId);

        IF (@PoTaskId IS NOT NULL)
        BEGIN
            UPDATE [dbo].[PoTask]
            SET [Status] = 'H', [BlockedReason] = ISNULL(@Detail,'blocked by exception'), [ModifiedBy] = @UserId, [ModifiedDate] = GETDATE()
            WHERE [PoTaskId] = @PoTaskId AND [Status] NOT IN ('C','X');
            INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[ToStatus],[Note],[ChangedBy])
            VALUES (@PoTaskId, 'H', 'held by exception', @UserId);
        END
        COMMIT;

        SELECT @excId AS [PoTaskId];
        RETURN;
    END

    /* ------------------------------------------------- CHECKLIST add / toggle */
    IF (@op = 'CHECKLIST_ADD')
    BEGIN
        INSERT INTO [dbo].[PoTaskChecklist] ([PoTaskId],[Text],[SortOrder])
        SELECT @PoTaskId, @Detail, ISNULL(MAX([SortOrder]),0) + 1
        FROM [dbo].[PoTaskChecklist] WHERE [PoTaskId] = @PoTaskId;
        RETURN;
    END

    IF (@op = 'CHECKLIST_TOGGLE')
    BEGIN
        UPDATE [dbo].[PoTaskChecklist]
        SET [IsDone] = CASE WHEN [IsDone] = 1 THEN 0 ELSE 1 END
        WHERE [ChecklistId] = @ChecklistId;
        RETURN;
    END

    /* ------------------------------------------------------------------ ATTACH
       Stores an uploaded document. The CHECK constraint also guards the 1 MB cap. */
    IF (@op = 'ATTACH')
    BEGIN
        IF (@SizeBytes IS NULL OR @SizeBytes > 1048576)
        BEGIN
            RAISERROR('Attachment exceeds the 1 MB limit.', 16, 1);
            RETURN;
        END
        INSERT INTO [dbo].[PoTaskAttachment] ([PoTaskId],[FileName],[ContentType],[SizeBytes],[Content],[UploadedBy])
        VALUES (@PoTaskId, @FileName, @ContentType, @SizeBytes, @Content, @UserId);
        SELECT CAST(SCOPE_IDENTITY() AS INT) AS [AttachmentId];
        RETURN;
    END

    /* ---------------------------------------------------------------- SNAPSHOT
       Capture the production-parameter hash for a PO (call when Planning -> C). */
    IF (@op = 'SNAPSHOT')
    BEGIN
        INSERT INTO [dbo].[PoPlanSnapshot] ([OrderNo],[ParamHash],[ParamJson],[CapturedBy])
        VALUES (@OrderNo, HASHBYTES('SHA2_256', ISNULL(@ParamJson,'')), @ParamJson, @UserId);
        RETURN;
    END

    /* -------------------------------------------------------------- ALERTCHECK
       Compare the PO's current params to the latest snapshot. If they differ AND
       a completed Planning task exists, raise a "Change in PO" alert task.
       Returns Changed (bit) so the API can surface a banner immediately. */
    IF (@op = 'ALERTCHECK')
    BEGIN
        DECLARE @newHash BINARY(32) = HASHBYTES('SHA2_256', ISNULL(@ParamJson,''));
        DECLARE @oldHash BINARY(32);
        SELECT TOP (1) @oldHash = [ParamHash]
        FROM [dbo].[PoPlanSnapshot]
        WHERE [OrderNo] = @OrderNo
        ORDER BY [CapturedDate] DESC;

        DECLARE @changed BIT = 0;
        IF (@oldHash IS NOT NULL AND @oldHash <> @newHash
            AND EXISTS (SELECT 1 FROM [dbo].[PoTask]
                        WHERE [OrderNo] = @OrderNo AND [Stage] = 3 AND [Status] = 'C' AND [IsActive] = 1))
        BEGIN
            SET @changed = 1;
            INSERT INTO [dbo].[PoTask]
                ([OrderNo],[Stage],[Status],[Title],[Detail],[PlanningAction],[StartDate],[CreatedBy])
            VALUES
                (@OrderNo, 3, 'S', 'Change in PO — re-plan required',
                 'Production parameters changed after planning.', 5, GETDATE(), @UserId);

            INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[ToStatus],[Note],[ChangedBy])
            VALUES (CAST(SCOPE_IDENTITY() AS INT), 'S', 'param-change alert', @UserId);
        END

        SELECT @changed AS [Changed];
        RETURN;
    END

    /* ------------------------------------------------------------- BOMATTACH
       Public PoTask write entry point for reviewed-order BOM batching. The
       helper owns locking/membership; this dispatcher owns date conversion. */
    IF (@op = 'BOMATTACH')
    BEGIN
        DECLARE @bomNotifyDate DATETIME = DATEADD(DAY, ISNULL(@NotifyAfterDays, 2), GETDATE());
        EXEC [dbo].[sp_PoTask_AttachOrCreateBom]
             @OrderNo = @OrderNo,
             @ReviewId = @ReviewId,
             @FactoryType = @FactoryType,
             @Detail = @Detail,
             @NotificationDate = @bomNotifyDate,
             @DueDate = @bomNotifyDate,
             @AssigneeUserIds = @AssigneeUserIds,
             @GroupId = @GroupId,
             @UserId = @UserId,
             @MaxOrders = @MaxOrders;
        RETURN;
    END

    /* ----------------------------------------------------------- BOMCOMPLETE */
    IF (@op = 'BOMCOMPLETE')
    BEGIN
        EXEC [dbo].[sp_PoTask_CompleteBomOrder]
             @PoTaskId = @PoTaskId,
             @OrderNo = @OrderNo,
             @Note = @Note,
             @UserId = @UserId;
        RETURN;
    END

    /* -------------------------------------------------------------- BOMMERGE */
    IF (@op = 'BOMMERGE')
    BEGIN
        EXEC [dbo].[sp_PoTask_MergeBomTask]
             @SourcePoTaskId = @SourcePoTaskId,
             @TargetPoTaskId = @TargetPoTaskId,
             @UserId = @UserId,
             @MaxOrders = @MaxOrders;
        RETURN;
    END

    RAISERROR('sp_ManagePoTask: unknown @Flag "%s".', 16, 1, @op);
END
