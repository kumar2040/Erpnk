-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.sp_PoTask_DueReminders  (SQL_STORED_PROCEDURE)
CREATE PROCEDURE [dbo].[sp_PoTask_DueReminders]
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @now DATETIME = GETDATE();

    -- Tasks whose reminder is due and hasn't fired for THIS NotificationDate yet.
    DECLARE @due TABLE ([PoTaskId] INT PRIMARY KEY, [Title] NVARCHAR(200), [UpdateFrequency] TINYINT, [NotificationDate] DATETIME);
    INSERT INTO @due
    SELECT [PoTaskId], [Title], [UpdateFrequency], [NotificationDate]
    FROM [dbo].[PoTask]
    WHERE [IsActive] = 1
      AND [Status] NOT IN ('C','X')
      AND [NotificationDate] IS NOT NULL
      AND [NotificationDate] <= @now
      AND ([LastReminderDate] IS NULL OR [LastReminderDate] < [NotificationDate]);

    -- One reminder per OPEN assignee (not yet completed).
    INSERT INTO [dbo].[PoTaskNotification] ([UserId],[PoTaskId],[Kind],[Title],[Body])
    SELECT a.[UserId], d.[PoTaskId], 'R', 'Task reminder', ISNULL(d.[Title], 'Task due')
    FROM @due d
    INNER JOIN [dbo].[PoTaskAssignee] a
        ON a.[PoTaskId] = d.[PoTaskId] AND a.[IsActive] = 1 AND a.[Status] <> 'C';

    -- Tasks with no open assignees -> remind the creator instead.
    INSERT INTO [dbo].[PoTaskNotification] ([UserId],[PoTaskId],[Kind],[Title],[Body])
    SELECT t.[CreatedBy], d.[PoTaskId], 'R', 'Task reminder', ISNULL(d.[Title], 'Task due')
    FROM @due d
    INNER JOIN [dbo].[PoTask] t ON t.[PoTaskId] = d.[PoTaskId]
    WHERE t.[CreatedBy] IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskAssignee] a
                      WHERE a.[PoTaskId] = d.[PoTaskId] AND a.[IsActive] = 1 AND a.[Status] <> 'C');

    -- Mark fired; advance recurring reminders, close one-off ones.
    UPDATE t
    SET [LastReminderDate] = @now,
        [NotificationDate] = CASE t.[UpdateFrequency]
            WHEN 1 THEN DATEADD(DAY,   1,  t.[NotificationDate])
            WHEN 2 THEN DATEADD(DAY,   7,  t.[NotificationDate])
            WHEN 3 THEN DATEADD(DAY,   14, t.[NotificationDate])
            WHEN 4 THEN DATEADD(MONTH, 1,  t.[NotificationDate])
            ELSE t.[NotificationDate] END    -- 0/NULL: stays put, but LastReminderDate guard stops a re-fire
    FROM [dbo].[PoTask] t
    INNER JOIN @due d ON d.[PoTaskId] = t.[PoTaskId];

    SELECT COUNT(*) AS [Fired] FROM @due;
END
