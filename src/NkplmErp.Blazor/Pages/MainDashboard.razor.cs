using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.QuickGrid;
using NkplmErp.Application.Interfaces;
using NkplmErp.Shared.DTOs;
using NkplmErp.Blazor.Services.Auth;
using System.Net;
using System.Net.Http;

namespace NkplmErp.Blazor.Pages;

public partial class MainDashboard : ComponentBase
{
    [Inject] 
    private IBuyerOrderSummaryService BuyerOrderSummaryService { get; set; } = default!;

    [Inject]
    private Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

    [Inject]
    private ILogger<MainDashboard> Logger { get; set; } = default!;

    [Inject]
    private TokenProvider _tokenProvider { get; set; } = default!;

    [Inject]
    private NkplmErp.Blazor.Services.Toast.ToastService ToastService { get; set; } = default!;

    [Inject]
    private NkplmErp.Blazor.Services.RoleManagement.PermissionService PermSvc { get; set; } = default!;

    [Inject]
    private NkplmErp.Blazor.Services.RoleManagement.RoleManagementApiClient RoleApi { get; set; } = default!;

    [Inject]
    private NavigationManager Nav { get; set; } = default!;

    private bool _redirecting = false;

    private IQueryable<BuyerOrderSummaryDto> OrderSummaries { get; set; } = Enumerable.Empty<BuyerOrderSummaryDto>().AsQueryable();
    private IQueryable<BuyerOrderSummaryDto> OrderSummaries_pop { get; set; } = Enumerable.Empty<BuyerOrderSummaryDto>().AsQueryable();
    private bool IsLoading { get; set; } = true;
    private int CurrentYear { get; set; } = DateTime.Now.Year;
    private string SelectedType { get; set; } = "All";
    private string? SelectedBuyer { get; set; }
    private int? SelectedBuyerId => int.TryParse(SelectedBuyer, out var id) ? id : null;
    private string? SelectedTypeCategory { get; set; }
    private bool showModal { get; set; } = false;
    private bool showHistoryModal { get; set; } = false;
    private bool showProductionFlowModal { get; set; } = false;
    private bool showYearHistoryModal { get; set; } = false;
    private bool showRunningOrdersModal { get; set; } = false;
    private List<int> AvailableYears { get; set; } = new();
    private IQueryable<BuyerOrderHistoryDto> SelectedBuyerHistory { get; set; } = Enumerable.Empty<BuyerOrderHistoryDto>().AsQueryable();
    private IQueryable<BuyerOrderHistoryDto> SelectedYearHistory { get; set; } = Enumerable.Empty<BuyerOrderHistoryDto>().AsQueryable();
    private IQueryable<AbsentBuyer> AbsentBuyerList { get; set; } = Enumerable.Empty<AbsentBuyer>().AsQueryable(); 
    private IQueryable<OrderStatusDetailDto> OrderStatusDetailList { get; set; } = Enumerable.Empty<OrderStatusDetailDto>().AsQueryable(); 
    private IQueryable<ProductionFlowDto> ProductionFlowList { get; set; } = Enumerable.Empty<ProductionFlowDto>().AsQueryable();   
    private IQueryable<BuyerOrderDto> RunningOrdersList { get; set; } = Enumerable.Empty<BuyerOrderDto>().AsQueryable();
    private int? ActiveFlowBuyerId { get; set; }
    private PaginationState absentPagination = new PaginationState { ItemsPerPage = 16 };
    private string absentSearchTerm = string.Empty;
    private string orderSearchTerm = string.Empty;
    private bool showOrderSearch { get; set; } = false;
    private bool showAbsentSearch { get; set; } = false;
    private List<string> RunningOrderCategories { get; set; } = new();
    private List<LineGraphDataPoint> _graphData = new()
    {
        new() { Label = "Mon", Value = 1200 },
        new() { Label = "Tue", Value = 1900 },
        new() { Label = "Wed", Value = 1500 },
        new() { Label = "Thu", Value = 2100 },
        new() { Label = "Fri", Value = 1800 },
        new() { Label = "Sat", Value = 2400 },
        new() { Label = "Sun", Value = 2200 }
    };

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
    private bool IsOrderDetailLoading { get; set; } = true;
    private bool IsProductionFlowLoading { get; set; } = false;
    private bool isHistoryGridView { get; set; } = true;
    private bool isYearHistoryGridView { get; set; } = true;
    private bool IsRunningOrdersLoading { get; set; } = false;
    private string CurrentRunningOrdersTitle { get; set; } = "Running Orders";
    private int SelectedYear { get; set; }
    private string? SelectedFlowOrderNo { get; set; }
    private bool showMoreOrdersMenu { get; set; } = false;
    private List<string> FlowOrderNoList { get; set; } = new();

