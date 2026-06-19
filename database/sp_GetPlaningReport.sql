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

        -- Per-day planned qty: each plan's Qty spread across its WORKING days
        -- (Saturdays excluded unless that plan has WorkSaturday=1). Overtime is
        -- already reflected in the plan's End date, so a shorter span => more/day.
        -- A plan only contributes on a day it actually works.
        -- ('1900-01-06' is a Saturday; the /7 difference counts Saturdays in a span.)
        ISNULL((
            SELECT SUM(
                CAST(mpd.Qty AS DECIMAL(18,4)) /
                NULLIF(
                    CASE WHEN ISNULL(mpd.WorkSaturday, 0) = 1
                         THEN DATEDIFF(DAY, CAST(mpd.StartDate AS DATE), CAST(mpd.EndDate AS DATE)) + 1
                         ELSE (DATEDIFF(DAY, CAST(mpd.StartDate AS DATE), CAST(mpd.EndDate AS DATE)) + 1)
                              - ( DATEDIFF(DAY, '1900-01-06', CAST(mpd.EndDate AS DATE)) / 7
                                  - DATEDIFF(DAY, '1900-01-06', DATEADD(DAY, -1, CAST(mpd.StartDate AS DATE))) / 7 )
                    END, 0)
            )
            FROM dbo.MasterPlanDetail mpd
            WHERE d BETWEEN CAST(mpd.StartDate AS DATE) AND CAST(mpd.EndDate AS DATE)
              AND ( DATENAME(WEEKDAY, d) <> 'Saturday' OR ISNULL(mpd.WorkSaturday, 0) = 1 )
        ), 0) AS LoadQty,

        -- Actual knitted pieces received on this calendar day (item count, weight > 0).
        ISNULL((
            SELECT COUNT(r.item_no)
            FROM tbl_knitter_recieved r
            WHERE CAST(r.r_date AS DATE) = d
              AND r.r_wt > 0
        ), 0) AS KnittedPC,

        -- Orders whose ship date (order_ldate) falls on this calendar day.
        ISNULL((
            SELECT COUNT(DISTINCT o.order_no)
            FROM tbl_order o
            WHERE CAST(o.order_ldate AS DATE) = d
        ), 0) AS ShipCount,

        ISNULL(STUFF((
            SELECT ', ' + o.order_no
            FROM tbl_order o
            WHERE CAST(o.order_ldate AS DATE) = d
            GROUP BY o.order_no
            ORDER BY o.order_no
            FOR XML PATH(''), TYPE
        ).value('.', 'NVARCHAR(MAX)'), 1, 2, ''), '') AS ShipOrders,

        @TotalMachines AS TotalMachines,
        @TotalKnitters AS TotalKnitters,
        DATENAME(WEEKDAY, d) AS DayName,
        CASE WHEN DATENAME(WEEKDAY, d) = 'Saturday' THEN 1 ELSE 0 END AS IsSaturday
    FROM Days
    ORDER BY d
    OPTION (MAXRECURSION 1000);
END
GO
