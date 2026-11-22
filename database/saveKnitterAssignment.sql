/* ============================================================
   Assign a knitter to a machine plan: upserts a KnitterAssignment
   row for every size line (MasterPlanDetailSize) under the given
   machine plan row (MasterPlanDetail.MasterPlanChildId).

   Conflict guard: if the knitter already has a NON-completed
   assignment on another plan overlapping this plan's date range,
   nothing is saved and -1 is returned.

   This procedure only writes SQL Server. PLM (MySQL
   tbl_production_plan_detail) is updated by the .NET service through the
   third-party API right after this runs, inside the same SQL transaction
   (ProductionPlanningService.SaveKnitterAssignmentAsync), so if PLM fails
   this save is rolled back. There is no linked-server mirror here any more.

   Result sets:
     1. AffectedLines  (-1 = double-booking blocked, 0 = no size lines)
     2. the plan's size lines, for the PLM call (only when AffectedLines > 0)
   ============================================================ */
CREATE OR ALTER PROCEDURE [dbo].[saveKnitterAssignment]
    @masterPlanDetailId INT,                 -- MasterPlanDetail.MasterPlanChildId
    @cardNo             NVARCHAR(50),
    @knitterName        NVARCHAR(150) = NULL,
    @assignedBy         NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    /* This plan's busy window */
    DECLARE @planStart DATETIME, @planEnd DATETIME;
    SELECT @planStart = [StartDate], @planEnd = [EndDate]
    FROM [dbo].[MasterPlanDetail]
    WHERE [MasterPlanChildId] = @masterPlanDetailId;

    /* Conflict guard: knitter already booked on an overlapping, different plan */
    IF EXISTS (
        SELECT 1
        FROM [dbo].[KnitterAssignment] ka
        INNER JOIN [dbo].[MasterPlanDetailSize] sz ON sz.[id] = ka.[MasterPlanDetailSizeId]
        WHERE ka.[card_no] = @cardNo
          AND ISNULL(ka.[status], 'Assigned') <> 'Completed'
          AND sz.[MasterPlanDetailId] <> @masterPlanDetailId
          AND ka.[start_date] IS NOT NULL AND ka.[end_date] IS NOT NULL
          AND ka.[start_date] <= @planEnd
          AND @planStart <= ka.[end_date]
    )
    BEGIN
        SELECT -1 AS AffectedLines;  -- double-booking blocked
        RETURN;
    END

    MERGE [dbo].[KnitterAssignment] AS tgt
    USING (
        SELECT
            sz.[id]              AS MasterPlanDetailSizeId,
            sz.[order_id]        AS order_id,
            mpd.[Guage]          AS gauge,
            mpd.[Machine]        AS machine,
            mpd.[MachineID]      AS machine_id,
            mpd.[StartDate]      AS start_date,
            mpd.[EndDate]        AS end_date,
            sz.[size]            AS size,
            sz.[qty]             AS qty
        FROM [dbo].[MasterPlanDetailSize] sz
        INNER JOIN [dbo].[MasterPlanDetail] mpd ON mpd.[MasterPlanChildId] = sz.[MasterPlanDetailId]
        WHERE sz.[MasterPlanDetailId] = @masterPlanDetailId
    ) AS src
        ON tgt.[MasterPlanDetailSizeId] = src.MasterPlanDetailSizeId
    WHEN MATCHED THEN
        UPDATE SET
            tgt.[card_no]       = @cardNo,
            tgt.[knitter_name]  = @knitterName,
            tgt.[order_id]      = src.order_id,
            tgt.[gauge]         = src.gauge,
            tgt.[machine]       = src.machine,
            tgt.[machine_id]    = src.machine_id,
            tgt.[start_date]    = src.start_date,
            tgt.[end_date]      = src.end_date,
            tgt.[size]          = src.size,
            tgt.[qty]           = src.qty,
            tgt.[assigned_by]   = @assignedBy,
            tgt.[assigned_date] = GETDATE(),
            tgt.[status]        = CASE WHEN tgt.[status] = 'Completed' THEN tgt.[status] ELSE 'Assigned' END
    WHEN NOT MATCHED THEN
        INSERT ([MasterPlanDetailSizeId], [order_id], [gauge], [machine], [machine_id], [start_date], [end_date],
                [size], [qty], [card_no], [knitter_name], [status], [assigned_by], [assigned_date])
        VALUES (src.MasterPlanDetailSizeId, src.order_id, src.gauge, src.machine, src.machine_id, src.start_date, src.end_date,
                src.size, src.qty, @cardNo, @knitterName, 'Assigned', @assignedBy, GETDATE());

    DECLARE @affected INT = @@ROWCOUNT;

    SELECT @affected AS AffectedLines;

    /* The size lines PLM needs (one row per MasterPlanDetailSize). Dates go out as yyyy-MM-dd text.
       OrderNo is the plan's own MasterPlan.OrderNo: order_id was taken live from PLM when the plan
       was made, and the local tbl_order copy is no longer synced (its ids now point at other orders),
       so looking the number up there sent PLM a mismatched id/number pair and it refused the save. */
    IF @affected > 0
        SELECT
            sz.[id]                                        AS SizeLineId,
            sz.[order_id]                                  AS OrderId,
            mp.[OrderNo]                                   AS OrderNo,
            sz.[size]                                      AS Size,
            sz.[qty]                                       AS Qty,
            mpd.[Machine]                                  AS Machine,
            mpd.[factory_type]                             AS FactoryType,
            CONVERT(CHAR(10), mpd.[StartDate], 23)         AS StartDate,
            CONVERT(CHAR(10), mpd.[EndDate], 23)           AS EndDate
        FROM [dbo].[MasterPlanDetailSize] sz
        INNER JOIN [dbo].[MasterPlanDetail] mpd ON mpd.[MasterPlanChildId] = sz.[MasterPlanDetailId]
        INNER JOIN [dbo].[MasterPlan] mp ON mp.[MaterID] = mpd.[MaterID]
        WHERE sz.[MasterPlanDetailId] = @masterPlanDetailId
        ORDER BY sz.[id];
END
GO
