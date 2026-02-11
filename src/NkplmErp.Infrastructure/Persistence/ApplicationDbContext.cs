using Microsoft.EntityFrameworkCore;
using NkplmErp.Application.Interfaces;
using NkplmErp.Domain.Common;
using NkplmErp.Domain.Entities;
using NkplmErp.Infrastructure.Persistence.Interceptors;

using System.Reflection;

namespace NkplmErp.Infrastructure.Persistence;

public class ApplicationDbContext : DbContext
{
    private readonly ICurrentUserService _currentUserService;
    public ICurrentUserService CurrentUserService => _currentUserService;
    private readonly AuditableEntityInterceptor _auditableEntityInterceptor;
    private readonly SoftDeleteInterceptor _softDeleteInterceptor;
    private readonly AuditLoggingInterceptor _auditLoggingInterceptor;

    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options,
        ICurrentUserService currentUserService,
        AuditableEntityInterceptor auditableEntityInterceptor,
        SoftDeleteInterceptor softDeleteInterceptor,
        AuditLoggingInterceptor auditLoggingInterceptor) : base(options)
    {
        _currentUserService = currentUserService;
        _auditableEntityInterceptor = auditableEntityInterceptor;
        _softDeleteInterceptor = softDeleteInterceptor;
        _auditLoggingInterceptor = auditLoggingInterceptor;
    }

    public DbSet<Product> Products { get; set; } = null!;
    public DbSet<Tenant> Tenants { get; set; } = null!;
    public DbSet<Order> Orders { get; set; } = null!;
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        base.OnModelCreating(modelBuilder);

        // Global Query Filters (Multi-Tenancy & Soft-Delete)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var filters = new List<System.Linq.Expressions.Expression>();
            var parameter = System.Linq.Expressions.Expression.Parameter(entityType.ClrType, "x");

            // Multi-Tenancy Filter
            if (typeof(ITenant).IsAssignableFrom(entityType.ClrType))
            {
                var property = System.Linq.Expressions.Expression.Property(parameter, "TenantId");
                var propertyAsNullable = System.Linq.Expressions.Expression.Convert(property, typeof(Guid?));

                var serviceProperty = System.Linq.Expressions.Expression.Property(
                    System.Linq.Expressions.Expression.Constant(this),
                    nameof(CurrentUserService));

                var tenantIdProperty = System.Linq.Expressions.Expression.Property(
                    serviceProperty,
                    nameof(ICurrentUserService.TenantId));

                filters.Add(System.Linq.Expressions.Expression.Equal(propertyAsNullable, tenantIdProperty));
            }

            // Soft-Delete Filter
            if (typeof(BaseAuditableEntity).IsAssignableFrom(entityType.ClrType))
            {
                var isDeletedProperty = System.Linq.Expressions.Expression.Property(parameter, "IsDeleted");
                filters.Add(System.Linq.Expressions.Expression.Equal(isDeletedProperty, System.Linq.Expressions.Expression.Constant(false)));
            }

            if (filters.Any())
            {
                var body = filters.Aggregate(System.Linq.Expressions.Expression.AndAlso);
                var lambda = System.Linq.Expressions.Expression.Lambda(body, parameter);
                modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
            }
        }
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.AddInterceptors(_auditableEntityInterceptor, _softDeleteInterceptor, _auditLoggingInterceptor);
    }
}
