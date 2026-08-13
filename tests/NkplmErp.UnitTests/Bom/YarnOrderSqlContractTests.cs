using FluentAssertions;

namespace NkplmErp.UnitTests.Bom;

public class YarnOrderSqlContractTests
{
    private static string ReadProcedure(string name)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "database")))
            directory = directory.Parent;

        directory.Should().NotBeNull("the test must run beneath the repository root");
        return File.ReadAllText(Path.Combine(
            directory!.FullName, "database", "dbo", "Procedure", name));
    }

    [Fact]
    public void SaveYarnOrder_SerializesReuseAndCreatesRefLinkedStage12Task()
    {
        var sql = ReadProcedure("sp_SaveYarnOrder.sql");

        sql.Should().Contain("SET XACT_ABORT ON;");
        sql.Should().Contain("sp_getapplock");
        sql.Should().Contain("@LockMode = 'Exclusive'");
        sql.Should().Contain("@LockOwner = 'Transaction'");

        var selectionStart = sql.IndexOf("@poTaskId = t.[PoTaskId]", StringComparison.Ordinal);
        selectionStart.Should().BeGreaterThanOrEqualTo(0);
        var selectionEnd = sql.IndexOf("ORDER BY t.[PoTaskId] DESC;", selectionStart, StringComparison.Ordinal);
        selectionEnd.Should().BeGreaterThan(selectionStart);
        var reuseSelection = sql[selectionStart..(selectionEnd + "ORDER BY t.[PoTaskId] DESC;".Length)];

        reuseSelection.Should().Contain("t.[Stage] = 12");
        reuseSelection.Should().Contain("t.[Status] = 'S'");
        reuseSelection.Should().Contain("t.[IsActive] = 1");
        reuseSelection.Should().Contain("AND EXISTS");
        reuseSelection.Should().Contain("AND NOT EXISTS");
        reuseSelection.Should().Contain("a.[IsActive] = 1");
        reuseSelection.Should().Contain("a.[StartDate] IS NOT NULL");
        reuseSelection.Should().Contain("a.[Status] <> 'S'");
        reuseSelection.Should().EndWith("ORDER BY t.[PoTaskId] DESC;");

        sql.Should().Contain("@Stage = 12");
        sql.Should().Contain("@RefId = @yoId");
        sql.Should().Contain("'U'");
        foreach (var alias in new[]
                 {
                     "YoNo", "YoId", "TotalKg", "PoTaskId", "WasAppended",
                     "OrderCount", "LineCount", "Message", "IsSuccess"
                 })
            sql.Split($"AS [{alias}]", StringSplitOptions.None).Should().HaveCount(4,
                $"all three result paths must expose {alias}");

        sql.Should().NotContain("BEGIN TRY");
    }

    [Fact]
    public void SaveYarnOrder_NormalizesValidAssigneeIdsBeforeValidationAndTaskCreation()
    {
        var sql = ReadProcedure("sp_SaveYarnOrder.sql");

        sql.Should().Contain("DECLARE @AssigneeIds TABLE");
        sql.Should().Contain("FROM STRING_SPLIT(ISNULL(@AssigneeUserIds, N''), N'|')");
        sql.Should().Contain("LEN(LTRIM(RTRIM([value]))) BETWEEN 1 AND 450");
        sql.Should().Contain("STRING_AGG(CONVERT(NVARCHAR(MAX), [UserId]), N'|')");
        sql.Should().Contain("WITHIN GROUP (ORDER BY [UserId])");
        sql.Should().Contain("NOT EXISTS (SELECT 1 FROM @AssigneeIds)");
        sql.Should().Contain("@AssigneeUserIds = @normalizedAssigneeUserIds");
        sql.Should().NotContain("NULLIF(LTRIM(RTRIM(@AssigneeUserIds)), '') IS NULL");
        sql.IndexOf("INSERT INTO @AssigneeIds", StringComparison.Ordinal).Should().BeLessThan(
            sql.IndexOf("BEGIN TRANSACTION;", StringComparison.Ordinal));
    }

    [Fact]
    public void SaveYarnOrder_ParsesCompleteSequenceSuffixAndPreservesFourDigitNumbers()
    {
        var sql = ReadProcedure("sp_SaveYarnOrder.sql");

        sql.Should().Contain("SUBSTRING([yo_no], LEN('Natureknit Yarn-') + 1, 30)");
        sql.Should().Contain("WHEN @nextNo < 1000");
        sql.Should().Contain("RIGHT('000' + CAST(@nextNo AS VARCHAR(10)), 3)");
        sql.Should().Contain("ELSE CAST(@nextNo AS VARCHAR(10))");
        sql.Should().NotContain("RIGHT([yo_no], 3)");
    }

    [Fact]
    public void GetPoTask_DerivesStage12OrdersAndLinkFromRefId()
    {
        var sql = ReadProcedure("sp_GetPoTask.sql");

        sql.Split("WHERE d.[yo_id] = t.[RefId]", StringSplitOptions.None).Should().HaveCount(3,
            "DETAIL and the shared BOARD/MYTASKS card query must aggregate through PoTask.RefId");
        sql.Split(") yarnOrders", StringSplitOptions.None).Should().HaveCount(3,
            "DETAIL and the shared BOARD/MYTASKS card query each need the RefId aggregation");
        sql.Should().Contain("SELECT DISTINCT LTRIM(RTRIM(d.[order_no])) AS [order_no]");
        sql.Should().Contain("STRING_AGG(CONVERT(nvarchar(max), yd.[order_no]), N', ')");
        sql.Should().Contain("WITHIN GROUP (ORDER BY yd.[order_no]) AS [OrderNos]");
        sql.Split("THEN yarnOrders.[OrderNos]", StringSplitOptions.None).Should().HaveCount(3,
            "DETAIL and the shared card query must prefer the RefId order list for Stage 12");
        sql.Split("THEN yarnOrders.[OrderCount]", StringSplitOptions.None).Should().HaveCount(3,
            "DETAIL and the shared card query must prefer the matching distinct order count");

        sql.Should().Contain("WHERE y.[yo_id] = t.[RefId]");
        sql.Should().Contain("yarnRef.[yo_id]");
        sql.Should().Contain("N'/yarn-orders/' + CAST(yarnRef.[yo_id] AS nvarchar(20))");
        sql.Should().Contain("WHEN t.[RefId] IS NULL AND yo.[yo_id] IS NOT NULL");
        sql.Should().NotContain("WHEN yo.[yo_id] IS NOT NULL",
            "a non-null stale Stage 12 RefId must not navigate through the legacy OrderNo lookup");
        sql.Should().Contain("WHEN 20 THEN COALESCE(N'/yarn-orders/' + CAST(yo.[yo_id] AS nvarchar(20)), N'/yarn-orders')");

        sql.Should().Contain("t.[Stage] = 12 AND EXISTS");
        sql.Should().Contain("WHERE yd.[yo_id] = t.[RefId]");
        sql.Should().Contain("yd.[order_no] LIKE '%' + @SearchOrderNo + '%'");
    }
}
