using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Blazored.LocalStorage;
using NkplmErp.Blazor.Data;
using NkplmErp.Blazor.Services.Auth;
using NkplmErp.Blazor.Services.Lookup;

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

// Local Auth Bridge Endpoint
app.MapPost("/auth/set-token", async (HttpContext context) => 
{
    var token = context.Request.Form["token"].ToString();
    if (!string.IsNullOrEmpty(token))
    {
        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure = false, // Set to false for local dev
            SameSite = SameSiteMode.Lax,
            Expires = DateTime.UtcNow.AddHours(1),
            Path = "/"
        };
        context.Response.Cookies.Append("X-Auth-Token", token, cookieOptions);
    }
    await Task.CompletedTask;
    return Results.Redirect("/main-dashboard");
});

app.MapGet("/auth/logout", (HttpContext context) => 
{
    context.Response.Cookies.Delete("X-Auth-Token");
    return Results.Redirect("/login");
});

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");
app.Run();
