using Bogus;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using NkplmErp.Domain.Entities;
using System.Security.Claims;

namespace NkplmErp.Infrastructure.Persistence.Seeders;

public class DataSeeder
{
    private readonly SecurityDbContext _securityContext;
    private readonly ApplicationDbContext _appContext;
    private readonly UserManager<User> _userManager;
    private readonly RoleManager<Role> _roleManager;

    public DataSeeder(
        SecurityDbContext securityContext,
        ApplicationDbContext appContext,
        UserManager<User> userManager,
        RoleManager<Role> roleManager)
    {
        _securityContext = securityContext;
        _appContext = appContext;
        _userManager = userManager;
        _roleManager = roleManager;
    }

    public async Task SeedAsync()
    {
        await SeedRolesAndPermissionsAsync();
        var defaultTenantId = await SeedTenantsAsync();
        await SeedUsersAsync(defaultTenantId);
        await SeedProductsAsync(defaultTenantId);
        await SeedOrdersAsync(defaultTenantId);
        await SeedLegacyTablesAsync();
    }

    private async Task SeedLegacyTablesAsync()
    {
        var sqlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "populate_tables.sql");
        // Fallback for different build environments
        if (!File.Exists(sqlPath))
        {
            sqlPath = Path.Combine(Directory.GetCurrentDirectory(), "scripts", "populate_tables.sql");
        }

