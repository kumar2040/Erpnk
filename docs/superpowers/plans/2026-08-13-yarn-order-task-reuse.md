# Yarn Order Task Reuse Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create one RefId-linked Yarn Order task for an import request and reuse that Yarn Order/task for later requests only until an assignee starts it, while displaying every attached production order number.

**Architecture:** Keep the existing BOM, Yarn Order, and PoTask slices. `BomService` resolves the configured Yarn-role users and calls an expanded `sp_SaveYarnOrder`; the procedure serializes the create-or-append decision with a transaction-owned application lock and creates the Stage 12 task atomically. `sp_GetPoTask` derives Stage 12 production orders and navigation from `PoTask.RefId`, while the API/UI consume the standard `IResponse<T>` envelope.

**Tech Stack:** .NET 9, C#, ASP.NET Core, Blazor, xUnit, Moq, FluentAssertions, SQL Server/T-SQL, Microsoft.Build.Sql

**Spec:** `docs/superpowers/specs/2026-08-13-yarn-order-task-reuse-design.md`

## Global Constraints

- Reuse only an active Stage 12 task whose stored status is `S` and whose active assignees all have status `S` with `StartDate IS NULL`.
- Once any assignee starts, holds, or completes the task, the next request creates a new Yarn Order and task.
- The Yarn Order number remains unchanged when a request is appended.
- Stage 12 task cards/details show distinct production order numbers from `tbl_yarn_order_detail` and navigate by `PoTask.RefId`.
- Do not add a database table, parallel API slice, new UI page, or `PoTaskOrder` membership for Yarn Orders.
- Stored procedures own validation, transaction decisions, and user-facing messages; C# services remain thin and return `IResponse<T>`.
- Use `IGenericRepository` for the changed save call; do not add raw ADO.NET.
- Prepare SQL files only. Never execute SQL against the user's database.
- Preserve the existing BOM decimal/round-up UI changes and do not stage `src/NkplmErp.API/Properties/PublishProfiles/FolderProfile.pubxml.user`.
- Existing legacy Stage 20 Yarn tasks remain unchanged and are never eligible for automatic reuse.

## File Map

- `src/NkplmErp.Shared/DTOs/BomDtos.cs`: expanded Yarn Order save result.
- `src/NkplmErp.Shared/DTOs/PoTaskDtos.cs`: notification-kind documentation.
- `src/NkplmErp.Application/Interfaces/IBomService.cs`: standard wrapped save signature.
- `src/NkplmErp.Infrastructure/Services/BomService.cs`: role resolution, JSON projection, generic repository call, response wrapping.
- `src/NkplmErp.API/Controllers/BomController.cs`: consume the envelope and stop creating manual follow-up tasks.
- `src/NkplmErp.Blazor/Services/Bom/BomApiClient.cs`: deserialize the response envelope.
- `src/NkplmErp.Blazor/Pages/Bom.razor.cs`: show the procedure-authored created/appended message.
- `database/dbo/Procedure/sp_SaveYarnOrder.sql`: transactional create/reuse, line upsert, Stage 12 task, notification.
- `database/dbo/Procedure/sp_GetPoTask.sql`: RefId-based Stage 12 orders, search, and URL.
- `tests/NkplmErp.UnitTests/NkplmErp.UnitTests.csproj`: project references needed by the focused tests.
- `tests/NkplmErp.UnitTests/Bom/PlaceYarnOrderResultTests.cs`: shared-contract regression tests.
- `tests/NkplmErp.UnitTests/Bom/BomServiceTests.cs`: service orchestration tests.
- `tests/NkplmErp.UnitTests/Bom/BomControllerTests.cs`: controller follow-up regression test.
- `tests/NkplmErp.UnitTests/Bom/YarnOrderSqlContractTests.cs`: offline SQL source-contract checks.

---

### Task 1: Expand the Yarn Order result contract

**Files:**
- Create: `tests/NkplmErp.UnitTests/Bom/PlaceYarnOrderResultTests.cs`
- Modify: `src/NkplmErp.Shared/DTOs/BomDtos.cs:86-95`
- Modify: `src/NkplmErp.Shared/DTOs/PoTaskDtos.cs:100-107`

**Interfaces:**
- Consumes: current `PlaceYarnOrderResult.YoNo`, `YoId`, `TotalKg`, `Message`.
- Produces: `PoTaskId`, `WasAppended`, `OrderCount`, `LineCount`, and `IsSuccess` requiring both persisted Yarn Order and task identities.

- [ ] **Step 1: Write the failing shared-contract tests**

Create `tests/NkplmErp.UnitTests/Bom/PlaceYarnOrderResultTests.cs`:

