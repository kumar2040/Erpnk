namespace NkplmErp.Application.Interfaces;

public interface ICurrentUserService
{
    string? UserId { get; }
    Guid? TenantId { get; }
}
