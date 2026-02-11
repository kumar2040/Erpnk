using System.ComponentModel.DataAnnotations;

namespace NkplmErp.Domain.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string Token { get; set; } = string.Empty;

    public DateTime Expires { get; set; }

    public bool IsExpired => DateTime.UtcNow >= Expires;

    public DateTime Created { get; set; } = DateTime.UtcNow;

    public string CreatedByIp { get; set; } = string.Empty;

    public DateTime? Revoked { get; set; }

    public string? RevokedByIp { get; set; }

    public string? ReplacedByToken { get; set; }

    public bool IsActive => Revoked == null && !IsExpired;

    [Required]
    public string UserId { get; set; } = string.Empty;
    public virtual User User { get; set; } = null!;
}
