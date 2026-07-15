USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   Busy/assignment windows per knitter, used to
     - block double-booking (rows whose status <> 'Completed')
     - show assignment/completion state on the master page.
   Returns one row per (knitter, machine plan) with its date range
   and the assignment status.
   ============================================================ */
IF OBJECT_ID('[dbo].[getKnitterBusy]', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[getKnitterBusy] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[getKnitterBusy]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        ka.[card_no]                AS CardNo,
        ka.[knitter_name]           AS KnitterName,
        sz.[MasterPlanDetailId]     AS PlanId,   -- MasterPlanChildId of the plan
        ka.[gauge]                  AS Gauge,
        ka.[machine]                AS Machine,
        ka.[order_id]               AS OrderId,
        ka.[start_date]             AS FromDate,
        ka.[end_date]               AS ToDate,
        ISNULL(ka.[status], 'Assigned') AS [Status]
    FROM [dbo].[KnitterAssignment] ka
    INNER JOIN [dbo].[MasterPlanDetailSize] sz ON sz.[id] = ka.[MasterPlanDetailSizeId]
    WHERE ka.[card_no] IS NOT NULL
      AND ka.[start_date] IS NOT NULL
      AND ka.[end_date]   IS NOT NULL;
END
GO
