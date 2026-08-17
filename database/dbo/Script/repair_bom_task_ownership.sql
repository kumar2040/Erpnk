/* One-time, idempotent repair for open Stage 2 BOM tasks that still have
   legacy non-Production-Manager assignees.

   Run this after deploying sp_PoTask_AttachOrCreateBom.sql. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @ProductionManagerRoleName nvarchar(256) = N'Production Manager';

DECLARE @ProductionManagers TABLE
(
    [UserId] nvarchar(450) NOT NULL PRIMARY KEY
);

INSERT INTO @ProductionManagers ([UserId])
SELECT DISTINCT userRole.[UserId]
FROM [identity].[AspNetUserRoles] AS userRole
INNER JOIN [identity].[Roles] AS role
    ON role.[Id] = userRole.[RoleId]
WHERE role.[Name] = @ProductionManagerRoleName;

IF NOT EXISTS (SELECT 1 FROM @ProductionManagers)
    THROW 50001, 'No Production Manager users found; no BOM task assignments were changed.', 1;

DECLARE @OpenBomTasks TABLE
(
    [PoTaskId] int NOT NULL PRIMARY KEY,
    [Status] char(1) NOT NULL,
    [StartDate] datetime NULL
);

INSERT INTO @OpenBomTasks ([PoTaskId], [Status], [StartDate])
SELECT task.[PoTaskId], task.[Status], task.[StartDate]
FROM [dbo].[PoTask] AS task
WHERE task.[Stage] = 2
  AND task.[IsActive] = 1
  AND task.[Status] IN ('S', 'P', 'H');

BEGIN TRANSACTION;

UPDATE [dbo].[PoTaskAssignee]
SET [IsActive] = 0
WHERE [PoTaskId] IN (SELECT [PoTaskId] FROM @OpenBomTasks)
  AND [IsActive] = 1;

UPDATE assignee
SET assignee.[Status] = task.[Status],
    assignee.[StartDate] = CASE WHEN task.[Status] IN ('P', 'H')
                                THEN COALESCE(assignee.[StartDate], task.[StartDate], GETDATE())
                                ELSE NULL END,
    assignee.[CompletedDate] = NULL,
    assignee.[Note] = CASE WHEN task.[Status] = 'H' THEN assignee.[Note] ELSE NULL END,
    assignee.[AssignedBy] = N'ownership-repair',
    assignee.[AssignedDate] = GETDATE(),
    assignee.[IsActive] = 1
FROM [dbo].[PoTaskAssignee] AS assignee
INNER JOIN @OpenBomTasks AS task
    ON task.[PoTaskId] = assignee.[PoTaskId]
INNER JOIN @ProductionManagers AS productionManager
    ON productionManager.[UserId] = assignee.[UserId];

INSERT INTO [dbo].[PoTaskAssignee]
    ([PoTaskId], [UserId], [Status], [StartDate], [AssignedBy])
SELECT task.[PoTaskId],
       productionManager.[UserId],
       task.[Status],
       CASE WHEN task.[Status] IN ('P', 'H')
            THEN COALESCE(task.[StartDate], GETDATE()) END,
       N'ownership-repair'
FROM @ProductionManagers AS productionManager
CROSS JOIN @OpenBomTasks AS task
WHERE NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[PoTaskAssignee] AS existing
          WHERE existing.[PoTaskId] = task.[PoTaskId]
            AND existing.[UserId] = productionManager.[UserId]
      );

UPDATE task
SET task.[ModifiedBy] = N'ownership-repair',
    task.[ModifiedDate] = GETDATE()
FROM [dbo].[PoTask] AS task
INNER JOIN @OpenBomTasks AS openTask
    ON openTask.[PoTaskId] = task.[PoTaskId];

COMMIT TRANSACTION;

SELECT COUNT(*) AS [RepairedTaskCount]
FROM @OpenBomTasks;