```csharp
using FluentAssertions;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.UnitTests.Bom;

public class PlaceYarnOrderResultTests
{
    [Fact]
    public void IsSuccess_RequiresYarnOrderAndTaskIdentities()
    {
        new PlaceYarnOrderResult { YoNo = "Natureknit Yarn-012", YoId = 12, PoTaskId = 0 }
            .IsSuccess.Should().BeFalse();

        new PlaceYarnOrderResult { YoNo = "Natureknit Yarn-012", YoId = 12, PoTaskId = 44 }
            .IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void AppendMetadata_PreservesExistingTaskAndOrderCounts()
    {
        var result = new PlaceYarnOrderResult
        {
            YoNo = "Natureknit Yarn-012",
            YoId = 12,
            PoTaskId = 44,
            WasAppended = true,
            OrderCount = 2,
            LineCount = 5
        };

        result.WasAppended.Should().BeTrue();
        result.OrderCount.Should().Be(2);
        result.LineCount.Should().Be(5);
    }
}
```

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
dotnet test tests/NkplmErp.UnitTests/NkplmErp.UnitTests.csproj --no-restore --filter "FullyQualifiedName~PlaceYarnOrderResultTests" --verbosity minimal
```

Expected: compilation fails because `PoTaskId`, `WasAppended`, `OrderCount`, and `LineCount` do not exist.

- [ ] **Step 3: Implement the result properties**

Replace `PlaceYarnOrderResult` with:

```csharp
public class PlaceYarnOrderResult
{
    public string? YoNo { get; set; }
    public int YoId { get; set; }
    public decimal TotalKg { get; set; }
    public int PoTaskId { get; set; }
    public bool WasAppended { get; set; }
    public int OrderCount { get; set; }
    public int LineCount { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsSuccess => YoId > 0 && PoTaskId > 0 && !string.IsNullOrWhiteSpace(YoNo);
}
```

Update the `PoTaskNotificationDto.Kind` comment to:

```csharp
public string? Kind { get; set; }       // 'A' assigned, 'R' reminder, 'U' task updated
```

- [ ] **Step 4: Run the focused test and verify GREEN**

Run the command from Step 2. Expected: both `PlaceYarnOrderResultTests` pass.

- [ ] **Step 5: Commit the contract**

```powershell
git add -f -- tests/NkplmErp.UnitTests/Bom/PlaceYarnOrderResultTests.cs
git add -- src/NkplmErp.Shared/DTOs/BomDtos.cs src/NkplmErp.Shared/DTOs/PoTaskDtos.cs
git commit -m "feat: expand yarn order task result"
```

Do not stage the publish-profile user file.

---

### Task 2: Move Yarn task orchestration into the BOM service boundary

**Files:**
- Create: `tests/NkplmErp.UnitTests/Bom/BomServiceTests.cs`
- Modify: `tests/NkplmErp.UnitTests/NkplmErp.UnitTests.csproj`
- Modify: `src/NkplmErp.Application/Interfaces/IBomService.cs:20-25`
- Modify: `src/NkplmErp.Infrastructure/Services/BomService.cs:1-88`

**Interfaces:**
- Consumes: `IRoleManagementService.GetAllUsersWithRolesAsync()`, configuration key `TaskAutomation:YarnRoleName`, and `IGenericRepository.GetQueryFirstOrDefaultResultAsync<T>()`.
- Produces: `Task<IResponse<PlaceYarnOrderResult>> PlaceYarnOrderAsync(PlaceYarnOrderRequest request, string? createdBy)` and procedure parameters `{ CreatedBy, LinesJson, AssigneeUserIds }`.

- [ ] **Step 1: Add test-project references**

Add these references beside the existing Security reference in `tests/NkplmErp.UnitTests/NkplmErp.UnitTests.csproj`:

```xml
<ProjectReference Include="..\..\src\NkplmErp.Application\NkplmErp.Application.csproj" />
<ProjectReference Include="..\..\src\NkplmErp.Infrastructure\NkplmErp.Infrastructure.csproj" />
<ProjectReference Include="..\..\src\NkplmErp.API\NkplmErp.API.csproj" />
```

- [ ] **Step 2: Write the failing service tests**

Create `tests/NkplmErp.UnitTests/Bom/BomServiceTests.cs`:

```csharp
using System.Data;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NkplmErp.Application.Interfaces;
using NkplmErp.Infrastructure.Services;
using NkplmErp.Shared.DataAccess.GenericRepository;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.UnitTests.Bom;

public class BomServiceTests
{
    [Fact]
    public async Task PlaceYarnOrderAsync_PassesDistinctConfiguredYarnUsersAndWrapsResult()
    {
        var repo = new Mock<IGenericRepository>();
        var roles = new Mock<IRoleManagementService>();
        object? captured = null;

        roles.Setup(x => x.GetAllUsersWithRolesAsync()).ReturnsAsync(new[]
        {
            new UserWithRolesDto { UserId = "u1", RoleName = "Yarn" },
            new UserWithRolesDto { UserId = "u1", RoleName = "Yarn" },
            new UserWithRolesDto { UserId = "u2", RoleName = "yarn" },
            new UserWithRolesDto { UserId = "u3", RoleName = "Production" }
        });
        repo.Setup(x => x.GetQueryFirstOrDefaultResultAsync<PlaceYarnOrderResult>(
                "sp_SaveYarnOrder", It.IsAny<object>(), CommandType.StoredProcedure))
            .Callback<string, object, CommandType>((_, value, _) => captured = value)
            .ReturnsAsync(new PlaceYarnOrderResult
            {
                YoNo = "Natureknit Yarn-012", YoId = 12, PoTaskId = 44,
                TotalKg = 3m, OrderCount = 1, LineCount = 1, Message = "Created"
            });

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;",
            ["TaskAutomation:YarnRoleName"] = "Yarn"
        }).Build();
        var sut = new BomService(config, repo.Object, roles.Object, NullLogger<BomService>.Instance);

        var response = await sut.PlaceYarnOrderAsync(new PlaceYarnOrderRequest
        {
            Lines =
            {
                new YarnOrderLineDto
                {
                    ProductId = "P1", YarnName = "Cashmere", Color = "Dust",
                    Ply = "10", OrderNo = "GT-26012", ImportKg = 2m
                }
            }
        }, "creator");

