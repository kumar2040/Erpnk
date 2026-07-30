/*==============================================================================
  spDropdown  —  one source for every dropdown option list in the app

  Every dropdown asks this proc for its options by name, so a list can grow
  without an application redeploy: add the row here, ALTER the proc, done.

  Parameters
    @Type      which list to return (see below)
    @Filter1   optional cascade key, meaning depends on the type
    @Filter2   optional second cascade key

  Types
    'YarnOrderStatus'      Order state filter on /yarn-orders. Any later state
                           slots in as another row in the VALUES list.
    'TaskPriority'         Add-Task priority. Ids match PoTask.PriorityId.
    'TaskUpdateFrequency'  Add-Task progress cadence. Ids match PoTask.UpdateFrequency.
    'TaskCompletionRule'   Add-Task roll-up rule. Ids match PoTask.CompletionRule.
    'TaskAssigneeStatus'   Per-card step buttons. Ids are the S/P/C codes the board
                           sends to the API unchanged.
    'PoTaskBoardColumn'    The /tasks board columns, left to right. Ids are the
                           status flags sp_GetPoTask matches on (S/P/C/O/H).
    'TaskStatus'           Task / assignee status labels (S/P/C/H/X).

  Contract — every type MUST return exactly these two columns, these names, so
  they bind to DropDownListModel. Dapper has MatchNamesWithUnderscores off in
  this project, so an alias that doesn't match PascalCase binds silently to null.

    [Id]     the code stored / sent back to the API
    [Value]  what the user reads

  REAL OPTIONS ONLY. The leading row is the control's business, not the data's:
  AutoCompleteSelect prepends "All" (id -1) or "Select" (id 0) from its All
  parameter. Emitting it here would mean every caller either filters it back out
  or ships it to the server as a bogus filter value.

  An unknown type returns an empty rowset, so a mistyped Type renders a dropdown
  holding just its leading row instead of erroring.
==============================================================================*/
CREATE OR ALTER PROCEDURE [dbo].[spDropdown]
    @Type    NVARCHAR(50),
    @Filter1 NVARCHAR(200) = NULL,
    @Filter2 NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Flag NVARCHAR(50) = UPPER(LTRIM(RTRIM(@Type)));

    /* -------------------------------------------------- YarnOrderStatus
       The yarn order's lifecycle, left to right:
         Not ordered -> no vendor order placed yet
         Ordered     -> vendor order(s) placed, at least one still uninvoiced
         Completed   -> every vendor order invoiced = the yarn arrived and is
                        ready for use (this is what raises the Planning task)
       The codes here are what sp_GetYarnOrders takes as @Status. Code 'P'
       still means the same underlying state (uninvoiced) -- only its label
       changed back to "Ordered"; the proc still honours legacy 'O' as P+C. */
    IF (@Flag = 'YARNORDERSTATUS')
    BEGIN
        SELECT [Id], [Value]
        FROM (VALUES
            ('N', 'Not ordered', 1),
            ('P', 'Ordered',     2),
            ('C', 'Completed',   3)
        ) AS v([Id], [Value], [SortOrder])
        ORDER BY [SortOrder];
        RETURN;
    END

    /* -------------------------------------------------- TaskPriority
       Ids match PoTask.PriorityId and the PriorityName CASE in sp_GetPoTask. */
    IF (@Flag = 'TASKPRIORITY')
    BEGIN
        SELECT [Id], [Value]
        FROM (VALUES
            ('1', 'Low',    1),
            ('2', 'Medium', 2),
            ('3', 'High',   3),
            ('4', 'Urgent', 4)
        ) AS v([Id], [Value], [SortOrder])
        ORDER BY [SortOrder];
        RETURN;
    END

    /* -------------------------------------------------- TaskUpdateFrequency
       Ids match PoTask.UpdateFrequency (0=None is not offered as a pick). */
    IF (@Flag = 'TASKUPDATEFREQUENCY')
    BEGIN
        SELECT [Id], [Value]
        FROM (VALUES
            ('1', 'Daily',    1),
            ('2', 'Weekly',   2),
            ('3', 'Biweekly', 3),
            ('4', 'Monthly',  4)
        ) AS v([Id], [Value], [SortOrder])
        ORDER BY [SortOrder];
        RETURN;
    END

    /* -------------------------------------------------- TaskCompletionRule
       Ids match PoTask.CompletionRule and the RuleName helper. */
    IF (@Flag = 'TASKCOMPLETIONRULE')
    BEGIN
        SELECT [Id], [Value]
        FROM (VALUES
            ('1', 'All must complete', 1),
            ('2', 'Any one completes', 2),
            ('3', 'Quorum',            3)
        ) AS v([Id], [Value], [SortOrder])
        ORDER BY [SortOrder];
        RETURN;
    END

    /* -------------------------------------------------- TaskAssigneeStatus
       The caller's own step buttons on a card. Ids are the S/P/C codes MyUpdate
       sends to the API unchanged -- letters are valid Ids (see YarnOrderStatus). */
    IF (@Flag = 'TASKASSIGNEESTATUS')
    BEGIN
        SELECT [Id], [Value]
        FROM (VALUES
            ('S', 'Scheduled',   1),
            ('P', 'In progress', 2),
            ('C', 'Complete',    3)
        ) AS v([Id], [Value], [SortOrder])
        ORDER BY [SortOrder];
        RETURN;
    END

    /* -------------------------------------------------- PoTaskBoardColumn
       The /tasks board columns, in display order. Id is the status flag the board
       sends to sp_GetPoTask (@StatusFlag IN 'S','P','C','O','H') and the value it
       compares card.DisplayFlag against -- it MUST stay the letter, not a numeric
       code, or the board fetch matches nothing and every column shows Scheduled. */
    IF (@Flag = 'POTASKBOARDCOLUMN')
    BEGIN
        SELECT [Id], [Value]
        FROM (VALUES
            ('S', 'Scheduled',   1),
            ('P', 'In Progress', 2),
            ('H', 'On Hold',     3),
            ('C', 'Completed',   4),
            ('O', 'Over Due',    5)
        ) AS v([Id], [Value], [SortOrder])
        ORDER BY [SortOrder];
        RETURN;
    END

    /* -------------------------------------------------- TaskStatus
       Task / assignee status label. Ids are the stored status letters, the same
       ones card.Status / PoTaskAssignee.Status hold. */
    IF (@Flag = 'TASKSTATUS')
    BEGIN
        SELECT [Id], [Value]
        FROM (VALUES
            ('S', 'Scheduled',   1),
            ('P', 'In progress', 2),
            ('C', 'Completed',   3),
            ('H', 'On hold',     4),
            ('X', 'Cancelled',   5)
        ) AS v([Id], [Value], [SortOrder])
        ORDER BY [SortOrder];
        RETURN;
    END

    /* Unknown type -> empty list, shaped so the caller still binds cleanly. */
    SELECT [Id], [Value]
    FROM (VALUES ('', '')) AS v([Id], [Value])
    WHERE 1 = 0;
END
