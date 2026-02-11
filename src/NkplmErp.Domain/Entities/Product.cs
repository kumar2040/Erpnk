using NkplmErp.Domain.Common;

namespace NkplmErp.Domain.Entities;

public class Product : BaseAuditableEntity, ITenant
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public Guid TenantId { get; set; }
}
