namespace NkplmErp.Application.Interfaces;

public interface IAuditService
{
    Task LogAsync(string userId, string action, string entityName, string entityId, string oldValues, string newValues);
}
