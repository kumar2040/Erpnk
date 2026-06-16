USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   CEO Planing Report: per-day factory load.
   For each day in [@fromDate, @toDate] returns how many machines
   are busy (occupied by an active plan that day), the day's load
   qty, and the factory ceilings (total machines, total distinct
   knitters). Utilisation is computed in the app against KNITTERS,
   since knitters (< machines) are the real capacity ceiling.
   ============================================================ */
IF OBJECT_ID('[dbo].[sp_GetPlaningReport]', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[sp_GetPlaningReport] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[sp_GetPlaningReport]
    @fromDate DATE = NULL,
    @toDate   DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @fromDate IS NULL SET @fromDate = CAST(GETDATE() AS DATE);
    IF @toDate   IS NULL SET @toDate   = DATEADD(DAY, 41, @fromDate); -- ~6 week calendar

    -- Factory-wide ceilings (constants for the range)
    DECLARE @TotalMachines INT = (SELECT COUNT(*) FROM KnitMachine WHERE Gauge IS NOT NULL);
    DECLARE @TotalKnitters INT = (SELECT COUNT(DISTINCT CardNo) FROM KnittersGauges WHERE Gauge IS NOT NULL);

    ;WITH Days AS (
        SELECT @fromDate AS d
        UNION ALL
        SELECT DATEADD(DAY, 1, d) FROM Days WHERE d < @toDate
    )
    SELECT
        d AS [Date],
        -- Busy machines: distinct real machines (knit rows have MachineID) plus the
        -- machine-count for rows without an id (weave/silk allocations).
        ISNULL((
            SELECT COUNT(DISTINCT mpd.MachineID)
            FROM dbo.MasterPlanDetail mpd
            WHERE mpd.MachineID IS NOT NULL
              AND d BETWEEN CAST(mpd.StartDate AS DATE) AND CAST(mpd.EndDate AS DATE)
        ), 0)
        + ISNULL((
            SELECT SUM(mpd.MachineCount)
            FROM dbo.MasterPlanDetail mpd
            WHERE mpd.MachineID IS NULL
              AND d BETWEEN CAST(mpd.StartDate AS DATE) AND CAST(mpd.EndDate AS DATE)
        ), 0) AS BusyMachines,

        ISNULL((
            SELECT SUM(mpd.Qty)
            FROM dbo.MasterPlanDetail mpd
            WHERE d BETWEEN CAST(mpd.StartDate AS DATE) AND CAST(mpd.EndDate AS DATE)
        ), 0) AS LoadQty,

        @TotalMachines AS TotalMachines,
        @TotalKnitters AS TotalKnitters,
        DATENAME(WEEKDAY, d) AS DayName,
        CASE WHEN DATENAME(WEEKDAY, d) = 'Saturday' THEN 1 ELSE 0 END AS IsSaturday
    FROM Days
    ORDER BY d
    OPTION (MAXRECURSION 1000);
END
GO
