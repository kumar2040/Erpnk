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
