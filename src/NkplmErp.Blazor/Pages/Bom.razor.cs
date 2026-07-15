using Microsoft.AspNetCore.Components;
using NkplmErp.Application.Interfaces;
using NkplmErp.Blazor.Services.Bom;
using NkplmErp.Blazor.Services.RoleManagement;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Pages;

public partial class Bom
{
    [Inject] private BomApiClient BomApi { get; set; } = default!;
    [Inject] private IProductionPlanningService PlanningService { get; set; } = default!;
    [Inject] private PermissionService PermSvc { get; set; } = default!;

    // ===== State =====
    private bool AccessDenied = false;

    private DateTime SelectedMonth = DateTime.Today;
    private string SelectedMonthStr
    {
        get => SelectedMonth.ToString("yyyy-MM-dd");
        set { if (DateTime.TryParse(value, out var d)) { SelectedMonth = d; _ = LoadOrdersAsync(); } }
    }

    // Column 1 — orders awaiting a yarn calculation.
    private List<MonthlyOrderDetailDto> Orders = new();
    private string OrderSearch = "";
    private bool IsLoadingOrders = false;

    // Production orders that already have a yarn order placed — hidden from the list.
    private HashSet<string> PlacedOrders = new(StringComparer.OrdinalIgnoreCase);

    // Column 2 — the selected order's yarn requirement.
    private string? SelectedOrderNo;
    private List<BomYarnLineDto> YarnLines = new();
    private bool IsCalculating = false;

    // Column 3 — yarn order basket (temporary, this session).
    // One line per yarn × color; import kg summed across orders, with each
    // contributing order's quantity kept for traceability.
    private readonly List<BasketLine> Basket = new();

    private sealed class BasketLine
    {
        public string ProductId { get; set; } = "";
        public string YarnName { get; set; } = "";
        public string OrderColor { get; set; } = "";
        public string StylePly { get; set; } = "";
        // orderNo -> user-edited ordered kg (what gets saved).
        public Dictionary<string, decimal> OrderQty { get; } = new(StringComparer.OrdinalIgnoreCase);
        // orderNo -> actual requirement kg (for reference / display).
        public Dictionary<string, decimal> NeedQty { get; } = new(StringComparer.OrdinalIgnoreCase);
        public decimal OrderedKg => OrderQty.Values.Sum();
        public decimal NeedKg => NeedQty.Values.Sum();
        public IEnumerable<string> Orders => OrderQty.Keys;
        public string Display => string.IsNullOrWhiteSpace(YarnName) ? ProductId : YarnName;
    }

    private string StatusMessage = "";
    private bool IsError = false;

    private IEnumerable<MonthlyOrderDetailDto> FilteredOrders =>
        Orders
            .Where(o => !PlacedOrders.Contains(o.OrderNo.Trim()))
            .Where(o => string.IsNullOrWhiteSpace(OrderSearch)
                        || o.OrderNo.Contains(OrderSearch, StringComparison.OrdinalIgnoreCase));

    // ===== Column 2 roll-ups =====
    private decimal TotalNeed => YarnLines.Sum(l => l.TotalNeed);
    private decimal TotalSelf => YarnLines.Sum(l => l.SelfWt);
    private decimal TotalStock => YarnLines.Sum(l => l.StockQty);
    private decimal TotalImport => YarnLines.Where(l => l.IsImport).Sum(l => l.OrderQtyKg);
    private int ImportLineCount => YarnLines.Count(l => l.IsImport);

    // ===== Column 3 roll-ups =====
    private decimal BasketOrderedKg => Basket.Sum(l => l.OrderedKg);  // user-edited order qty
    private decimal BasketNeedKg => Basket.Sum(l => l.NeedKg);        // actual need
    private int BasketOrderCount => Basket.SelectMany(l => l.Orders).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    // ===== Lifecycle =====
    protected override async Task OnInitializedAsync()
    {
        if (!PermSvc.IsLoaded)
            await PermSvc.LoadPermissionsAsync();

        if (!PermSvc.CanView("Bom"))
        {
            AccessDenied = true;
            return;
        }

        await LoadPlacedOrdersAsync();
        await LoadOrdersAsync();
    }

