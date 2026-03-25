using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using NkplmErp.Application.Interfaces;
using NkplmErp.Domain.Entities;
using NkplmErp.Infrastructure.Logging;
using NkplmErp.Infrastructure.Persistence;
using NkplmErp.Security.Authentication;
using NkplmErp.Security.DeviceFingerprint;
using NkplmErp.Shared.DTOs;
using Xunit;

namespace NkplmErp.IntegrationTests.Security;

public class IdentityServiceIntegrationTests : IDisposable
{
    private readonly SecurityDbContext _context;
    private readonly UserManager<User> _userManager;
    private readonly IdentityService _sut;
    private readonly SqliteConnection _connection;

    public IdentityServiceIntegrationTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<SecurityDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new SecurityDbContext(options);
        _context.Database.EnsureCreated();

        // Setup UserManager
        var userStore = new UserStore<User, Role, SecurityDbContext, string>(_context);
        var optionsAccessor = new Mock<IOptions<IdentityOptions>>();
        optionsAccessor.Setup(p => p.Value).Returns(new IdentityOptions());
        var passwordHasher = new PasswordHasher<User>();
        var userValidators = new List<IUserValidator<User>> { new UserValidator<User>() };
        var passwordValidators = new List<IPasswordValidator<User>> { new PasswordValidator<User>() };
        var keyNormalizer = new UpperInvariantLookupNormalizer();
        var errors = new IdentityErrorDescriber();
        var logger = new Mock<ILogger<UserManager<User>>>();

        _userManager = new UserManager<User>(
            userStore,
            optionsAccessor.Object,
            passwordHasher,
            userValidators,
            passwordValidators,
            keyNormalizer,
            errors,
            null!, // ServiceProvider
            logger.Object);

        // Setup JwtTokenService
        var configMock = new Mock<IConfiguration>();
        configMock.Setup(x => x["Jwt:Key"]).Returns("very_secret_key_that_is_long_enough_for_sha256");
        configMock.Setup(x => x["Jwt:Issuer"]).Returns("nkplm.erp");
        configMock.Setup(x => x["Jwt:Audience"]).Returns("nkplm.erp.users");
        var jwtTokenService = new JwtTokenService(configMock.Object, _context);

        // Setup AuditService
        var auditService = new AuditService(_context);

        // Setup MFA, Device and CurrentUser Services
        var mfaService = new MfaService();
        var httpContextAccessorMock = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        var deviceService = new DeviceService(httpContextAccessorMock.Object);
        var currentUserServiceMock = new Mock<ICurrentUserService>();
        var webAuthnServiceMock = new Mock<IWebAuthnService>();

        _sut = new IdentityService(_userManager, jwtTokenService, auditService, mfaService, deviceService, currentUserServiceMock.Object, webAuthnServiceMock.Object, _context);
    }

    public void Dispose()
    {
        _connection.Close();
        _context.Dispose();
        _userManager.Dispose();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnSuccess_WhenCredentialsAreCorrect()
    {
        // Arrange
        var email = "user@nkplm.erp";
        var password = "Password123!";
        await _sut.RegisterAsync(email, password, "Test", "User");

        var loginRequest = new LoginRequest { Email = email, Password = password };

        // Act
        var result = await _sut.LoginAsync(loginRequest);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Token.Should().NotBeNullOrEmpty();

        // Verify Audit Log
        var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(x => x.Type == "LoginSuccess");
        auditLog.Should().NotBeNull();
        auditLog!.UserId.Should().NotBe("system");
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnFailure_WhenPasswordIsIncorrect()
    {
        // Arrange
        var email = "user2@nkplm.erp";
        var password = "Password123!";
        await _sut.RegisterAsync(email, password, "Test", "User");

        var loginRequest = new LoginRequest { Email = email, Password = "WrongPassword" };

        // Act
        var result = await _sut.LoginAsync(loginRequest);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Token.Should().BeNullOrEmpty();

        // Verify Audit Log
        var auditLog = await _context.AuditLogs.FirstOrDefaultAsync(x => x.Type == "LoginFailed");
        auditLog.Should().NotBeNull();
    }
}
