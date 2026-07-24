-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.sp_PoTask_Recompute  (SQL_STORED_PROCEDURE)
CREATE PROCEDURE [dbo].[sp_PoTask_Recompute]
    @PoTaskId  INT,
    @ChangedBy NVARCHAR(450) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @rule TINYINT, @quorum INT, @cur CHAR(1);
    SELECT @rule = [CompletionRule], @quorum = [QuorumCount], @cur = [Status]
    FROM [dbo].[PoTask]
    WHERE [PoTaskId] = @PoTaskId AND [IsActive] = 1;

    IF @cur IS NULL RETURN;            -- not found / inactive
    IF @cur IN ('H','X') RETURN;       -- held / cancelled: never auto-changed

    DECLARE @total INT, @done INT, @started INT;
    SELECT @total   = COUNT(*),
           @done    = SUM(CASE WHEN [Status] = 'C' THEN 1 ELSE 0 END),
           @started = SUM(CASE WHEN [Status] IN ('P','C') THEN 1 ELSE 0 END)
    FROM [dbo].[PoTaskAssignee]
    WHERE [PoTaskId] = @PoTaskId AND [IsActive] = 1;

    IF @total = 0 RETURN;              -- single-owner task: status managed directly

    DECLARE @new CHAR(1);
    IF @rule = 2                       -- Any
        SET @new = CASE WHEN @done >= 1 THEN 'C' WHEN @started >= 1 THEN 'P' ELSE 'S' END;
    ELSE IF @rule = 3                  -- Quorum (fallback to All if no count set)
        SET @new = CASE WHEN @done >= ISNULL(@quorum, @total) THEN 'C' WHEN @started >= 1 THEN 'P' ELSE 'S' END;
    ELSE                               -- All (default)
        SET @new = CASE WHEN @done = @total THEN 'C' WHEN @started >= 1 THEN 'P' ELSE 'S' END;

    IF @new <> @cur
    BEGIN
        UPDATE [dbo].[PoTask]
        SET [Status]        = @new,
            [CompletedDate] = CASE WHEN @new = 'C' THEN GETDATE() ELSE NULL END,
            [ModifiedBy]    = @ChangedBy,
            [ModifiedDate]  = GETDATE()
        WHERE [PoTaskId] = @PoTaskId;

        INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[AssigneeId],[FromStatus],[ToStatus],[Note],[ChangedBy])
        VALUES (@PoTaskId, NULL, @cur, @new, 'rollup', @ChangedBy);
    END
END
