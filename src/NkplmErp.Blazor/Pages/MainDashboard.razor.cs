using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.QuickGrid;
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
    private IQueryable<BuyerOrderSummaryDto> OrderSummaries_pop { get; set; } = Enumerable.Empty<BuyerOrderSummaryDto>().AsQueryable();
    private bool IsLoading { get; set; } = false;
    private int CurrentYear { get; set; } = DateTime.Now.Year;
    private string SelectedType { get; set; } = "All";
    private string? SelectedBuyer { get; set; }
    private int? SelectedBuyerId => int.TryParse(SelectedBuyer, out var id) ? id : null;
    private string? SelectedTypeCategory { get; set; }
    private bool showModal = false;
    private bool showHistoryModal = false;
    private bool showProductionFlowModal=false;
    private bool showHYearistoryModal = false;
    private List<int> AvailableYears { get; set; } = new();
    private IQueryable<BuyerOrderHistoryDto> SelectedBuyerHistory { get; set; } = Enumerable.Empty<BuyerOrderHistoryDto>().AsQueryable();
    private IQueryable<BuyerOrderHistoryDto> SelectedYearHistory { get; set; } = Enumerable.Empty<BuyerOrderHistoryDto>().AsQueryable();
    private IQueryable<AbsentBuyer> AbsentBuyerList { get; set; } = Enumerable.Empty<AbsentBuyer>().AsQueryable(); 
    private IQueryable<OrderStatusDetailDto> OrderStatusDetailList { get; set; } = Enumerable.Empty<OrderStatusDetailDto>().AsQueryable(); 
    private IQueryable<ProductionFlowDto> ProductionFlowList { get; set; } = Enumerable.Empty<ProductionFlowDto>().AsQueryable();   
     private PaginationState absentPagination = new PaginationState { ItemsPerPage = 20 };
    private string absentSearchTerm = string.Empty;
    private string orderSearchTerm = string.Empty;

    private IQueryable<BuyerOrderSummaryDto> FilteredOrderSummariesPop => 
        string.IsNullOrWhiteSpace(orderSearchTerm)
            ? OrderSummaries_pop
            : OrderSummaries_pop.Where(x => x.CustomerName.Contains(orderSearchTerm, StringComparison.OrdinalIgnoreCase));

    private IQueryable<AbsentBuyer> FilteredAbsentBuyerList => 
        string.IsNullOrWhiteSpace(absentSearchTerm) 
            ? AbsentBuyerList 
            : AbsentBuyerList.Where(x => x.CustomerName.Contains(absentSearchTerm, StringComparison.OrdinalIgnoreCase));


    private BuyerProfile? SelectedBuyerProfile { get; set; }
    private string SelectedBuyerName { get; set; } = string.Empty;
    private bool IsHistoryLoading { get; set; } = false;
    private bool IsYearHistoryLoading { get; set; } = false;
    private string? LastErrorMessage { get; set; }
    private bool IsOrderDetailLoading { get; set; } = false;
    private bool IsProductionFlowLoading { get; set; } = false;
    private int SelectedYear { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        Console.WriteLine($">>>> [DEBUG] MainDashboard.OnAfterRenderAsync (firstRender: {firstRender})");
        if (firstRender)
        {
            try
            {
                Console.WriteLine(">>>> [DEBUG] MainDashboard - Getting Auth State...");
                if (AuthStateProvider == null)
                {
                    throw new Exception("AuthStateProvider is null!");
                }
                var authState = await AuthStateProvider.GetAuthenticationStateAsync();
                var user = authState?.User;

                if (user?.Identity?.IsAuthenticated == true)
                {
                    Console.WriteLine(">>>> [DEBUG] MainDashboard - User IS authenticated. Starting LoadData.");
                    Logger.LogInformation("DEBUG: MainDashboard - User IS authenticated. Starting LoadData.");
                    await LoadData();
                    Console.WriteLine(">>>> [DEBUG] MainDashboard - LoadData complete. Starting LoadBuyerYears.");
                    await LoadBuyerYears();
                    await LoadOrderStatusDetail(2026, "Running");
                    Console.WriteLine(">>>> [DEBUG] MainDashboard - LoadBuyerYears complete. Calling StateHasChanged.");
                    StateHasChanged();
                }
                else
                {
                    Console.WriteLine(">>>> [DEBUG] MainDashboard - User is NOT authenticated.");
                    Logger.LogWarning("DEBUG: MainDashboard - User is NOT authenticated. Skipping LoadData.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($">>>> [DEBUG] CRITICAL ERROR in MainDashboard.OnAfterRenderAsync: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                LastErrorMessage = $"Critical Error during initialization: {ex.Message}";
                IsLoading = false; // Fix: Ensure loading ends on error
                StateHasChanged();
            }
        }
    }

    private async Task LoadData(int count = 10)   
    {
        Console.WriteLine($">>>> [DEBUG] MainDashboard.LoadData starting (Year: {CurrentYear}, Type: {SelectedType})");
        Logger.LogInformation("DEBUG: MainDashboard.LoadData starting (CurrentYear: {Year}, SelectedType: {Type})", CurrentYear, SelectedType);
        IsLoading = true;
        LastErrorMessage = null;
        try
        {
            if (BuyerOrderSummaryService == null)
            {
                throw new Exception("BuyerOrderSummaryService is null!");
            }
            var result = await BuyerOrderSummaryService.GetBuyerOrderSummaryAsync(CurrentYear, SelectedType, count);
            if (count >10)
            {
                OrderSummaries_pop = (result ?? Enumerable.Empty<BuyerOrderSummaryDto>()).AsQueryable();
                Console.WriteLine($">>>> [DEBUG] MainDashboard.LoadData - Received {OrderSummaries_pop.Count()} records for summary.");
            }
            else
            {
                OrderSummaries = (result ?? Enumerable.Empty<BuyerOrderSummaryDto>()).AsQueryable();
                Console.WriteLine($">>>> [DEBUG] MainDashboard.LoadData - Received {OrderSummaries.Count()} records.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($">>>> [DEBUG] ERROR in LoadData: {ex.Message}");
            LastErrorMessage = $"Error loading dashboard: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task LoadBuyerYears()
    {
        try
        {
            var result = await BuyerOrderSummaryService.GetBuyerOrderYearsAsync(SelectedBuyerId);
            AvailableYears = result.ToList();
            if (AvailableYears.Any() && !AvailableYears.Contains(CurrentYear))
            {
                CurrentYear = AvailableYears.First();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading buyer years");
        }
    }

    private async Task HandleBuyerSelected()
    {
        Logger.LogInformation("DEBUG: HandleBuyerSelected triggered. SelectedBuyer: {SelectedBuyer}, SelectedBuyerId: {SelectedBuyerId}", SelectedBuyer, SelectedBuyerId);
        await LoadBuyerYears();
        await LoadData(20);
    }

    private async Task OpenMainModal()
    {
        showModal = true;
        await LoadData(20);
        await LoadBuyerYears();
        await LoadAbsentBuyerList();
        
        StateHasChanged();
    }

    private void CloseMainModal()
    {
        showModal = false;
    }

    private async Task ShowHistory(OrderStatusDetailDto summary)
    {
        SelectedBuyerName = summary.CustomerName;
        showHistoryModal = true;
        IsHistoryLoading = true;

        try
        {
            var result = await BuyerOrderSummaryService.GetBuyerOrderHistoryAsync(summary.CustomerId, null);
            SelectedBuyerHistory = result.AsQueryable();
            await LoadBuyerProfile(summary.CustomerId, null);

        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading buyer history for {BuyerId}", summary.CustomerId);
        }
        finally
        {
            IsHistoryLoading = false;
        }
    }
    private async Task ShowHistory(BuyerOrderSummaryDto summary)
    {
        SelectedBuyerName = summary.CustomerName;
        showHistoryModal = true;
        IsHistoryLoading = true;
        
        try
        {
            var result = await BuyerOrderSummaryService.GetBuyerOrderHistoryAsync(summary.CustomerId, null);
            SelectedBuyerHistory = result.AsQueryable();
            await LoadBuyerProfile(summary.CustomerId, null);

        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading buyer history for {BuyerId}", summary.CustomerId);
        }
        finally
        {
            IsHistoryLoading = false;
        }
    }

    private async Task ShowHistory(AbsentBuyer summary)
    {
        SelectedBuyerName = summary.CustomerName;
        showHistoryModal = true;
        IsHistoryLoading = true;

        try
        {
            var result = await BuyerOrderSummaryService.GetBuyerOrderHistoryAsync(summary.CustomerId, null);
            SelectedBuyerHistory = result.AsQueryable();
            await LoadBuyerProfile(summary.CustomerId, null);

        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading buyer history for {BuyerId}", summary.CustomerId);
        }
        finally
        {
            IsHistoryLoading = false;
        }
    }
    private async Task ShowYearlyHistory( BuyerOrderHistoryDto history)
    {
        SelectedYear=history.Year;
        showHYearistoryModal = true;
        IsYearHistoryLoading = true;
        
        try
        {
            var result = await BuyerOrderSummaryService.GetBuyerOrderHistoryAsync(history.CustomerId, history.Year);
            SelectedYearHistory = result.AsQueryable();
            await LoadBuyerProfile(history.CustomerId,history.Year);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading buyer history for {BuyerId}", history.CustomerId);
        }
        finally
        {
            IsYearHistoryLoading = false;
        }


        
    }
 
    private void CloseHistoryModal()
    {
        showHistoryModal = false;
    }
    private void CloseYearHistoryModal()
    {
        showHYearistoryModal = false;
    }
    private async Task LoadBuyerProfile(int Buyer, int? year = null)
    {
        try
        {
            var result = await BuyerOrderSummaryService.GetBuyerProfileAsync(Buyer, year);
            SelectedBuyerProfile = result.FirstOrDefault();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading buyer profile for {BuyerId}", Buyer);
        }

    }
    private async Task LoadAbsentBuyerList()
    {
        try
        {
            var result = await BuyerOrderSummaryService.GetAbsentBuyer();
            AbsentBuyerList = result.AsQueryable();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading absent buyers");
        }
    }

    private async Task LoadOrderStatusDetail(int CurrentYear,string SelectedType)
    {
        IsOrderDetailLoading = true;
        try
        {
            var result = await BuyerOrderSummaryService.GetOrderStatusDetailAsync(CurrentYear, SelectedType);
            OrderStatusDetailList = result.AsQueryable().Take(10);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading order status detail");
        }
        finally
        {
            IsOrderDetailLoading = false;
        }
    }
    private async Task LoadProductionFlow(int buyerId, string? OrderNo)
    {
        IsProductionFlowLoading = true;
        StateHasChanged(); // force spinner to show immediately
        try
        {
            var result = await BuyerOrderSummaryService.GetProductionFlowAsync(buyerId, OrderNo);
            ProductionFlowList = result.AsQueryable();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading production flow");
        }
        finally
        {
            IsProductionFlowLoading = false;
            StateHasChanged(); // force re-render so cards appear
        }
    }
  private async Task ShowProductionFlow(OrderStatusDetailDto summary)
    {
        showProductionFlowModal  = true;
        IsProductionFlowLoading = true;

        try
        {
            await LoadProductionFlow(summary.CustomerId, summary.OrderNo);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading production flow for {BuyerId}", summary.CustomerId);
        }
        finally
        {
            IsProductionFlowLoading = false;
        }
    }

    /// <summary>
    /// Opens the production flow modal for a buyer showing ALL orders (OrderNo = null).
    /// Used by the sidebar list where only CustomerId is available.
    /// </summary>
    private async Task ShowProductionFlowByBuyer(int customerId)
    {
        showProductionFlowModal = true;
        IsProductionFlowLoading = true;

        try
        {
            await LoadProductionFlow(customerId, null);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading production flow for buyer {BuyerId}", customerId);
        }
        finally
        {
            IsProductionFlowLoading = false;
        }
    }

}