USE [NatureKnit]
GO

SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/* ============================================================
   Returns the knitters available for a given gauge by joining
   KnittersGauges (CardNo, Gauge) to Knitters (CardNo).
   Gauge values like '7GG' / '7G' are normalised so they match
   the plain gauge stored on MasterPlanDetail (e.g. '7').
   @gauge = NULL returns all knitters (all gauges).

   Knitters columns: CardNo (int), KnitterName (nvarchar), PRSalary.
   ============================================================ */
IF OBJECT_ID('[dbo].[sp_GetKnittersByGauge]', 'P') IS NULL
    EXEC('CREATE PROCEDURE [dbo].[sp_GetKnittersByGauge] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[sp_GetKnittersByGauge]
    @gauge NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @g DECIMAL(10,2) =
        TRY_CAST(REPLACE(REPLACE(REPLACE(@gauge, 'GG', ''), 'G', ''), ' ', '') AS DECIMAL(10,2));

    SELECT DISTINCT
        kg.[CardNo]                              AS [CardNo],
        k.[KnitterName]                          AS [KnitterName],
        kg.[Gauge]                               AS [Gauge],
        TRY_CAST(REPLACE(REPLACE(REPLACE(kg.[Gauge], 'GG', ''), 'G', ''), ' ', '') AS DECIMAL(10,2)) AS [GaugeValue]
    FROM [dbo].[KnittersGauges] kg
    INNER JOIN [dbo].[Knitters] k
        ON k.[CardNo] = kg.[CardNo]
    WHERE kg.[Gauge] IS NOT NULL
      AND (
            @g IS NULL
            OR TRY_CAST(REPLACE(REPLACE(REPLACE(kg.[Gauge], 'GG', ''), 'G', ''), ' ', '') AS DECIMAL(10,2)) = @g
          )
    ORDER BY [KnitterName];
END
GO
