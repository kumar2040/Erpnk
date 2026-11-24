/* ==========================================================================
   sp_Replan  -  Replan popup on Order Planning, MSSQL side.

   Saved plans live in MSSQL (MasterPlan / MasterPlanDetail / MasterPlanDetailSize)
   until a knitter is assigned; only then are they pushed to PLM. So the plans
   that can still be re-planned here are the ones with NO KnitterAssignment.
   (Old plans made directly in PLM come from the PLM API, not from here.)

   @Flag
     'L'  list: one row per size line of every plan with no knitter assigned.
          StyleTarget = the plan's own rate (plan qty / planned days), so the
          popup can re-date the plan on another machine.
     'M'  move: put plan @PlanId on machine @Machine/@MachineId with new
          @StartDate/@EndDate (yyyy-MM-dd text). Refused once a knitter is
          assigned, since the plan is in PLM from then on.
          Returns Succeeded + Message.
   ========================================================================== */
CREATE OR ALTER PROCEDURE [dbo].[sp_Replan]
    @Flag      CHAR(1),
    @PlanId    INT           = NULL,
    @Machine   NVARCHAR(50)  = NULL,
    @MachineId INT           = NULL,
    @StartDate NVARCHAR(20)  = NULL,
    @EndDate   NVARCHAR(20)  = NULL,
    @UserId    NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF @Flag = 'L'
    BEGIN
        ;WITH plans AS
        (
            SELECT
                mpd.[MasterPlanChildId],
                PlanQty  = SUM(sz.[qty]),
                -- planned duration in days (EndDate carries a time part); at least 1 day
                PlanDays = CASE WHEN DATEDIFF(MINUTE, mpd.[StartDate], mpd.[EndDate]) / 1440.0 < 1 THEN 1.0
                                ELSE DATEDIFF(MINUTE, mpd.[StartDate], mpd.[EndDate]) / 1440.0 END
            FROM [dbo].[MasterPlanDetail] mpd
            INNER JOIN [dbo].[MasterPlanDetailSize] sz ON sz.[MasterPlanDetailId] = mpd.[MasterPlanChildId]
            WHERE NOT EXISTS (SELECT 1
                              FROM [dbo].[MasterPlanDetailSize] s2
                              INNER JOIN [dbo].[KnitterAssignment] ka ON ka.[MasterPlanDetailSizeId] = s2.[id]
                              WHERE s2.[MasterPlanDetailId] = mpd.[MasterPlanChildId])
            GROUP BY mpd.[MasterPlanChildId], mpd.[StartDate], mpd.[EndDate]
        )
        SELECT
            [Id]          = sz.[id],
            [PlanId]      = mpd.[MasterPlanChildId],
            [OrderId]     = ISNULL(sz.[order_id], 0),
            [OrderNo]     = mp.[OrderNo],
            [StyleNo]     = ISNULL(sz.[style_no], ''),
            [Gauge]       = LTRIM(RTRIM(ISNULL(mpd.[Guage], ''))),
            [StyleTarget] = CAST(p.[PlanQty] / p.[PlanDays] AS FLOAT),
            [KnitType]    = ISNULL(mpd.[factory_type], 'knit'),
            [Color]       = ISNULL(sz.[color], ''),
            [Size]        = ISNULL(sz.[size], ''),
            [Qty]         = sz.[qty],
            [Knitter]     = '',
            [KnitterName] = '',
            [Machine]     = ISNULL(mpd.[Machine], ''),
            [StartDate]   = mpd.[StartDate],
            [EndDate]     = mpd.[EndDate],
            [Source]      = 'ERP'
        FROM plans p
        INNER JOIN [dbo].[MasterPlanDetail] mpd     ON mpd.[MasterPlanChildId] = p.[MasterPlanChildId]
        INNER JOIN [dbo].[MasterPlan] mp            ON mp.[MaterID] = mpd.[MaterID]
        INNER JOIN [dbo].[MasterPlanDetailSize] sz  ON sz.[MasterPlanDetailId] = mpd.[MasterPlanChildId]
        ORDER BY mp.[OrderNo], mpd.[MasterPlanChildId], sz.[id];
        RETURN;
    END

    IF @Flag = 'M'
    BEGIN
        DECLARE @start DATETIME = TRY_CONVERT(DATETIME, @StartDate, 23);
        DECLARE @end   DATETIME = TRY_CONVERT(DATETIME, @EndDate, 23);
        DECLARE @mc    NVARCHAR(50) = LTRIM(RTRIM(ISNULL(@Machine, '')));

        IF NOT EXISTS (SELECT 1 FROM [dbo].[MasterPlanDetail] WHERE [MasterPlanChildId] = @PlanId)
        BEGIN
            SELECT CAST(0 AS BIT) AS Succeeded, 'This plan no longer exists.' AS [Message];
            RETURN;
        END

        IF EXISTS (SELECT 1
                   FROM [dbo].[MasterPlanDetailSize] sz
                   INNER JOIN [dbo].[KnitterAssignment] ka ON ka.[MasterPlanDetailSizeId] = sz.[id]
                   WHERE sz.[MasterPlanDetailId] = @PlanId)
        BEGIN
            SELECT CAST(0 AS BIT) AS Succeeded, 'A knitter is already assigned to this plan, so it can no longer be re-planned here.' AS [Message];
            RETURN;
        END

        IF @mc = '' OR @MachineId IS NULL
        BEGIN
            SELECT CAST(0 AS BIT) AS Succeeded, 'Select one machine for this plan.' AS [Message];
            RETURN;
        END

        IF @start IS NULL OR @end IS NULL OR @end < @start
        BEGIN
            SELECT CAST(0 AS BIT) AS Succeeded, 'Start and end dates are not valid.' AS [Message];
            RETURN;
        END

        BEGIN TRANSACTION;

        UPDATE [dbo].[MasterPlanDetail]
        SET [Machine]    = @mc,
            [machineID]  = @MachineId,
            [StartDate]  = @start,
            [EndDate]    = @end,
            [ModifyDate] = GETDATE(),
            [ModifiedBy] = @UserId
        WHERE [MasterPlanChildId] = @PlanId;

        COMMIT TRANSACTION;

        SELECT CAST(1 AS BIT) AS Succeeded, 'Plan moved to ' + @mc + '.' AS [Message];
        RETURN;
    END

    SELECT CAST(0 AS BIT) AS Succeeded, 'Unknown flag.' AS [Message];
END
GO
