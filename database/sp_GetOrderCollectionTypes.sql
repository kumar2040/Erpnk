-- =============================================================================
-- sp_GetOrderCollectionTypes
-- Returns, per order, whether it has SAMPLE and/or PRODUCTION collection rows.
--
-- Source: tbl_order  JOIN  tbl_order_collection  ON order_no
--   tbl_order_collection.type = 's'  -> Sample
--   tbl_order_collection.type = 'p' (or anything not 's') -> Production
--
-- The OrderPlanning page calls this to filter the month's order list by the
-- Sample / Production step chosen in the planning wizard. It does NOT change the
-- existing sp_GetMonthlyOrderReport - it is an additive companion lookup.
--
-- Collection-type column is [typ] ('s' = Sample, else Production).
-- =============================================================================
CREATE OR ALTER PROCEDURE [dbo].[sp_GetOrderCollectionTypes]
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        oc.order_no AS OrderNo,
        -- Sample: typ is 's' or 'S' (case + whitespace insensitive)
        MAX(CASE WHEN UPPER(LTRIM(RTRIM(oc.[typ]))) = 'S' THEN 1 ELSE 0 END) AS IsSample,
        -- Production: anything that is NOT sample (typ 'p'/'P' or any other non-'s' value)
        MAX(CASE WHEN UPPER(LTRIM(RTRIM(oc.[typ]))) <> 'S' THEN 1 ELSE 0 END) AS IsProduction
    FROM tbl_order_collection oc
    WHERE oc.order_no IS NOT NULL
      AND oc.[typ] IS NOT NULL
    GROUP BY oc.order_no;
END
GO
