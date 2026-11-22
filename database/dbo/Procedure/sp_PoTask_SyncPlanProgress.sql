-- Object: dbo.sp_PoTask_SyncPlanProgress (SQL_STORED_PROCEDURE)
CREATE OR ALTER PROCEDURE [dbo].[sp_PoTask_SyncPlanProgress]
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @changes TABLE ([PoTaskId] INT, [RefId] INT, [FromStatus] CHAR(1), [ToStatus] CHAR(1), [CompletionDate] DATETIME);
    DECLARE @completedPlans TABLE ([PlanId] INT, [CompletionDate] DATETIME);

    ;WITH flags AS
    (
        SELECT
            t.[PoTaskId],
            t.[RefId],
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
                  AND (k.[ret_pic] IS NULL OR k.[pics] <> k.[ret_pic])) THEN 1 ELSE 0 END,
            -- Max actual return date from knitter records
            [MaxKrd] = (
                SELECT MAX(k.[krd])
                FROM [dbo].[MasterPlanDetailSize] s WITH (NOLOCK)
                INNER JOIN [dbo].[tbl_knitter_record_data] k WITH (NOLOCK) ON k.[plan_id] = s.[id]
                WHERE s.[MasterPlanDetailId] = t.[RefId]
            )
        FROM [dbo].[PoTask] t WITH (NOLOCK)
        WHERE t.[Stage] = 3
          AND t.[RefId] IS NOT NULL
          AND t.[IsActive] = 1
          AND t.[Status] IN ('S','P')      -- forward-only; leaves C/H/X alone
    )
    UPDATE t
    SET [Status]        = nf.[NewStatus],
        [CompletedDate] = CASE WHEN nf.[NewStatus] = 'C' THEN ISNULL(f.[MaxKrd], GETDATE()) ELSE t.[CompletedDate] END,
        [ModifiedBy]    = 'system',
        [ModifiedDate]  = GETDATE()
    OUTPUT inserted.[PoTaskId], inserted.[RefId], deleted.[Status], inserted.[Status], inserted.[CompletedDate] INTO @changes
    FROM [dbo].[PoTask] t
    INNER JOIN flags f ON f.[PoTaskId] = t.[PoTaskId]
    CROSS APPLY (SELECT [NewStatus] =
        CASE WHEN f.[HasReturned] = 1 AND f.[HasOutstanding] = 0 THEN 'C'
             WHEN f.[Started] = 1 THEN 'P'
             ELSE f.[CurStatus] END) nf
    WHERE nf.[NewStatus] <> f.[CurStatus];

    -- Collect newly completed plans
    INSERT INTO @completedPlans ([PlanId], [CompletionDate])
    SELECT DISTINCT [RefId], [CompletionDate]
    FROM @changes
    WHERE [ToStatus] = 'C' AND [RefId] IS NOT NULL;

    IF EXISTS (SELECT 1 FROM @completedPlans)
    BEGIN
        -- 1. Free Knitter: Mark KnitterAssignment as Completed & set completed_date
        UPDATE ka
        SET ka.[status]         = 'Completed',
            ka.[completed_date] = ISNULL(cp.[CompletionDate], GETDATE()),
            ka.[end_date]       = CASE WHEN ka.[end_date] > cp.[CompletionDate] THEN cp.[CompletionDate] ELSE ka.[end_date] END
        FROM [dbo].[KnitterAssignment] ka
        INNER JOIN [dbo].[MasterPlanDetailSize] sz ON sz.[id] = ka.[MasterPlanDetailSizeId]
        INNER JOIN @completedPlans cp ON cp.[PlanId] = sz.[MasterPlanDetailId]
        WHERE ISNULL(ka.[status], 'Assigned') <> 'Completed';

        -- 2. Free Machine: Shorten MasterPlanDetail.EndDate to actual completion date (frees Day 3+)
        UPDATE mpd
        SET mpd.[EndDate] = CAST(cp.[CompletionDate] AS DATE)
        FROM [dbo].[MasterPlanDetail] mpd
        INNER JOIN @completedPlans cp ON cp.[PlanId] = mpd.[MasterPlanChildId]
        WHERE mpd.[EndDate] > CAST(cp.[CompletionDate] AS DATE);
    END

    -- History for exactly the rows that changed.
    INSERT INTO [dbo].[PoTaskHistory] ([PoTaskId],[FromStatus],[ToStatus],[Note],[ChangedBy])
    SELECT [PoTaskId], [FromStatus], [ToStatus], 'auto: knitter record completion', 'system'
    FROM @changes;

    SELECT COUNT(*) AS [Changed] FROM @changes;
END
GO
