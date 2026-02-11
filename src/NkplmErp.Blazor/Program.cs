using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Blazored.LocalStorage;
using NkplmErp.Blazor.Data;
using NkplmErp.Blazor.Services.Auth;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();

// Authentication & LocalStorage
builder.Services.AddBlazoredLocalStorage();
builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(sp => sp.GetRequiredService<CustomAuthStateProvider>());
builder.Services.AddScoped<NkplmErp.Blazor.Services.Toast.ToastService>();

Console.WriteLine("===[ NkplmErp Blazor APP STARTING ]===");

// HttpClient configuration - Register handler as Transient so each service gets its own copy
// but it resolves Scoped dependencies (like CustomAuthStateProvider) correctly.
builder.Services.AddTransient<AuthenticationDelegatingHandler>();

// Manual Scoped Client for IAuthService
builder.Services.AddScoped<IAuthService>(sp => 
{
    var handler = sp.GetRequiredService<AuthenticationDelegatingHandler>();
    handler.InnerHandler = new HttpClientHandler(); 
    var client = new HttpClient(handler);
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5270/");
    
    var authStateProvider = sp.GetRequiredService<AuthenticationStateProvider>();
    var localStorage = sp.GetRequiredService<ILocalStorageService>();
    var jsRuntime = sp.GetRequiredService<Microsoft.JSInterop.IJSRuntime>();
    
    return new AuthService(client, authStateProvider, localStorage, jsRuntime);
});

// Manual Scoped Client for IBuyerOrderSummaryService
builder.Services.AddScoped<NkplmErp.Application.Interfaces.IBuyerOrderSummaryService>(sp => 
{
    var handler = sp.GetRequiredService<AuthenticationDelegatingHandler>();
    handler.InnerHandler = new HttpClientHandler(); 
    var client = new HttpClient(handler);
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "http://localhost:5270/");
    
    var logger = sp.GetRequiredService<ILogger<NkplmErp.Blazor.Services.BuyerOrderSummary.BuyerOrderSummaryService>>();
    return new NkplmErp.Blazor.Services.BuyerOrderSummary.BuyerOrderSummaryService(client, logger);
});

Console.WriteLine("DEBUG: Program.cs - Manual Scoped registrations complete.");


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

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
