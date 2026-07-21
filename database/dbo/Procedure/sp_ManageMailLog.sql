CREATE PROCEDURE [dbo].[sp_ManageMailLog]
    @Flag     NVARCHAR(10),          -- PENDING | CLAIM | SENT | FAILED
    @Top      INT           = NULL,  -- PENDING: batch size
    @MaxRetry INT           = NULL,  -- PENDING: exclude rows at/over this retry_count
    @MailId   INT           = NULL,  -- CLAIM / SENT / FAILED
    @ErrorMsg NVARCHAR(500) = NULL   -- FAILED
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @op NVARCHAR(10) = UPPER(LTRIM(RTRIM(@Flag)));

    -- Outbox drain — dbo.tblMailLog rows not yet sent, oldest first, aliased
    -- snake_case -> PascalCase for Dapper (see MailLogDto).
    IF (@op = 'PENDING')
    BEGIN
        SELECT TOP (ISNULL(@Top, 20))
               [mail_id]   AS MailId,
               [mail_to]   AS MailTo,
               [mail_cc]   AS MailCc,
               [subject]   AS Subject,
               [body]      AS Body,
               [mail_type] AS MailType
        FROM [dbo].[tblMailLog]
        WHERE [is_sent] = 0
          AND [retry_count] < ISNULL(@MaxRetry, 5)
        ORDER BY [created_date] ASC, [mail_id] ASC;
        RETURN;
    END

    -- Optimistic claim: is_sent 0 -> 1 only if still unclaimed, so two
    -- overlapping job runs can't both pick up and send the same row.
    -- Returns 1 if this call won the claim, 0 if another run already took it
    -- (or the row no longer exists).
    IF (@op = 'CLAIM')
    BEGIN
        UPDATE [dbo].[tblMailLog]
           SET [is_sent] = 1
         WHERE [mail_id] = @MailId
           AND [is_sent] = 0;

        SELECT CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS Claimed;
        RETURN;
    END

    -- Confirms a claimed row actually sent. NOTE: is_sent is already 1 from
    -- CLAIM; a crash between CLAIM and SENT leaves is_sent = 1 with
    -- sent_date still NULL — a known limitation, cleared up manually.
    IF (@op = 'SENT')
    BEGIN
        UPDATE [dbo].[tblMailLog]
           SET [sent_date] = GETDATE(),
               [error_msg] = NULL
         WHERE [mail_id] = @MailId;

        SELECT CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS Updated;
        RETURN;
    END

    -- Releases the claim (is_sent back to 0) so the row is eligible for
    -- PENDING again, bumps retry_count, and records the reason. Once
    -- retry_count reaches @MaxRetry (EmailService.MaxRetryCount) the
    -- PENDING filter above excludes it permanently — it stays is_sent = 0
    -- with the last error_msg, effectively dead-lettered.
    IF (@op = 'FAILED')
    BEGIN
        UPDATE [dbo].[tblMailLog]
           SET [is_sent]     = 0,
               [retry_count] = [retry_count] + 1,
               [error_msg]   = LEFT(@ErrorMsg, 500)
         WHERE [mail_id] = @MailId;

        SELECT CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS Updated;
        RETURN;
    END

    RAISERROR('sp_ManageMailLog: unknown @Flag "%s".', 16, 1, @op);
END
