-- =====================================================================================
-- SQL Query to find the first Free Date for each Gauge based on timeline availability.
-- Matches the chronological scan logic used in the Blazor application.
-- =====================================================================================

WITH TimelineCapacity AS (
    SELECT 
        Gauge,
        PlanSnapshotDate,
        TotalActiveCapacityLimit,
        EngagedMachines,
        ImmediateFreeMachines,
        EngagedMachinesReleaseDate,
        TodayDate,
        -- Calculate free machines on this specific date
        CASE 
            WHEN ImmediateFreeMachines > 0 THEN ImmediateFreeMachines
            WHEN (TotalActiveCapacityLimit - EngagedMachines) > 0 THEN (TotalActiveCapacityLimit - EngagedMachines)
            ELSE 0 
        END AS FreeMachines
    FROM ForwardTimeline
),
FirstFreeDateOption AS (
    -- Option A: Earliest snapshot date where there is free capacity. 
    -- If immediate free machines exist today, we return the TodayDate or current date.
    SELECT 
        Gauge,
        CASE 
            WHEN ImmediateFreeMachines > 0 THEN ISNULL(TodayDate, GETDATE())
            ELSE PlanSnapshotDate 
        END AS CalculatedFreeDate,
        1 AS PriorityRank,
        ROW_NUMBER() OVER (PARTITION BY Gauge ORDER BY PlanSnapshotDate ASC) AS RowNum
    FROM TimelineCapacity
    WHERE FreeMachines > 0
),
EarliestReleaseOption AS (
    -- Option B (Fallback): Earliest release date of the busy machines
    SELECT 
        Gauge,
        EngagedMachinesReleaseDate AS CalculatedFreeDate,
        2 AS PriorityRank,
        ROW_NUMBER() OVER (PARTITION BY Gauge ORDER BY EngagedMachinesReleaseDate ASC) AS RowNum
    FROM TimelineCapacity
    WHERE FreeMachines = 0 AND EngagedMachinesReleaseDate IS NOT NULL
),
LastSnapshotOption AS (
    -- Option C (Fallback): Last snapshot date in the timeline
    SELECT 
        Gauge,
        PlanSnapshotDate AS CalculatedFreeDate,
        3 AS PriorityRank,
        ROW_NUMBER() OVER (PARTITION BY Gauge ORDER BY PlanSnapshotDate DESC) AS RowNum
    FROM TimelineCapacity
    WHERE FreeMachines = 0 AND EngagedMachinesReleaseDate IS NULL
),
CombinedOptions AS (
    SELECT Gauge, CalculatedFreeDate, PriorityRank FROM FirstFreeDateOption WHERE RowNum = 1
    UNION ALL
    SELECT Gauge, CalculatedFreeDate, PriorityRank FROM EarliestReleaseOption WHERE RowNum = 1
    UNION ALL
    SELECT Gauge, CalculatedFreeDate, PriorityRank FROM LastSnapshotOption WHERE RowNum = 1
),
RankedOptions AS (
    SELECT 
        Gauge,
        CalculatedFreeDate,
        ROW_NUMBER() OVER (PARTITION BY Gauge ORDER BY PriorityRank ASC) AS Rank
    FROM CombinedOptions
)
SELECT 
    Gauge,
    ISNULL(CalculatedFreeDate, GETDATE()) AS FinalFreeDate
FROM RankedOptions
WHERE Rank = 1;
