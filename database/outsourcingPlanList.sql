USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

-- =========================================================================
-- Author:      Antigravity
-- Create date: 2026-06-03
-- Description: Gets list of outsourced plans based on thirdparty (Gauge)
--              and flag parameter.
-- =========================================================================
CREATE PROCEDURE [dbo].[outsourcingPlanList]
    @thirdparty NVARCHAR(50),
    @flag INT = 0
AS
BEGIN
    SET NOCOUNT ON;

    -- Query joining MasterPlan and MasterPlanDetail filtered by Gauge (Third Party)
    SELECT 
        mp.[MaterID],
        mp.[OrderNo],
        mp.[OrderType],
        mp.[ProductionType],
        mp.[PlanStartDate] AS [MasterPlanStartDate],
        mp.[OrderStatus],
        mp.[PlanWorkingStatus],
        mp.[EntryDate] AS [MasterEntryDate],
        mp.[CreatedBy] AS [MasterCreatedBy],
        
        mpd.[MasterPlanChildId],
        mpd.[Guage] AS [Gauge],
        mpd.[StartDate] AS [DetailStartDate],
        mpd.[EndDate] AS [DetailEndDate],
        mpd.[Machine],
        mpd.[PlaningStatus],
        mpd.[EntryDate] AS [DetailEntryDate],
        mpd.[CreatedBy] AS [DetailCreatedBy],
        mpd.[Qty],
        mpd.[MachineCount]
    FROM [dbo].[MasterPlan] mp
    INNER JOIN [dbo].[MasterPlanDetail] mpd ON mp.[MaterID] = mpd.[MaterID]
    WHERE mpd.[Guage] = @thirdparty
    ORDER BY mpd.[StartDate] ASC;
END
GO
