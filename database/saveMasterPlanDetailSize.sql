USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   Inserts a single style/color/size allocation line for a
   given machine plan row (MasterPlanDetail.MasterPlanChildId).
   Called once per size line after doPlan returns the child id.
   ============================================================ */
IF OBJECT_ID('[dbo].[saveMasterPlanDetailSize]', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[saveMasterPlanDetailSize] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[saveMasterPlanDetailSize]
    @masterPlanDetailId INT,
    @orderId            INT = NULL,
    @styleNo            NVARCHAR(50)  = NULL,
    @color              NVARCHAR(100) = NULL,
    @size               NVARCHAR(20)  = NULL,
    @qty                DECIMAL(18,2) = 0
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[MasterPlanDetailSize]
    (
        [MasterPlanDetailId],
        [order_id],
        [style_no],
        [color],
        [size],
        [qty]
    )
    VALUES
    (
        @masterPlanDetailId,
        @orderId,
        @styleNo,
        @color,
        @size,
        @qty
    );

    SELECT SCOPE_IDENTITY() AS id;
END
GO
