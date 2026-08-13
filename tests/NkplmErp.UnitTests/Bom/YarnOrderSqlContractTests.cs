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

        sql.Should().Contain("sp_getapplock");
        sql.Should().Contain("@LockOwner = 'Transaction'");
        sql.Should().Contain("t.[Stage] = 12");
        sql.Should().Contain("a.[StartDate] IS NOT NULL");
        sql.Should().Contain("@Stage = 12");
        sql.Should().Contain("@RefId = @yoId");
        sql.Should().Contain("'U'");
        sql.Should().Contain("AS [WasAppended]");
        sql.Should().NotContain("BEGIN TRY");
    }
}
