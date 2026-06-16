using Microsoft.AspNetCore.Identity;

namespace NkplmErp.Domain.Entities;

public class User : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Guid? BranchId { get; set; }
    public bool IsActive { get; set; } = true;
    public string? MfaSecret { get; set; }
    public bool MfaEnabled { get; set; }
    public string? AssignedGauge { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
}
