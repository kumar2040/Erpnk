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
