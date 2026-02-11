using System;
using NkplmErp.Domain.Common;

namespace NkplmErp.Domain.Entities;

public class Order : BaseAuditableEntity, ITenant
{
    public string CustomerId { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
    public string Status { get; set; } = "NotStarted"; // NotStarted, Running, Completed
    public decimal TotalAmount { get; set; }
    public Guid TenantId { get; set; }
}
