CREATE OR ALTER PROCEDURE [dbo].[sp_PoTask_MergeBomTask]
    @SourcePoTaskId int,
    @TargetPoTaskId int,
    @UserId nvarchar(450) = NULL,
    @MaxOrders int = 5
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    IF @SourcePoTaskId = @TargetPoTaskId
    BEGIN
        RAISERROR('Source and target BOM tasks must be different.', 16, 1);
        RETURN;
    END;

    BEGIN TRANSACTION;

    IF (SELECT COUNT(*) FROM [dbo].[PoTask] WITH (UPDLOCK, HOLDLOCK)
        WHERE [PoTaskId] IN (@SourcePoTaskId, @TargetPoTaskId)
          AND [Stage] = 2 AND [Status] = 'S' AND [IsActive] = 1) <> 2
    BEGIN
        RAISERROR('Both BOM tasks must be active, Scheduled, and unstarted.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    IF EXISTS (SELECT 1 FROM [dbo].[PoTaskAssignee] WITH (HOLDLOCK)
               WHERE [PoTaskId] IN (@SourcePoTaskId, @TargetPoTaskId)
                 AND [IsActive] = 1
                 AND ([Status] IN ('P','C') OR [StartDate] IS NOT NULL))
    BEGIN
        RAISERROR('A BOM task has already started and cannot be merged.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    -- Materialize legacy primary orders before counting and moving membership.
    INSERT INTO [dbo].[PoTaskOrder] ([PoTaskId], [OrderNo], [Status], [AddedBy])
    SELECT t.[PoTaskId], LTRIM(RTRIM(t.[OrderNo])), 'S', ISNULL(@UserId, N'system')
    FROM [dbo].[PoTask] t
    WHERE t.[PoTaskId] IN (@SourcePoTaskId, @TargetPoTaskId)
      AND NULLIF(LTRIM(RTRIM(t.[OrderNo])), N'') IS NOT NULL
      AND NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskOrder] o
                      WHERE o.[OrderNo] = LTRIM(RTRIM(t.[OrderNo])) AND o.[IsActive] = 1);

    DECLARE @TargetCount int = (SELECT COUNT(*) FROM [dbo].[PoTaskOrder]
                                WHERE [PoTaskId] = @TargetPoTaskId AND [IsActive] = 1),
            @SourceCount int = (SELECT COUNT(*) FROM [dbo].[PoTaskOrder]
                                WHERE [PoTaskId] = @SourcePoTaskId AND [IsActive] = 1);

    IF @TargetCount + @SourceCount > ISNULL(NULLIF(@MaxOrders, 0), 5)
    BEGIN
        RAISERROR('The merged BOM task would exceed its order limit.', 16, 1);
        ROLLBACK TRANSACTION;
        RETURN;
    END;

    UPDATE [dbo].[PoTaskOrder]
    SET [PoTaskId] = @TargetPoTaskId
    WHERE [PoTaskId] = @SourcePoTaskId AND [IsActive] = 1;

    UPDATE [dbo].[PoTask]
    SET [Status] = 'X', [IsActive] = 0,
        [ModifiedBy] = ISNULL(@UserId, N'system'), [ModifiedDate] = GETDATE()
    WHERE [PoTaskId] = @SourcePoTaskId;

    INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId], [FromStatus], [ToStatus], [Note], [ChangedBy])
    VALUES (@SourcePoTaskId, 'S', 'X',
            N'Merged into BOM task ' + CONVERT(nvarchar(20), @TargetPoTaskId),
            ISNULL(@UserId, N'system'));

    COMMIT TRANSACTION;

    SELECT @TargetPoTaskId AS [PoTaskId],
           (SELECT COUNT(*) FROM [dbo].[PoTaskOrder]
            WHERE [PoTaskId] = @TargetPoTaskId AND [IsActive] = 1) AS [OrderCount],
           N'BOM tasks merged.' AS [Message];
END;