    // Global Style Detail Properties
    private bool IsStyleModalVisible { get; set; } = false;
    private StyleDetailsDto? SelectedStyleDetails { get; set; }
    private bool IsLoadingStyleDetails { get; set; } = false;
    private string? SelectedStyleNo { get; set; }

    // Order Detail Popup State (Hoisted from ProductionFlow)
    private bool IsOrderDetailVisible { get; set; }
    private bool IsLoadingOrderDetail { get; set; }
    private IEnumerable<OrderViewHeaderDto> SelectedOrderDetails { get; set; } = Enumerable.Empty<OrderViewHeaderDto>();
    private string SelectedOrderNo { get; set; } = string.Empty;
    private List<string> sizeHeaders { get; set; } = new();

    // Price Analysis Popup State (Hoisted from ProductionFlow)
    private bool IsAnalysisPopupVisible { get; set; }
    private bool IsLoadingAnalysis { get; set; }
    private IEnumerable<OrderPriceAnalysisDto> AnalysisItems { get; set; } = Enumerable.Empty<OrderPriceAnalysisDto>();
    private decimal UsdRate { get; set; } = 150m;
    private bool IsAnalysisUnmasked { get; set; } = false;
    private string AnalysisPinInput { get; set; } = string.Empty;
    private string? AnalysisPinError { get; set; }

    private async Task ShowStyleDetails(string styleNo)
    {
        SelectedStyleNo = styleNo;
        IsStyleModalVisible = true;
        IsLoadingStyleDetails = true;
        StateHasChanged();

        try 
        {
            SelectedStyleDetails = await BuyerOrderSummaryService.GetStyleDetailsAsync(styleNo);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            IsStyleModalVisible = false;
            ToastService.ShowWarning("Session Expired. Redirecting to login...");
            _tokenProvider.NotifySessionExpired();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading style details for {StyleNo}", styleNo);
        }
        finally
        {
            IsLoadingStyleDetails = false;
            StateHasChanged();
        }
    }

    public bool showHistoryKnit { get; set; } = false;
    public bool showHistoryWeave { get; set; } = false;
    public bool showHistorySilk { get; set; } = false;
    public bool showHistoryLinen { get; set; } = false;
    public bool showHistoryOther { get; set; } = false;
    public bool showHistoryAll { get; set; } = true;
    public bool showHistoryFilterDropdown { get; set; } = false;

    private void ToggleHistoryFilter(string type)
    {
        if (type == "All")
        {
            showHistoryAll = !showHistoryAll;
            if (showHistoryAll)
            {
                showHistoryKnit = showHistoryWeave = showHistorySilk = showHistoryLinen = showHistoryOther = false;
            }
        }
        else
        {
            showHistoryAll = false;
            if (type == "Knit") showHistoryKnit = !showHistoryKnit;
            if (type == "Weave") showHistoryWeave = !showHistoryWeave;
            if (type == "Silk") showHistorySilk = !showHistorySilk;
            if (type == "Linen") showHistoryLinen = !showHistoryLinen;
            if (type == "Other") showHistoryOther = !showHistoryOther;
            
            if (!showHistoryKnit && !showHistoryWeave && !showHistorySilk && !showHistoryLinen && !showHistoryOther)
            {
                showHistoryAll = true;
            }
        }
        StateHasChanged();
    }