    private async Task LoadOrdersAsync()
    {
        IsLoadingOrders = true;
        StateHasChanged();
        var data = await PlanningService.GetMonthlyOrderDetailsAsync(SelectedMonth);
        Orders = data?.ToList() ?? new();
        IsLoadingOrders = false;
        StateHasChanged();
    }

    private async Task LoadPlacedOrdersAsync()
    {
        var placed = await BomApi.GetYarnOrderedOrdersAsync();
        PlacedOrders = new HashSet<string>(placed.Select(p => p.Trim()), StringComparer.OrdinalIgnoreCase);
    }

    // ===== Column 2 — calculate =====
    private async Task SelectOrderAsync(string orderNo)
    {
        SelectedOrderNo = orderNo;
        IsCalculating = true;
        YarnLines = new();
        StateHasChanged();

        YarnLines = await BomApi.GetYarnRequirementAsync(orderNo, flag: 1);

        IsCalculating = false;
        StateHasChanged();
    }

    private Task RecalculateAsync() =>
        string.IsNullOrEmpty(SelectedOrderNo) ? Task.CompletedTask : SelectOrderAsync(SelectedOrderNo);

    // ===== Column 3 — basket =====
    private static string LineKey(string productId, string color) => $"{productId}|{color}".ToLowerInvariant();
    private static string LineKey(BomYarnLineDto l) => LineKey(l.ProductId, l.OrderColor);
    private static string LineKey(BasketLine l) => LineKey(l.ProductId, l.OrderColor);

    private void AddImportLinesToBasket()
    {
        if (string.IsNullOrEmpty(SelectedOrderNo)) return;

        var touched = 0;
        foreach (var line in YarnLines.Where(l => l.IsImport))
        {
            var bl = Basket.FirstOrDefault(b => LineKey(b) == LineKey(line));
            if (bl == null)
            {
                bl = new BasketLine
                {
                    ProductId = line.ProductId,
                    YarnName = line.YarnName,
                    OrderColor = line.OrderColor,
                    StylePly = line.StylePly
                };
                Basket.Add(bl);
            }
            // Set (not add) this order's contribution so re-adding the same
            // order updates rather than double-counts. Order qty = the
            // (possibly edited) weight; need = the actual requirement.
            bl.OrderQty[SelectedOrderNo] = line.OrderQtyKg;
            bl.NeedQty[SelectedOrderNo] = line.ImportKg;
            touched++;
        }
        ShowStatus(touched > 0
            ? $"Added/updated {touched} line(s) from {SelectedOrderNo}."
            : "No import lines to add.", touched == 0);
    }

    private void RemoveFromBasket(BasketLine line) => Basket.RemoveAll(b => LineKey(b) == LineKey(line));

    private void ClearBasket() => Basket.Clear();

    private bool IsPlacing = false;

    private async Task PlaceYarnOrderAsync()
    {
        if (Basket.Count == 0) { ShowStatus("Basket is empty.", true); return; }

        // Expand each summed cart line into per-order rows (order_no tracking).
        var lines = Basket.SelectMany(b => b.OrderQty.Select(o => new YarnOrderLineDto
        {
            ProductId = b.ProductId,
            YarnName = b.YarnName,
            Color = b.OrderColor,
            Ply = b.StylePly,
            OrderNo = o.Key,
            ImportKg = o.Value
        })).ToList();

        IsPlacing = true;
        StateHasChanged();

        var result = await BomApi.PlaceYarnOrderAsync(new PlaceYarnOrderRequest { Lines = lines });

        IsPlacing = false;

        if (result is { IsSuccess: true })
        {
            var placedNos = lines.Select(l => l.OrderNo.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            ShowStatus($"{result.YoNo} placed — {lines.Count} line(s) from {placedNos.Count} order(s), {result.TotalKg:N2} kg.", false);
            ClearBasket();

            // Drop the now-ordered production orders from the pending list.
            foreach (var no in placedNos) PlacedOrders.Add(no);
            if (!string.IsNullOrEmpty(SelectedOrderNo) && PlacedOrders.Contains(SelectedOrderNo.Trim()))
            {
                SelectedOrderNo = null;
                YarnLines = new();
            }
        }
        else
        {
            ShowStatus($"Could not place yarn order: {result?.Message ?? "no response"}", true);
        }
    }

    private void ShowStatus(string message, bool isError)
    {
        StatusMessage = message;
        IsError = isError;
        StateHasChanged();
    }
}
