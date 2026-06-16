USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   Assignment history (audit) for the master planning page:
   one row per machine plan + knitter, newest first.
   @days = how far back to look (default 30).
   ============================================================ */
IF OBJECT_ID('[dbo].[getKnitterAssignmentHistory]', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[getKnitterAssignmentHistory] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[getKnitterAssignmentHistory]
    @days INT = 30
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        sz.[MasterPlanDetailId]          AS PlanId,
        MAX(ka.[order_id])               AS OrderId,
        MAX(ka.[gauge])                  AS Gauge,
        MAX(ka.[machine])                AS Machine,
        ka.[card_no]                     AS CardNo,
        MAX(ka.[knitter_name])           AS KnitterName,
        SUM(ISNULL(ka.[qty], 0))         AS Qty,
        MIN(ka.[start_date])             AS StartDate,
        MAX(ka.[end_date])               AS EndDate,
        MAX(ISNULL(ka.[status], 'Assigned')) AS [Status],
        MAX(ka.[assigned_by])            AS AssignedBy,
        MAX(ka.[assigned_date])          AS AssignedDate,
        MAX(ka.[completed_date])         AS CompletedDate
    FROM [dbo].[KnitterAssignment] ka
    INNER JOIN [dbo].[MasterPlanDetailSize] sz ON sz.[id] = ka.[MasterPlanDetailSizeId]
    WHERE ka.[assigned_date] >= DATEADD(DAY, -@days, GETDATE())
    GROUP BY sz.[MasterPlanDetailId], ka.[card_no]
    ORDER BY MAX(ka.[assigned_date]) DESC;
END
GO
