using Microsoft.AspNetCore.Components;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Pages;

public partial class MainDashboard : ComponentBase
{
    [Inject] 
    private IBuyerOrderSummaryService BuyerOrderSummaryService { get; set; } = default!;

    [Inject]
    private Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    [Inject]
    private ILogger<MainDashboard> Logger { get; set; } = default!;

    private IQueryable<BuyerOrderSummaryDto> OrderSummaries { get; set; } = Enumerable.Empty<BuyerOrderSummaryDto>().AsQueryable();
    private bool IsLoading { get; set; } = true;
    private int CurrentYear { get; set; } = DateTime.Now.Year;
    private string SelectedType { get; set; } = "All";
     private bool showModal = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Awaiting authentication state ensures the Blazor Server circuit is fully established
            // and the JS Runtime (needed for LocalStorage) is available.
            var authState = await AuthStateProvider.GetAuthenticationStateAsync();
            var user = authState.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                Logger.LogInformation("DEBUG: MainDashboard - User IS authenticated. Starting LoadData.");
                await LoadData();
                StateHasChanged();
            }
            else
            {
                Logger.LogWarning("DEBUG: MainDashboard - User is NOT authenticated. Skipping LoadData.");
            }
        }
    }

    private async Task LoadData()
    {
        Logger.LogInformation("DEBUG: MainDashboard.LoadData starting (CurrentYear: {Year}, SelectedType: {Type})", CurrentYear, SelectedType);
        IsLoading = true;
        try
        {
            var result = await BuyerOrderSummaryService.GetBuyerOrderSummaryAsync(CurrentYear, SelectedType);
            OrderSummaries = result.AsQueryable();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading dashboard data: {ex.Message}");
        }
        finally
        {
            IsLoading = false;
        }
    }
    private async Task OpenMainModal()
    {
        showModal=true;
        
     }
    }