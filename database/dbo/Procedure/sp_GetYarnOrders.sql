Create PROCEDURE [dbo].[sp_GetYarnOrders]
AS
BEGIN
    SET NOCOUNT ON;
    SELECT o.yo_id, o.yo_no, o.created_date, o.created_by, o.total_kg, o.order_count, o.line_count, o.[status],
	od.order_no
    FROM dbo.tbl_yarn_order o (nolock)
	left join [dbo].[tbl_yarn_order_detail] od (nolock) on o.yo_id = od.yo_id
    ORDER BY created_date DESC, yo_id DESC;
END