        if (File.Exists(sqlPath))
        {
            var sql = await File.ReadAllTextAsync(sqlPath);
            await _appContext.Database.ExecuteSqlRawAsync(sql);
        }
        else
        {
            // If file not found in build path, we can still use the raw SQL here if needed, 
            // but for now we rely on the script file being present.
        }
    }

    private async Task SeedRolesAndPermissionsAsync()
    {
        // Define Roles and their Permissions
        var roles = new Dictionary<string, List<string>>
        {
            { "Admin", new List<string> { "ManageUsers", "ManageRoles", "ViewDashboard", "ManageProducts", "ViewProducts", "ManageTenants" } },
            { "Manager", new List<string> { "ViewDashboard", "ManageProducts", "ViewProducts", "ViewUsers" } },
            { "Employee", new List<string> { "ViewDashboard", "ViewProducts" } }
        };

        foreach (var roleName in roles.Keys)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                await _roleManager.CreateAsync(new Role { Name = roleName, Description = $"Default {roleName} Role" });
            }

            var role = await _roleManager.FindByNameAsync(roleName);
            if (role == null) continue;

            // Seed Permissions
            foreach (var permissionName in roles[roleName])
            {
                var permission = await _securityContext.Permissions.FirstOrDefaultAsync(p => p.Name == permissionName);
                if (permission == null)
                {
                    permission = new Permission { Name = permissionName, Description = $"Permission to {permissionName}" };
                    _securityContext.Permissions.Add(permission);
                }
            }
            await _securityContext.SaveChangesAsync();

            // Assign Permissions to Role
            foreach (var permissionName in roles[roleName])
            {
                var permission = await _securityContext.Permissions.FirstAsync(p => p.Name == permissionName);
                if (!await _securityContext.RolePermissions.AnyAsync(rp => rp.RoleId == role.Id && rp.PermissionId == permission.Id))
                {
                    _securityContext.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id });
                }
            }
            await _securityContext.SaveChangesAsync();
        }
    }

    private async Task<Guid> SeedTenantsAsync()
    {
        var defaultTenant = await _appContext.Tenants.FirstOrDefaultAsync(t => t.Code == "DEFAULT");
        if (defaultTenant == null)
        {
            defaultTenant = new Tenant
            {
                Name = "Default Tenant",
                Code = "DEFAULT",
                IsActive = true
            };
            _appContext.Tenants.Add(defaultTenant);
            await _appContext.SaveChangesAsync();
        }
        return defaultTenant.Id;
    }

    private async Task SeedUsersAsync(Guid tenantId)
    {
        var users = new List<(string Email, string Role, string FirstName, string LastName)>
        {
            ("admin@nkplm.erp", "Admin", "System", "Administrator"),
            ("manager@nkplm.erp", "Manager", "System", "Manager"),
            ("employee@nkplm.erp", "Employee", "System", "Employee")
        };

        foreach (var (email, role, firstName, lastName) in users)
        {
            var user = await _userManager.FindByEmailAsync(email);
            if (user == null)
            {
                user = new User
                {
                    UserName = email,
                    Email = email,
                    FirstName = firstName,
                    LastName = lastName,
                    EmailConfirmed = true,
                    BranchId = tenantId,
                    IsActive = true
                };
                
                var result = await _userManager.CreateAsync(user, "Password123!");
                if (result.Succeeded)
                {
                    await _userManager.AddToRoleAsync(user, role);
                }
            }
            // NOTE: existing users are left untouched. Do NOT reset passwords on
            // every boot - that would clobber any rotated/admin-set credentials.
        }

        // Random Users
        if (await _userManager.Users.CountAsync() < 25)
        {
            var faker = new Faker<User>()
                .CustomInstantiator(f => new User
                {
                    UserName = f.Internet.Email(),
                    Email = f.Internet.Email(), // Will be overwritten by UserName sync usually, but setting both
                    FirstName = f.Name.FirstName(),
                    LastName = f.Name.LastName(),
                    EmailConfirmed = true,
                    BranchId = tenantId,
                    IsActive = true
                });

            var randomUsers = faker.Generate(20);
            foreach (var user in randomUsers)
            {
                user.UserName = user.Email; // Sync
                if (await _userManager.FindByEmailAsync(user.Email!) == null)
                {
                    var result = await _userManager.CreateAsync(user, "Password123!");
                    if (result.Succeeded)
                    {
                        await _userManager.AddToRoleAsync(user, "Employee");
                    }
                }
            }
        }

        // Ensure original admin has Admin role
        var adminUser = await _userManager.FindByEmailAsync("admin@nkplm.erp");
        if (adminUser != null && !await _userManager.IsInRoleAsync(adminUser, "Admin"))
        {
            await _userManager.AddToRoleAsync(adminUser, "Admin");
        }
    }

    private async Task SeedProductsAsync(Guid tenantId)
    {
        if (await _appContext.Products.IgnoreQueryFilters().AnyAsync(p => p.TenantId == tenantId)) return;

        var productFaker = new Faker<Product>()
            .RuleFor(p => p.Name, f => f.Commerce.ProductName())
            .RuleFor(p => p.Price, f => decimal.Parse(f.Commerce.Price(100, 5000)))
            .RuleFor(p => p.TenantId, f => tenantId);

        var products = productFaker.Generate(50);
        await _appContext.Products.AddRangeAsync(products);
        await _appContext.SaveChangesAsync();
    }


    private async Task SeedOrdersAsync(Guid tenantId)
    {
        if (await _appContext.Orders.AnyAsync()) return;

        var users = await _userManager.Users.ToListAsync();
        if (!users.Any()) return;

        var orderFaker = new Faker<Order>()
            .RuleFor(o => o.OrderDate, f => f.Date.Between(new DateTime(2026, 1, 1), DateTime.UtcNow.AddDays(30))) 
            .RuleFor(o => o.Status, f => f.PickRandom("NotStarted", "Running", "Completed"))
            .RuleFor(o => o.TotalAmount, f => decimal.Parse(f.Commerce.Price(50, 1000)))
            .RuleFor(o => o.TenantId, f => tenantId);

        var orders = new List<Order>();
        foreach (var user in users)
        {
            // Create 1-5 orders for each user
            var userOrders = orderFaker.Generate(new Random().Next(1, 6));
            foreach(var order in userOrders) 
            {
                order.CustomerId = user.Id;
            }
            orders.AddRange(userOrders);
        }

        await _appContext.Orders.AddRangeAsync(orders);
        await _appContext.SaveChangesAsync();
    }
}
