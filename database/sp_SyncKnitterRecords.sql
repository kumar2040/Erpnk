USE [NatureKnit]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =========================================================================
-- sp_SyncKnitterRecords
-- Incrementally pulls NEW rows from MySQL (linked server MYSQL_NatureKnit)
-- into SQL Server, using a high-water-mark (MAX local id) so already-synced
-- rows are never re-inserted (no duplicates).
--   tbl_knitter_record_data : key column d_id
--   tbl_knitter_record      : key column kr_id
--
-- An app-lock serializes concurrent callers so two requests can't both read
-- the same MAX and insert the same rows. Returns the rows inserted per table.
--
-- NOTE: assumes d_id / kr_id are plain columns (the existing manual INSERTs set
-- them explicitly). If they are IDENTITY columns, IDENTITY_INSERT handling is
-- needed — tell me and I'll add it.
-- =========================================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_SyncKnitterRecords]
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @insData INT = 0, @insRec INT = 0, @lastId BIGINT, @sql NVARCHAR(MAX);

    -- Serialize concurrent syncs (LockTimeout 0 => skip if another sync is running).
    DECLARE @lock INT;
    EXEC @lock = sp_getapplock @Resource = N'sync_knitter_records',
                               @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 0;
    IF @lock < 0
    BEGIN
        SELECT 0 AS InsertedData, 0 AS InsertedRecord, CAST(0 AS BIT) AS Ran, 'Sync already running.' AS [Message];
        RETURN;
    END

    BEGIN TRY
        -- 1) tbl_knitter_record_data  (watermark = MAX(d_id))
        SELECT @lastId = ISNULL(MAX(d_id), 0) FROM dbo.tbl_knitter_record_data;
        SET @sql = N'
            INSERT INTO dbo.tbl_knitter_record_data
                (d_id, r_id, knitter, cone_id, pics, status, knd, krd, cone_wt, order_id, req_wt,
                 barcode, ret_pic, forward, ret_wt, r_status, for_pics, p_typ, will_ret_daate, plan_id, setting_pc)
            SELECT * FROM OPENQUERY(MYSQL_NatureKnit,
                ''SELECT d_id, r_id, knitter, cone_id, pics, status, CAST(knd AS DATE), CAST(krd AS DATE),
                         cone_wt, order_id, req_wt, barcode, ret_pic, forward, ret_wt, r_status, for_pics,
                         p_typ, CAST(will_ret_daate AS DATETIME), plan_id, setting_pc
                  FROM db_natureknit.tbl_knitter_record_data
                  WHERE d_id > ' + CAST(@lastId AS NVARCHAR(20)) + N' ORDER BY d_id'')';
        EXEC sp_executesql @sql;
        SET @insData = @@ROWCOUNT;

        -- 2) tbl_knitter_record  (watermark = MAX(kr_id))
        SELECT @lastId = ISNULL(MAX(kr_id), 0) FROM dbo.tbl_knitter_record;
        SET @sql = N'
            INSERT INTO dbo.tbl_knitter_record
                (kr_id, knitter_id, po, style_no, kpics, color, size, kcone_id, cone_wt, kr_status,
                 machine_no, [return], order_id, flag, i_time, date_ty)
            SELECT * FROM OPENQUERY(MYSQL_NatureKnit,
                ''SELECT kr_id, knitter_id, po, style_no, kpics, color, size, kcone_id, cone_wt, kr_status,
                         machine_no, `return`, order_id, flag, i_time, CAST(date_ty AS DATETIME)
                  FROM db_natureknit.tbl_knitter_record
                  WHERE kr_id > ' + CAST(@lastId AS NVARCHAR(20)) + N' ORDER BY kr_id'')';
        EXEC sp_executesql @sql;
        SET @insRec = @@ROWCOUNT;

        EXEC sp_releaseapplock @Resource = N'sync_knitter_records', @LockOwner = N'Session';

        SELECT @insData AS InsertedData, @insRec AS InsertedRecord, CAST(1 AS BIT) AS Ran,
               'OK' AS [Message];
    END TRY
    BEGIN CATCH
        EXEC sp_releaseapplock @Resource = N'sync_knitter_records', @LockOwner = N'Session';
        ;THROW;
    END CATCH
END
GO

PRINT 'sp_SyncKnitterRecords created.';
GO
