using NkplmErp.Application.Interfaces;

namespace NkplmErp.IntegrationTests.Mocks;

public class TestCurrentUserService : ICurrentUserService
{
    public string? UserId { get; set; } = "test-user";
    public Guid? TenantId { get; set; }
}
