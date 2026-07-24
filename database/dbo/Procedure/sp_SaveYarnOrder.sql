-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.sp_SaveYarnOrder  (SQL_STORED_PROCEDURE)

/* ---------------------------------------------------------------------
   sp_SaveYarnOrder â€” insert one yarn order from a JSON line array.
   @LinesJson: [{ "productId","yarnName","color","ply","orderNo","importKg" }, ...]
   Auto-numbers yo_no as the next 'Natureknit Yarn-NNN'.
   Returns the new yo_no / yo_id / totals.
   --------------------------------------------------------------------- */
CREATE   PROCEDURE dbo.sp_SaveYarnOrder
    @CreatedBy VARCHAR(50)   = NULL,
    @LinesJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    IF ISJSON(@LinesJson) <> 1 OR @LinesJson IS NULL
    BEGIN
        SELECT CAST(NULL AS VARCHAR(30)) AS yo_no, -1 AS yo_id, 0 AS total_kg,
               'Invalid or empty line data.' AS [message];
        RETURN;
    END

    BEGIN TRY
        BEGIN TRAN;

        -- Next sequence number from existing 'Natureknit Yarn-NNN'
        DECLARE @nextNo INT =
            ISNULL((SELECT MAX(TRY_CONVERT(INT, RIGHT(yo_no, 3)))
                    FROM dbo.tbl_yarn_order
                    WHERE yo_no LIKE 'Natureknit Yarn-%'), 0) + 1;

        DECLARE @yoNo VARCHAR(30) =
            'Natureknit Yarn-' + RIGHT('000' + CAST(@nextNo AS VARCHAR(10)), 3);

        DECLARE @total DECIMAL(18,3) =
            (SELECT ISNULL(SUM(CAST(JSON_VALUE(value, '$.importKg') AS DECIMAL(18,3))), 0)
             FROM OPENJSON(@LinesJson));

        DECLARE @orderCnt INT =
            (SELECT COUNT(DISTINCT JSON_VALUE(value, '$.orderNo')) FROM OPENJSON(@LinesJson));

        DECLARE @lineCnt INT =
            (SELECT COUNT(DISTINCT CONCAT(JSON_VALUE(value, '$.productId'), '|', JSON_VALUE(value, '$.color')))
             FROM OPENJSON(@LinesJson));

        INSERT dbo.tbl_yarn_order (yo_no, created_by, total_kg, order_count, line_count, [status])
        VALUES (@yoNo, @CreatedBy, @total, @orderCnt, @lineCnt, 'Placed');

        DECLARE @yoId INT = SCOPE_IDENTITY();

        INSERT dbo.tbl_yarn_order_detail (yo_id, product_id, yarn_name, color, ply, order_no, import_kg)
        SELECT @yoId,
               JSON_VALUE(value, '$.productId'),
               JSON_VALUE(value, '$.yarnName'),
               JSON_VALUE(value, '$.color'),
               JSON_VALUE(value, '$.ply'),
               JSON_VALUE(value, '$.orderNo'),
               CAST(JSON_VALUE(value, '$.importKg') AS DECIMAL(18,3))
        FROM OPENJSON(@LinesJson);

        COMMIT TRAN;

        SELECT @yoNo AS yo_no, @yoId AS yo_id, @total AS total_kg, 'OK' AS [message];
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        SELECT CAST(NULL AS VARCHAR(30)) AS yo_no, -1 AS yo_id, 0 AS total_kg,
               ERROR_MESSAGE() AS [message];
    END CATCH
END