        response.Succeeded.Should().BeTrue();
        response.Data.PoTaskId.Should().Be(44);
        captured.Should().NotBeNull();
        captured!.GetType().GetProperty("AssigneeUserIds")!.GetValue(captured)
            .Should().Be("u1|u2");
        var json = (string)captured.GetType().GetProperty("LinesJson")!.GetValue(captured)!;
        JsonDocument.Parse(json).RootElement[0].GetProperty("importKg").GetDecimal().Should().Be(2m);
    }

    [Fact]
    public async Task PlaceYarnOrderAsync_WithNoLines_ReturnsFailureWithoutDatabaseCall()
    {
        var repo = new Mock<IGenericRepository>();
        var roles = new Mock<IRoleManagementService>();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = "Server=unused;Database=unused;"
        }).Build();
        var sut = new BomService(config, repo.Object, roles.Object, NullLogger<BomService>.Instance);

        var response = await sut.PlaceYarnOrderAsync(new PlaceYarnOrderRequest(), "creator");

        response.Succeeded.Should().BeFalse();
        repo.Verify(x => x.GetQueryFirstOrDefaultResultAsync<PlaceYarnOrderResult>(
            It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CommandType>()), Times.Never);
    }
}
```

- [ ] **Step 3: Run the service tests and verify RED**

```powershell
dotnet test tests/NkplmErp.UnitTests/NkplmErp.UnitTests.csproj --no-restore --filter "FullyQualifiedName~BomServiceTests" --verbosity minimal
```

Expected: compilation fails because the constructor has no role-service parameter and `PlaceYarnOrderAsync` still returns a raw result.

- [ ] **Step 4: Change the application interface**

In `IBomService.cs`, import `NkplmErp.Shared.Wrapper` and use:

```csharp
Task<IResponse<PlaceYarnOrderResult>> PlaceYarnOrderAsync(
    PlaceYarnOrderRequest request,
    string? createdBy);
```

- [ ] **Step 5: Implement the thin service orchestration**

Add `IRoleManagementService roleManagementService` to `BomService`'s constructor and store it in `_roleManagementService`. Replace the raw ADO.NET implementation of `PlaceYarnOrderAsync` with:

```csharp
public async Task<IResponse<PlaceYarnOrderResult>> PlaceYarnOrderAsync(
    PlaceYarnOrderRequest request,
    string? createdBy)
{
    if (request?.Lines == null || request.Lines.Count == 0)
        return Response<PlaceYarnOrderResult>.Fail("No lines to place.");

    try
    {
        var yarnRole = _configuration["TaskAutomation:YarnRoleName"] ?? "Yarn";
        var assigneeUserIds = (await _roleManagementService.GetAllUsersWithRolesAsync())
            .Where(u => string.Equals(u.RoleName, yarnRole, StringComparison.OrdinalIgnoreCase))
            .Select(u => u.UserId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList();

        var payload = request.Lines.Select(l => new
        {
            productId = l.ProductId,
            yarnName = l.YarnName,
            color = l.Color,
            ply = l.Ply,
            orderNo = l.OrderNo,
            importKg = l.ImportKg
        });

        var result = await _genericRepository.GetQueryFirstOrDefaultResultAsync<PlaceYarnOrderResult>(
            "sp_SaveYarnOrder",
            new
            {
                CreatedBy = createdBy,
                LinesJson = JsonSerializer.Serialize(payload),
                AssigneeUserIds = string.Join('|', assigneeUserIds)
            },
            CommandType.StoredProcedure);

        return result is { IsSuccess: true }
            ? Response<PlaceYarnOrderResult>.Success(result, result.Message)
            : Response<PlaceYarnOrderResult>.Fail(result?.Message ?? "No response from procedure.");
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Yarn order request failed.");
        return Response<PlaceYarnOrderResult>.Fail(ex.Message);
    }
}
```

Store the injected configuration in `_configuration`; retain `_connectionString` because the untouched Yarn Order read/vendor methods still use it.

- [ ] **Step 6: Run the service tests and verify GREEN**

Run the command from Step 3. Expected: both `BomServiceTests` pass.

- [ ] **Step 7: Commit the service boundary**

```powershell
git add -f -- tests/NkplmErp.UnitTests/Bom/BomServiceTests.cs
git add -- tests/NkplmErp.UnitTests/NkplmErp.UnitTests.csproj src/NkplmErp.Application/Interfaces/IBomService.cs src/NkplmErp.Infrastructure/Services/BomService.cs
git commit -m "refactor: route yarn order saves through repository"
```

---

### Task 3: Consume the envelope and remove duplicate manual-task creation

**Files:**
- Create: `tests/NkplmErp.UnitTests/Bom/BomControllerTests.cs`
- Modify: `src/NkplmErp.API/Controllers/BomController.cs:64-132`
- Modify: `src/NkplmErp.Blazor/Services/Bom/BomApiClient.cs:44-55`
- Modify: `src/NkplmErp.Blazor/Pages/Bom.razor.cs:227-268`

**Interfaces:**
- Consumes: `IResponse<PlaceYarnOrderResult>` from Task 2 and the existing BOM-task completion hooks.
- Produces: an API response envelope, no `IPoTaskService.CreateAsync` call, and a Blazor status message sourced from the procedure result.

- [ ] **Step 1: Write the failing controller regression test**

Create `tests/NkplmErp.UnitTests/Bom/BomControllerTests.cs`:

```csharp
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NkplmErp.API.Controllers;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.UnitTests.Bom;

public class BomControllerTests
{
    [Fact]
    public async Task PlaceYarnOrder_CompletesBomTaskWithoutCreatingManualYarnTask()
    {
        var bom = new Mock<IBomService>();
        var roles = new Mock<IRoleManagementService>();
        var tasks = new Mock<IPoTaskService>();
        var request = new PlaceYarnOrderRequest
        {
            Lines =
            {
                new YarnOrderLineDto { ProductId = "P1", Color = "Dust", OrderNo = "GT-26012", ImportKg = 2m }
            }
        };
        var saved = new PlaceYarnOrderResult
        {
            YoNo = "Natureknit Yarn-012", YoId = 12, PoTaskId = 44, Message = "Created"
        };

        roles.Setup(x => x.GetUserPermissionsAsync("creator")).ReturnsAsync(new UserPermissionsResponse
        {
            Permissions = { new UserPermissionDto { PageKey = "yarn-orders", CanEdit = true } }
        });
        roles.Setup(x => x.GetAllUsersWithRolesAsync()).ReturnsAsync(new[]
        {
            new UserWithRolesDto { UserId = "yarn-user", RoleName = "Yarn" }
        });
        bom.Setup(x => x.PlaceYarnOrderAsync(request, "creator"))
            .ReturnsAsync(Response<PlaceYarnOrderResult>.Success(saved, saved.Message));
        tasks.Setup(x => x.EnsureBomTaskAsync("GT-26012", null,
                It.IsAny<IEnumerable<string>>(), It.IsAny<int>(), "creator", null, null))
            .ReturnsAsync(20);
        tasks.Setup(x => x.CompleteBomOrderAsync(20, "GT-26012", It.IsAny<string>(), "creator"))
            .ReturnsAsync(Response<PoTaskBomCompleteResultDto>.Success(new PoTaskBomCompleteResultDto()));

        var controller = new BomController(
            bom.Object,
            roles.Object,
            tasks.Object,
            new ConfigurationBuilder().AddInMemoryCollection().Build(),
            NullLogger<BomController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, "creator") }, "test"))
                }
            }
        };

        var action = await controller.PlaceYarnOrder(request);

        action.Should().BeOfType<OkObjectResult>();
        tasks.Verify(x => x.CompleteBomOrderAsync(20, "GT-26012", It.IsAny<string>(), "creator"), Times.Once);
        tasks.Verify(x => x.CreateAsync(It.IsAny<CreatePoTaskRequest>(), It.IsAny<string>()), Times.Never);
    }
}
```

- [ ] **Step 2: Run the controller test and verify RED**

```powershell
dotnet test tests/NkplmErp.UnitTests/NkplmErp.UnitTests.csproj --no-restore --filter "FullyQualifiedName~BomControllerTests" --verbosity minimal
```

Expected: the test fails because the current controller calls `CreateAsync` for a manual task and consumes a raw result.

- [ ] **Step 3: Update the controller workflow**

Change the action to unwrap only for its local automation hook while returning the full envelope:

```csharp
var response = await _bomService.PlaceYarnOrderAsync(request, GetCurrentUserId());

