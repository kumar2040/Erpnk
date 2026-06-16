USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   Update the qty of one saved size line.

   Over-allocation guard (server-side): the size's total across
   ALL machine plans of the same order line may not exceed the
   order's size quantity in tbl_order. Excess input is CLAMPED
   to the maximum allowed and reported back.

   Keeps in sync:
     - MasterPlanDetail.Qty  = sum of its size lines
     - KnitterAssignment.qty = the line's new qty (snapshot)

   Returns: Affected, FinalQty, WasClamped, MaxAllowed
   ============================================================ */
IF OBJECT_ID('[dbo].[updateMasterPlanDetailSize]', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[updateMasterPlanDetailSize] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[updateMasterPlanDetailSize]
    @sizeLineId INT,
    @qty        DECIMAL(18,2)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @planId INT, @orderId INT, @size NVARCHAR(20);
    SELECT @planId = [MasterPlanDetailId], @orderId = [order_id], @size = [size]
    FROM [dbo].[MasterPlanDetailSize] WHERE [id] = @sizeLineId;

    IF @planId IS NULL
    BEGIN
        SELECT 0 AS Affected, CAST(0 AS DECIMAL(18,2)) AS FinalQty,
               CAST(0 AS BIT) AS WasClamped, CAST(0 AS DECIMAL(18,2)) AS MaxAllowed;
        RETURN;
    END

    IF @qty < 0 SET @qty = 0;

    DECLARE @wasClamped BIT = 0;
    DECLARE @maxAllowed DECIMAL(18,2) = NULL;

    /* Over-allocation guard: only when the line knows its order + size */
    IF @orderId IS NOT NULL AND @size IS NOT NULL
    BEGIN
        DECLARE @orderSizeQty DECIMAL(18,2) =
        (
            SELECT CASE UPPER(LTRIM(RTRIM(@size)))
                       WHEN 'XXXS' THEN [xxxs]
                       WHEN 'XXS'  THEN [xxs]
                       WHEN 'S'    THEN [s]
                       WHEN 'M'    THEN [m]
                       WHEN 'L'    THEN [l]
                       WHEN 'XL'   THEN [xl]
                       WHEN 'XXL'  THEN [xxl]
                       WHEN 'XXXL' THEN [xxxl]
                       WHEN 'OSFA' THEN [osfa]
                       ELSE NULL
                   END
            FROM [dbo].[tbl_order]
            WHERE [order_id] = @orderId
        );

        IF @orderSizeQty IS NOT NULL
        BEGIN
            -- Same order line + size already allocated on OTHER plans/lines.
            DECLARE @othersQty DECIMAL(18,2) = ISNULL((
                SELECT SUM([qty])
                FROM [dbo].[MasterPlanDetailSize]
                WHERE [order_id] = @orderId
                  AND UPPER(LTRIM(RTRIM([size]))) = UPPER(LTRIM(RTRIM(@size)))
                  AND [id] <> @sizeLineId
            ), 0);

            SET @maxAllowed = @orderSizeQty - @othersQty;
            IF @maxAllowed < 0 SET @maxAllowed = 0;

            IF @qty > @maxAllowed
            BEGIN
                SET @qty = @maxAllowed;   -- clamp to the maximum allowed
                SET @wasClamped = 1;
            END
        END
    END

    BEGIN TRANSACTION;

    UPDATE [dbo].[MasterPlanDetailSize] SET [qty] = @qty WHERE [id] = @sizeLineId;

    -- Keep the machine plan total in sync with its size lines.
    UPDATE mpd
    SET mpd.[Qty] = ISNULL((SELECT SUM(sz.[qty]) FROM [dbo].[MasterPlanDetailSize] sz WHERE sz.[MasterPlanDetailId] = @planId), 0)
    FROM [dbo].[MasterPlanDetail] mpd
    WHERE mpd.[MasterPlanChildId] = @planId;

    -- Keep the knitter assignment snapshot in sync (if assigned).
    UPDATE [dbo].[KnitterAssignment] SET [qty] = @qty WHERE [MasterPlanDetailSizeId] = @sizeLineId;

    COMMIT TRANSACTION;

    SELECT 1 AS Affected, @qty AS FinalQty, @wasClamped AS WasClamped,
           ISNULL(@maxAllowed, @qty) AS MaxAllowed;
END
GO
