/* Targeted, idempotent repair for Natureknit Yarn-029 if it was incorrectly
   approved before YarnControl acted.

   Run this only after deploying sp_ApproveYarnOrder.sql and sp_GetYarnOrders.sql.
   It does nothing when Yarn-029 is already Pending Approval, Ordered, Completed,
   or Rejected, and it refuses to change an order that already has a vendor order. */
SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @YoNo varchar(30) = 'Natureknit Yarn-029';
DECLARE @YarnControlRoleName nvarchar(256) = N'YarnControl';
DECLARE @RepairUserId nvarchar(450) = N'workflow-repair';
DECLARE @YoId int,
        @CurrentStatus varchar(30),
        @PoTaskId int;

SELECT @YoId = yarnOrder.[yo_id],
       @CurrentStatus = yarnOrder.[status]
FROM [dbo].[tbl_yarn_order] AS yarnOrder
WHERE yarnOrder.[yo_no] = @YoNo;

IF @YoId IS NULL
    THROW 50001, 'Natureknit Yarn-029 was not found; nothing was changed.', 1;

IF @CurrentStatus <> 'Approved'
BEGIN
    SELECT CAST(0 AS bit) AS [Repaired],
           N'Yarn-029 is not incorrectly Approved; nothing was changed.' AS [Message];
    RETURN;
END;

IF EXISTS (SELECT 1 FROM [dbo].[tbl_yarn_vendor_order] WHERE [yo_id] = @YoId)
    THROW 50002, 'Yarn-029 already has a vendor order and cannot be reset automatically.', 1;

SELECT TOP (1) @PoTaskId = task.[PoTaskId]
FROM [dbo].[PoTask] AS task
WHERE task.[Stage] = 12
  AND task.[RefId] = @YoId
  AND task.[IsActive] = 1
ORDER BY task.[PoTaskId] DESC;

IF @PoTaskId IS NULL
    THROW 50003, 'The Yarn-029 workflow task was not found; nothing was changed.', 1;

DECLARE @YarnControlUsers TABLE
(
    [UserId] nvarchar(450) NOT NULL PRIMARY KEY
);

INSERT INTO @YarnControlUsers ([UserId])
SELECT DISTINCT userRole.[UserId]
FROM [identity].[AspNetUserRoles] AS userRole
INNER JOIN [identity].[Roles] AS role
    ON role.[Id] = userRole.[RoleId]
WHERE role.[Name] = @YarnControlRoleName;

IF NOT EXISTS (SELECT 1 FROM @YarnControlUsers)
    THROW 50004, 'No YarnControl users were found; nothing was changed.', 1;

BEGIN TRANSACTION;

UPDATE [dbo].[tbl_yarn_order]
SET [status] = 'Pending Approval'
WHERE [yo_id] = @YoId
  AND [status] = 'Approved';

UPDATE [dbo].[PoTaskAssignee]
SET [IsActive] = 0
WHERE [PoTaskId] = @PoTaskId
  AND [IsActive] = 1;

UPDATE assignee
SET assignee.[Status] = 'S',
    assignee.[StartDate] = NULL,
    assignee.[CompletedDate] = NULL,
    assignee.[Note] = NULL,
    assignee.[AssignedBy] = @RepairUserId,
    assignee.[AssignedDate] = GETDATE(),
    assignee.[IsActive] = 1
FROM [dbo].[PoTaskAssignee] AS assignee
INNER JOIN @YarnControlUsers AS yarnControl
    ON yarnControl.[UserId] = assignee.[UserId]
WHERE assignee.[PoTaskId] = @PoTaskId;

INSERT INTO [dbo].[PoTaskAssignee]
    ([PoTaskId], [UserId], [Status], [AssignedBy])
SELECT @PoTaskId, yarnControl.[UserId], 'S', @RepairUserId
FROM @YarnControlUsers AS yarnControl
WHERE NOT EXISTS
      (
          SELECT 1
          FROM [dbo].[PoTaskAssignee] AS existing
          WHERE existing.[PoTaskId] = @PoTaskId
            AND existing.[UserId] = yarnControl.[UserId]
      );

UPDATE [dbo].[PoTask]
SET [Status] = 'S',
    [Title] = N'Approve yarn order - ' + @YoNo,
    [Detail] = N'Yarn order ' + @YoNo + N' is awaiting YarnControl approval.',
    [StartDate] = NULL,
    [CompletedDate] = NULL,
    [ModifiedBy] = @RepairUserId,
    [ModifiedDate] = GETDATE()
WHERE [PoTaskId] = @PoTaskId;

INSERT INTO [dbo].[PoTaskHistory]
    ([PoTaskId], [FromStatus], [ToStatus], [Note], [ChangedBy])
VALUES
    (@PoTaskId, 'P', 'S', N'Reset incorrect pre-approval state for Yarn-029.', @RepairUserId);

INSERT INTO [dbo].[PoTaskNotification]
    ([UserId], [PoTaskId], [Kind], [Title], [Body])
SELECT yarnControl.[UserId], @PoTaskId, 'U', N'Yarn order awaiting approval',
       N'Yarn order ' + @YoNo + N' requires YarnControl approval.'
FROM @YarnControlUsers AS yarnControl;

COMMIT TRANSACTION;

SELECT CAST(1 AS bit) AS [Repaired],
       N'Natureknit Yarn-029 reset to Pending Approval and assigned to YarnControl.' AS [Message];
