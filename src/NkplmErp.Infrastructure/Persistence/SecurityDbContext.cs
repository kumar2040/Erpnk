using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NkplmErp.Domain.Entities;

namespace NkplmErp.Infrastructure.Persistence;

public class SecurityDbContext : IdentityDbContext<User, Role, string>
{
    public SecurityDbContext(DbContextOptions<SecurityDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Security / Identity schema
        builder.HasDefaultSchema("identity");

        // Customizations for Identity tables if needed
        builder.Entity<User>(entity =>
        {
            entity.ToTable(name: "Users");
        });

        builder.Entity<Role>(entity =>
        {
            entity.ToTable(name: "Roles");
        });

        builder.Entity<AuditLog>(entity =>
        {
            entity.ToTable(name: "AuditLogs", schema: "identity");
            entity.HasKey(e => e.Id);
        });

        builder.Entity<Permission>(entity =>
        {
            entity.ToTable(name: "Permissions", schema: "identity");
            entity.HasKey(e => e.Id);
        });

        builder.Entity<RolePermission>(entity =>
        {
            entity.ToTable(name: "RolePermissions", schema: "identity");
            entity.HasKey(rp => new { rp.RoleId, rp.PermissionId });

            entity.HasOne(rp => rp.Role)
                .WithMany()
                .HasForeignKey(rp => rp.RoleId);

            entity.HasOne(rp => rp.Permission)
                .WithMany(p => p.RolePermissions)
                .HasForeignKey(rp => rp.PermissionId);
        });

        builder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable(name: "RefreshTokens", schema: "identity");
            entity.HasKey(e => e.Id);

            entity.HasOne(rt => rt.User)
                .WithMany()
                .HasForeignKey(rt => rt.UserId);
        });

        builder.Entity<BiometricCredential>(entity =>
        {
            entity.ToTable(name: "BiometricCredentials", schema: "identity");
            entity.HasKey(e => e.Id);

            entity.HasOne(bc => bc.User)
                .WithMany()
                .HasForeignKey(bc => bc.UserId);
        });
    }

    public DbSet<AuditLog> AuditLogs { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;
    public DbSet<BiometricCredential> BiometricCredentials { get; set; } = null!;
}
