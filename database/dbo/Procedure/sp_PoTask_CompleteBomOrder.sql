CREATE OR ALTER PROCEDURE [dbo].[sp_PoTask_CompleteBomOrder]
    @PoTaskId int,
    @OrderNo nvarchar(50),
    @Note nvarchar(400) = NULL,
    @UserId nvarchar(450) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE [dbo].[PoTaskOrder]
    SET [Status] = 'C', [CompletedDate] = GETDATE()
    WHERE [PoTaskId] = @PoTaskId AND [OrderNo] = LTRIM(RTRIM(@OrderNo))
      AND [IsActive] = 1 AND [Status] <> 'C';

    DECLARE @OldStatus char(1), @NewStatus char(1);
    SELECT @OldStatus = [Status] FROM [dbo].[PoTask] WHERE [PoTaskId] = @PoTaskId;
    SET @NewStatus = CASE
        WHEN NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskOrder]
                         WHERE [PoTaskId] = @PoTaskId AND [IsActive] = 1 AND [Status] <> 'C') THEN 'C'
        WHEN EXISTS (SELECT 1 FROM [dbo].[PoTaskOrder]
                     WHERE [PoTaskId] = @PoTaskId AND [IsActive] = 1 AND [Status] IN ('P','C')) THEN 'P'
        ELSE 'S' END;

    UPDATE [dbo].[PoTask]
    SET [Status] = @NewStatus,
        [CompletedDate] = CASE WHEN @NewStatus = 'C' THEN GETDATE() ELSE NULL END,
        [ModifiedBy] = ISNULL(@UserId, N'system'), [ModifiedDate] = GETDATE()
    WHERE [PoTaskId] = @PoTaskId AND [Stage] = 2 AND [IsActive] = 1;

    IF @OldStatus <> @NewStatus
        INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId], [FromStatus], [ToStatus], [Note], [ChangedBy])
        VALUES (@PoTaskId, @OldStatus, @NewStatus, ISNULL(@Note, N'BOM order completed'), ISNULL(@UserId, N'system'));

    SELECT @PoTaskId AS [PoTaskId], @NewStatus AS [Status],
           CASE WHEN @NewStatus = 'C' THEN N'All attached BOM orders are complete.'
                ELSE N'Order completed; other attached BOM orders are still pending.' END AS [Message];
END;
