-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.sp_ManagePoTaskNotification  (SQL_STORED_PROCEDURE)
CREATE PROCEDURE [dbo].[sp_ManagePoTaskNotification]
    @Flag           NVARCHAR(20),                 -- LIST | UNREAD | MARKREAD | MARKALLREAD | PENDING | MARKPUSHED
    @UserId         NVARCHAR(450) = NULL,
    @NotificationId INT           = NULL,
    @Top            INT           = 30,
    @Ids            NVARCHAR(MAX) = NULL           -- MARKPUSHED: pipe-delimited notification ids
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @op NVARCHAR(20) = UPPER(LTRIM(RTRIM(@Flag)));

    IF (@op = 'LIST')
    BEGIN
        SELECT TOP (@Top)
            [NotificationId], [PoTaskId], [Kind], [Title], [Body], [IsRead], [CreatedDate]
        FROM [dbo].[PoTaskNotification]
        WHERE [UserId] = @UserId
        ORDER BY [CreatedDate] DESC, [NotificationId] DESC;
        RETURN;
    END

    IF (@op = 'UNREAD')
    BEGIN
        SELECT COUNT(*) AS [UnreadCount]
        FROM [dbo].[PoTaskNotification]
        WHERE [UserId] = @UserId AND [IsRead] = 0;
        RETURN;
    END

    IF (@op = 'MARKREAD')
    BEGIN
        UPDATE [dbo].[PoTaskNotification]
        SET [IsRead] = 1
        WHERE [NotificationId] = @NotificationId AND [UserId] = @UserId;   -- zero trust: only your own
        RETURN;
    END

    IF (@op = 'MARKALLREAD')
    BEGIN
        UPDATE [dbo].[PoTaskNotification]
        SET [IsRead] = 1
        WHERE [UserId] = @UserId AND [IsRead] = 0;
        RETURN;
    END

    -- Outbox drain: notifications not yet pushed over SignalR (includes UserId so the
    -- publisher knows the recipient group).
    IF (@op = 'PENDING')
    BEGIN
        SELECT TOP (@Top)
            [NotificationId], [UserId], [PoTaskId], [Kind], [Title], [Body], [IsRead], [CreatedDate]
        FROM [dbo].[PoTaskNotification]
        WHERE [IsPushed] = 0
        ORDER BY [NotificationId];
        RETURN;
    END

    IF (@op = 'MARKPUSHED')
    BEGIN
        IF (@Ids IS NOT NULL AND LTRIM(RTRIM(@Ids)) <> '')
            UPDATE n
            SET [IsPushed] = 1
            FROM [dbo].[PoTaskNotification] n
            INNER JOIN (SELECT TRY_CONVERT(INT, LTRIM(RTRIM([value]))) AS [id]
                        FROM STRING_SPLIT(@Ids, '|')
                        WHERE LTRIM(RTRIM([value])) <> '') s
                ON s.[id] = n.[NotificationId];
        RETURN;
    END

    RAISERROR('sp_ManagePoTaskNotification: unknown @Flag "%s".', 16, 1, @op);
END
