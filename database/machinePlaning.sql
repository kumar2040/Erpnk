USE [NatureKnit]
GO

/****** Object:  StoredProcedure [dbo].[machinePlaning]    Script Date: 6/9/2026 3:34:33 PM ******/
SET ANSI_NULLS ON
GO

SET QUOTED_IDENTIFIER ON
GO

CREATE OR ALTER PROCEDURE [dbo].[machinePlaning]
    @TargetGauge NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @Today DATE = CAST(GETDATE() AS DATE);
    DECLARE @CleanGauge FLOAT = NULL;

    -- Clean the gauge string input to parse numeric float
    IF @TargetGauge IS NOT NULL AND LTRIM(RTRIM(@TargetGauge)) <> ''
    BEGIN
        SET @CleanGauge = TRY_CAST(REPLACE(REPLACE(REPLACE(@TargetGauge, 'GG', ''), 'G', ''), ' ', '') AS FLOAT);
    END

    -- 1. Calculate active capacity for each gauge (knitters vs machines limit)
    ;WITH GaugeCapacity AS (
        SELECT
            G.Gauge,
            ISNULL(M.TotalMachines, 0) AS TotalMachines,
            ISNULL(K.AvailableKnitters, 0) AS AvailableKnitters,
            CASE
                WHEN ISNULL(M.TotalMachines, 0) < ISNULL(K.AvailableKnitters, 0) THEN ISNULL(M.TotalMachines, 0)
                ELSE ISNULL(K.AvailableKnitters, 0)
            END AS ActiveCapacity
        FROM (
            SELECT DISTINCT TRY_CAST(REPLACE(REPLACE(REPLACE(Gauge, 'GG', ''), 'G', ''), ' ', '') AS FLOAT) AS Gauge
            FROM KnitMachine
            WHERE Gauge IS NOT NULL
            UNION
            SELECT DISTINCT TRY_CAST(REPLACE(REPLACE(REPLACE(Gauge, 'GG', ''), 'G', ''), ' ', '') AS FLOAT) AS Gauge
            FROM KnittersGauges
            WHERE Gauge IS NOT NULL
        ) G
        LEFT JOIN (
            SELECT
                TRY_CAST(REPLACE(REPLACE(REPLACE(Gauge, 'GG', ''), 'G', ''), ' ', '') AS FLOAT) as Gauge,
                COUNT(MachineNo) as TotalMachines
            FROM KnitMachine
            WHERE Gauge IS NOT NULL
            GROUP BY TRY_CAST(REPLACE(REPLACE(REPLACE(Gauge, 'GG', ''), 'G', ''), ' ', '') AS FLOAT)
        ) M ON G.Gauge = M.Gauge
        LEFT JOIN (
            SELECT
                TRY_CAST(REPLACE(REPLACE(REPLACE(Gauge, 'GG', ''), 'G', ''), ' ', '') AS FLOAT) as Gauge,
                COUNT(DISTINCT CardNo) as AvailableKnitters
            FROM KnittersGauges
            WHERE Gauge IS NOT NULL
            GROUP BY TRY_CAST(REPLACE(REPLACE(REPLACE(Gauge, 'GG', ''), 'G', ''), ' ', '') AS FLOAT)
        ) K ON G.Gauge = K.Gauge
        WHERE G.Gauge IS NOT NULL
    ),

    -- 2. Retrieve all active plans for knitting (robustly mapping machine IDs)
    ActivePlans AS (
        SELECT
            TRY_CAST(REPLACE(REPLACE(REPLACE(mpd.Guage, 'GG', ''), 'G', ''), ' ', '') AS FLOAT) AS Gauge,
            CAST(mpd.EndDate AS DATE) AS EndDate,
            COALESCE(
                mpd.machineID,
                TRY_CAST(mpd.Machine AS INT),
                (SELECT TOP 1 km.Machine_ID FROM dbo.KnitMachine km WHERE km.MachineNo = mpd.Machine)
            ) AS MachineID
        FROM dbo.MasterPlanDetail mpd
        WHERE mpd.EndDate >= @Today
          AND (mpd.factory_type IS NULL OR LTRIM(RTRIM(LOWER(mpd.factory_type))) = 'knit' OR LTRIM(RTRIM(mpd.factory_type)) = '')
    ),

    -- 3. Generate a 120-day timeline forward to project capacity utilization
    Numbers AS (
        SELECT TOP 120 ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS n
        FROM sys.all_objects a
        CROSS JOIN sys.all_objects b
    ),
    TimelineDates AS (
        SELECT DATEADD(day, n, @Today) AS d
        FROM Numbers
    ),

    -- 4. Calculate active plans count for each day in the timeline.
    DailyActive AS (
        SELECT
            gc.Gauge,
            td.d AS DateVal,
            gc.ActiveCapacity,
            ISNULL(COUNT(ap.Gauge), 0) AS ActivePlansCount
        FROM GaugeCapacity gc
        CROSS JOIN TimelineDates td
        LEFT JOIN ActivePlans ap ON gc.Gauge = ap.Gauge
            AND td.d < ap.EndDate
        GROUP BY gc.Gauge, td.d, gc.ActiveCapacity
    ),

    -- 5. Find the earliest date for each gauge where capacity becomes available
    GaugeFreeDates AS (
        SELECT
            Gauge,
            MIN(DateVal) AS GaugeFreeDate
        FROM DailyActive
        WHERE ActivePlansCount < ActiveCapacity OR ActiveCapacity IS NULL OR ActiveCapacity = 0
        GROUP BY Gauge
    )

    -- 6. Select all machines and enrich with their planning details and effective capacity-based free dates
    SELECT
        km.Machine_ID,
        km.MachineNo,
        km.Gauge,
        km.Size,
        -- The effective FreeDate is the later of:
        -- a) The machine's physical release date (LatestPlan.EndDate)
        -- b) The gauge's earliest available labor capacity date (gfd.GaugeFreeDate)
        CASE
            WHEN ISNULL(LatestPlan.EndDate, @Today) < ISNULL(gfd.GaugeFreeDate, @Today)
            THEN ISNULL(gfd.GaugeFreeDate, @Today)
            ELSE ISNULL(LatestPlan.EndDate, @Today)
        END AS FreeDate,
        -- Status is FREE if it can accept a plan today (i.e. FreeDate <= @Today), otherwise BUSY
        CASE
            WHEN CASE
                WHEN ISNULL(LatestPlan.EndDate, @Today) < ISNULL(gfd.GaugeFreeDate, @Today)
                THEN ISNULL(gfd.GaugeFreeDate, @Today)
                ELSE ISNULL(LatestPlan.EndDate, @Today)
            END <= @Today THEN 'FREE'
            ELSE 'BUSY'
        END AS [Status],
        LatestPlan.OrderNo,
        -- Current Workload = the active plan qty for THIS machine only (not the whole gauge).
        ISNULL((
            SELECT SUM(mpd_m.Qty)
            FROM dbo.MasterPlanDetail mpd_m
            WHERE (
                    mpd_m.machineID = km.Machine_ID
                    OR (mpd_m.machineID IS NULL AND TRY_CAST(mpd_m.Machine AS INT) = km.Machine_ID)
                    OR (mpd_m.machineID IS NULL AND mpd_m.Machine = km.MachineNo)
                  )
              AND mpd_m.EndDate >= @Today
              AND (mpd_m.factory_type IS NULL OR LTRIM(RTRIM(LOWER(mpd_m.factory_type))) = 'knit' OR LTRIM(RTRIM(mpd_m.factory_type)) = '')
        ), 0) AS PlannedQty,
        LatestPlan.PlaningStatus
    FROM dbo.KnitMachine km
    LEFT JOIN GaugeFreeDates gfd ON km.Gauge = gfd.Gauge
    OUTER APPLY (
        SELECT TOP 1
            mp.OrderNo,
            CAST(mpd.EndDate AS DATE) AS EndDate,
            mpd.PlaningStatus
        FROM dbo.MasterPlanDetail mpd
        INNER JOIN dbo.MasterPlan mp ON mp.MaterID = mpd.MaterID
        WHERE (
            mpd.machineID = km.Machine_ID
            OR (mpd.machineID IS NULL AND TRY_CAST(mpd.Machine AS INT) = km.Machine_ID)
            OR (mpd.machineID IS NULL AND mpd.Machine = km.MachineNo)
        )
          AND mpd.EndDate >= @Today
          AND (mpd.factory_type IS NULL OR LTRIM(RTRIM(LOWER(mpd.factory_type))) = 'knit' OR LTRIM(RTRIM(mpd.factory_type)) = '')
        ORDER BY mpd.EndDate DESC
    ) LatestPlan
    WHERE km.Gauge IS NOT NULL
      AND (@CleanGauge IS NULL OR km.Gauge = @CleanGauge)
    ORDER BY
        FreeDate ASC,
        km.MachineNo ASC;
END
GO
