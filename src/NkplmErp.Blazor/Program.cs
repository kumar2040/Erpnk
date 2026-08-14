using Microsoft.AspNetCore.Components.Authorization;
using Blazored.LocalStorage;
using NkplmErp.Blazor.Data;
using NkplmErp.Blazor.Services.Auth;
using NkplmErp.Blazor.Services.RoleManagement;
using NkplmErp.Application.Interfaces;
using NkplmErp.Blazor.Services.BuyerOrderSummary;
using NkplmErp.Blazor.Services.Lookup;
using NkplmErp.Blazor.Services.Users;
using NkplmErp.Blazor.Services.ProductionPlanning;
using NkplmErp.Blazor.Services.KnitterManagement;
using NkplmErp.Blazor.Services.MachineManagement;
using NkplmErp.Blazor.Services.Bom;
using NkplmErp.Blazor.Services.PoTask;
using NkplmErp.Blazor.Services.TaskManagement.Manager.Interface;
using NkplmErp.Blazor.Services.TaskManagement.Manager.Implementation;
using NkplmErp.Blazor.Services.Yarn_Orders.Manager.Interface;
using NkplmErp.Blazor.Services.Yarn_Orders.Manager.Implementation;
using NkplmErp.Blazor.Services.Task_Gate;
using NkplmErp.Blazor.Services.Task_Gate.Manager.Interface;
using NkplmErp.Blazor.Services.Task_Gate.Manager.Implementation;
using NkplmErp.Blazor.Services.Toast;
using NkplmErp.Blazor.Shared.Http;
using NkplmErp.Blazor.Services.Dropdown.Manager.Interface;
using NkplmErp.Blazor.Services.Dropdown.Manager.Implementation;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddHttpClient(); // Fixed: Added naked HttpClient for pages that inject it directly

// Authentication & LocalStorage
builder.Services.AddHttpContextAccessor();
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorization();
builder.Services.AddScoped<TokenProvider>();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddScoped<ToastService>();
builder.Services.AddScoped<TaskBoardNotifier>();

// App-wide loading veil, driven by _loading.Show()/Hide() from any page. Scoped, so each
// user circuit gets its own: a Singleton would show one user's spinner to everyone.
builder.Services.AddScoped<NkplmErp.Blazor.Services.Loading.LoadingService>();

// Login task gate state: scoped so it lives per circuit, shared by the gate
// modal and the header badge.
builder.Services.AddScoped<TaskGateState>();

Console.WriteLine("===[ NkplmErp Blazor APP STARTING ]===");

// HttpClient configuration with proper handler lifecycle
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5270/";

builder.Services.AddTransient<AuthenticationDelegatingHandler>();

builder.Services.AddHttpClient<IAuthService, AuthService>(client => 
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<IBuyerOrderSummaryService, BuyerOrderSummaryService>(client => 
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();


builder.Services.AddHttpClient<ILookupClient, LookupClient>(client => 
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

builder.Services.AddHttpClient<UsersApiClient>(client => 
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

builder.Services.AddHttpClient<IProductionPlanningService, ProductionPlanningService>(client => 
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

// Zero Trust Role Management
builder.Services.AddHttpClient<RoleManagementApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

// Knitter Management
builder.Services.AddHttpClient<KnitterManagementApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

// Machine Management
builder.Services.AddHttpClient<MachineManagementApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

// Bill of Materials (yarn requirement)
builder.Services.AddHttpClient<BomApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

// Task Management board
builder.Services.AddHttpClient<ITaskManagementManager, TaskManagementManager>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

// Named client behind the shared HttpServices wrapper. New managers take
// IHttpServices instead of their own HttpClient.
builder.Services.AddHttpClient("ApiGateway", client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

// SCOPED, never singleton — one instance per Blazor circuit, so a bearer token
// can never leak from one signed-in user to another.
builder.Services.AddScoped<IHttpServices, HttpServices>();

// Yarn order timeline (departure / arrival)
builder.Services.AddScoped<IYarnOrderManager, YarnOrderManager>();
builder.Services.AddScoped<IDropdownManager, DropdownManager>();

// Login task gate (blocking one-task-at-a-time popup)
builder.Services.AddScoped<ITaskGateManager, TaskGateManager>();

// PO lifecycle task board (new /tasks page)
builder.Services.AddHttpClient<PoTaskApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

// PermissionService: scoped so it lives per circuit (Blazor Server)
// Loads on login, cleared on logout
builder.Services.AddScoped<PermissionService>();

Console.WriteLine("DEBUG: Program.cs - Typed HttpClient registrations complete.");


var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

// Only allow internal, non-auth paths as a post-login redirect target (prevents open-redirect / loops).
static bool IsSafeReturnUrl(string? url)
{
    if (string.IsNullOrEmpty(url)) return false;
    if (!url.StartsWith("/")) return false;                 // must be a relative app path
    if (url.StartsWith("//") || url.StartsWith("/\\")) return false; // protocol-relative
    if (url.StartsWith("/login", StringComparison.OrdinalIgnoreCase)) return false;
    if (url.StartsWith("/auth", StringComparison.OrdinalIgnoreCase)) return false;
    return true;
}

// Local Auth Bridge Endpoint
var isDevelopment = app.Environment.IsDevelopment();
app.MapPost("/auth/set-token", async (HttpContext context) =>
{
    // Read the posted form ASYNCHRONOUSLY. The sync context.Request.Form accessor can
    // throw BadHttpRequestException ("Unexpected end of request content") when the
    // hidden-form POST body arrives truncated (a duplicate/cancelled submit, or an
    // HTTPS redirect dropping the body). Read async and fail gracefully back to login.
    IFormCollection form;
    try
    {
        form = await context.Request.ReadFormAsync();
    }
    catch (Exception)
    {
        return Results.Redirect("/login");
    }

    var token = form["token"].ToString();
    if (!string.IsNullOrEmpty(token))
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = !isDevelopment, // HTTPS-only outside local dev
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddHours(8), // Extended: covers a full working day idle
            Path = "/"
        };
        context.Response.Cookies.Append("X-Auth-Token", token, cookieOptions);
    }

    // After login, return the user to where they were (session-expiry deep link), else dashboard.
    var returnUrl = form["returnUrl"].ToString();
    return Results.Redirect(IsSafeReturnUrl(returnUrl) ? returnUrl : "/main-dashboard");
});

app.MapGet("/auth/logout", (HttpContext context) =>
{
    context.Response.Cookies.Delete("X-Auth-Token");

    // Carry the page the user was on through to the login screen.
    var returnUrl = context.Request.Query["returnUrl"].ToString();
    return Results.Redirect(IsSafeReturnUrl(returnUrl)
        ? "/login?returnUrl=" + Uri.EscapeDataString(returnUrl)
        : "/login");
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.Run();
