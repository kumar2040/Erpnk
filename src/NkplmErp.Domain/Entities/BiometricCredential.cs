using System.ComponentModel.DataAnnotations;

namespace NkplmErp.Domain.Entities;

public class BiometricCredential
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public byte[] DescriptorId { get; set; } = Array.Empty<byte>();

    [Required]
    public byte[] PublicKey { get; set; } = Array.Empty<byte>();

    [Required]
    public byte[] UserHandle { get; set; } = Array.Empty<byte>();

    public uint SignatureCounter { get; set; }

    public string CredType { get; set; } = "public-key";

    public DateTime RegDate { get; set; } = DateTime.UtcNow;

    [MaxLength(100)]
    public string DeviceFriendlyName { get; set; } = string.Empty;

    public virtual User User { get; set; } = null!;
}
