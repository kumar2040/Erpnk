CREATE PROCEDURE [dbo].[spEmailSetting]
    @Flag    NVARCHAR(10),          -- S = one setting row by @EmailId | G = all sender emails
    @EmailId INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @op NVARCHAR(10) = UPPER(LTRIM(RTRIM(@Flag)));

    IF (@op = 'S')
    BEGIN
        SELECT [Id], [EmailType], [MailServer], [Port], [SenderName],
               [SenderEmail], [EmailFormat], [Password], [Username]
        FROM [dbo].[tblEmailSetting]
        WHERE [Id] = @EmailId;
        RETURN;
    END

    IF (@op = 'G')
    BEGIN
        SELECT [SenderEmail]
        FROM [dbo].[tblEmailSetting]
        ORDER BY [Id];
        RETURN;
    END

    RAISERROR('spEmailSetting: unknown @Flag "%s".', 16, 1, @op);
END
