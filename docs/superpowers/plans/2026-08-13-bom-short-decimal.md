# BOM Short Quantity Decimal Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Preserve the exact calculated BOM shortage as the default yarn-order quantity and display shortage values with two-decimal precision.

**Architecture:** Keep the existing stored-procedure, API, basket, and save flow unchanged because they already use `decimal`. Change the shared DTO's default editable order quantity to the existing exact `ImportKg`, then align the BOM Razor input increment and total formatting with that precision.

**Tech Stack:** .NET 9, C#, xUnit, Blazor Razor components

## Global Constraints

- Do not change the import/stock decision logic.
- Do not change stored procedures, endpoints, database schema, basket aggregation, or the API save flow.
- Preserve non-negative user overrides of `OrderQtyKg`.
- Use two-decimal display precision and a `0.01` kg input increment.
- Leave unrelated working-tree changes untouched.

---

### Task 1: Preserve decimal shortage quantities

**Files:**
- Create: `tests/NkplmErp.UnitTests/Bom/BomYarnLineDtoTests.cs`
- Modify: `src/NkplmErp.Shared/DTOs/BomDtos.cs:44-59`
- Modify: `src/NkplmErp.Blazor/Pages/Bom.razor:153-180`

**Interfaces:**
- Consumes: `BomYarnLineDto.ShortfallKg`, `BomYarnLineDto.ImportKg`, and `BomYarnLineDto.OrderQtyKg`.
- Produces: `BomYarnLineDto.OrderQtyKg` defaulting to the exact positive `ImportKg`; the public property signatures remain unchanged.

- [ ] **Step 1: Write the failing regression test**

Create `tests/NkplmErp.UnitTests/Bom/BomYarnLineDtoTests.cs`:

```csharp
using FluentAssertions;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.UnitTests.Bom;

public class BomYarnLineDtoTests
{
    [Fact]
    public void OrderQtyKg_WhenShortageHasDecimals_DefaultsToExactImportQuantity()
    {
        var line = new BomYarnLineDto { ShortfallKg = -2.42m };

        line.ImportKg.Should().Be(2.42m);
        line.OrderQtyKg.Should().Be(2.42m);
    }

    [Fact]
    public void OrderQtyKg_WhenOverridden_PreservesDecimalAndClampsNegativeValues()
    {
        var line = new BomYarnLineDto { ShortfallKg = -2.42m };

        line.OrderQtyKg = 2.37m;
        line.OrderQtyKg.Should().Be(2.37m);

        line.OrderQtyKg = -1m;
        line.OrderQtyKg.Should().Be(0m);
    }
}
```

The first test catches a reintroduction of whole-kilogram ceiling behavior. Its expected `2.42m` value is hand-derived from the literal signed shortage rather than computed with production helpers.

- [ ] **Step 2: Run the focused test and verify RED**

Run:

```powershell
dotnet test tests/NkplmErp.UnitTests/NkplmErp.UnitTests.csproj --no-restore --filter "FullyQualifiedName~BomYarnLineDtoTests" --verbosity minimal
```

Expected: `OrderQtyKg_WhenShortageHasDecimals_DefaultsToExactImportQuantity` fails because the current default is `3m`, while the expected value is `2.42m`.

- [ ] **Step 3: Implement the exact decimal default**

In `src/NkplmErp.Shared/DTOs/BomDtos.cs`, remove the whole-cone ceiling rule and make the default order quantity equal the exact import quantity:

```csharp
/// <summary>Suggested order quantity: the exact positive shortage in kg.</summary>
public decimal OrderKg => ImportKg;

// User-editable order weight. Defaults to the exact shortage (OrderKg);
// the buyer can override it, and the override is what gets ordered/saved.
```

Keep the existing `OrderQtyKg` setter unchanged so negative overrides remain clamped to zero.

- [ ] **Step 4: Align the BOM table input and total display**

In `src/NkplmErp.Blazor/Pages/Bom.razor`, change the import quantity input and total shortage formatting:

```razor
<input type="number" step="0.01" min="0" class="qtyinput"
       @bind="l.OrderQtyKg" @bind:event="oninput" /> kg
```

```razor
<td class="@(YarnLines.Sum(l => l.ShortfallKg) < 0 ? "short-pos" : "short-neg")">@YarnLines.Sum(l => l.ShortfallKg).ToString("N2")</td>
```

- [ ] **Step 5: Run the focused test and verify GREEN**

Run:

```powershell
dotnet test tests/NkplmErp.UnitTests/NkplmErp.UnitTests.csproj --no-restore --filter "FullyQualifiedName~BomYarnLineDtoTests" --verbosity minimal
```

Expected: both `BomYarnLineDtoTests` pass.

- [ ] **Step 6: Run regression verification**

Run:

```powershell
dotnet test tests/NkplmErp.UnitTests/NkplmErp.UnitTests.csproj --no-restore --verbosity minimal
```

Expected: all unit tests pass.

Run the Blazor build from `src/NkplmErp.Blazor` through `cmd.exe` so Windows PowerShell execution policy does not intercept `npm.ps1`:

```powershell
cmd /c dotnet build NkplmErp.Blazor.csproj --no-restore --verbosity:minimal
```

Expected: Tailwind generation and Blazor compilation succeed with no errors.

- [ ] **Step 7: Review the diff and commit the implementation**

Run:

```powershell
git diff --check
git diff -- tests/NkplmErp.UnitTests/Bom/BomYarnLineDtoTests.cs src/NkplmErp.Shared/DTOs/BomDtos.cs src/NkplmErp.Blazor/Pages/Bom.razor
```

Confirm that no stored procedure, API, basket, or unrelated file changed. Then commit only the three implementation files:

```powershell
git add -- tests/NkplmErp.UnitTests/Bom/BomYarnLineDtoTests.cs src/NkplmErp.Shared/DTOs/BomDtos.cs src/NkplmErp.Blazor/Pages/Bom.razor
git commit -m "fix: preserve decimal BOM shortage quantities"
```
