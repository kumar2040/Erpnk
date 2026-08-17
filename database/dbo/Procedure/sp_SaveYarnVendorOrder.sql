-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.sp_SaveYarnVendorOrder  (SQL_STORED_PROCEDURE)

/* ---------------------------------------------------------------------
   sp_SaveYarnVendorOrder — place one vendor sub-order under a parent.
   @LinesJson: [{ "productId","yarnName","color","ply","orderNo","importKg" }, ...]
   vyo_no = '<parent yo_no>-V<n>' where n is the next sub-order for that parent.
   --------------------------------------------------------------------- */
CREATE   PROCEDURE dbo.sp_SaveYarnVendorOrder
    @YoId      INT,
    @Vendor    VARCHAR(150) = NULL,
    @CreatedBy VARCHAR(50)  = NULL,
    @LinesJson NVARCHAR(MAX)
AS
BEGIN
    SET NOCOUNT ON;

    IF ISJSON(@LinesJson) <> 1 OR @LinesJson IS NULL
    BEGIN
        SELECT CAST(NULL AS VARCHAR(40)) AS vyo_no, -1 AS vyo_id, 0 AS total_kg,
               'Invalid or empty line data.' AS [message];
        RETURN;
    END

    DECLARE @parentNo VARCHAR(30) = (SELECT yo_no FROM dbo.tbl_yarn_order WHERE yo_id = @YoId);
    IF @parentNo IS NULL
    BEGIN
        SELECT CAST(NULL AS VARCHAR(40)) AS vyo_no, -1 AS vyo_id, 0 AS total_kg,
               'Parent yarn order not found.' AS [message];
        RETURN;
    END

    BEGIN TRY
        BEGIN TRAN;

        DECLARE @nextN INT =
            (SELECT COUNT(*) FROM dbo.tbl_yarn_vendor_order WHERE yo_id = @YoId) + 1;
        DECLARE @vyoNo VARCHAR(40) = @parentNo + '-V' + CAST(@nextN AS VARCHAR(10));

        DECLARE @total DECIMAL(18,3) =
            (SELECT ISNULL(SUM(CAST(JSON_VALUE(value, '$.importKg') AS DECIMAL(18,3))), 0)
             FROM OPENJSON(@LinesJson));
        DECLARE @lineCnt INT = (SELECT COUNT(*) FROM OPENJSON(@LinesJson));

        INSERT dbo.tbl_yarn_vendor_order (yo_id, vyo_no, vendor, created_by, total_kg, line_count, [status])
        VALUES (@YoId, @vyoNo, @Vendor, @CreatedBy, @total, @lineCnt, 'Placed');

        DECLARE @vyoId INT = SCOPE_IDENTITY();

        INSERT dbo.tbl_yarn_vendor_order_detail (vyo_id, product_id, yarn_name, color, ply, order_no, import_kg)
        SELECT @vyoId,
               JSON_VALUE(value, '$.productId'),
               JSON_VALUE(value, '$.yarnName'),
               JSON_VALUE(value, '$.color'),
               JSON_VALUE(value, '$.ply'),
               JSON_VALUE(value, '$.orderNo'),
               CAST(JSON_VALUE(value, '$.importKg') AS DECIMAL(18,3))
        FROM OPENJSON(@LinesJson);

        -- Maintain Stage 12 Yarn Task as In Progress ('P') when vendor order is placed
        DECLARE @poTaskId INT = (SELECT TOP (1) [PoTaskId] FROM dbo.[PoTask] WHERE [Stage] = 12 AND [RefId] = @YoId AND [IsActive] = 1 ORDER BY [PoTaskId] DESC);
        IF @poTaskId IS NOT NULL
        BEGIN
            UPDATE dbo.[PoTask]
               SET [Status] = 'P',
                   [ModifiedBy] = ISNULL(@CreatedBy, 'system'),
                   [ModifiedDate] = GETDATE()
             WHERE [PoTaskId] = @poTaskId AND [Status] NOT IN ('C', 'X');

            UPDATE dbo.[PoTaskAssignee]
               SET [Status] = 'P'
             WHERE [PoTaskId] = @poTaskId AND [IsActive] = 1 AND [Status] NOT IN ('C', 'X');

            INSERT INTO dbo.[PoTaskHistory] ([PoTaskId], [FromStatus], [ToStatus], [Note], [ChangedBy])
            VALUES (@poTaskId, 'P', 'P', N'Vendor order ' + @vyoNo + N' placed.', ISNULL(@CreatedBy, 'system'));
        END;

        COMMIT TRAN;

        SELECT @vyoNo AS vyo_no, @vyoId AS vyo_id, @total AS total_kg, 'OK' AS [message];
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRAN;
        SELECT CAST(NULL AS VARCHAR(40)) AS vyo_no, -1 AS vyo_id, 0 AS total_kg,
               ERROR_MESSAGE() AS [message];
    END CATCH
END
