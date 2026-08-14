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

    [Fact]
    public void OrderKg_RoundsExactShortQuantityUpForImportRequest()
    {
        var line = new BomYarnLineDto { ShortfallKg = -1.10m };

        line.OrderQtyKg.Should().Be(1.10m);
        line.OrderKg.Should().Be(2m);

        line.OrderQtyKg = 0.70m;
        line.OrderKg.Should().Be(1m);
    }
}