if (response.Succeeded && response.Data is { IsSuccess: true } result)
    await AdvanceBomTasksAsync(request, result.YoNo);

return response.Succeeded ? Ok(response) : BadRequest(response);
```

In `AdvanceBomTasksAsync`, retain Yarn-role lookup, distinct order enumeration, `EnsureBomTaskAsync`, and `CompleteBomOrderAsync`. Delete only this call and its request object:

```csharp
await _poTaskService.CreateAsync(new CreatePoTaskRequest
{
    OrderNo = orderNo,
    Title = $"Make yarn order - {orderNo}",
    Detail = $"BOM {yoNo} placed for {orderNo}. Split by vendor on the Yarn Orders page and send the purchase order(s) to the supplier(s).",
    PriorityId = 2,
    CompletionRule = 2,
    StartDate = DateTime.Today,
    UserIds = yarnUsers
}, userId);
```

Update the method comments to state that `sp_SaveYarnOrder` has already created or reused the Stage 12 Yarn task.

- [ ] **Step 4: Update the typed Blazor client**

Change the method signature to:

```csharp
public async Task<Response<PlaceYarnOrderResult>> PlaceYarnOrderAsync(PlaceYarnOrderRequest request)
```

Use the standard envelope on both success and error responses:

```csharp
try
{
    var httpResponse = await _httpClient.PostAsJsonAsync($"{Base}/yarn-order", request);
    var response = await httpResponse.Content.ReadFromJsonAsync<Response<PlaceYarnOrderResult>>();
    return response ?? Response<PlaceYarnOrderResult>.Fail("The Yarn Order API returned an empty response.");
}
catch (Exception ex)
{
    _logger.LogError(ex, "PlaceYarnOrderAsync failed");
    return Response<PlaceYarnOrderResult>.Fail(ex.Message);
}
```

- [ ] **Step 5: Show the procedure-authored result in the BOM page**

Replace the raw-result condition with:

```csharp
var response = await BomApi.PlaceYarnOrderAsync(new PlaceYarnOrderRequest { Lines = lines });
var result = response.Data;

IsPlacing = false;

