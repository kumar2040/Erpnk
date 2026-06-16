USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   Master Planning view: joins MasterPlan + MasterPlanDetail +
   MasterPlanDetailSize and pivots the size rows into columns,
   one row per (machine plan + style + color).
   Used by the "for Master Planning" page where a Master assigns
   knitters to each machine allocation.
   ============================================================ */
IF OBJECT_ID('[dbo].[sp_GetMasterPlanning]', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[sp_GetMasterPlanning] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[sp_GetMasterPlanning]
    @orderNo NVARCHAR(50) = NULL,
    @gauge   NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        mp.[OrderNo]                AS [Order],
        mpd.[Guage]                 AS [Guage],
        mpd.[Machine]               AS [Machine],
        mpd.[MachineID]             AS [MachineID],
        sz.[style_no]               AS [Style],
        sz.[color]                  AS [Color],
        SUM(CASE WHEN sz.[size] = 'XXXS' THEN sz.[qty] ELSE 0 END) AS [XXXS],
        SUM(CASE WHEN sz.[size] = 'XXS'  THEN sz.[qty] ELSE 0 END) AS [XXS],
        SUM(CASE WHEN sz.[size] = 'XS'   THEN sz.[qty] ELSE 0 END) AS [XS],
        SUM(CASE WHEN sz.[size] = 'S'    THEN sz.[qty] ELSE 0 END) AS [S],
        SUM(CASE WHEN sz.[size] = 'M'    THEN sz.[qty] ELSE 0 END) AS [M],
        SUM(CASE WHEN sz.[size] = 'L'    THEN sz.[qty] ELSE 0 END) AS [L],
        SUM(CASE WHEN sz.[size] = 'XL'   THEN sz.[qty] ELSE 0 END) AS [XL],
        SUM(CASE WHEN sz.[size] = 'XXL'  THEN sz.[qty] ELSE 0 END) AS [XXL],
        SUM(CASE WHEN sz.[size] = 'XXXL' THEN sz.[qty] ELSE 0 END) AS [XXXL],
        SUM(CASE WHEN sz.[size] = 'OSFA' THEN sz.[qty] ELSE 0 END) AS [OSFA],
        mpd.[StartDate]             AS [StartDate],
        mpd.[EndDate]               AS [EndDate],
        mpd.[MasterPlanChildId]     AS [PlanID],
        sz.[order_id]               AS [ORDER_id]
    FROM [dbo].[MasterPlan] mp
    INNER JOIN [dbo].[MasterPlanDetail] mpd
        ON mp.[MaterID] = mpd.[MaterID]
    INNER JOIN [dbo].[MasterPlanDetailSize] sz
        ON sz.[MasterPlanDetailId] = mpd.[MasterPlanChildId]
    WHERE (@orderNo IS NULL OR mp.[OrderNo] = @orderNo)
      AND (@gauge   IS NULL OR mpd.[Guage]  = @gauge)
    GROUP BY
        mp.[OrderNo],
        mpd.[Guage],
        mpd.[Machine],
        mpd.[MachineID],
        sz.[style_no],
        sz.[color],
        mpd.[StartDate],
        mpd.[EndDate],
        mpd.[MasterPlanChildId],
        sz.[order_id]
    ORDER BY
        mp.[OrderNo],
        mpd.[MachineID],
        sz.[style_no],
        sz.[color];
END
GO
