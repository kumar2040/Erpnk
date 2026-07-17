using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;
using NkplmErp.Application.Interfaces;
using NkplmErp.Domain.Common;
using NkplmErp.Domain.Entities;
using NkplmErp.Infrastructure.Logging;
using NkplmErp.Infrastructure.Persistence;
using NkplmErp.Infrastructure.Services;
using NkplmErp.Security.Authentication;
using NkplmErp.Security.Authorization;
using NkplmErp.Security.DeviceFingerprint;
using NkplmErp.Infrastructure.Persistence.Interceptors;
using NkplmErp.API.Middleware;
using Asp.Versioning;
using Fido2NetLib;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.Swagger;
using Swashbuckle.AspNetCore.SwaggerUI;
using Hangfire;
using Hangfire.SqlServer;

var builder = WebApplication.CreateBuilder(args);

// 1. Database Configuration
builder.Services.AddDbContext<SecurityDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// ... (Identity remains the same)

// 2. Identity Configuration
builder.Services.AddIdentity<NkplmErp.Domain.Entities.User, Role>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;

    // Brute-force protection: lock the account after repeated failures.
    options.Lockout.AllowedForNewUsers = true;
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
})
.AddEntityFrameworkStores<SecurityDbContext>()
.AddDefaultTokenProviders();

// 3. Authentication & JWT Configuration
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is missing");

// Refuse to start in non-development with a missing/weak/known-leaked signing key.
const string LeakedDefaultJwtKey = "nkplm_erp_super_secret_key_fixed_length_32_chars";
if (!builder.Environment.IsDevelopment() &&
    (jwtKey.Length < 32 || jwtKey == LeakedDefaultJwtKey))
{
    throw new InvalidOperationException(
        "Insecure Jwt:Key. Configure a strong, secret signing key (>= 32 chars) via " +
        "environment variable or user-secrets — the default/committed key must not be used in production.");
}
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };

    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            // SignalR (WebSocket) sends the JWT as the access_token query param on hub
            // requests — pick it up so [Authorize] hubs authenticate.
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
            {
                context.Token = accessToken;
                return Task.CompletedTask;
            }

            var cookieToken = context.Request.Cookies["X-Auth-Token"];
            if (!string.IsNullOrEmpty(cookieToken))
            {
                context.Token = cookieToken;
            }
            return Task.CompletedTask;
        },

        // Zero Trust live check: reject the token if the user is deactivated or has
        // been force-logged-out (security stamp rotated). Runs on every request, so
        // an admin "Force logout" / "Deactivate" takes effect immediately.
        OnTokenValidated = async context =>
        {
            var principal = context.Principal;
            var userId = principal?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
                         ?? principal?.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                context.Fail("No subject in token.");
                return;
            }

            var userManager = context.HttpContext.RequestServices.GetRequiredService<UserManager<User>>();
            var user = await userManager.FindByIdAsync(userId);
            if (user is null || !user.IsActive)
            {
                context.Fail("User is inactive or no longer exists.");
                return;
            }

            var tokenStamp = principal?.FindFirst("sstamp")?.Value ?? string.Empty;
            var currentStamp = user.SecurityStamp ?? string.Empty;
            if (!string.Equals(tokenStamp, currentStamp, StringComparison.Ordinal))
            {
                context.Fail("Session has been ended.");
            }
        }
    };
});

// 4. Custom Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUserService, NkplmErp.API.Services.CurrentUserService>();
builder.Services.AddScoped<AuditableEntityInterceptor>();
builder.Services.AddScoped<SoftDeleteInterceptor>();
builder.Services.AddScoped<AuditLoggingInterceptor>();
builder.Services.AddScoped<IUnitOfWork, NkplmErp.Infrastructure.Persistence.Repositories.UnitOfWork>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IIdentityService, IdentityService>();
builder.Services.AddScoped<IAuditService, AuditService>();
builder.Services.AddScoped<IMfaService, MfaService>();
builder.Services.AddScoped<IDeviceService, DeviceService>();
builder.Services.AddScoped<IWebAuthnService, WebAuthnService>();
builder.Services.AddScoped<NkplmErp.Application.Interfaces.IUserService, NkplmErp.Application.Services.UserService>();
builder.Services.AddScoped<IBuyerOrderSummaryService, NkplmErp.Infrastructure.Services.BuyerOrderSummaryService>();
builder.Services.AddScoped<ILookupService, NkplmErp.Infrastructure.Services.LookupService>();
builder.Services.AddScoped<IProductionPlanningService, NkplmErp.Infrastructure.Services.ProductionPlanningService>();
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();
builder.Services.AddScoped<IKnitterManagementService, NkplmErp.Infrastructure.Services.KnitterManagementService>();
builder.Services.AddScoped<IMachineManagementService, NkplmErp.Infrastructure.Services.MachineManagementService>();
builder.Services.AddScoped<IBomService, NkplmErp.Infrastructure.Services.BomService>();
builder.Services.AddScoped<NkplmErp.Shared.Repositories.Interface.IDapperRepository, NkplmErp.Shared.Repositories.Implementation.DapperRepository>();
builder.Services.AddScoped<NkplmErp.API.Controllers.TaskManagement.Service.Interface.ITaskManagementService, NkplmErp.API.Controllers.TaskManagement.Service.Implementation.TaskManagementService>();
builder.Services.AddScoped<IPoTaskService, NkplmErp.Infrastructure.Services.PoTaskService>();
builder.Services.AddScoped<IEmailService, NkplmErp.Infrastructure.Services.EmailService>();
builder.Services.AddSingleton<INotificationPublisher, NkplmErp.API.Hubs.SignalRNotificationPublisher>();
builder.Services.AddSignalR();
builder.Services.AddHostedService<NkplmErp.API.Services.PoTaskReminderService>();