if (response.Succeeded && result is { IsSuccess: true })
{
    var placedNos = lines.Select(l => l.OrderNo.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    ShowStatus(string.IsNullOrWhiteSpace(result.Message)
        ? $"{result.YoNo} saved — {result.TotalKg:N2} kg across {result.OrderCount} order(s)."
        : result.Message, false);
    ClearBasket();

    foreach (var no in placedNos) PlacedOrders.Add(no);
    if (!string.IsNullOrEmpty(SelectedOrderNo) && PlacedOrders.Contains(SelectedOrderNo.Trim()))
    {
        SelectedOrderNo = null;
        YarnLines = new();
    }
}
else
{
    ShowStatus($"Could not place yarn order: {response.Messages ?? result?.Message ?? "no response"}", true);
}
```

- [ ] **Step 6: Run the controller test and compile the Blazor client**

Run the focused test from Step 2. Expected: PASS.

Run:

```powershell
cmd /c dotnet build src\NkplmErp.Blazor\NkplmErp.Blazor.csproj --no-restore --verbosity:minimal
```

Expected: Tailwind and Blazor compilation complete with zero errors.

- [ ] **Step 7: Commit the API/UI workflow**

```powershell
git add -f -- tests/NkplmErp.UnitTests/Bom/BomControllerTests.cs
git add -- src/NkplmErp.API/Controllers/BomController.cs src/NkplmErp.Blazor/Services/Bom/BomApiClient.cs src/NkplmErp.Blazor/Pages/Bom.razor.cs
git commit -m "fix: create yarn tasks only through yarn order save"
```

---

### Task 4: Make Yarn Order create-or-reuse atomic

**Files:**
- Create: `tests/NkplmErp.UnitTests/Bom/YarnOrderSqlContractTests.cs`
- Modify: `database/dbo/Procedure/sp_SaveYarnOrder.sql`

**Interfaces:**
- Consumes: `@CreatedBy`, `@LinesJson`, new `@AssigneeUserIds`, `sp_ManagePoTask @Flag='CREATE'`, Stage 12, and `PoTask.RefId`.
- Produces: one result row aliased `YoNo`, `YoId`, `TotalKg`, `PoTaskId`, `WasAppended`, `OrderCount`, `LineCount`, `Message`, `IsSuccess`.

- [ ] **Step 1: Write failing offline SQL contract tests**

Create `tests/NkplmErp.UnitTests/Bom/YarnOrderSqlContractTests.cs`:

```csharp
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
```

- [ ] **Step 2: Run the SQL contract test and verify RED**

```powershell
dotnet test tests/NkplmErp.UnitTests/NkplmErp.UnitTests.csproj --no-restore --filter "FullyQualifiedName~YarnOrderSqlContractTests.SaveYarnOrder" --verbosity minimal
```

Expected: multiple assertions fail because the current procedure always creates a header and uses `TRY/CATCH`.

- [ ] **Step 3: Add and normalize procedure inputs**

Change the signature and opening to:

```sql
CREATE OR ALTER PROCEDURE dbo.sp_SaveYarnOrder
    @CreatedBy       VARCHAR(50)  = NULL,
    @LinesJson       NVARCHAR(MAX),
    @AssigneeUserIds NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Raw TABLE
    (
        product_id VARCHAR(100), yarn_name VARCHAR(200), color VARCHAR(100),
        ply VARCHAR(20), order_no VARCHAR(50), import_kg_text NVARCHAR(50)
    );
    DECLARE @Incoming TABLE
    (
        product_id VARCHAR(100) NOT NULL, yarn_name VARCHAR(200) NULL,
        color VARCHAR(100) NOT NULL, ply VARCHAR(20) NULL,
        order_no VARCHAR(50) NOT NULL, import_kg DECIMAL(18,3) NOT NULL
    );

    IF @LinesJson IS NULL OR ISJSON(@LinesJson) <> 1
    BEGIN
        SELECT CAST(NULL AS VARCHAR(30)) AS [YoNo], -1 AS [YoId],
               CAST(0 AS DECIMAL(18,3)) AS [TotalKg], -1 AS [PoTaskId],
               CAST(0 AS BIT) AS [WasAppended], 0 AS [OrderCount], 0 AS [LineCount],
               'Invalid or empty line data.' AS [Message], CAST(0 AS BIT) AS [IsSuccess];
        RETURN;
    END;

    INSERT @Raw (product_id, yarn_name, color, ply, order_no, import_kg_text)
    SELECT NULLIF(LTRIM(RTRIM(productId)), ''), NULLIF(LTRIM(RTRIM(yarnName)), ''),
           NULLIF(LTRIM(RTRIM(color)), ''), NULLIF(LTRIM(RTRIM(ply)), ''),
           NULLIF(LTRIM(RTRIM(orderNo)), ''), importKg
    FROM OPENJSON(@LinesJson) WITH
    (
        productId VARCHAR(100) '$.productId', yarnName VARCHAR(200) '$.yarnName',
        color VARCHAR(100) '$.color', ply VARCHAR(20) '$.ply',
        orderNo VARCHAR(50) '$.orderNo', importKg NVARCHAR(50) '$.importKg'
    );

    IF NOT EXISTS (SELECT 1 FROM @Raw)
       OR EXISTS (SELECT 1 FROM @Raw WHERE product_id IS NULL OR color IS NULL OR order_no IS NULL
                  OR TRY_CONVERT(DECIMAL(18,3), import_kg_text) IS NULL
                  OR TRY_CONVERT(DECIMAL(18,3), import_kg_text) <= 0)
       OR NULLIF(LTRIM(RTRIM(@AssigneeUserIds)), '') IS NULL
    BEGIN
        SELECT CAST(NULL AS VARCHAR(30)) AS [YoNo], -1 AS [YoId],
               CAST(0 AS DECIMAL(18,3)) AS [TotalKg], -1 AS [PoTaskId],
               CAST(0 AS BIT) AS [WasAppended], 0 AS [OrderCount], 0 AS [LineCount],
               'Valid yarn lines and at least one Yarn-role assignee are required.' AS [Message],
               CAST(0 AS BIT) AS [IsSuccess];
        RETURN;
    END;

    INSERT @Incoming (product_id, yarn_name, color, ply, order_no, import_kg)
    SELECT product_id, MAX(yarn_name), color, ply, order_no,
           SUM(TRY_CONVERT(DECIMAL(18,3), import_kg_text))
    FROM @Raw
    GROUP BY product_id, color, ply, order_no;
```

- [ ] **Step 4: Serialize eligibility and select only an unstarted task**

Continue the procedure with:

```sql
    DECLARE @lockResult INT, @yoId INT, @poTaskId INT,
            @yoNo VARCHAR(30), @wasAppended BIT = 0,
            @firstOrder VARCHAR(50), @incomingOrders NVARCHAR(MAX);

    SELECT TOP (1) @firstOrder = order_no FROM @Incoming ORDER BY order_no;
    SELECT @incomingOrders = STRING_AGG(CONVERT(NVARCHAR(MAX), x.order_no), N', ')
    FROM (SELECT DISTINCT order_no FROM @Incoming) x;

    BEGIN TRANSACTION;

    EXEC @lockResult = sys.sp_getapplock
        @Resource = N'NkplmErp.YarnOrder.Request',
        @LockMode = 'Exclusive',
        @LockOwner = 'Transaction',
        @LockTimeout = 15000;

    IF @lockResult < 0
        THROW 50001, 'Could not acquire the Yarn Order request lock.', 1;

    SELECT TOP (1)
        @poTaskId = t.[PoTaskId], @yoId = t.[RefId], @yoNo = y.[yo_no]
    FROM dbo.[PoTask] t WITH (UPDLOCK, HOLDLOCK)
    INNER JOIN dbo.[tbl_yarn_order] y WITH (UPDLOCK, HOLDLOCK)
        ON y.[yo_id] = t.[RefId]
    WHERE t.[Stage] = 12
      AND t.[Status] = 'S'
      AND t.[IsActive] = 1
      AND EXISTS
          (SELECT 1 FROM dbo.[PoTaskAssignee] a
           WHERE a.[PoTaskId] = t.[PoTaskId] AND a.[IsActive] = 1)
      AND NOT EXISTS
          (SELECT 1 FROM dbo.[PoTaskAssignee] a
           WHERE a.[PoTaskId] = t.[PoTaskId] AND a.[IsActive] = 1
             AND (a.[StartDate] IS NOT NULL OR a.[Status] <> 'S'))
    ORDER BY t.[PoTaskId] DESC;

    IF @poTaskId IS NOT NULL
        SET @wasAppended = 1;
    ELSE
    BEGIN
        DECLARE @nextNo INT =
            ISNULL((SELECT MAX(TRY_CONVERT(INT, RIGHT([yo_no], 3)))
                    FROM dbo.[tbl_yarn_order] WITH (UPDLOCK, HOLDLOCK)
                    WHERE [yo_no] LIKE 'Natureknit Yarn-%'), 0) + 1;
        SET @yoNo = 'Natureknit Yarn-' + RIGHT('000' + CAST(@nextNo AS VARCHAR(10)), 3);

        INSERT dbo.[tbl_yarn_order]
            ([yo_no], [created_by], [total_kg], [order_count], [line_count], [status])
        VALUES (@yoNo, @CreatedBy, 0, 0, 0, 'Placed');
        SET @yoId = CAST(SCOPE_IDENTITY() AS INT);
    END;
```

- [ ] **Step 5: Upsert lines and recalculate the header**

Use an update-then-insert pattern; do not use SQL Server `MERGE`:

```sql
    UPDATE d
       SET d.[yarn_name] = i.yarn_name,
           d.[import_kg] = i.import_kg
    FROM dbo.[tbl_yarn_order_detail] d
    INNER JOIN @Incoming i
      ON i.product_id = d.[product_id]
     AND i.color = d.[color]
     AND ISNULL(i.ply, '') = ISNULL(d.[ply], '')
     AND i.order_no = d.[order_no]
    WHERE d.[yo_id] = @yoId;

    INSERT dbo.[tbl_yarn_order_detail]
        ([yo_id], [product_id], [yarn_name], [color], [ply], [order_no], [import_kg])
    SELECT @yoId, i.product_id, i.yarn_name, i.color, i.ply, i.order_no, i.import_kg
    FROM @Incoming i
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.[tbl_yarn_order_detail] d WITH (UPDLOCK, HOLDLOCK)
        WHERE d.[yo_id] = @yoId
          AND d.[product_id] = i.product_id
          AND d.[color] = i.color
          AND ISNULL(d.[ply], '') = ISNULL(i.ply, '')
          AND d.[order_no] = i.order_no
    );

    DECLARE @total DECIMAL(18,3), @orderCnt INT, @lineCnt INT;
    SELECT @total = ISNULL(SUM([import_kg]), 0),
           @orderCnt = COUNT(DISTINCT [order_no]),
           @lineCnt = COUNT(*)
    FROM dbo.[tbl_yarn_order_detail]
    WHERE [yo_id] = @yoId;

    UPDATE dbo.[tbl_yarn_order]
       SET [total_kg] = @total, [order_count] = @orderCnt, [line_count] = @lineCnt
    WHERE [yo_id] = @yoId;
