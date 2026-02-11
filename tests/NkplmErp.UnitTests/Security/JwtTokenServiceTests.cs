using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using NkplmErp.Domain.Entities;
using NkplmErp.Infrastructure.Persistence;
using NkplmErp.Security.Authentication;
using Xunit;

namespace NkplmErp.UnitTests.Security;

public class JwtTokenServiceTests
{
    private readonly Mock<IConfiguration> _configMock;
    private readonly SecurityDbContext _context;
    private readonly JwtTokenService _sut;

    public JwtTokenServiceTests()
    {
        _configMock = new Mock<IConfiguration>();

        var options = new DbContextOptionsBuilder<SecurityDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _context = new SecurityDbContext(options);

        // Setup configuration
        _configMock.Setup(x => x["Jwt:Key"]).Returns("very_secret_key_that_is_long_enough_for_sha256");
        _configMock.Setup(x => x["Jwt:Issuer"]).Returns("nkplm.erp");
        _configMock.Setup(x => x["Jwt:Audience"]).Returns("nkplm.erp.users");
        _configMock.Setup(x => x["Jwt:ExpireMinutes"]).Returns("60");

        _sut = new JwtTokenService(_configMock.Object, _context);
    }

    [Fact]
    public async Task GenerateTokenAsync_ShouldReturnValidToken_WhenCredentialsAreCorrect()
    {
        // Arrange
        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = "test@example.com",
            FirstName = "John",
            LastName = "Doe"
        };
        var roles = new List<string> { "Admin", "User" };

        // Act
        var token = await _sut.GenerateTokenAsync(user, roles);

        // Assert
        token.Should().NotBeNullOrEmpty();

        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);

        jwtToken.Issuer.Should().Be("nkplm.erp");
        jwtToken.Audiences.Should().Contain("nkplm.erp.users");
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == user.Id);
        jwtToken.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email && c.Value == user.Email);
        jwtToken.Claims.Should().Contain(c => c.Type == "firstName" && c.Value == "John");
        jwtToken.Claims.Should().Contain(c => c.Type == "lastName" && c.Value == "Doe");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "Admin");
        jwtToken.Claims.Should().Contain(c => c.Type == ClaimTypes.Role && c.Value == "User");
    }

    [Fact]
    public async Task GenerateTokenAsync_ShouldIncludePermissions_WhenRolesHavePermissionsInDb()
    {
        // Arrange
        var user = new User { Id = "1", Email = "a@a.com", FirstName = "A", LastName = "B" };
        var roles = new List<string> { "Manager" };

        var role = new Role { Name = "Manager" };
        var permission = new Permission { Name = "Invoice.View" };
        _context.Roles.Add(role);
        _context.Permissions.Add(permission);
        _context.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
        await _context.SaveChangesAsync();

        // Act
        var token = await _sut.GenerateTokenAsync(user, roles);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwtToken = handler.ReadJwtToken(token);
        jwtToken.Claims.Should().Contain(c => c.Type == "Permission" && c.Value == "Invoice.View");
    }

    [Fact]
    public async Task GenerateTokenAsync_ShouldThrowException_WhenKeyIsMissing()
    {
        // Arrange
        _configMock.Setup(x => x["Jwt:Key"]).Returns((string?)null);
        var user = new User { Id = "1", FirstName = "A", LastName = "B" };

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.GenerateTokenAsync(user, new List<string>()));
    }
}