// Background job processing (Hangfire). Storage lives in the same NatureKnit DB;
// Hangfire creates and owns its [HangFire] schema tables on first start.
builder.Services.AddHangfire(config => config
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(builder.Configuration.GetConnectionString("DefaultConnection"), new SqlServerStorageOptions
    {
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.FromSeconds(15),
        JobExpirationCheckInterval = TimeSpan.FromHours(1)
    }));

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Environment.ProcessorCount * 2;
    options.SchedulePollingInterval = TimeSpan.FromSeconds(15);
});

builder.Services.AddMemoryCache();
builder.Services.AddFido2(options =>
{
    options.ServerDomain = builder.Configuration["fido2:serverDomain"];
    options.ServerName = builder.Configuration["fido2:serverName"];
    options.Origins = new HashSet<string> { builder.Configuration["fido2:origin"] ?? string.Empty };
});

// Authorization logic
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("CanViewUsers", policy => policy.Requirements.Add(new PermissionRequirement("Permissions.Users.View")))
    .AddPolicy("CanCreateUsers", policy => policy.Requirements.Add(new PermissionRequirement("Permissions.Users.Create")));

// 5. API Core
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorPolicy", builder =>
    {
        builder.WithOrigins("http://localhost:5076", "http://127.0.0.1:5076")
               .AllowAnyMethod()
               .AllowAnyHeader()
               .AllowCredentials();
    });
});
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

// Rate limiting: throttle the auth endpoints to blunt brute-force / credential stuffing.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.Window = TimeSpan.FromMinutes(1);
        o.PermitLimit = 10;          // 10 auth requests / minute / instance
        o.QueueLimit = 0;
    });
});

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
    options.ApiVersionReader = new UrlSegmentApiVersionReader();
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\""
    });
    options.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {
        {
            new Microsoft.OpenApi.Models.OpenApiSecurityScheme
            {
                Reference = new Microsoft.OpenApi.Models.OpenApiReference
                {
                    Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Support for multiple versions
    // This part is usually handled by a separate class, but we can do a simple version for now
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "NkplmErp API v1", Version = "v1" });
});




// Register DataSeeder
builder.Services.AddScoped<NkplmErp.Infrastructure.Persistence.Seeders.DataSeeder>();

var app = builder.Build();

// Run Migrations and Seeder
if (app.Environment.IsDevelopment())
{
    using (var scope = app.Services.CreateScope())
    {
        try
        {
            var securityContext = scope.ServiceProvider.GetRequiredService<SecurityDbContext>();
            var appContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            
            Console.WriteLine("Applying Migrations...");
            await securityContext.Database.MigrateAsync();
            await appContext.Database.MigrateAsync();
            
            Console.WriteLine("Seeding Data...");
            var seeder = scope.ServiceProvider.GetRequiredService<NkplmErp.Infrastructure.Persistence.Seeders.DataSeeder>();
            await seeder.SeedAsync();
        }
        catch (Exception ex)
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            logger.LogError(ex, "An error occurred while seeding the database.");
        }
    }
}

// 6. Middleware Pipeline
app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "V1");
    options.RoutePrefix = "swagger";
});

app.UseExceptionHandler();

// Transport security: enforce HTTPS + HSTS outside local development.
if (!app.Environment.IsDevelopment())
{
    // Behind a TLS-terminating proxy / IIS, trust X-Forwarded-Proto so the app
    // sees the original HTTPS scheme (prevents HTTPS-redirect loops, fixes Secure cookies).
    var fwd = new Microsoft.AspNetCore.Builder.ForwardedHeadersOptions
    {
        ForwardedHeaders = Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto
                         | Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor
    };
    fwd.KnownNetworks.Clear();
    fwd.KnownProxies.Clear(); // accept forwarded headers from the front-end proxy
    app.UseForwardedHeaders(fwd);

    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStaticFiles();

app.UseCors("BlazorPolicy");

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

// Hangfire dashboard — development only. Hangfire's default authorization
// filter additionally restricts the dashboard to local requests.
if (app.Environment.IsDevelopment())
{
    app.UseHangfireDashboard("/hangfire");
}

app.MapControllers();
app.MapHub<NkplmErp.API.Hubs.NotificationHub>("/hubs/notifications");

app.Run();