```

- [ ] **Step 6: Create the task or notify its assignees, then return metadata**

Complete the transaction with:

```sql
    IF @wasAppended = 0
    BEGIN
        DECLARE @CreatedTask TABLE ([PoTaskId] INT);
        INSERT @CreatedTask ([PoTaskId])
        EXEC dbo.[sp_ManagePoTask]
            @Flag = 'CREATE',
            @OrderNo = @firstOrder,
            @Stage = 12,
            @Title = N'Make yarn order - ' + @yoNo,
            @Detail = N'Place the vendor yarn order for ' + @yoNo + N'. Production orders: ' + @incomingOrders,
            @RefId = @yoId,
            @PriorityId = 2,
            @CompletionRule = 2,
            @StartDate = GETDATE(),
            @AssigneeUserIds = @AssigneeUserIds,
            @UserId = @CreatedBy;

        SELECT @poTaskId = [PoTaskId] FROM @CreatedTask;
    END
    ELSE
    BEGIN
        UPDATE dbo.[PoTask]
           SET [ModifiedBy] = @CreatedBy, [ModifiedDate] = GETDATE(),
               [Detail] = N'Place the vendor yarn order for ' + @yoNo
                        + N'. Production orders: '
                        + (SELECT STRING_AGG(CONVERT(NVARCHAR(MAX), x.[order_no]), N', ')
                           FROM (SELECT DISTINCT [order_no]
                                 FROM dbo.[tbl_yarn_order_detail]
                                 WHERE [yo_id] = @yoId) x)
        WHERE [PoTaskId] = @poTaskId;

        INSERT dbo.[PoTaskNotification] ([UserId], [PoTaskId], [Kind], [Title], [Body])
        SELECT a.[UserId], @poTaskId, 'U', N'Yarn order updated',
               @yoNo + N' now includes ' + @incomingOrders
        FROM dbo.[PoTaskAssignee] a
        WHERE a.[PoTaskId] = @poTaskId AND a.[IsActive] = 1;
    END;

    COMMIT TRANSACTION;

    SELECT @yoNo AS [YoNo], @yoId AS [YoId], @total AS [TotalKg],
           @poTaskId AS [PoTaskId], @wasAppended AS [WasAppended],
           @orderCnt AS [OrderCount], @lineCnt AS [LineCount],
           CASE WHEN @wasAppended = 1
                THEN 'Added request to ' + @yoNo + '. Yarn Order task reused.'
                ELSE @yoNo + ' created. Yarn Order task created.' END AS [Message],
           CAST(1 AS BIT) AS [IsSuccess];
