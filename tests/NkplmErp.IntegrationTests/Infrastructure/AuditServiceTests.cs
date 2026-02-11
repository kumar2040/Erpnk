using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using NkplmErp.Infrastructure.Logging;
using NkplmErp.Infrastructure.Persistence;
using Xunit;

using Microsoft.Data.Sqlite;

namespace NkplmErp.IntegrationTests.Infrastructure;

public class AuditServiceTests : IDisposable
{
    private readonly SecurityDbContext _context;
    private readonly AuditService _sut;
    private readonly SqliteConnection _connection;

    public AuditServiceTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SecurityDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new SecurityDbContext(options);
        _context.Database.EnsureCreated();
        _sut = new AuditService(_context);
    }

    public void Dispose()
    {
        _connection.Close();
        _context.Dispose();
    }

    [Fact]
    public async Task LogAsync_ShouldCreateAuditLog_InDatabase()
    {
        // Arrange
        var userId = "user-123";
        var action = "Update";
        var entityName = "Invoice";
        var entityId = "inv-001";
        var oldValues = "{\"Amount\": 100}";
        var newValues = "{\"Amount\": 120}";

        // Act
        await _sut.LogAsync(userId, action, entityName, entityId, oldValues, newValues);

        // Assert
        var logs = await _context.AuditLogs.ToListAsync();
        logs.Should().HaveCount(1);

        var log = logs.First();
        log.UserId.Should().Be(userId);
        log.Type.Should().Be(action);
        log.TableName.Should().Be(entityName);
        log.PrimaryKey.Should().Be(entityId);
        log.OldValues.Should().Be(oldValues);
        log.NewValues.Should().Be(newValues);
        log.DateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
