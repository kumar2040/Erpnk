/* One-time, idempotent repair for existing Stage 12 yarn tasks that are still
   awaiting submission to YarnControl.

   Run this after deploying sp_SaveYarnOrder.sql and sp_ApproveYarnOrder.sql.
   It deliberately ignores every other yarn-order state. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @YarnRoleName NVARCHAR(256) = N'Yarn';
DECLARE @YarnControlRoleName NVARCHAR(256) = N'YarnControl';

DECLARE @YarnOwners TABLE
(
    [UserId] NVARCHAR(450) NOT NULL PRIMARY KEY
);

INSERT INTO @YarnOwners ([UserId])
SELECT DISTINCT yarnUserRole.[UserId]
FROM [identity].[AspNetUserRoles] AS yarnUserRole
INNER JOIN [identity].[Roles] AS yarnRole
    ON yarnRole.[Id] = yarnUserRole.[RoleId]
WHERE yarnRole.[Name] = @YarnRoleName
  AND NOT EXISTS
      (
          SELECT 1
          FROM [identity].[AspNetUserRoles] AS controlUserRole
          INNER JOIN [identity].[Roles] AS controlRole
              ON controlRole.[Id] = controlUserRole.[RoleId]
          WHERE controlUserRole.[UserId] = yarnUserRole.[UserId]
            AND controlRole.[Name] = @YarnControlRoleName
      );

IF NOT EXISTS (SELECT 1 FROM @YarnOwners)
    THROW 50001, 'No Yarn-only users found; no task assignments were changed.', 1;

DECLARE @ReadyTasks TABLE
(
    [PoTaskId] INT NOT NULL PRIMARY KEY,
    [YoId] INT NOT NULL,
    [YoNo] VARCHAR(30) NOT NULL
);

INSERT INTO @ReadyTasks ([PoTaskId], [YoId], [YoNo])
SELECT task.[PoTaskId], yarnOrder.[yo_id], yarnOrder.[yo_no]
FROM dbo.[PoTask] AS task
INNER JOIN dbo.[tbl_yarn_order] AS yarnOrder
    ON yarnOrder.[yo_id] = task.[RefId]
WHERE task.[Stage] = 12
  AND task.[IsActive] = 1
  AND task.[Status] = 'S'
  AND yarnOrder.[status] IN ('Ready for Approval', 'Placed');

BEGIN TRANSACTION;

UPDATE dbo.[PoTaskAssignee]
SET [IsActive] = 0
WHERE [PoTaskId] IN (SELECT [PoTaskId] FROM @ReadyTasks)
  AND [IsActive] = 1;

UPDATE assignee
SET assignee.[Status] = 'S',
    assignee.[StartDate] = NULL,
    assignee.[CompletedDate] = NULL,
    assignee.[Note] = NULL,
    assignee.[AssignedBy] = N'ownership-repair',
    assignee.[AssignedDate] = GETDATE(),
    assignee.[IsActive] = 1
FROM dbo.[PoTaskAssignee] AS assignee
INNER JOIN @ReadyTasks AS task
    ON task.[PoTaskId] = assignee.[PoTaskId]
INNER JOIN @YarnOwners AS owner
    ON owner.[UserId] = assignee.[UserId];

INSERT INTO dbo.[PoTaskAssignee]
    ([PoTaskId], [UserId], [Status], [AssignedBy])
SELECT task.[PoTaskId], owner.[UserId], 'S', N'ownership-repair'
FROM @YarnOwners AS owner
CROSS JOIN @ReadyTasks AS task
WHERE NOT EXISTS
      (
          SELECT 1
          FROM dbo.[PoTaskAssignee] AS existing
          WHERE existing.[PoTaskId] = task.[PoTaskId]
            AND existing.[UserId] = owner.[UserId]
      );

UPDATE task
SET task.[Status] = 'S',
    task.[Title] = N'Yarn import request ready - ' + ready.[YoNo],
    task.[Detail] = N'Review yarn import request ' + ready.[YoNo]
                  + N' and send it to YarnControl. Production orders: '
                  + ISNULL
                    (
                        (
                            SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), orders.[order_no]), N', ')
                                   WITHIN GROUP (ORDER BY orders.[order_no])
                            FROM
                            (
                                SELECT DISTINCT detail.[order_no]
                                FROM dbo.[tbl_yarn_order_detail] AS detail
                                WHERE detail.[yo_id] = ready.[YoId]
                            ) AS orders
                        ),
                        N''
                    ),
    task.[ModifiedBy] = N'ownership-repair',
    task.[ModifiedDate] = GETDATE()
FROM dbo.[PoTask] AS task
INNER JOIN @ReadyTasks AS ready
    ON ready.[PoTaskId] = task.[PoTaskId];

COMMIT TRANSACTION;

SELECT COUNT(*) AS [RepairedTaskCount]
FROM @ReadyTasks;