END;
```

- [ ] **Step 7: Run the save-procedure contract test and verify GREEN**

Run the command from Step 2. Expected: PASS.

- [ ] **Step 8: Commit the atomic write path**

```powershell
git add -f -- tests/NkplmErp.UnitTests/Bom/YarnOrderSqlContractTests.cs
git add -- database/dbo/Procedure/sp_SaveYarnOrder.sql
git commit -m "feat: reuse unstarted yarn order tasks atomically"
```

---

### Task 5: Display all linked production orders and navigate by RefId

**Files:**
- Modify: `tests/NkplmErp.UnitTests/Bom/YarnOrderSqlContractTests.cs`
- Modify: `database/dbo/Procedure/sp_GetPoTask.sql:106-136,164-310`

**Interfaces:**
- Consumes: Stage 12 `PoTask.RefId = tbl_yarn_order.yo_id` and Yarn Order detail rows.
- Produces: correct `OrderNos`, `OrderCount`, `LinkUrl`, and search matching for board, My Tasks, and detail reads.

- [ ] **Step 1: Add a failing read-procedure contract test**

Add this method to `YarnOrderSqlContractTests`:

```csharp
[Fact]
public void GetPoTask_DerivesStage12OrdersAndLinkFromRefId()
{
    var sql = ReadProcedure("sp_GetPoTask.sql");

    sql.Should().Contain("t.[RefId] = yd.[yo_id]");
    sql.Should().Contain("yarnOrders.[OrderNos]");
    sql.Should().Contain("yarnOrders.[OrderCount]");
    sql.Should().Contain("yarnRef.[yo_id]");
    sql.Should().Contain("N'/yarn-orders/' + CAST(yarnRef.[yo_id] AS nvarchar(20))");
}
```

- [ ] **Step 2: Run the read-procedure test and verify RED**

```powershell
dotnet test tests/NkplmErp.UnitTests/NkplmErp.UnitTests.csproj --no-restore --filter "FullyQualifiedName~YarnOrderSqlContractTests.GetPoTask" --verbosity minimal
```

Expected: the assertions fail because Stage 12 currently derives the Yarn Order from `PoTask.OrderNo`.

- [ ] **Step 3: Add the Stage 12 order aggregation to DETAIL**

Add this `OUTER APPLY` after `FROM [dbo].[PoTask] t` in the DETAIL query:

```sql
OUTER APPLY
(
    SELECT STRING_AGG(CONVERT(nvarchar(max), yd.[order_no]), N', ')
               WITHIN GROUP (ORDER BY yd.[order_no]) AS [OrderNos],
           COUNT(*) AS [OrderCount]
    FROM
    (
        SELECT DISTINCT LTRIM(RTRIM(d.[order_no])) AS [order_no]
        FROM dbo.[tbl_yarn_order_detail] d
        WHERE t.[RefId] = d.[yo_id]
          AND NULLIF(LTRIM(RTRIM(d.[order_no])), '') IS NOT NULL
    ) yd
) yarnOrders
```

Replace the DETAIL `OrderNos` and `OrderCount` expressions with:

```sql
CASE WHEN t.[Stage] = 12 AND yarnOrders.[OrderNos] IS NOT NULL
     THEN yarnOrders.[OrderNos]
     ELSE COALESCE((SELECT STRING_AGG(CONVERT(nvarchar(max), o.[OrderNo]), N', ')
                    FROM dbo.[PoTaskOrder] o
                    WHERE o.[PoTaskId] = t.[PoTaskId] AND o.[IsActive] = 1), t.[OrderNo])
END AS [OrderNos],
CASE WHEN t.[Stage] = 12 AND yarnOrders.[OrderNos] IS NOT NULL
     THEN yarnOrders.[OrderCount]
     WHEN EXISTS (SELECT 1 FROM dbo.[PoTaskOrder] o
                  WHERE o.[PoTaskId] = t.[PoTaskId] AND o.[IsActive] = 1)
     THEN (SELECT COUNT(*) FROM dbo.[PoTaskOrder] o
           WHERE o.[PoTaskId] = t.[PoTaskId] AND o.[IsActive] = 1)
     WHEN t.[OrderNo] IS NULL THEN 0 ELSE 1
