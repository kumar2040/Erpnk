USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

ALTER PROCEDURE [dbo].[doPlan]
    @orderNo NVARCHAR(50),
    @guage NVARCHAR(50),
    @startDate DATETIME,
    @endDate DATETIME,
    @qty DECIMAL(18,2),
    @machine INT,
    @orderType NVARCHAR(50),
    @knitType NVARCHAR(50),
    @userId NVARCHAR(100),
    @createdDate DATETIME,
    @machineNo NVARCHAR(50) = NULL,   -- machine name e.g. KN-56
    @machineId INT = NULL,            -- numeric machine id e.g. 25
    @isOvertime BIT = 0,              -- overtime applied
    @overtimeHours DECIMAL(5,2) = 0,  -- extra OT hours/day
    @workSaturday BIT = 0             -- Saturdays treated as working days
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @materId INT;
    DECLARE @childId INT;

    /* 1. CHECK IF ORDER ALREADY EXISTS IN MasterPlan TABLE */
    SELECT TOP 1 @materId = [MaterID]
    FROM [dbo].[MasterPlan]
    WHERE [OrderNo] = @orderNo;

    /* 2. IF NOT EXISTS, INSERT NEW RECORD */
    IF @materId IS NULL
    BEGIN
        INSERT INTO [dbo].[MasterPlan]
        (
            [OrderNo],
            [OrderType],
            [ProductionType],
            [PlanStartDate],
            [OrderStatus],
            [PlanWorkingStatus],
            [EntryDate],
            [CreatedBy]
        )
        VALUES
        (
            @orderNo,
            @orderType,
            @knitType,
            @startDate,
            'Active',
            'Planning',
            @createdDate,
            @userId
        );

        SET @materId = SCOPE_IDENTITY();
    END

    /* 3. INSERT INTO MasterPlanDetail TABLE */
    INSERT INTO [dbo].[MasterPlanDetail]
    (
        [MaterID],
        [Guage],
        [StartDate],
        [EndDate],
        [Machine],
        [MachineID],
        [PlaningStatus],
        [EntryDate],
        [CreatedBy],
        [Qty],
        [MachineCount],
        [IsOvertime],
        [OvertimeHours],
        [WorkSaturday],
        [factory_type]
    )
    VALUES
    (
        @materId,
        @guage,
        @startDate,
        @endDate,
        -- Machine name (KN-56) when supplied; otherwise fall back to the legacy value.
        ISNULL(@machineNo, CAST(@machine AS NVARCHAR(50))),
        @machineId,
        'Planned',
        @createdDate,
        @userId,
        @qty,
        @machine,
        @isOvertime,
        @overtimeHours,
        @workSaturday,
        -- Department of this plan row: 'knit' / 'weave' / 'silk' / 'other' / 'linen'.
        LOWER(LTRIM(RTRIM(ISNULL(@knitType, 'knit'))))
    );

    SET @childId = SCOPE_IDENTITY();

    /* ============================================================
       WEAVE ONLY: mirror this plan row to the linked MySQL server
       (MYSQL_NatureKnit -> db_natureknit.tbl_weave_plandetail).
       Best-effort: a linked-server failure is swallowed so it never
       breaks the local save. Requires 'rpc out' on the linked server.

       Mapping:
         plan_id = MasterPlanChildId (@childId)
         factory = MasterPlanDetail.Guage (@guage)  -- the value saved locally
         qty     = @qty
         orderNo = @orderNo
         enddate = @endDate (date)
         date_   = NOW(), status = 'Planed'
       NOTE: if @guage is a factory NAME (non-numeric), the MySQL
             [factory] column must be VARCHAR to hold it.
       ============================================================ */
    IF UPPER(LTRIM(RTRIM(ISNULL(@knitType, '')))) = 'WEAVE'
    BEGIN
        BEGIN TRY
            DECLARE @wFactory   NVARCHAR(100)  = REPLACE(ISNULL(@guage, ''), '''', '''''');
            DECLARE @wOrderNo   NVARCHAR(200)  = REPLACE(ISNULL(@orderNo, ''), '''', '''''');
            DECLARE @wEndDate   NVARCHAR(10)   = CONVERT(NVARCHAR(10), @endDate, 23);   -- yyyy-MM-dd
            DECLARE @wStartDate NVARCHAR(10)   = CONVERT(NVARCHAR(10), @startDate, 23); -- yyyy-MM-dd

            DECLARE @wIns NVARCHAR(MAX) =
                N'INSERT INTO db_natureknit.tbl_weave_plandetail '
              + N'(plan_id, factory, qty, orderNo, date_, startDate, enddate, status) VALUES ('
              + CAST(@childId AS NVARCHAR(20)) + N','
              + N'''' + @wFactory + N''','                                 -- factory = gauge value
              + CAST(CAST(ROUND(@qty, 0) AS INT) AS NVARCHAR(20)) + N','
              + N'''' + @wOrderNo + N''','
              + N'NOW(),'
              + N'''' + @wStartDate + N''','                               -- startDate = plan start
              + N'''' + @wEndDate + N''','
              + N'''Planed'');';

            EXEC (@wIns) AT [MYSQL_NatureKnit];
        END TRY
        BEGIN CATCH
            /* best-effort: ignore so the local plan still succeeds */
        END CATCH
    END

    /* RETURN THE INSERTED CHILD (DETAIL) ID FIRST so callers can link
       size lines (MasterPlanDetailSize) to this exact machine row,
       followed by MaterID and the order's max end date. */
    SELECT
        @childId AS MasterPlanChildId,
        @materId AS MaterID,
        (SELECT MAX(EndDate) FROM [dbo].[MasterPlanDetail] WHERE [MaterID] = @materId) AS MaxEndDate;
END
GO
