-- Scripted from live DB [NatureKnit] on 2026-07-28 (read-only). Source of truth = database.
-- Object: dbo.sp_PoTask_CompleteStage  (SQL_STORED_PROCEDURE)
--
-- Auto-completes every open task of (OrderNo, Stage) when the real-world event that
-- the task was chasing happens. One caller today: ProductionPlanningController:278,
-- via IPoTaskService.CompleteStageAsync -- saving a plan closes the order's Stage-1
-- "Create plan" task.
--
-- KNOWN ISSUES, checked in as-is so the repo matches the database. Both are real on
-- any task that has assignee rows:
--
--   1) It writes PoTask.Status only. sp_PoTask_Recompute DERIVES the parent status
--      from PoTaskAssignee and runs on every MYUPDATE, returning early only for 'H'
--      and 'X' -- not 'C'. So the next time any assignee taps their own status chip,
--      the rollup sees @done = 0 and drags the parent back to 'P'/'S'. The auto-
--      completion is silently lost, and until then the card reads Completed at 0/N.
--      sp_ManageYarnOrder flag 'I' deliberately does NOT call this proc for that
--      reason -- it completes the assignee rows and lets Recompute settle the parent.
--
--   2) It excludes only 'C' and 'X', so it force-completes tasks somebody parked on
--      HOLD ('H'). Recompute's own rule is that H and X are never auto-changed.
--
-- Fixing this changes Stage-1 plan-save behaviour, not just yarn, so it is left alone
-- pending a decision.
CREATE PROCEDURE [dbo].[sp_PoTask_CompleteStage]
    @OrderNo NVARCHAR(50),
    @Stage   TINYINT,
    @Note    NVARCHAR(400) = NULL,
    @UserId  NVARCHAR(450) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @done TABLE ([PoTaskId] INT, [FromStatus] CHAR(1));

    UPDATE [dbo].[PoTask]
    SET [Status]        = 'C',
        [CompletedDate] = GETDATE(),
        [ModifiedBy]    = ISNULL(@UserId, 'system'),
        [ModifiedDate]  = GETDATE()
    OUTPUT inserted.[PoTaskId], deleted.[Status] INTO @done
    WHERE [OrderNo] = @OrderNo AND [Stage] = @Stage
      AND [IsActive] = 1 AND [Status] NOT IN ('C','X');

    INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[FromStatus],[ToStatus],[Note],[ChangedBy])
    SELECT [PoTaskId], [FromStatus], 'C', ISNULL(@Note, 'auto-completed'), ISNULL(@UserId, 'system')
    FROM @done;

    SELECT COUNT(*) AS [Completed] FROM @done;
END
