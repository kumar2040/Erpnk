CREATE OR ALTER PROCEDURE [dbo].[sp_PoTask_PendingReviews]
    @Top int = 50,
    @CutoffDate datetime = '2026-07-21'
AS
BEGIN
    SET NOCOUNT ON;
    ;WITH latest AS
    (
        SELECT r.[id] AS [ReviewId], r.[order_no], r.[remark], r.[date_],
               ROW_NUMBER() OVER (PARTITION BY r.[order_no] ORDER BY r.[id] DESC) AS rn
        FROM [dbo].[tbl_order_review] r WITH (NOLOCK)
        WHERE r.[date_] > @CutoffDate
    )
    SELECT TOP (@Top) l.[ReviewId], l.[order_no] AS [OrderNo],
           l.[remark] AS [Remark], l.[date_] AS [ReviewDate]
    FROM latest l
    WHERE l.rn = 1
      AND (NOT EXISTS (SELECT 1 FROM [dbo].[PoTask] t WITH (NOLOCK)
                       WHERE t.[OrderNo] = l.[order_no] AND t.[Stage] = 1 AND t.[IsActive] = 1)
           OR NOT EXISTS (SELECT 1 FROM [dbo].[PoTaskOrder] o WITH (NOLOCK)
                          WHERE o.[OrderNo] = l.[order_no] AND o.[IsActive] = 1))
    ORDER BY l.[ReviewId] ASC;
END;