    private void ToggleYearHistoryFilter(string type)
    {
        if (type == "All")
        {
            showYearAll = !showYearAll;
            if (showYearAll)
            {
                showYearKnit = showYearWeave = showYearSilk = showYearLinen = showYearOther = false;
            }
        }
        else
        {
            showYearAll = false;
            if (type == "Knit") showYearKnit = !showYearKnit;
            if (type == "Weave") showYearWeave = !showYearWeave;
            if (type == "Silk") showYearSilk = !showYearSilk;
            if (type == "Linen") showYearLinen = !showYearLinen;
            if (type == "Other") showYearOther = !showYearOther;
            
            if (!showYearKnit && !showYearWeave && !showYearSilk && !showYearLinen && !showYearOther)
            {
                showYearAll = true;
            }
        }
        StateHasChanged();
    }


    private List<MultiLineGraphSeries> BuyerHistoryMultiGraphData
    {
        get
        {
            var data = SelectedBuyerHistory.ToList().OrderBy(x => x.Year).ToList();
            var seriesList = new List<MultiLineGraphSeries>();

            if (showHistoryAll)
                seriesList.Add(new MultiLineGraphSeries { Name = "All", Color = "#2e2b8e", DataPoints = data.Select(x => new LineGraphDataPoint { Label = x.Year.ToString(), Value = (double)x.TotalPcs }).ToList() });
            if (showHistoryKnit)
                seriesList.Add(new MultiLineGraphSeries { Name = "Knit", Color = "#ef4444", DataPoints = data.Select(x => new LineGraphDataPoint { Label = x.Year.ToString(), Value = (double)x.Knit }).ToList() });
            if (showHistoryWeave)
                seriesList.Add(new MultiLineGraphSeries { Name = "Weave", Color = "#3b82f6", DataPoints = data.Select(x => new LineGraphDataPoint { Label = x.Year.ToString(), Value = (double)x.Weave }).ToList() });
            if (showHistorySilk)
                seriesList.Add(new MultiLineGraphSeries { Name = "Silk", Color = "#10b981", DataPoints = data.Select(x => new LineGraphDataPoint { Label = x.Year.ToString(), Value = (double)x.Silk }).ToList() });
            if (showHistoryLinen)
                seriesList.Add(new MultiLineGraphSeries { Name = "Linen", Color = "#f59e0b", DataPoints = data.Select(x => new LineGraphDataPoint { Label = x.Year.ToString(), Value = (double)x.Linen }).ToList() });
            if (showHistoryOther)
                seriesList.Add(new MultiLineGraphSeries { Name = "Other", Color = "#8b5cf6", DataPoints = data.Select(x => new LineGraphDataPoint { Label = x.Year.ToString(), Value = (double)x.Other }).ToList() });

            return seriesList;
        }
    }

    public bool showYearKnit { get; set; } = false;
    public bool showYearWeave { get; set; } = false;
    public bool showYearSilk { get; set; } = false;
    public bool showYearLinen { get; set; } = false;
    public bool showYearOther { get; set; } = false;
    public bool showYearAll { get; set; } = true;
    public bool showYearHistoryFilterDropdown { get; set; } = false;


