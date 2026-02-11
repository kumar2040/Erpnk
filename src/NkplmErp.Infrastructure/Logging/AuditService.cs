using NkplmErp.Application.Interfaces;
using NkplmErp.Infrastructure.Persistence;
using NkplmErp.Domain.Entities;

namespace NkplmErp.Infrastructure.Logging;

public class AuditService : IAuditService
{
    private readonly SecurityDbContext _context;

    public AuditService(SecurityDbContext context)
    {
        _context = context;
    }

    public async Task LogAsync(string userId, string action, string entityName, string entityId, string oldValues, string newValues)
    {
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = action,
            TableName = entityName,
            PrimaryKey = entityId,
            OldValues = oldValues,
            NewValues = newValues,
            DateTime = DateTime.UtcNow
        };

        _context.Set<AuditLog>().Add(auditLog);
        await _context.SaveChangesAsync();
    }
}
