/*==============================================================================
  Backfill + catch-up for the yarn-invoice task sync.

  PART 1 — legacy yarn-lifecycle PoTasks from Stage 20 (Manual) -> Stage 12.
  The vendor-order lifecycle hook (BomController.CreateLifecycleTaskAsync) only
  started stamping Stage 12 partway through; every "Yarn order <n> placed",
  "Departure confirmed <n>" and "Yarn arriving <n>" task raised before that is
  still on Stage 20. sp_GetPoTask already works around this for the deep link
  (its Stage 20 -> /yarn-orders/<id> case), but that is not enough now:
  sp_ManageYarnOrder flag 'I' closes a yarn order's open lifecycle tasks when the
  last vendor invoice lands, and it matches on Stage 12. Left on 20, the legacy
  cards would never close. Restamping also makes the board label them "Yarn
  order" instead of "Manual" and gives them the proper stage-12 deep link.

  PART 2 — close the tasks whose yarn ALREADY arrived. Flag 'I' fires on save, so
  a yarn order invoiced BEFORE that logic was deployed left its lifecycle tasks
  open with nothing left to trigger them (nobody is going to re-save that
  invoice). This closes those once, using the same rule flag 'I' uses.

  Completion is done at the ASSIGNEE level, not by stamping the parent, because
  sp_PoTask_Recompute derives the parent from PoTaskAssignee and would drag a
  parent-only 'C' back to 'P' the next time an assignee touched their own row.

  SAFE TO RE-RUN: part 1 only touches rows still on Stage 20; part 2 only touches
  tasks not already 'C'. Held ('H') and cancelled ('X') tasks are never closed --
  somebody parked those deliberately, matching Recompute's own rule.
==============================================================================*/

SET NOCOUNT ON;

DECLARE @restamped INT = 0;
DECLARE @note NVARCHAR(400) = 'auto: yarn received (one-time catch-up)';
DECLARE @by   NVARCHAR(450) = 'system';

-- ---- 0) Preview: exactly what part 1 will restamp. ----
SELECT [PoTaskId], [OrderNo], [Stage], [Status], [Title], [CreatedDate]
FROM [dbo].[PoTask]
WHERE [Stage] = 20
  AND ([Title] LIKE 'Yarn order %placed%'
    OR [Title] LIKE 'Departure confirmed %'
    OR [Title] LIKE 'Yarn arriving %')
ORDER BY [PoTaskId];

-- ---- 1) Restamp legacy yarn lifecycle tasks onto Stage 12. ----
UPDATE [dbo].[PoTask]
   SET [Stage]        = 12,
       [ModifiedDate] = GETDATE()
WHERE [Stage] = 20
  AND ([Title] LIKE 'Yarn order %placed%'
    OR [Title] LIKE 'Departure confirmed %'
    OR [Title] LIKE 'Yarn arriving %');

SET @restamped = @@ROWCOUNT;

-- ---- 2) Catch-up: Stage-12 tasks whose order's yarn is already fully invoiced. ----
DECLARE @catch TABLE (rn INT IDENTITY(1,1) PRIMARY KEY, PoTaskId INT);

INSERT INTO @catch (PoTaskId)
SELECT DISTINCT t.[PoTaskId]
FROM [dbo].[PoTask] AS t
WHERE t.[Stage]    = 12
  AND t.[IsActive] = 1
  AND t.[Status] NOT IN ('C', 'X', 'H')
  AND EXISTS (
        -- ...a yarn order behind this production order that has vendor orders and
        -- no uninvoiced one left. Same test sp_GetYarnOrders uses for 'Completed'.
        SELECT 1
        FROM [dbo].[tbl_yarn_order_detail] AS d
        WHERE LTRIM(RTRIM(ISNULL(d.[order_no], ''))) = t.[OrderNo]
          AND EXISTS (SELECT 1 FROM [dbo].[tbl_yarn_vendor_order] v
                       WHERE v.[yo_id] = d.[yo_id])
          AND NOT EXISTS (SELECT 1 FROM [dbo].[tbl_yarn_vendor_order] v
                           WHERE v.[yo_id] = d.[yo_id]
                             AND NULLIF(LTRIM(RTRIM(ISNULL(v.[invoice_no], ''))), '') IS NULL));

-- History first: the assignee's CURRENT status is the FromStatus, and the UPDATE
-- below is about to overwrite it.
INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[AssigneeId],[FromStatus],[ToStatus],[Note],[ChangedBy])
SELECT a.[PoTaskId], a.[AssigneeId], a.[Status], 'C', @note, @by
FROM [dbo].[PoTaskAssignee] AS a
INNER JOIN @catch AS c ON c.PoTaskId = a.[PoTaskId]
WHERE a.[IsActive] = 1 AND a.[Status] <> 'C';

UPDATE a
   SET a.[Status]        = 'C',
       a.[StartDate]     = ISNULL(a.[StartDate], GETDATE()),
       a.[CompletedDate] = GETDATE(),
       a.[Note]          = @note
FROM [dbo].[PoTaskAssignee] AS a
INNER JOIN @catch AS c ON c.PoTaskId = a.[PoTaskId]
WHERE a.[IsActive] = 1 AND a.[Status] <> 'C';

-- Single-owner tasks have no assignee rows; Recompute bails out on those
-- (@total = 0, "status managed directly"), so the parent is set here.
INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[FromStatus],[ToStatus],[Note],[ChangedBy])
SELECT t.[PoTaskId], t.[Status], 'C', @note, @by
FROM [dbo].[PoTask] AS t
INNER JOIN @catch AS c ON c.PoTaskId = t.[PoTaskId]
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskAssignee] a
                   WHERE a.[PoTaskId] = t.[PoTaskId] AND a.[IsActive] = 1);

UPDATE t
   SET t.[Status]        = 'C',
       t.[CompletedDate] = GETDATE(),
       t.[ModifiedBy]    = @by,
       t.[ModifiedDate]  = GETDATE()
FROM [dbo].[PoTask] AS t
INNER JOIN @catch AS c ON c.PoTaskId = t.[PoTaskId]
WHERE NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskAssignee] a
                   WHERE a.[PoTaskId] = t.[PoTaskId] AND a.[IsActive] = 1);

-- Roll each parent up from its now-completed assignee rows.
DECLARE @k INT = 1, @kn INT = (SELECT COUNT(*) FROM @catch), @tid INT;
WHILE @k <= @kn
BEGIN
    SELECT @tid = PoTaskId FROM @catch WHERE rn = @k;
    EXEC [dbo].[sp_PoTask_Recompute] @PoTaskId = @tid, @ChangedBy = @by;
    SET @k = @k + 1;
END

SELECT @restamped AS [tasks_restamped], @kn AS [tasks_closed];
