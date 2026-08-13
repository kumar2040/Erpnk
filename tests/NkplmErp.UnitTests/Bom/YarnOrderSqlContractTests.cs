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
}
