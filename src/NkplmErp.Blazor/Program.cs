using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Blazored.LocalStorage;
using NkplmErp.Blazor.Data;
using NkplmErp.Blazor.Services.Auth;
using NkplmErp.Blazor.Services.Lookup;
using NkplmErp.Blazor.Services.RoleManagement;

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
builder.Services.AddScoped<NkplmErp.Blazor.Services.Toast.ToastService>();

Console.WriteLine("===[ NkplmErp Blazor APP STARTING ]===");

// HttpClient configuration with proper handler lifecycle
var apiBaseUrl = builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5270/";

builder.Services.AddTransient<AuthenticationDelegatingHandler>();

builder.Services.AddHttpClient<IAuthService, AuthService>(client => 
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddHttpClient<NkplmErp.Application.Interfaces.IBuyerOrderSummaryService, NkplmErp.Blazor.Services.BuyerOrderSummary.BuyerOrderSummaryService>(client => 
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();


builder.Services.AddHttpClient<NkplmErp.Blazor.Services.Lookup.ILookupClient, NkplmErp.Blazor.Services.Lookup.LookupClient>(client => 
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

builder.Services.AddHttpClient<NkplmErp.Blazor.Services.Users.UsersApiClient>(client => 
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

builder.Services.AddHttpClient<NkplmErp.Application.Interfaces.IProductionPlanningService, NkplmErp.Blazor.Services.ProductionPlanning.ProductionPlanningService>(client => 
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
builder.Services.AddHttpClient<NkplmErp.Blazor.Services.KnitterManagement.KnitterManagementApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

// Machine Management
builder.Services.AddHttpClient<NkplmErp.Blazor.Services.MachineManagement.MachineManagementApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
})
.AddHttpMessageHandler<AuthenticationDelegatingHandler>();

// Task Management board
builder.Services.AddHttpClient<NkplmErp.Blazor.Services.TaskManagement.Manager.Interface.ITaskManagementManager, NkplmErp.Blazor.Services.TaskManagement.Manager.Implementation.TaskManagementManager>(client =>
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
