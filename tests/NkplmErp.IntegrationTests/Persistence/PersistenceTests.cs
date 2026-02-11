using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using NkplmErp.Application.Interfaces;
using NkplmErp.Domain.Entities;
using NkplmErp.Infrastructure.Persistence;
using NkplmErp.Infrastructure.Persistence.Interceptors;
using NkplmErp.IntegrationTests.Mocks;
using Xunit;

namespace NkplmErp.IntegrationTests.Persistence;

public class PersistenceTests : IDisposable
{
    private readonly ApplicationDbContext _context;
    private readonly TestCurrentUserService _currentUserService;
    private readonly SqliteConnection _connection;

    public PersistenceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        _currentUserService = new TestCurrentUserService();

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;

        var auditableInterceptor = new AuditableEntityInterceptor(_currentUserService);
        var softDeleteInterceptor = new SoftDeleteInterceptor();
        var auditLoggingInterceptor = new AuditLoggingInterceptor(_currentUserService);

        _context = new ApplicationDbContext(options, _currentUserService, auditableInterceptor, softDeleteInterceptor, auditLoggingInterceptor);
        _context.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _connection.Close();
        _context.Dispose();
    }

    [Fact]
    public async Task SaveChanges_ShouldPopulateAuditFields()
    {
        // Arrange
        var userId = "test-user";
        _currentUserService.UserId = userId;
        var product = new Product { Name = "Test Product", Price = 100 };

        // Act
        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        // Assert
        product.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        product.CreatedBy.Should().Be(userId);
    }

    [Fact]
    public async Task Remove_ShouldSoftDeleteEntity()
    {
        // Arrange
        var product = new Product { Name = "Delete Me", Price = 50 };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var productToDelete = await _context.Products.FirstAsync(p => p.Name == "Delete Me");
        _context.Products.Remove(productToDelete);
        await _context.SaveChangesAsync();

        // Assert
        _context.ChangeTracker.Clear();
        // Use IgnoreQueryFilters to see the soft-deleted row
        var deletedProduct = await _context.Products.IgnoreQueryFilters().FirstAsync(p => p.Name == "Delete Me");
        deletedProduct.IsDeleted.Should().BeTrue();

        // Verify it's hidden by default filter
        var visibleProducts = await _context.Products.ToListAsync();
        visibleProducts.Should().NotContain(p => p.Name == "Delete Me");
    }

    [Fact]
    public async Task Update_ShouldCreateDetailedAuditLog()
    {
        // Arrange
        var product = new Product { Name = "Original Name", Price = 10 };
        _context.Products.Add(product);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act
        var productToUpdate = await _context.Products.FirstAsync(p => p.Name == "Original Name");
        productToUpdate.Name = "Updated Name";
        await _context.SaveChangesAsync();

        // Assert
        var auditLog = await _context.AuditLogs
            .Where(x => x.TableName == "Products" && x.Type == "Modified")
            .OrderByDescending(x => x.DateTime)
            .FirstOrDefaultAsync();

        auditLog.Should().NotBeNull();
        auditLog!.OldValues.Should().Contain("Original Name");
        auditLog.NewValues.Should().Contain("Updated Name");
    }

    [Fact]
    public async Task GlobalQueryFilter_ShouldIsolateTenantData()
    {
        // Arrange
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Seed data using a separate context to avoid interceptor side effects if needed, 
        // but here we can just add them and Save.
        _currentUserService.TenantId = tenantA;
        _context.Products.Add(new Product { Name = "Product A", TenantId = tenantA });
        
        _context.ChangeTracker.Clear();
        _currentUserService.TenantId = tenantB;
        _context.Products.Add(new Product { Name = "Product B", TenantId = tenantB });
        
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        // Act & Assert for Tenant A
        _currentUserService.TenantId = tenantA;
        var productsA = await _context.Products.ToListAsync();
        productsA.Should().HaveCount(1);
        productsA.First().Name.Should().Be("Product A");

        // Act & Assert for Tenant B
        _context.ChangeTracker.Clear();
        _currentUserService.TenantId = tenantB;
        var productsB = await _context.Products.ToListAsync();
        productsB.Should().HaveCount(1);
        productsB.First().Name.Should().Be("Product B");
    }
}
