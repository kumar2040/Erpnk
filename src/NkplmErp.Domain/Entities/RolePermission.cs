using System.ComponentModel.DataAnnotations;

namespace NkplmErp.Domain.Entities;

public class RolePermission
{
    [Required]
    public string RoleId { get; set; } = string.Empty;
    public virtual Role Role { get; set; } = null!;

    [Required]
    public Guid PermissionId { get; set; }
    public virtual Permission Permission { get; set; } = null!;
}
