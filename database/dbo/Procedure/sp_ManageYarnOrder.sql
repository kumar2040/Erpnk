CREATE PROCEDURE [dbo].[sp_ManageYarnOrder]
    @Flag          CHAR(1),
    @VyoId         INT           = NULL,
    @ColorsJson    NVARCHAR(MAX) = NULL,
    @DropBy        VARCHAR(50)   = NULL,
    @DropNote      VARCHAR(200)  = NULL,
    @YarnId        VARCHAR(20)   = NULL,
    @DepartureDate VARCHAR(30)   = NULL,
    @ArrivalDate   VARCHAR(30)   = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Flag = 'D'
    BEGIN
        IF @VyoId IS NULL OR @ColorsJson IS NULL OR ISJSON(@ColorsJson) <> 1
        BEGIN
            SELECT 0 AS dropped_count, 0 AS mail_count, 0 AS notify_count,
                   'Invalid input: vendor order id and a JSON color list are required.' AS [message];
            RETURN;
        END

        -- Resolve the vendor sub-order -> parent order + display info.
        DECLARE @yoId INT, @vyoNo VARCHAR(40), @vendor VARCHAR(150);
        SELECT @yoId = yo_id, @vyoNo = vyo_no, @vendor = vendor
        FROM dbo.tbl_yarn_vendor_order
        WHERE vyo_id = @VyoId;

        IF @yoId IS NULL
        BEGIN
            SELECT 0 AS dropped_count, 0 AS mail_count, 0 AS notify_count,
                   'Vendor order not found.' AS [message];
            RETURN;
        END

        DECLARE @colors TABLE (color VARCHAR(100) PRIMARY KEY);
        INSERT INTO @colors (color)
        SELECT DISTINCT LTRIM(RTRIM(value))
        FROM OPENJSON(@ColorsJson)
        WHERE LTRIM(RTRIM(ISNULL(value, ''))) <> '';

        IF NOT EXISTS (SELECT 1 FROM @colors)
        BEGIN
            SELECT 0 AS dropped_count, 0 AS mail_count, 0 AS notify_count,
                   'No colors supplied.' AS [message];
            RETURN;
        END

        BEGIN TRY
            BEGIN TRAN;

            /* 1) Flag the parent detail lines (only ones not already dropped). */
            UPDATE d
               SET d.is_dropped = 1,
                   d.drop_date  = GETDATE(),
                   d.drop_by    = @DropBy,
                   d.drop_note  = @DropNote
            FROM dbo.tbl_yarn_order_detail AS d
            INNER JOIN @colors AS c ON c.color = d.color
            WHERE d.yo_id = @yoId
              AND d.is_dropped = 0;

            DECLARE @dropped INT = @@ROWCOUNT;

            IF @dropped = 0
            BEGIN
                ROLLBACK TRAN;
                SELECT 0 AS dropped_count, 0 AS mail_count, 0 AS notify_count,
                       'No matching (undropped) color lines found on this order.' AS [message];
                RETURN;
            END

            DECLARE @colorList NVARCHAR(MAX) =
                (SELECT STRING_AGG(CAST(color AS NVARCHAR(MAX)), ', ') FROM @colors);

            /* ------------------------------------------------------------------
               2) RECIPIENTS — Admin / Manager users (id + email).
               vvv  EDIT THIS BLOCK IF YOUR ROLE NAMES / TABLES DIFFER  vvv
               Original draft joined u.Id = r.Id (user GUID = role GUID), which
               never matches — the link goes through the AspNetUserRoles junction.
               Verify the junction table name with:
                 SELECT TABLE_SCHEMA, TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
                 WHERE TABLE_NAME LIKE '%UserRoles%';
               ------------------------------------------------------------------ */
            DECLARE @recipients TABLE (UserId NVARCHAR(450) PRIMARY KEY, Email NVARCHAR(256) NULL);
            INSERT INTO @recipients (UserId, Email)
            SELECT DISTINCT u.Id, u.Email
            FROM [identity].[Users] AS u
            INNER JOIN [identity].[AspNetUserRoles] AS ur ON ur.UserId = u.Id
            INNER JOIN [identity].[Roles] AS r ON r.Id = ur.RoleId
            WHERE r.Name IN ('Manager', 'Admin');
            /* ^^^  END EDITABLE RECIPIENTS BLOCK  ^^^ */

            DECLARE @subject NVARCHAR(255) =
                CONCAT('Vendor dropped ', @dropped, ' color(s) on ', @vyoNo);

            DECLARE @body NVARCHAR(MAX) = CONCAT(
                '<p>Vendor <b>', ISNULL(@vendor, N'—'), '</b> dropped the following color(s) on <b>', @vyoNo, '</b>:</p>',
                '<p><b>', @colorList, '</b></p>',
                CASE WHEN ISNULL(@DropNote, '') = '' THEN ''
                     ELSE CONCAT('<p>Note: ', @DropNote, '</p>') END,
                '<p>Dropped by user ', ISNULL(@DropBy, N'—'), ' on ',
                CONVERT(NVARCHAR(20), GETDATE(), 113), '.</p>');

            /* 3) Mail outbox: one row per distinct non-empty email.
                  If no emails found, exactly ONE row with mail_to = ''. */
            INSERT INTO dbo.tblMailLog (mail_to, subject, body, mail_type, created_by)
            SELECT DISTINCT Email, @subject, @body, 'YarnColorDrop', @DropBy
            FROM @recipients
            WHERE LTRIM(RTRIM(ISNULL(Email, ''))) <> '';

            DECLARE @mails INT = @@ROWCOUNT;
            IF @mails = 0
            BEGIN
                INSERT INTO dbo.tblMailLog (mail_to, subject, body, mail_type, created_by)
                VALUES ('', @subject, @body, 'YarnColorDrop', @DropBy);
                SET @mails = 1;
            END

            /* 4) In-app bell: one PoTaskNotification per Admin/Manager user.
                  Kind 'D' = color drop. IsPushed = 0 -> the existing SignalR
                  outbox sweep (DispatchPendingPushesAsync) delivers it live. */
            INSERT INTO dbo.PoTaskNotification (UserId, PoTaskId, Kind, Title, Body, IsRead, CreatedDate, IsPushed)
            SELECT UserId, NULL, 'D', @subject,
                   CONCAT('Vendor ', ISNULL(@vendor, N'—'), ' dropped: ', @colorList,
                          CASE WHEN ISNULL(@DropNote, '') = '' THEN '' ELSE CONCAT(' — ', @DropNote) END),
                   0, GETDATE(), 0
            FROM @recipients;

            DECLARE @notifs INT = @@ROWCOUNT;

            COMMIT TRAN;

            SELECT @dropped AS dropped_count, @mails AS mail_count, @notifs AS notify_count,
                   CONCAT(@dropped, ' color line(s) flagged as dropped on ', @vyoNo, '.') AS [message];
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRAN;
            SELECT 0 AS dropped_count, 0 AS mail_count, 0 AS notify_count,
                   ERROR_MESSAGE() AS [message];
        END CATCH
        RETURN;
    END

    /* ================= Flag 'T' — shipment TIMELINE ==================
       Sets departure and/or arrival on a vendor sub-order. Both dates arrive
       as strings and the DB converts them: TRY_CONVERT yields NULL for a
       null/blank/unparseable value, and COALESCE then keeps the column's
       existing value — so sending only one date leaves the other untouched.

       Result columns are aliased in exact PascalCase because Dapper's
       MatchNamesWithUnderscores is not enabled anywhere in this solution;
       a snake_case alias would bind silently to zero. Exactly one row is
       returned on every path, so "not found" is distinguishable from a
       no-rowset default. */
    IF @Flag = 'T'
    BEGIN
        DECLARE @Updated INT;

        BEGIN TRANSACTION;

            UPDATE dbo.tbl_yarn_vendor_order
               SET departure_date = COALESCE(TRY_CONVERT(DATE, @DepartureDate), departure_date),
                   arrival_date   = COALESCE(TRY_CONVERT(DATE, @ArrivalDate),   arrival_date)
             WHERE vyo_id = TRY_CONVERT(INT, @YarnId);

            SET @Updated = @@ROWCOUNT;

        COMMIT TRANSACTION;

        SELECT @Updated AS UpdatedCount,
               CASE WHEN @Updated > 0
                    THEN 'Date saved.'
                    ELSE 'Vendor order not found.'
               END AS [Message];
        RETURN;
    END

END
