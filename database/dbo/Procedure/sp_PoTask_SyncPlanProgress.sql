-- Object: dbo.sp_PoTask_SyncPlanProgress (SQL_STORED_PROCEDURE)
/* =====================================================================
   Moves the per-gauge Planning tasks (PoTask Stage 3, RefId = the plan row's
   MasterPlanDetail.MasterPlanChildId) from the plan's own state in MSSQL:

     In Progress (P) : the plan row has a machine (MasterPlanDetail.Machine set)
     Completed   (C) : every size line of the plan row has a knitter
                       (a KnitterAssignment row per MasterPlanDetailSize)

   Forward only (S -> P -> C); C/H/X are never touched. The task's assignee rows
   move with it so the card's x/y count matches. Run by the background sweep
   (PoTaskReminderService) and right after a plan / knitter assignment is saved.

   (Before 2026-11-22 this used knitting records pulled over the MySQL linked
   server; that pull is gone, so the plan's own machine + knitter state is used.)
   ===================================================================== */
CREATE OR ALTER PROCEDURE [dbo].[sp_PoTask_SyncPlanProgress]
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @changes TABLE ([PoTaskId] INT, [FromStatus] CHAR(1), [ToStatus] CHAR(1));

    BEGIN TRANSACTION;

    ;WITH flags AS
    (
        SELECT
            t.[PoTaskId],
            t.[Status] AS [CurStatus],
            -- machine assigned on the plan row
            [HasMachine] = CASE WHEN EXISTS (
                SELECT 1 FROM [dbo].[MasterPlanDetail] mpd
                WHERE mpd.[MasterPlanChildId] = t.[RefId]
                  AND LTRIM(RTRIM(ISNULL(mpd.[Machine], N''))) NOT IN (N'', N'0')) THEN 1 ELSE 0 END,
            -- every size line has a knitter (and there is at least one line)
            [AllKnitters] = CASE WHEN EXISTS (
                    SELECT 1 FROM [dbo].[MasterPlanDetailSize] s WHERE s.[MasterPlanDetailId] = t.[RefId])
                AND NOT EXISTS (
                    SELECT 1 FROM [dbo].[MasterPlanDetailSize] s
                    WHERE s.[MasterPlanDetailId] = t.[RefId]
                      AND NOT EXISTS (SELECT 1 FROM [dbo].[KnitterAssignment] ka
                                      WHERE ka.[MasterPlanDetailSizeId] = s.[id]))
                THEN 1 ELSE 0 END
        FROM [dbo].[PoTask] t WITH (UPDLOCK)
        WHERE t.[Stage] = 3
          AND t.[RefId] IS NOT NULL
          AND t.[IsActive] = 1
          AND t.[Status] IN ('S','P')      -- forward-only; leaves C/H/X alone
    )
    UPDATE t
    SET [Status]        = nf.[NewStatus],
        [CompletedDate] = CASE WHEN nf.[NewStatus] = 'C' THEN GETDATE() ELSE t.[CompletedDate] END,
        [ModifiedBy]    = 'system',
        [ModifiedDate]  = GETDATE()
    OUTPUT inserted.[PoTaskId], deleted.[Status], inserted.[Status] INTO @changes
    FROM [dbo].[PoTask] t
    INNER JOIN flags f ON f.[PoTaskId] = t.[PoTaskId]
    CROSS APPLY (SELECT [NewStatus] =
        CASE WHEN f.[AllKnitters] = 1 THEN 'C'
             WHEN f.[HasMachine]  = 1 THEN 'P'
             ELSE f.[CurStatus] END) nf
    WHERE nf.[NewStatus] <> f.[CurStatus]
      AND NOT (f.[CurStatus] = 'P' AND nf.[NewStatus] = 'S');

    -- Assignee rows follow the task, so the card's x/y count matches (never moved backwards).
    UPDATE a
    SET [Status]        = c.[ToStatus],
        [StartDate]     = ISNULL(a.[StartDate], GETDATE()),
        [CompletedDate] = CASE WHEN c.[ToStatus] = 'C' THEN GETDATE() ELSE a.[CompletedDate] END
    FROM [dbo].[PoTaskAssignee] a
    INNER JOIN @changes c ON c.[PoTaskId] = a.[PoTaskId]
    WHERE a.[IsActive] = 1
      AND a.[Status] IN ('S','P')
      AND a.[Status] <> c.[ToStatus];

    -- History for exactly the rows that changed.
    INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[FromStatus],[ToStatus],[Note],[ChangedBy])
    SELECT [PoTaskId], [FromStatus], [ToStatus],
           CASE [ToStatus] WHEN 'C' THEN 'auto: knitter assigned to every line'
                           ELSE 'auto: machine assigned' END,
           'system'
    FROM @changes;

    COMMIT TRANSACTION;

    SELECT COUNT(*) AS [Changed] FROM @changes;
END
GO
