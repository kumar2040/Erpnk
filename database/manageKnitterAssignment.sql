USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   Manage the knitter assignment of a machine plan:
     @action = 'complete' -> mark all its size-line assignments Completed
     @action = 'unassign' -> remove all its size-line assignments
   @masterPlanDetailId = MasterPlanDetail.MasterPlanChildId
   ============================================================ */
IF OBJECT_ID('[dbo].[manageKnitterAssignment]', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[manageKnitterAssignment] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[manageKnitterAssignment]
    @masterPlanDetailId INT,
    @action             NVARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;

    IF LOWER(@action) = 'complete'
    BEGIN
        UPDATE ka
        SET ka.[status] = 'Completed',
            ka.[completed_date] = GETDATE()
        FROM [dbo].[KnitterAssignment] ka
        INNER JOIN [dbo].[MasterPlanDetailSize] sz ON sz.[id] = ka.[MasterPlanDetailSizeId]
        WHERE sz.[MasterPlanDetailId] = @masterPlanDetailId;
    END
    ELSE IF LOWER(@action) = 'unassign'
    BEGIN
        DELETE ka
        FROM [dbo].[KnitterAssignment] ka
        INNER JOIN [dbo].[MasterPlanDetailSize] sz ON sz.[id] = ka.[MasterPlanDetailSizeId]
        WHERE sz.[MasterPlanDetailId] = @masterPlanDetailId;
    END

    SELECT @@ROWCOUNT AS AffectedLines;
END
GO
