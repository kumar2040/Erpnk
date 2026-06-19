USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   Assign a knitter to a machine plan: upserts a KnitterAssignment
   row for every size line (MasterPlanDetailSize) under the given
   machine plan row (MasterPlanDetail.MasterPlanChildId).

   Conflict guard: if the knitter already has a NON-completed
   assignment on another plan overlapping this plan's date range,
   nothing is saved and -1 is returned.
   ============================================================ */
IF OBJECT_ID('[dbo].[saveKnitterAssignment]', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[saveKnitterAssignment] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[saveKnitterAssignment]
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

    /* ============================================================
       Mirror the just-saved size lines to the linked MySQL server
       (MYSQL_NatureKnit -> db_natureknit.tbl_production_plan_detail).

       Best-effort: a linked-server failure is caught and reported in
       LinkedServerError but does NOT roll back the local save.

       Strategy: replace this plan's rows on MySQL (DELETE by plan_id,
       then INSERT one row per size line) so re-assignment stays in sync.

       Column mappings:
         id_no  = MasterPlanDetail.MasterPlanChildId  (@masterPlanDetailId)
         plan_id= MasterPlanDetailSize.id             (size-line id)
         knitter= card_no
         old_qty= qty
       ============================================================ */
    DECLARE @LinkedServerError NVARCHAR(2000) = NULL;

    BEGIN TRY
        /* 1. Remove existing MySQL rows for this plan so we don't duplicate.
              id_no holds the MasterPlanChildId, so delete by id_no. */
        DECLARE @del NVARCHAR(MAX) =
            N'DELETE FROM db_natureknit.tbl_production_plan_detail WHERE id_no = '
            + CAST(@masterPlanDetailId AS NVARCHAR(20)) + N';';
        EXEC (@del) AT [MYSQL_NatureKnit];

        /* 2. Insert each size line. */
        DECLARE
            @s_idNo   NVARCHAR(20),
            @s_order  NVARCHAR(50),
            @s_size   NVARCHAR(50),
            @s_qty    DECIMAL(18,2),
            @s_mc     NVARCHAR(100),
            @s_start  DATETIME,
            @s_end    DATETIME;

        DECLARE line_cur CURSOR LOCAL FAST_FORWARD FOR
            SELECT
                CAST(sz.[id] AS NVARCHAR(20)),
                CAST(sz.[order_id] AS NVARCHAR(50)),
                sz.[size],
                sz.[qty],
                mpd.[Machine],
                mpd.[StartDate],
                mpd.[EndDate]
            FROM [dbo].[MasterPlanDetailSize] sz
            INNER JOIN [dbo].[MasterPlanDetail] mpd ON mpd.[MasterPlanChildId] = sz.[MasterPlanDetailId]
            WHERE sz.[MasterPlanDetailId] = @masterPlanDetailId;

        OPEN line_cur;
        FETCH NEXT FROM line_cur INTO @s_idNo, @s_order, @s_size, @s_qty, @s_mc, @s_start, @s_end;

        WHILE @@FETCH_STATUS = 0
        BEGIN
            /* Escape single quotes for the pass-through statement. */
            DECLARE
                @q_order NVARCHAR(100) = REPLACE(ISNULL(@s_order,''), '''', ''''''),
                @q_knit  NVARCHAR(150) = REPLACE(ISNULL(@cardNo,''), '''', ''''''),
                @q_size  NVARCHAR(100) = REPLACE(ISNULL(@s_size,''), '''', ''''''),
                @q_mc    NVARCHAR(200) = REPLACE(ISNULL(@s_mc,''), '''', ''''''),
                @q_user  NVARCHAR(200) = REPLACE(ISNULL(@assignedBy,''), '''', ''''''),
                @q_start NVARCHAR(30)  = CONVERT(NVARCHAR(19), @s_start, 120),
                @q_end   NVARCHAR(30)  = CONVERT(NVARCHAR(19), @s_end, 120);

            DECLARE @ins NVARCHAR(MAX) =
                N'INSERT INTO db_natureknit.tbl_production_plan_detail '
              + N'(order_id, id_no, plan_id, knitter, size, qty, machine_, date_, user_, status_, old_qty, startDate, endDate) VALUES ('
              + N'''' + @q_order + N''','
              + CAST(@masterPlanDetailId AS NVARCHAR(20)) + N','           /* id_no  = MasterPlanChildId */
              + @s_idNo + N','                                            /* plan_id = size-line id     */
              + N'''' + @q_knit + N''','                                   /* knitter = card_no          */
              + N'''' + @q_size + N''','
              + CAST(@s_qty AS NVARCHAR(30)) + N','
              + N'''' + @q_mc + N''','
              + N'NOW(),'
              + N'''' + @q_user + N''','
              + N'''Assigned'','
              + CAST(@s_qty AS NVARCHAR(30)) + N','                        /*? old_qty */
              + CASE WHEN @s_start IS NULL THEN N'NULL' ELSE N'''' + @q_start + N'''' END + N','
              + CASE WHEN @s_end   IS NULL THEN N'NULL' ELSE N'''' + @q_end   + N'''' END
              + N');';

            EXEC (@ins) AT [MYSQL_NatureKnit];

            FETCH NEXT FROM line_cur INTO @s_idNo, @s_order, @s_size, @s_qty, @s_mc, @s_start, @s_end;
        END

        CLOSE line_cur;
        DEALLOCATE line_cur;
    END TRY
    BEGIN CATCH
        IF CURSOR_STATUS('local','line_cur') >= 0
        BEGIN
            CLOSE line_cur;
            DEALLOCATE line_cur;
        END
        SET @LinkedServerError = ERROR_MESSAGE();
    END CATCH

    SELECT @affected AS AffectedLines, @LinkedServerError AS LinkedServerError;
END
GO