    private List<MultiLineGraphSeries> YearHistoryMultiGraphData
    {
        get
        {
            var data = SelectedYearHistory.ToList();
            var seriesList = new List<MultiLineGraphSeries>();

            if (showYearAll)
                seriesList.Add(new MultiLineGraphSeries { Name = "All", Color = "#2e2b8e", DataPoints = data.Select(x => new LineGraphDataPoint { Label = x.MonthName, Value = (double)x.TotalPcs }).ToList() });
            if (showYearKnit)
                seriesList.Add(new MultiLineGraphSeries { Name = "Knit", Color = "#ef4444", DataPoints = data.Select(x => new LineGraphDataPoint { Label = x.MonthName, Value = (double)x.Knit }).ToList() });
            if (showYearWeave)
                seriesList.Add(new MultiLineGraphSeries { Name = "Weave", Color = "#3b82f6", DataPoints = data.Select(x => new LineGraphDataPoint { Label = x.MonthName, Value = (double)x.Weave }).ToList() });
            if (showYearSilk)
                seriesList.Add(new MultiLineGraphSeries { Name = "Silk", Color = "#10b981", DataPoints = data.Select(x => new LineGraphDataPoint { Label = x.MonthName, Value = (double)x.Silk }).ToList() });
            if (showYearLinen)
                seriesList.Add(new MultiLineGraphSeries { Name = "Linen", Color = "#f59e0b", DataPoints = data.Select(x => new LineGraphDataPoint { Label = x.MonthName, Value = (double)x.Linen }).ToList() });
            if (showYearOther)
                seriesList.Add(new MultiLineGraphSeries { Name = "Other", Color = "#8b5cf6", DataPoints = data.Select(x => new LineGraphDataPoint { Label = x.MonthName, Value = (double)x.Other }).ToList() });

            return seriesList;
        }
    }

    protected override async Task OnInitializedAsync()
    {
        // Per-user landing: send users who can't view the Dashboard to their first
        // permitted page. (Admins resolve to the dashboard itself, so they stay.)
        try
        {
            if (!PermSvc.IsLoaded)
                await PermSvc.LoadPermissionsAsync();

            if (!PermSvc.CanView("Dashboard") && !PermSvc.LandingApplied)
            {
                PermSvc.LandingApplied = true; // one-shot — never loop back to the dashboard
                var landing = await RoleApi.GetMyLandingAsync();
                // Never redirect to a dashboard route from the dashboard (avoids loops),
                // and never redirect to the page we're already on.
                static string Norm(string? p) => "/" + (p ?? "").Trim().TrimStart('/').TrimEnd('/').ToLowerInvariant();
                var dashAliases = new[] { "/", "/main-dashboard", "/dashboard" };
                var landingNorm = Norm(landing);
                var currentNorm = Norm(Nav.ToBaseRelativePath(Nav.Uri).Split('?')[0]);
                if (!string.IsNullOrWhiteSpace(landing)
                    && !dashAliases.Contains(landingNorm)
                    && landingNorm != currentNorm)
                {
                    _redirecting = true;
                    Nav.NavigateTo(landing, replace: true);
                }
            }
        }
        catch { /* fall through to the dashboard if landing resolution fails */ }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_redirecting) return; // navigating away to the user's landing page
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
                    Console.WriteLine(">>>> [DEBUG] MainDashboard - User IS authenticated. Starting LoadBuyerYears.");
                    Logger.LogInformation("DEBUG: MainDashboard - User IS authenticated. Starting initialization sequence.");
                    
                    // Load available years FIRST so we have a valid CurrentYear from the database
                    await LoadBuyerYears();
                    Console.WriteLine($">>>> [DEBUG] MainDashboard - Years loaded. CurrentYear is now: {CurrentYear}. Starting LoadData.");
                    
                    await LoadData();
                    await LoadOrderStatusDetail(CurrentYear, "Running");
                    Console.WriteLine(">>>> [DEBUG] MainDashboard - LoadData complete.");
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
            var list = (result ?? Enumerable.Empty<BuyerOrderSummaryDto>()).ToList();
            for (int i = 0; i < list.Count; i++) list[i].SN = i + 1;
            
