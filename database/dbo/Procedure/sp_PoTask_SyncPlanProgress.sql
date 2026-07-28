-- Scripted from live DB [NatureKnit] on 2026-07-24 (read-only). Source of truth = database.
-- Object: dbo.sp_PoTask_SyncPlanProgress  (SQL_STORED_PROCEDURE)
CREATE PROCEDURE [dbo].[sp_PoTask_SyncPlanProgress]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @changes TABLE ([PoTaskId] INT, [FromStatus] CHAR(1), [ToStatus] CHAR(1));

    ;WITH flags AS
    (
        SELECT
            t.[PoTaskId],
            t.[Status] AS [CurStatus],
            -- started: any size line of this plan line has a knitter record with pics
            [Started] = CASE WHEN EXISTS (
                SELECT 1
                FROM [dbo].[MasterPlanDetailSize] s WITH (NOLOCK)
                INNER JOIN [dbo].[tbl_knitter_record_data] k WITH (NOLOCK) ON k.[plan_id] = s.[id]
                WHERE s.[MasterPlanDetailId] = t.[RefId] AND k.[pics] IS NOT NULL) THEN 1 ELSE 0 END,
            -- has a fully-returned piece (pics = ret_pic)
            [HasReturned] = CASE WHEN EXISTS (
                SELECT 1
                FROM [dbo].[MasterPlanDetailSize] s WITH (NOLOCK)
                INNER JOIN [dbo].[tbl_knitter_record_data] k WITH (NOLOCK) ON k.[plan_id] = s.[id]
                WHERE s.[MasterPlanDetailId] = t.[RefId] AND k.[pics] IS NOT NULL AND k.[pics] = k.[ret_pic]) THEN 1 ELSE 0 END,
            -- still has an outstanding (not fully returned) piece
            [HasOutstanding] = CASE WHEN EXISTS (
                SELECT 1
                FROM [dbo].[MasterPlanDetailSize] s WITH (NOLOCK)
                INNER JOIN [dbo].[tbl_knitter_record_data] k WITH (NOLOCK) ON k.[plan_id] = s.[id]
                WHERE s.[MasterPlanDetailId] = t.[RefId] AND k.[pics] IS NOT NULL
                  AND (k.[ret_pic] IS NULL OR k.[pics] <> k.[ret_pic])) THEN 1 ELSE 0 END
        FROM [dbo].[PoTask] t WITH (NOLOCK)
        WHERE t.[Stage] = 3
          AND t.[RefId] IS NOT NULL
          AND t.[IsActive] = 1
          AND t.[Status] IN ('S','P')      -- forward-only; leaves C/H/X alone
    )
    UPDATE t
    SET [Status]        = nf.[NewStatus],
        [CompletedDate] = CASE WHEN nf.[NewStatus] = 'C' THEN GETDATE() ELSE t.[CompletedDate] END,
        [ModifiedBy]    = 'system',
        [ModifiedDate]  = GETDATE()
    OUTPUT inserted.[PoTaskId], deleted.[Status], inserted.[Status] INTO @changes
    FROM [dbo].[PoTask] t
    INNER JOIN flags f ON f.[PoTaskId] = t.[PoTaskId]
    CROSS APPLY (SELECT [NewStatus] =
        CASE WHEN f.[HasReturned] = 1 AND f.[HasOutstanding] = 0 THEN 'C'
             WHEN f.[Started] = 1 THEN 'P'
             ELSE f.[CurStatus] END) nf
    WHERE nf.[NewStatus] <> f.[CurStatus];

    -- History for exactly the rows that changed.
    INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[FromStatus],[ToStatus],[Note],[ChangedBy])
    SELECT [PoTaskId], [FromStatus], [ToStatus], 'auto: knitter record', 'system'
    FROM @changes;

    SELECT COUNT(*) AS [Changed] FROM @changes;
END
