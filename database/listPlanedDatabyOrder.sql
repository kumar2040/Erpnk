USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER PROCEDURE [dbo].[listPlanedDatabyOrder]
    @orderNo NVARCHAR(50),
    @gauge NVARCHAR(50) = NULL,
    @qty DECIMAL(18,2) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        mpd.[MasterPlanChildId] AS [MasterPlanChildId],
        mpd.[StartDate] AS [StartDate],
        mpd.[Guage] AS [Gauge],
        mpd.[factory_type] AS [KnitType],
        mpd.[Machine] AS [Mc],
        mpd.[MachineID] AS [MachineID],
        mpd.[Qty] AS [Quantity],
        mpd.[EndDate] AS [EstEndDate],
        (SELECT TOP 1 sz.[order_id]
           FROM [dbo].[MasterPlanDetailSize] sz
          WHERE sz.[MasterPlanDetailId] = mpd.[MasterPlanChildId]
          ORDER BY sz.[order_id]) AS [order_id]
    FROM [dbo].[MasterPlan] mp
    INNER JOIN [dbo].[MasterPlanDetail] mpd ON mp.[MaterID] = mpd.[MaterID]
    WHERE mp.[OrderNo] = @orderNo
      AND (@gauge IS NULL OR mpd.[Guage] = @gauge)
      AND (@qty IS NULL OR mpd.[Qty] = @qty)
    ORDER BY
        (SELECT TOP 1 sz.[order_id]
           FROM [dbo].[MasterPlanDetailSize] sz
          WHERE sz.[MasterPlanDetailId] = mpd.[MasterPlanChildId]
          ORDER BY sz.[order_id]) ASC,
        mpd.[MachineID] ASC,
        mpd.[StartDate] ASC;
END
GO
