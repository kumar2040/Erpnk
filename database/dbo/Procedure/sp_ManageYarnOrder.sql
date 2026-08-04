CREATE OR ALTER PROCEDURE [dbo].[sp_ManageYarnOrder]
    @Flag          CHAR(1),
    @VyoId         INT           = NULL,
    @ColorsJson    NVARCHAR(MAX) = NULL,
    @DropBy        VARCHAR(50)   = NULL,
    @DropNote      VARCHAR(200)  = NULL,
    @YarnId        VARCHAR(20)   = NULL,
    @DepartureDate VARCHAR(30)   = NULL,
    @ArrivalDate   VARCHAR(30)   = NULL,
    @InvoiceNo     VARCHAR(50)   = NULL,
    @InvoiceBy     VARCHAR(50)   = NULL,
    @Weight        VARCHAR(30)   = NULL,
    @PragyapanNo   VARCHAR(50)   = NULL,
    @LcTtNo        VARCHAR(50)   = NULL
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

    /* ================= Flag 'I' — vendor INVOICE =====================
       Entering an invoice number is the "the yarn arrived from the vendor and
       is ready for use" event: it completes that ONE vendor sub-order. Passing
       a blank/NULL @InvoiceNo is the deliberate correction path -- it clears
       the invoice and drops that sub-order back to 'Placed'.

       @Weight/@PragyapanNo/@LcTtNo travel alongside the invoice and are only
       written on the SAVE path (@inv IS NOT NULL) -- clearing an invoice is a
       correction to the invoice itself, not a reason to erase an already-
       recorded arrival weight/pragyapan/LC-TT, so those three columns are left
       untouched on the clear path via COALESCE against their current value.

       The PARENT header only completes when EVERY vendor sub-order under it
       carries an invoice, and it is that last invoice which raises the Planning
       task: planning cannot start while part of the yarn is still at a vendor.

       Two transactions on purpose. The invoice write commits FIRST and alone,
       so a failure inside the best-effort task automation can never roll back
       the user's save -- the same rule the controllers' task hooks follow.

       Result columns are PascalCase because Dapper's MatchNamesWithUnderscores
       is not enabled anywhere in this solution; a snake_case alias would bind
       silently to zero. Exactly one row is returned on every path. */
    IF @Flag = 'I'
    BEGIN
        DECLARE @vyo INT         = TRY_CONVERT(INT, @YarnId);
        DECLARE @inv VARCHAR(50) = NULLIF(LTRIM(RTRIM(@InvoiceNo)), '');

        DECLARE @invYoId INT, @invVyoNo VARCHAR(40), @invVendor VARCHAR(150), @invYoNo VARCHAR(30);
        IF @vyo IS NOT NULL
            SELECT @invYoId   = v.yo_id,
                   @invVyoNo  = v.vyo_no,
                   @invVendor = v.vendor,
                   @invYoNo   = o.yo_no
            FROM dbo.tbl_yarn_vendor_order AS v
            INNER JOIN dbo.tbl_yarn_order  AS o ON o.yo_id = v.yo_id
            WHERE v.vyo_id = @vyo;

        IF @invYoId IS NULL
        BEGIN
            SELECT 0 AS UpdatedCount, CAST(0 AS BIT) AS HeaderCompleted, 0 AS TaskCount,
                   'Vendor order not found.' AS [Message];
            RETURN;
        END

        DECLARE @invUpdated INT = 0;
        DECLARE @headerDone BIT = 0;

        BEGIN TRY
            BEGIN TRAN;

            UPDATE dbo.tbl_yarn_vendor_order
               SET invoice_no   = @inv,
                   invoice_date = CASE WHEN @inv IS NULL THEN NULL ELSE GETDATE() END,
                   invoice_by   = CASE WHEN @inv IS NULL THEN NULL ELSE @InvoiceBy END,
                   weight       = CASE WHEN @inv IS NULL THEN weight
                                       ELSE COALESCE(TRY_CONVERT(DECIMAL(18,3), @Weight), weight) END,
                   pragyapan_no = CASE WHEN @inv IS NULL THEN pragyapan_no
                                       ELSE COALESCE(NULLIF(LTRIM(RTRIM(@PragyapanNo)), ''), pragyapan_no) END,
                   lc_tt_no     = CASE WHEN @inv IS NULL THEN lc_tt_no
                                       ELSE COALESCE(NULLIF(LTRIM(RTRIM(@LcTtNo)), ''), lc_tt_no) END,
                   [status]     = CASE WHEN @inv IS NULL THEN 'Placed' ELSE 'Completed' END
             WHERE vyo_id = @vyo;

            SET @invUpdated = @@ROWCOUNT;

            -- Header state stays DERIVED, exactly as "Ordered" always was: sp_GetYarnOrders
            -- answers this same question straight from the vendor rows. Deliberately NOT
            -- written back to tbl_yarn_order.status -- a stored copy would be a second
            -- source of truth that silently drifts the moment a vendor order is added.
            -- Computed here only to decide whether to raise the Planning task and what to
            -- tell the user.
            SET @headerDone =
                CASE WHEN EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order
                                   WHERE yo_id = @invYoId)
                      AND NOT EXISTS (SELECT 1 FROM dbo.tbl_yarn_vendor_order
                                       WHERE yo_id = @invYoId
                                         AND NULLIF(LTRIM(RTRIM(ISNULL(invoice_no, ''))), '') IS NULL)
                     THEN 1 ELSE 0 END;

            COMMIT TRAN;
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRAN;
            SELECT 0 AS UpdatedCount, CAST(0 AS BIT) AS HeaderCompleted, 0 AS TaskCount,
                   ERROR_MESSAGE() AS [Message];
            RETURN;
        END CATCH

        /* ---- Best effort from here: the invoice is already committed. ----
           The last invoice on a header means every vendor has delivered, so the
           order is ready to plan. One Planning task per production order behind
           the yarn order; sp_ManagePoTask CREATE already dedupes stage 3 per
           (OrderNo, Stage) while a task is open, so re-invoicing or correcting a
           number cannot spawn a second card. ASSIGN inside it also writes the
           PoTaskNotification bell rows, which is where the notification comes
           from -- nothing extra to insert here. No mail: tblMailLog has no
           sender draining it yet, so a row would just sit unsent. */
        DECLARE @tasks INT = 0;
        DECLARE @closed INT = 0;
        DECLARE @taskError NVARCHAR(400) = NULL;

        -- The production orders behind this yarn order: what both automations key on.
        -- Populated outside the TRY blocks so a failure in one can't starve the other.
        DECLARE @orders TABLE (rn INT IDENTITY(1,1) PRIMARY KEY, order_no NVARCHAR(50));

        IF @headerDone = 1 AND @inv IS NOT NULL
            INSERT INTO @orders (order_no)
            SELECT DISTINCT LTRIM(RTRIM(order_no))
            FROM dbo.tbl_yarn_order_detail
            WHERE yo_id = @invYoId
              AND NULLIF(LTRIM(RTRIM(ISNULL(order_no, ''))), '') IS NOT NULL;

        IF @headerDone = 1 AND @inv IS NOT NULL
        BEGIN
            BEGIN TRY
                DECLARE @pmUsers NVARCHAR(MAX) =
                    (SELECT STRING_AGG(CAST(UserId AS NVARCHAR(MAX)), '|')
                     FROM (SELECT DISTINCT u.Id AS UserId
                           FROM [identity].[Users]           AS u
                           INNER JOIN [identity].[AspNetUserRoles] AS ur ON ur.UserId = u.Id
                           INNER JOIN [identity].[Roles]     AS r  ON r.Id = ur.RoleId
                           WHERE r.Name = 'Production Manager') AS x);

                DECLARE @i INT = 1, @n INT = (SELECT COUNT(*) FROM @orders);
                DECLARE @ordNo NVARCHAR(50), @taskTitle NVARCHAR(200), @taskDetail NVARCHAR(MAX);

                -- sp_ManagePoTask CREATE ends with SELECT @NewId AS PoTaskId. Left alone that
                -- rowset would surface as this procedure's FIRST result set and Dapper would
                -- map it instead of the message row below, so it is swallowed into @sink.
                DECLARE @sink TABLE (PoTaskId INT);

                SET @taskDetail = CONCAT(
                    'All vendor orders on ', @invYoNo, ' are invoiced and received (last: ',
                    @invVyoNo, ' — ', ISNULL(@invVendor, N'—'), ', invoice ', @inv,
                    '). The yarn is ready for use — start planning.');

                WHILE @i <= @n
                BEGIN
                    SELECT @ordNo = order_no FROM @orders WHERE rn = @i;

                    SET @taskTitle = CONCAT('Yarn received — plan production for ', @ordNo);

                    DELETE FROM @sink;
                    INSERT INTO @sink (PoTaskId)
                    EXEC dbo.sp_ManagePoTask
                         @Flag            = 'CREATE',
                         @OrderNo         = @ordNo,
                         @Stage           = 3,              -- Planning
                         @Title           = @taskTitle,
                         @Detail          = @taskDetail,
                         @PriorityId      = 3,              -- High
                         @CompletionRule  = 2,              -- Any assignee completes
                         @AssigneeUserIds = @pmUsers,
                         @UserId          = @InvoiceBy;

                    SET @tasks = @tasks + 1;
                    SET @i = @i + 1;
                END
            END TRY
            BEGIN CATCH
                -- Swallowed: the save already succeeded and must still report success.
                -- @tasks keeps however many were raised before the failure.
                SET @taskError = ERROR_MESSAGE();
            END CATCH

            /* ---- Close the yarn lifecycle tasks this invoice just made obsolete. ----
               "Yarn order <n> placed", "Departure confirmed <n>", "Yarn arriving <n>"
               (all Stage 12) exist to chase yarn that hasn't landed. Once every vendor
               order is invoiced the yarn HAS landed, so leaving them open on /tasks is
               noise the user has to clear by hand.

               Completed at the ASSIGNEE level, not by stamping the parent. The parent's
               status is derived by sp_PoTask_Recompute from its assignee rows, so the
               parent-only write that the modal's "Mark complete" button does (SETSTATUS)
               is undone the next time any assignee touches their own row -- @done is
               still 0, so the rollup drags it back to In progress. Completing the rows
               and letting Recompute settle the parent is the durable form, and it also
               leaves the card reading 2/2 instead of a Completed card at 0/2.

               Held ('H') and cancelled ('X') tasks are skipped, matching Recompute's own
               rule that those two are never auto-changed -- somebody parked them on
               purpose. */
            BEGIN TRY
                DECLARE @closing TABLE (rn INT IDENTITY(1,1) PRIMARY KEY, PoTaskId INT);
                INSERT INTO @closing (PoTaskId)
                SELECT DISTINCT t.[PoTaskId]
                FROM dbo.[PoTask] AS t
                INNER JOIN @orders AS o ON o.order_no = t.[OrderNo]
                WHERE t.[Stage]    = 12          -- Yarn order lifecycle
                  AND t.[IsActive] = 1
                  AND t.[Status] NOT IN ('C', 'X', 'H');

                DECLARE @closeNote NVARCHAR(400) =
                    CONCAT('auto: yarn received, invoice ', @inv, ' on ', @invVyoNo);

                -- History first: the assignee's CURRENT status is the FromStatus, and the
                -- UPDATE below is about to overwrite it.
                INSERT INTO dbo.[PoTaskHistory] ([PoTaskId],[AssigneeId],[FromStatus],[ToStatus],[Note],[ChangedBy])
                SELECT a.[PoTaskId], a.[AssigneeId], a.[Status], 'C', @closeNote, @InvoiceBy
                FROM dbo.[PoTaskAssignee] AS a
                INNER JOIN @closing AS c ON c.PoTaskId = a.[PoTaskId]
                WHERE a.[IsActive] = 1 AND a.[Status] <> 'C';

                UPDATE a
                   SET a.[Status]        = 'C',
                       a.[StartDate]     = ISNULL(a.[StartDate], GETDATE()),
                       a.[CompletedDate] = GETDATE(),
                       a.[Note]          = @closeNote
                FROM dbo.[PoTaskAssignee] AS a
                INNER JOIN @closing AS c ON c.PoTaskId = a.[PoTaskId]
                WHERE a.[IsActive] = 1 AND a.[Status] <> 'C';

                -- Single-owner tasks have no assignee rows at all; Recompute bails out on
                -- those (@total = 0, "status managed directly"), so the parent is set here.
                INSERT INTO dbo.[PoTaskHistory] ([PoTaskId],[FromStatus],[ToStatus],[Note],[ChangedBy])
                SELECT t.[PoTaskId], t.[Status], 'C', @closeNote, @InvoiceBy
                FROM dbo.[PoTask] AS t
                INNER JOIN @closing AS c ON c.PoTaskId = t.[PoTaskId]
                WHERE NOT EXISTS (SELECT 1 FROM dbo.[PoTaskAssignee] a
                                   WHERE a.[PoTaskId] = t.[PoTaskId] AND a.[IsActive] = 1);

                UPDATE t
                   SET t.[Status]        = 'C',
                       t.[CompletedDate] = GETDATE(),
                       t.[ModifiedBy]    = @InvoiceBy,
                       t.[ModifiedDate]  = GETDATE()
                FROM dbo.[PoTask] AS t
                INNER JOIN @closing AS c ON c.PoTaskId = t.[PoTaskId]
                WHERE NOT EXISTS (SELECT 1 FROM dbo.[PoTaskAssignee] a
                                   WHERE a.[PoTaskId] = t.[PoTaskId] AND a.[IsActive] = 1);

                -- Roll each parent up from its now-completed assignee rows.
                DECLARE @k INT = 1, @kn INT = (SELECT COUNT(*) FROM @closing), @closeId INT;
                WHILE @k <= @kn
                BEGIN
                    SELECT @closeId = PoTaskId FROM @closing WHERE rn = @k;
                    EXEC dbo.[sp_PoTask_Recompute] @PoTaskId = @closeId, @ChangedBy = @InvoiceBy;
                    SET @k = @k + 1;
                END

                SET @closed = @kn;
            END TRY
            BEGIN CATCH
                -- Same best-effort rule: the invoice is committed and stays committed.
                SET @taskError = ISNULL(@taskError + N' / ', N'') + ERROR_MESSAGE();
            END CATCH
        END

        DECLARE @stillOpen INT =
            (SELECT COUNT(*) FROM dbo.tbl_yarn_vendor_order
              WHERE yo_id = @invYoId
                AND NULLIF(LTRIM(RTRIM(ISNULL(invoice_no, ''))), '') IS NULL);

        SELECT @invUpdated AS UpdatedCount,
               @headerDone AS HeaderCompleted,
               @tasks      AS TaskCount,
               @closed     AS ClosedTaskCount,
               CONCAT(
                   CASE
                       WHEN @inv IS NULL
                           THEN CONCAT('Invoice cleared on ', @invVyoNo, ' — back to pending.')
                       WHEN @headerDone = 1
                           THEN CONCAT('Invoice ', @inv, ' saved on ', @invVyoNo,
                                       '. Every vendor order received — ', @invYoNo,
                                       ' is completed and ready for planning.')
                       ELSE CONCAT('Invoice ', @inv, ' saved on ', @invVyoNo, '. ',
                                   @stillOpen, ' vendor order(s) still awaiting invoice.')
                   END,
                   CASE WHEN @closed > 0
                        THEN CONCAT(' ', @closed, ' yarn task(s) closed.') ELSE '' END,
                   CASE WHEN @taskError IS NULL THEN ''
                        ELSE CONCAT(' (Task automation issue: ', @taskError, ')') END
               ) AS [Message];
        RETURN;
    END

END
