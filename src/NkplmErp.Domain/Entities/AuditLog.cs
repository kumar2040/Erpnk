using NkplmErp.Domain.Common;

namespace NkplmErp.Domain.Entities;

public class AuditLog : BaseEntity
{
    public string? UserId { get; set; }
    public string? Type { get; set; } // Create, Update, Delete
    public string? TableName { get; set; }
    public DateTime DateTime { get; set; }
    public string? OldValues { get; set; } // JSON
    public string? NewValues { get; set; } // JSON
    public string? AffectedColumns { get; set; } // JSON
    public string? PrimaryKey { get; set; } // JSON
}
