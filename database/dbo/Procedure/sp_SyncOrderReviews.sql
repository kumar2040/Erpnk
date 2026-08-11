/* =====================================================================
   sp_SyncOrderReviews — synchronize order master rows and order reviews
   from the MySQL source exposed through linked server PML.

   Orders are synchronized before reviews so the background PO-task sweep
   never sees a newly synchronized review without its corresponding order
   master row. Both inserts heal gaps by comparing source identity values.

   IDENTITY_INSERT is enabled for only one table at a time. The active
   table is tracked so an error cannot leave it enabled for this session.
   ===================================================================== */
CREATE OR ALTER PROCEDURE [dbo].[sp_SyncOrderReviews]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @reviewsPulled int = 0;
    DECLARE @ordersPulled int = 0;
    DECLARE @identityTable varchar(20) = NULL;

    BEGIN TRY
        SET IDENTITY_INSERT dbo.tbl_order ON;
        SET @identityTable = 'order';

        INSERT INTO dbo.tbl_order
        (
            order_id,
            order_no,
            product_name,
            order_buyer,
            order_date,
            order_ldate,
            order_color,
            order_size,
            order_yarn,
            order_status,
            order_unit_price,
            order_pics,
            order_nk,
            order_ms,
            remark,
            xxxs,
            xxs,
            xs,
            s,
            m,
            l,
            xl,
            xxl,
            osfa,
            order_packing,
            [18m],
            [2y],
            [3y],
            [4y],
            [5y],
            [6y],
            [7y],
            tp,
            [8y],
            [9y],
            [10y],
            [11y],
            [12y],
            [14y],
            order_setting,
            xxxl,
            date_u,
            user_od,
            pcfab
        )
        SELECT
            q.order_id,
            q.order_no,
            q.product_name,
            q.order_buyer,
            q.order_date,
            q.order_ldate,
            q.order_color,
            q.order_size,
            q.order_yarn,
            q.order_status,
            q.order_unit_price,
            q.order_pics,
            q.order_nk,
            q.order_ms,
            q.remark,
            q.xxxs,
            q.xxs,
            q.xs,
            q.s,
            q.m,
            q.l,
            q.xl,
            q.xxl,
            q.osfa,
            q.order_packing,
            q.[18m],
            q.[2y],
            q.[3y],
            q.[4y],
            q.[5y],
            q.[6y],
            q.[7y],
            q.tp,
            q.[8y],
            q.[9y],
            q.[10y],
            q.[11y],
            q.[12y],
            q.[14y],
            q.order_setting,
            q.xxxl,
            q.date_u,
            q.user_od,
            q.pcfab
        FROM OPENQUERY
        (
            PML,
            'SELECT order_id,
                    order_no,
                    product_name,
                    order_buyer,
                    CAST(order_date AS DATE) AS order_date,
                    CAST(order_ldate AS DATE) AS order_ldate,
                    order_color,
                    order_size,
                    order_yarn,
                    order_status,
                    order_unit_price,
                    order_pics,
                    order_nk,
                    order_ms,
                    remark,
                    xxxs,
                    xxs,
                    xs,
                    s,
                    m,
                    l,
                    xl,
                    xxl,
                    osfa,
                    order_packing,
                    `18m`,
                    `2y`,
                    `3y`,
                    `4y`,
                    `5y`,
                    `6y`,
                    `7y`,
                    tp,
                    `8y`,
                    `9y`,
                    `10y`,
                    `11y`,
                    `12y`,
                    `14y`,
                    order_setting,
                    xxxl,
                    CAST(date_u AS DATETIME) AS date_u,
                    user_od,
                    pcfab
             FROM db_nature.tbl_order'
        ) AS q
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.tbl_order AS local_order
            WHERE local_order.order_id = q.order_id
        );

        SET @ordersPulled = @@ROWCOUNT;

        SET IDENTITY_INSERT dbo.tbl_order OFF;
        SET @identityTable = NULL;

        SET IDENTITY_INSERT dbo.tbl_order_review ON;
        SET @identityTable = 'review';

        INSERT INTO dbo.tbl_order_review
        (
            id,
            order_no,
            remark,
            date_,
            user_,
            meeting_dash,
            pc
        )
        SELECT
            q.id,
            q.order_no,
            q.remark,
            q.date_,
            q.user_,
            q.meeting_dash,
            q.pc
        FROM OPENQUERY
        (
            PML,
            'SELECT id, order_no, remark, date_, user_, meeting_dash, pc
             FROM db_nature.tbl_order_review'
        ) AS q
        WHERE NOT EXISTS
        (
            SELECT 1
            FROM dbo.tbl_order_review AS local_review
            WHERE local_review.id = q.id
        );

        SET @reviewsPulled = @@ROWCOUNT;

        SET IDENTITY_INSERT dbo.tbl_order_review OFF;
        SET @identityTable = NULL;
    END TRY
    BEGIN CATCH
        IF @identityTable = 'order'
        BEGIN
            BEGIN TRY
                SET IDENTITY_INSERT dbo.tbl_order OFF;
            END TRY
            BEGIN CATCH
            END CATCH;
        END;

        IF @identityTable = 'review'
        BEGIN
            BEGIN TRY
                SET IDENTITY_INSERT dbo.tbl_order_review OFF;
            END TRY
            BEGIN CATCH
            END CATCH;
        END;

        THROW;
    END CATCH;

    -- Keep the first column compatible with SyncOrderReviewsAsync(), which
    -- reads this procedure as a scalar review count.
    SELECT
        @reviewsPulled AS pulled,
        @ordersPulled AS orders_pulled;
END;