            if (count > 10)
            {
                OrderSummaries_pop = list.AsQueryable();
                Console.WriteLine($">>>> [DEBUG] MainDashboard.LoadData - Received {OrderSummaries_pop.Count()} records for summary.");
            }
            else
            {
                OrderSummaries = list.AsQueryable();
                Console.WriteLine($">>>> [DEBUG] MainDashboard.LoadData - Received {OrderSummaries.Count()} records.");
            }
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            ToastService.ShowWarning("Session Expired. Redirecting to login...");
            _tokenProvider.NotifySessionExpired();
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
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            ToastService.ShowWarning("Session Expired. Redirecting to login...");
            _tokenProvider.NotifySessionExpired();
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
        ActiveFlowBuyerId = summary.CustomerId;
        SelectedBuyerName = summary.CustomerName;
        showHistoryModal = true;
        IsHistoryLoading = true;
        StateHasChanged();

        try
        {
            var result = await BuyerOrderSummaryService.GetBuyerOrderHistoryAsync(summary.CustomerId, null);
            var list = (result ?? Enumerable.Empty<BuyerOrderHistoryDto>()).ToList();
            for (int i = 0; i < list.Count; i++) list[i].SN = i + 1;
            SelectedBuyerHistory = list.AsQueryable();
            await LoadBuyerProfile(summary.CustomerId, null);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            showHistoryModal = false;
            ToastService.ShowWarning("Session Expired. Redirecting to login...");
            _tokenProvider.NotifySessionExpired();
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
        ActiveFlowBuyerId = summary.CustomerId;
        SelectedBuyerName = summary.CustomerName;
        showHistoryModal = true;
        IsHistoryLoading = true;
        StateHasChanged();
        
        try
        {
            var result = await BuyerOrderSummaryService.GetBuyerOrderHistoryAsync(summary.CustomerId, null);
            SelectedBuyerHistory = result.AsQueryable();
            await LoadBuyerProfile(summary.CustomerId, null);

        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            showHistoryModal = false;
            ToastService.ShowWarning("Session Expired. Redirecting to login...");
            _tokenProvider.NotifySessionExpired();
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
        ActiveFlowBuyerId = summary.CustomerId;
        SelectedBuyerName = summary.CustomerName;
        showHistoryModal = true;
        IsHistoryLoading = true;
        StateHasChanged();

        try
        {
            var result = await BuyerOrderSummaryService.GetBuyerOrderHistoryAsync(summary.CustomerId, null);
            SelectedBuyerHistory = result.AsQueryable();
            await LoadBuyerProfile(summary.CustomerId, null);

        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            showHistoryModal = false;
            ToastService.ShowWarning("Session Expired. Redirecting to login...");
            _tokenProvider.NotifySessionExpired();
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
        showYearHistoryModal = true;
        IsYearHistoryLoading = true;
        
        try
        {
            var result = await BuyerOrderSummaryService.GetBuyerOrderHistoryAsync(history.CustomerId, history.Year);
            var list = (result ?? Enumerable.Empty<BuyerOrderHistoryDto>()).ToList();
            for (int i = 0; i < list.Count; i++) list[i].SN = i + 1;
            SelectedYearHistory = list.AsQueryable();
            await LoadBuyerProfile(history.CustomerId,history.Year);
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            showYearHistoryModal = false;
            ToastService.ShowWarning("Session Expired. Redirecting to login...");
            _tokenProvider.NotifySessionExpired();
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
        showYearHistoryModal = false;
    }
    private async Task LoadBuyerProfile(int Buyer, int? year = null)
    {
        try
        {
            var result = await BuyerOrderSummaryService.GetBuyerProfileAsync(Buyer, year);
            SelectedBuyerProfile = result.FirstOrDefault();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            ToastService.ShowWarning("Session Expired. Redirecting to login...");
            _tokenProvider.NotifySessionExpired();
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
            var list = (result ?? Enumerable.Empty<AbsentBuyer>()).ToList();
            for (int i = 0; i < list.Count; i++) list[i].SN = i + 1;
            AbsentBuyerList = list.AsQueryable();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            ToastService.ShowWarning("Session Expired. Redirecting to login...");
            _tokenProvider.NotifySessionExpired();
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
            var list = (result ?? Enumerable.Empty<OrderStatusDetailDto>()).ToList();
            for (int i = 0; i < list.Count; i++) list[i].SN = i + 1;
            OrderStatusDetailList = list.AsQueryable();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            ToastService.ShowWarning("Session Expired. Redirecting to login...");
            _tokenProvider.NotifySessionExpired();
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
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            showProductionFlowModal = false;
            ToastService.ShowWarning("Session Expired. Redirecting to login...");
            _tokenProvider.NotifySessionExpired();
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
            ActiveFlowBuyerId = summary.CustomerId;
            SelectedFlowOrderNo = summary.OrderNo;
            InitializeFlowOrderNoList(summary.CustomerId);
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
            ActiveFlowBuyerId = customerId;
            // Automatically select the first order for this buyer
            SelectedFlowOrderNo = OrderStatusDetailList
                .Where(x => x.CustomerId == customerId)
                .Select(x => x.OrderNo)
                .FirstOrDefault();

            InitializeFlowOrderNoList(customerId);
                
            await LoadProductionFlow(customerId, SelectedFlowOrderNo);
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

    private async Task SelectFlowOrder(string? orderNo)
    {
        if (orderNo != null && FlowOrderNoList.Contains(orderNo))
        {
            var currentIndex = FlowOrderNoList.IndexOf(orderNo);
            if (currentIndex >= 10)
            {
                // Promotion logic: replace the 10th tab (index 9)
                var lastTabItem = FlowOrderNoList[9];
                FlowOrderNoList.RemoveAt(currentIndex);
                FlowOrderNoList.RemoveAt(9);
                FlowOrderNoList.Insert(9, orderNo);
                FlowOrderNoList.Add(lastTabItem);
            }
        }

        SelectedFlowOrderNo = orderNo;
        showMoreOrdersMenu = false;
        if (ActiveFlowBuyerId.HasValue)
        {
            await LoadProductionFlow(ActiveFlowBuyerId.Value, orderNo);
        }
    }

    private async Task ShowRunningOrders(BuyerOrderSummaryDto summary, int flag = 2)
    {
        ActiveFlowBuyerId = summary.CustomerId;
        SelectedBuyerName = summary.CustomerName;
        CurrentRunningOrdersTitle = flag == 1 ? "Waiting Orders" : "Running Orders";
        showRunningOrdersModal = true;
        IsRunningOrdersLoading = true;
        StateHasChanged();

        try
        {
            var result = await BuyerOrderSummaryService.GetBuyersOrdersAsync(summary.CustomerId, flag);
            var list = (result ?? Enumerable.Empty<BuyerOrderDto>()).ToList();
            for (int i = 0; i < list.Count; i++) list[i].SN = i + 1;
            
            // Extract unique categories
            RunningOrderCategories = list
                .SelectMany(x => x.Categories.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            RunningOrdersList = list.AsQueryable();
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            showRunningOrdersModal = false;
            ToastService.ShowWarning("Session Expired. Redirecting to login...");
            _tokenProvider.NotifySessionExpired();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading running orders for {BuyerId}", summary.CustomerId);
        }
        finally
        {
            IsRunningOrdersLoading = false;
            StateHasChanged();
        }
    }

    private async Task ShowProductionFlowByOrder(BuyerOrderDto order)
    {
        showProductionFlowModal = true;
        IsProductionFlowLoading = true;
        try
        {
            SelectedFlowOrderNo = order.OrderNo;
            if (ActiveFlowBuyerId.HasValue)
            {
                InitializeFlowOrderNoList(ActiveFlowBuyerId.Value);
            }
            await LoadProductionFlow(ActiveFlowBuyerId ?? 0, order.OrderNo);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading production flow for {OrderNo}", order.OrderNo);
        }
        finally
        {
            IsProductionFlowLoading = false;
        }
    }

    private void InitializeFlowOrderNoList(int customerId)
    {
        FlowOrderNoList = OrderStatusDetailList
            .Where(x => x.CustomerId == customerId)
            .GroupBy(x => x.OrderNo)
            .Select(g => new { 
                OrderNo = g.Key, 
                ShippingDate = g.Min(x => x.LatestShippingDate) ?? DateOnly.MaxValue 
            })
            .OrderBy(x => x.ShippingDate)
            .Select(x => x.OrderNo)
            .ToList();
            
        // If the selected order is in the "more" section, promote it immediately
        if (SelectedFlowOrderNo != null && FlowOrderNoList.Contains(SelectedFlowOrderNo))
        {
            var idx = FlowOrderNoList.IndexOf(SelectedFlowOrderNo);
            if (idx >= 10)
            {
                var itemAt10 = FlowOrderNoList[9];
                FlowOrderNoList.RemoveAt(idx);
                FlowOrderNoList.RemoveAt(9);
                FlowOrderNoList.Insert(9, SelectedFlowOrderNo);
                FlowOrderNoList.Add(itemAt10);
            }
        }
    }

    // NEW: Handler invoked by ProductionFlow component to show order details in the hoisted modal
    private async Task ShowOrderDetails(string orderNo)
    {
        if (string.IsNullOrWhiteSpace(orderNo)) return;

        SelectedOrderNo = orderNo;
        IsOrderDetailVisible = true;
        IsLoadingOrderDetail = true;
        SelectedOrderDetails = Enumerable.Empty<OrderViewHeaderDto>();
        StateHasChanged();

        try
        {
            var result = await BuyerOrderSummaryService.GetOrderViewDataAsync(orderNo);
            var list = (result ?? Enumerable.Empty<OrderViewHeaderDto>()).ToList();
            for (int i = 0; i < list.Count; i++) list[i].SN = i + 1;
            
            // Extract unique sizes for dynamic columns
            sizeHeaders = list
                .SelectMany(x => x.Sizes.Keys)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            SelectedOrderDetails = list;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            IsOrderDetailVisible = false;
            ToastService.ShowWarning("Session Expired. Redirecting to login...");
            _tokenProvider.NotifySessionExpired();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading order details for {OrderNo}", orderNo);
        }
        finally
        {
            IsLoadingOrderDetail = false;
            StateHasChanged();
        }
    }

    // NEW: Handler invoked by ProductionFlow component to show price analysis in the hoisted modal
    private async Task ShowOrderAnalysis(string orderNo)
    {
        if (string.IsNullOrWhiteSpace(orderNo)) return;

        SelectedOrderNo = orderNo;
        IsAnalysisPopupVisible = true;
        IsLoadingAnalysis = true;
        IsAnalysisUnmasked = false;
        AnalysisPinInput = string.Empty;
        AnalysisPinError = null;
        AnalysisItems = Enumerable.Empty<OrderPriceAnalysisDto>();
        StateHasChanged();

        try
        {
            var result = await BuyerOrderSummaryService.GetOrderPriceAnalysisAsync(orderNo, UsdRate);
            var list = (result ?? Enumerable.Empty<OrderPriceAnalysisDto>()).ToList();
            for (int i = 0; i < list.Count; i++) list[i].SN = i + 1;
            AnalysisItems = list;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            IsAnalysisPopupVisible = false;
            ToastService.ShowWarning("Session Expired. Redirecting to login...");
            _tokenProvider.NotifySessionExpired();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error loading order analysis for {OrderNo}", orderNo);
        }
        finally
        {
            IsLoadingAnalysis = false;
            StateHasChanged();
        }
    }

    // NEW: Verify PIN to unmask pricing data in the analysis modal
    private Task VerifyAnalysisPin()
    {
        if (AnalysisPinInput == "1221")
        {
            IsAnalysisUnmasked = true;
            AnalysisPinError = null;
        }
        else
        {
            AnalysisPinError = "Incorrect PIN. Please try again.";
            AnalysisPinInput = string.Empty;
        }
        return Task.CompletedTask;
    }
}