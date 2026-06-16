USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   Size lines of one machine plan (MasterPlanDetail.MasterPlanChildId),
   used to view/edit a saved plan's style/color/size breakdown.
   ============================================================ */
IF OBJECT_ID('[dbo].[getMasterPlanSizeLines]', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[getMasterPlanSizeLines] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[getMasterPlanSizeLines]
    @masterPlanDetailId INT
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        sz.[id]        AS SizeLineId,
        sz.[order_id]  AS OrderId,
        sz.[style_no]  AS StyleNo,
        sz.[color]     AS Color,
        sz.[size]      AS Size,
        sz.[qty]       AS Qty
    FROM [dbo].[MasterPlanDetailSize] sz
    WHERE sz.[MasterPlanDetailId] = @masterPlanDetailId
    ORDER BY sz.[style_no], sz.[color], sz.[id];
END
GO