END AS [OrderCount],
```

- [ ] **Step 4: Add the same aggregation and exact Yarn reference to board/My Tasks**

Add the same `yarnOrders` apply after the existing `ord` apply in the card query. Add:

```sql
OUTER APPLY
(
    SELECT y.[yo_id]
    FROM dbo.[tbl_yarn_order] y WITH (NOLOCK)
    WHERE y.[yo_id] = t.[RefId]
) yarnRef
```

Change the card fields to:

```sql
CASE WHEN t.[Stage] = 12 AND yarnOrders.[OrderNos] IS NOT NULL
     THEN yarnOrders.[OrderNos]
     ELSE COALESCE(ord.[OrderNos], t.[OrderNo]) END AS [OrderNos],
CASE WHEN t.[Stage] = 12 AND yarnOrders.[OrderNos] IS NOT NULL
     THEN yarnOrders.[OrderCount]
     WHEN ord.[OrderNos] IS NULL THEN CASE WHEN t.[OrderNo] IS NULL THEN 0 ELSE 1 END
     ELSE ord.[OrderCount] END AS [OrderCount],
```

Replace only the Stage 12 link branch with:

```sql
WHEN 12 THEN CASE WHEN yarnRef.[yo_id] IS NOT NULL
                  THEN N'/yarn-orders/' + CAST(yarnRef.[yo_id] AS nvarchar(20))
                  WHEN yo.[yo_id] IS NOT NULL
                  THEN N'/yarn-orders/' + CAST(yo.[yo_id] AS nvarchar(20)) END
```

The second branch is the legacy display fallback; it does not make legacy tasks reusable.

- [ ] **Step 5: Include attached production orders in board search**

Extend the `@SearchOrderNo` predicate with:

```sql
OR (t.[Stage] = 12 AND EXISTS
    (SELECT 1
     FROM dbo.[tbl_yarn_order_detail] yd WITH (NOLOCK)
     WHERE yd.[yo_id] = t.[RefId]
       AND yd.[order_no] LIKE '%' + @SearchOrderNo + '%'))
```

- [ ] **Step 6: Run the read-procedure contract test and verify GREEN**

Run the command from Step 2. Expected: PASS.

- [ ] **Step 7: Commit the read model**

```powershell
git add -f -- tests/NkplmErp.UnitTests/Bom/YarnOrderSqlContractTests.cs
git add -- database/dbo/Procedure/sp_GetPoTask.sql
git commit -m "feat: show yarn task production orders"
```

---

### Task 6: Run full offline verification and prepare deployment handoff

**Files:**
- Verify only; no new production file is expected.

**Interfaces:**
- Consumes: all changes from Tasks 1-5.
- Produces: evidence that .NET tests, Blazor/API compilation, SQL project compilation, and repository hygiene pass without touching a live database.

- [ ] **Step 1: Run all unit tests**

```powershell
dotnet test tests/NkplmErp.UnitTests/NkplmErp.UnitTests.csproj --no-restore --verbosity minimal
```

Expected: all tests pass, including existing security/BOM tests and the new Yarn Order tests.

- [ ] **Step 2: Build the API and Blazor projects**

```powershell
dotnet build src/NkplmErp.API/NkplmErp.API.csproj --no-restore --verbosity minimal
cmd /c dotnet build src\NkplmErp.Blazor\NkplmErp.Blazor.csproj --no-restore --verbosity:minimal
```

Expected: both builds finish with zero errors. Existing unrelated warnings may be reported separately.

- [ ] **Step 3: Compile the database project offline**

```powershell
dotnet build database/NkplmErp.Database.sqlproj --no-restore --verbosity minimal
```

Expected: the SQL model builds successfully. If the Microsoft.Build.Sql workload is unavailable, record that tooling limitation and retain the passing SQL contract tests; do not connect to a database as a workaround.

- [ ] **Step 4: Inspect exactly what changed**

```powershell
git diff --check
git status --short
git diff HEAD~5 -- src/NkplmErp.Shared/DTOs/BomDtos.cs src/NkplmErp.Shared/DTOs/PoTaskDtos.cs src/NkplmErp.Application/Interfaces/IBomService.cs src/NkplmErp.Infrastructure/Services/BomService.cs src/NkplmErp.API/Controllers/BomController.cs src/NkplmErp.Blazor/Services/Bom/BomApiClient.cs src/NkplmErp.Blazor/Pages/Bom.razor.cs database/dbo/Procedure/sp_SaveYarnOrder.sql database/dbo/Procedure/sp_GetPoTask.sql
```

Confirm the publish-profile user file remains unstaged and no live SQL command was run.

- [ ] **Step 5: Perform a final requirements audit**

Confirm from code and tests:

- a first request returns one new `YoId` and `PoTaskId`;
- a later request can return the same IDs with `WasAppended = true`;
- eligibility requires every active assignee to remain Scheduled with a null start date;
- task creation and Yarn Order writes share one transaction/application lock;
- no controller-created manual Yarn task remains;
- Stage 12 reads show all distinct attached production orders and link by `RefId`;
- existing Stage 20 tasks are read-only legacy fallbacks;
- SQL files are ready for user deployment, but were not executed.

- [ ] **Step 6: Commit any verification-only corrections**

If verification required a focused correction, stage only its named source/test files and commit:

```powershell
git commit -m "fix: complete yarn order task reuse verification"
```

If no correction was needed, do not create an empty commit.
