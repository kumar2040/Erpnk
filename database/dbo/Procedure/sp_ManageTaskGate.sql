/*==============================================================================
  sp_ManageTaskGate  —  the login task gate (Task-Gate feature), reads + writes

  Flags
    'Q'  Queue — the caller's not-yet-started assignments, oldest first. Feeds the
                 blocking modal shown once per login.
    'S'  Start — the caller accepts a task. Moves ONLY the caller's own
                 PoTaskAssignee row from 'S' (Scheduled) to 'P' (In progress),
                 logs it to PoTaskHistory, then delegates the parent rollup to
                 sp_PoTask_Recompute.

  Skip is deliberately NOT a flag. Skipping is client-side and session-only:
  nothing is written, so the task stays Scheduled and reappears at the user's
  next login. If skip ever needs to persist it needs its own column — do not
  overload 'H' (On hold), which means something else to the board.

  ---------------------------------------------------------------------------
  'Q' — why not just call sp_GetPoTask with @Flag='MYTASKS', @StatusFlag='S'?

  Two reasons, both of which would be silent bugs:

  1. That branch buckets on the DERIVED [DisplayFlag], which reclassifies a
     Scheduled task as 'O' (Overdue) once its DueDate has passed. An overdue,
     never-started task would be MISSING from the gate — exactly the task the
     user most needs to see. Filtering on the STORED a.[Status] = 'S' includes it.
  2. It orders by [DueDate], [TaskId]. The gate is specified as FIFO, so this
     orders strictly by [PoTaskId] ASC (an IDENTITY, so it is creation order).

  No factory/gauge scoping. sp_GetPoTask narrows by identity.Users.AssignedGauge
  because it browses the whole org's board. This only ever returns rows the
  caller is explicitly assigned to, so the assignment IS the scope; a gauge
  filter would hide tasks deliberately given to this person.
  ---------------------------------------------------------------------------

  'Q' returns zero or more task rows; zero rows is the correct empty answer.
  'S' returns exactly one row: [UpdatedCount] + [Message].
  All aliases are exact PascalCase — Dapper's MatchNamesWithUnderscores is not
  enabled in this solution, and an unmatched column binds silently to default.

  Unlike sp_ManagePoTask's MYUPDATE branch this proc never RAISERRORs, because
  GlobalExceptionHandler maps every exception to HTTP 500 — which would turn
  "not assigned to you" into a server error.
==============================================================================*/
CREATE PROCEDURE [dbo].[sp_ManageTaskGate]
    @Flag   CHAR(1),
    @UserId NVARCHAR(450) = NULL,
    @TaskId VARCHAR(20)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    -- Match sp_ManagePoTask's tolerance for casing/padding on the dispatcher.
    DECLARE @op CHAR(1) = UPPER(LTRIM(RTRIM(@Flag)));

    -- The id arrives as text from the UI layer; the proc converts it.
    DECLARE @Id      INT = TRY_CONVERT(INT, @TaskId);
    DECLARE @Updated INT = 0;

    /*--------------------------------------------------------------------- 'Q' */
    IF @op = 'Q'
    BEGIN
        DECLARE @today DATE = CAST(GETDATE() AS DATE);

        -- A NULL/blank @UserId never matches the equality predicate, so an
        -- unauthenticated or unresolved caller simply gets an empty queue.
        SELECT
            t.[PoTaskId]                                   AS [TaskId],
            t.[OrderNo]                                    AS [OrderNo],
            t.[Stage]                                      AS [Stage],
            CASE t.[Stage] WHEN 1  THEN 'PO entry'
                           WHEN 2  THEN 'BOM task'
                           WHEN 3  THEN 'Planning'
                           WHEN 10 THEN 'Yarn issue'
                           WHEN 11 THEN 'Product return'
                           WHEN 20 THEN 'Manual'
                           ELSE 'Task' END                 AS [StageName],
            t.[Title]                                      AS [Title],
            t.[Detail]                                     AS [Detail],
            t.[PriorityId]                                 AS [PriorityId],
            CASE t.[PriorityId] WHEN 1 THEN 'Low'
                                WHEN 2 THEN 'Medium'
                                WHEN 3 THEN 'High'
                                WHEN 4 THEN 'Urgent' END   AS [PriorityName],
            t.[DueDate]                                    AS [DueDate],
            CASE WHEN t.[DueDate] IS NOT NULL
                  AND CAST(t.[DueDate] AS DATE) < @today
                 THEN CAST(1 AS BIT) ELSE CAST(0 AS BIT) END AS [IsOverdue]
        FROM [dbo].[PoTask] t WITH (NOLOCK)
        INNER JOIN [dbo].[PoTaskAssignee] a WITH (NOLOCK)
                ON a.[PoTaskId] = t.[PoTaskId]
        WHERE a.[UserId]   = @UserId
          AND a.[IsActive] = 1
          AND a.[Status]   = 'S'      -- not yet started by THIS user
          AND t.[IsActive] = 1
          AND t.[Status]  <> 'X'      -- never gate on a cancelled task
        ORDER BY t.[PoTaskId] ASC;    -- FIFO

        RETURN;
    END

    /*--------------------------------------------------------------------- 'S' */
    IF @op = 'S'
    BEGIN
        IF (@Id IS NULL OR @UserId IS NULL OR LTRIM(RTRIM(@UserId)) = '')
        BEGIN
            SELECT 0 AS [UpdatedCount], 'Task or user was not supplied.' AS [Message];
            RETURN;
        END

        DECLARE @AssigneeId INT, @FromStatus CHAR(1);

        SELECT @AssigneeId = [AssigneeId],
               @FromStatus = [Status]
        FROM [dbo].[PoTaskAssignee]
        WHERE [PoTaskId] = @Id
          AND [UserId]   = @UserId
          AND [IsActive] = 1;

        IF (@AssigneeId IS NULL)
        BEGIN
            SELECT 0 AS [UpdatedCount], 'This task is not assigned to you.' AS [Message];
            RETURN;
        END

        -- Already moved on (another tab, the /tasks board, or a double submit).
        -- This reports SUCCESS on purpose. The desired end state — "this row is no
        -- longer waiting to be started" — is already true, so there is nothing to
        -- do and the caller should advance to the next task.
        --
        -- Returning 0 here would be a trap: the service maps UpdatedCount = 0 to
        -- Fail, the gate would show an error and keep the SAME task on screen, and
        -- since the row can never move to 'P' again the user would be pinned to it
        -- for the rest of the run. In a modal with no close button that is close to
        -- a lockout. Only genuine faults (bad id, not your task) return 0.
        IF (@FromStatus <> 'S')
        BEGIN
            SELECT 1 AS [UpdatedCount], 'This task was already started.' AS [Message];
            RETURN;
        END

        BEGIN TRY
            BEGIN TRANSACTION;

                UPDATE [dbo].[PoTaskAssignee]
                SET [Status]    = 'P',
                    [StartDate] = COALESCE([StartDate], GETDATE())
                WHERE [AssigneeId] = @AssigneeId
                  AND [Status]     = 'S';   -- re-check under the transaction

                SET @Updated = @@ROWCOUNT;

                IF (@Updated > 0)
                BEGIN
                    INSERT INTO [dbo].[PoTaskHistory]
                        ([PoTaskId], [AssigneeId], [FromStatus], [ToStatus], [Note], [ChangedBy])
                    VALUES
                        (@Id, @AssigneeId, @FromStatus, 'P', 'started at login gate', @UserId);

                    -- Same rollup the /tasks board uses, so CompletionRule
                    -- (1=All, 2=Any, 3=Quorum) is honoured. Hand-writing
                    -- "UPDATE PoTask SET Status='P'" here would silently disagree
                    -- with the board on any multi-assignee task.
                    -- sp_PoTask_Recompute opens no transaction of its own, so it
                    -- safely enlists in this one.
                    EXEC [dbo].[sp_PoTask_Recompute] @PoTaskId = @Id, @ChangedBy = @UserId;
                END

            COMMIT TRANSACTION;
        END TRY
        BEGIN CATCH
            -- Without this, a failure inside the transaction would return an open
            -- transaction to the connection pool along with an unhandled error.
            IF (XACT_STATE() <> 0) ROLLBACK TRANSACTION;

            SELECT 0 AS [UpdatedCount], ERROR_MESSAGE() AS [Message];
            RETURN;
        END CATCH

        -- @Updated = 0 here means a concurrent writer moved the row between the
        -- status check above and the UPDATE. Same reasoning as that branch: the row
        -- is no longer waiting to be started, so report success and let the gate
        -- advance rather than pinning the user to a task that can never move.
        SELECT 1 AS [UpdatedCount],
               CASE WHEN @Updated > 0
                    THEN 'Task started.'
                    ELSE 'This task was already started.' END AS [Message];
        RETURN;
    END

    SELECT 0 AS [UpdatedCount], 'Unknown flag.' AS [Message];
END
