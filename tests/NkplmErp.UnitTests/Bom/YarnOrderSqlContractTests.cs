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
        var manageTaskSql = ReadProcedure("sp_ManagePoTask.sql");

        sql.Should().Contain("SET XACT_ABORT ON;");
        sql.Should().Contain("sp_getapplock");
        sql.Should().Contain("@LockMode = 'Exclusive'");
        sql.Should().Contain("@LockOwner = 'Transaction'");

        var appLock = sql.IndexOf("EXEC @lockResult = sys.sp_getapplock", StringComparison.Ordinal);
        var candidates = sql.IndexOf("INSERT INTO @CandidateTasks", appLock, StringComparison.Ordinal);
        var childLock = sql.IndexOf(
            "FROM dbo.[PoTaskAssignee] AS a WITH (UPDLOCK, HOLDLOCK, INDEX([IX_PoTaskAssignee_Task]))",
            Math.Max(candidates, 0),
            StringComparison.Ordinal);
        var parentLock = sql.IndexOf(
            "FROM dbo.[PoTask] AS t WITH (UPDLOCK, HOLDLOCK)",
            Math.Max(childLock, 0),
            StringComparison.Ordinal);

        candidates.Should().BeGreaterThan(appLock,
            "candidate discovery must happen after save-vs-save serialization");
        childLock.Should().BeGreaterThan(candidates);
        parentLock.Should().BeGreaterThan(childLock,
            "assignee mutations lock the child before recomputing the parent, so reuse must do the same");
        sql[appLock..childLock].Should().NotContain("WITH (UPDLOCK, HOLDLOCK)",
            "candidate discovery must not retain a parent lock before the child range is acquired");
        sql.Should().Contain("SELECT @candidatePoTaskId = MAX([PoTaskId])",
            "the newest eligible candidate must be considered first");

        var candidateEnd = sql.IndexOf("DECLARE @candidatePoTaskId", candidates, StringComparison.Ordinal);
        candidateEnd.Should().BeGreaterThan(candidates);
        var candidateDiscovery = sql[candidates..candidateEnd];
        candidateDiscovery.Should().Contain("AND EXISTS");
        candidateDiscovery.Should().Contain("AND NOT EXISTS");
        candidateDiscovery.Should().Contain("a.[IsActive] = 1");
        candidateDiscovery.Should().Contain("a.[StartDate] IS NOT NULL OR a.[Status] <> 'S'");

        var parentRecheckEnd = sql.IndexOf(";", parentLock, StringComparison.Ordinal);
        parentRecheckEnd.Should().BeGreaterThan(parentLock);
        var parentRecheck = sql[parentLock..parentRecheckEnd];
        parentRecheck.Should().Contain("t.[Stage] = 12");
        parentRecheck.Should().Contain("t.[Status] = 'S'");
        parentRecheck.Should().Contain("t.[IsActive] = 1");
        parentRecheck.Should().Contain("EXISTS (SELECT 1 FROM @LockedAssignees WHERE [IsActive] = 1)");
        parentRecheck.Should().Contain("[StartDate] IS NOT NULL OR [Status] <> 'S'");

        sql.Should().Contain("FROM dbo.[PoTask] AS t WITH (READCOMMITTED)",
            "candidate discovery must not retain a parent lock while waiting for child locks");
        sql.Should().Contain("FROM dbo.[tbl_yarn_order] AS y WITH (UPDLOCK, HOLDLOCK)");

        var myUpdateStart = manageTaskSql.IndexOf("IF (@op = 'MYUPDATE')", StringComparison.Ordinal);
        var assigneeMutation = manageTaskSql.IndexOf(
            "UPDATE [dbo].[PoTaskAssignee]",
            Math.Max(myUpdateStart, 0),
            StringComparison.Ordinal);
        var parentRecompute = manageTaskSql.IndexOf(
            "EXEC [dbo].[sp_PoTask_Recompute]",
            Math.Max(assigneeMutation, 0),
            StringComparison.Ordinal);
        assigneeMutation.Should().BeGreaterThan(myUpdateStart);
        parentRecompute.Should().BeGreaterThan(assigneeMutation,
            "MYUPDATE establishes the child-then-parent mutation order that reuse must match");

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
    public void SaveYarnOrder_ReactivatesExactDroppedRowAndCountsOnlyActiveDetails()
    {
        var sql = ReadProcedure("sp_SaveYarnOrder.sql");

        var updateStart = sql.IndexOf("UPDATE d", StringComparison.Ordinal);
        updateStart.Should().BeGreaterThanOrEqualTo(0);
        var insertStart = sql.IndexOf("INSERT INTO dbo.[tbl_yarn_order_detail]", updateStart, StringComparison.Ordinal);
        insertStart.Should().BeGreaterThan(updateStart);
        var exactKeyUpdate = sql[updateStart..insertStart];

        exactKeyUpdate.Should().Contain("d.[is_dropped] = 0");
        exactKeyUpdate.Should().Contain("d.[drop_date] = NULL");
        exactKeyUpdate.Should().Contain("d.[drop_by] = NULL");
        exactKeyUpdate.Should().Contain("d.[drop_note] = NULL");

        var totalsStart = sql.IndexOf("SELECT @total =", insertStart, StringComparison.Ordinal);
        totalsStart.Should().BeGreaterThan(insertStart);
        var totalsEnd = sql.IndexOf("UPDATE dbo.[tbl_yarn_order]", totalsStart, StringComparison.Ordinal);
        totalsEnd.Should().BeGreaterThan(totalsStart);
        sql[totalsStart..totalsEnd].Should().Contain("[is_dropped] = 0",
            "dropped rows must not contribute to total kilograms, order count, or line count");

        var duplicateGuardStart = sql.IndexOf("WHERE NOT EXISTS", insertStart, StringComparison.Ordinal);
        duplicateGuardStart.Should().BeGreaterThan(insertStart);
        var duplicateGuardEnd = sql.IndexOf(");", duplicateGuardStart, StringComparison.Ordinal);
        duplicateGuardEnd.Should().BeGreaterThan(duplicateGuardStart);
        sql[duplicateGuardStart..duplicateGuardEnd].Should().NotContain("[is_dropped] = 0",
            "an exact dropped row must be reactivated instead of bypassed by a duplicate insert");
    }

    [Fact]
    public void SaveYarnOrder_ValidatesRawJsonWidthsBeforeTypedConversion()
    {
        var sql = ReadProcedure("sp_SaveYarnOrder.sql");

        var rawStart = sql.IndexOf("DECLARE @Raw TABLE", StringComparison.Ordinal);
        rawStart.Should().BeGreaterThanOrEqualTo(0);
        var rawEnd = sql.IndexOf("DECLARE @Incoming TABLE", rawStart, StringComparison.Ordinal);
        rawEnd.Should().BeGreaterThan(rawStart);
        var rawDeclaration = sql[rawStart..rawEnd];
        foreach (var column in new[] { "product_id", "yarn_name", "color", "ply", "order_no", "import_kg_text" })
            rawDeclaration.Should().Contain($"[{column}] NVARCHAR(MAX)");

        var jsonStart = sql.IndexOf("FROM OPENJSON(@LinesJson)", StringComparison.Ordinal);
        jsonStart.Should().BeGreaterThanOrEqualTo(0);
        var jsonEnd = sql.IndexOf(");", jsonStart, StringComparison.Ordinal);
        jsonEnd.Should().BeGreaterThan(jsonStart);
        var jsonProjection = sql[jsonStart..jsonEnd];
        foreach (var property in new[] { "productId", "yarnName", "color", "ply", "orderNo", "importKg" })
            jsonProjection.Should().Contain($"[{property}] NVARCHAR(MAX)");

        sql.Should().Contain("LEN([product_id]) > 100");
        sql.Should().Contain("LEN([yarn_name]) > 200");
        sql.Should().Contain("LEN([color]) > 100");
        sql.Should().Contain("LEN([ply]) > 20");
        sql.Should().Contain("LEN([order_no]) > 50");
        sql.Should().Contain("CONVERT(VARCHAR(100), [product_id])");
        sql.Should().Contain("CONVERT(VARCHAR(200), [yarn_name])");
        sql.Should().Contain("CONVERT(VARCHAR(100), [color])");
        sql.Should().Contain("CONVERT(VARCHAR(20), [ply])");
        sql.Should().Contain("CONVERT(VARCHAR(50), [order_no])");
    }

    [Fact]
    public void ManageYarnOrder_CompletesExactRefTaskAndUsesOrderOnlyForLegacyNullRef()
    {
        var sql = ReadProcedure("sp_ManageYarnOrder.sql");

        var selectionStart = sql.IndexOf("INSERT INTO @closing (PoTaskId)", StringComparison.Ordinal);
        selectionStart.Should().BeGreaterThanOrEqualTo(0);
        var selectionEnd = sql.IndexOf("DECLARE @closeNote", selectionStart, StringComparison.Ordinal);
        selectionEnd.Should().BeGreaterThan(selectionStart);
        var closingSelection = sql[selectionStart..selectionEnd];

        closingSelection.Should().Contain("t.[RefId] = @invYoId",
            "RefId is the authoritative identity of a current aggregate Yarn Order task");
        closingSelection.Should().Contain("OR (t.[RefId] IS NULL",
            "only legacy tasks without RefId may fall back to production-order membership");
        closingSelection.Should().Contain("FROM @orders AS o");
        closingSelection.Should().Contain("WHERE o.[order_no] = t.[OrderNo]");
        closingSelection.Should().NotContain("INNER JOIN @orders",
            "an unconditional display-order join can close a different RefId-linked aggregate task");
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
