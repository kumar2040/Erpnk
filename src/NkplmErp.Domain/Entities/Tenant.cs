using NkplmErp.Domain.Common;

namespace NkplmErp.Domain.Entities;

public class Tenant : BaseAuditableEntity
{
    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty; // Unique mnemonic
    public bool IsActive { get; set; } = true;
}
