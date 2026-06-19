using System.Text;
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
})
.AddEntityFrameworkStores<SecurityDbContext>()
.AddDefaultTokenProviders();

// 3. Authentication & JWT Configuration
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is missing");
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
            var cookieToken = context.Request.Cookies["X-Auth-Token"];
            if (!string.IsNullOrEmpty(cookieToken))
            {
                context.Token = cookieToken;
            }
            return Task.CompletedTask;
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
builder.Services.AddScoped<NkplmErp.Shared.Repositories.Interface.IDapperRepository, NkplmErp.Shared.Repositories.Implementation.DapperRepository>();
builder.Services.AddScoped<NkplmErp.API.Controllers.TaskManagement.Service.Interface.ITaskManagementService, NkplmErp.API.Controllers.TaskManagement.Service.Implementation.TaskManagementService>();

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
// app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("BlazorPolicy");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
