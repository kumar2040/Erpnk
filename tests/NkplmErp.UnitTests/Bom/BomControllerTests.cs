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
