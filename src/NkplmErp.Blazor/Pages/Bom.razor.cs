using System.Globalization;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NkplmErp.Application.Interfaces;
using NkplmErp.Blazor.Services.Bom;
using NkplmErp.Blazor.Services.RoleManagement;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Pages;

public partial class Bom
{
    [Inject] private BomApiClient BomApi { get; set; } = default!;
    // The month's orders come from PLM through the third-party API.
    [Inject] private NkplmErp.Blazor.Services.ThirdPartyApi.Manager.Interface.IThirdPartyApiManager ThirdPartyApiManager { get; set; } = default!;
    [Inject] private NkplmErp.Blazor.Services.Toast.ToastService ToastService { get; set; } = default!;
    [Inject] private PermissionService PermSvc { get; set; } = default!;
    [Inject] private NkplmErp.Blazor.Services.Loading.LoadingService _loading { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    // ===== State =====
    // Deep link from a BOM task card on /tasks: /bom?orderNo=GT-26011A opens that order's
    // yarn requirement straight away.
    [Parameter, SupplyParameterFromQuery(Name = "orderNo")] public string? FromOrderNo { get; set; }
    [Parameter, SupplyParameterFromQuery(Name = "poTaskId")] public int? FromPoTaskId { get; set; }
    [Parameter, SupplyParameterFromQuery(Name = "orderNos")] public string? FromOrderNos { get; set; }

    // The month that order ships in, sent alongside orderNo by sp_GetPoTask. Without it the
    // list below defaults to today, so an order shipping in another month opens a list it
    // isn't in. Any date in the month works — the report is queried by month.
    [Parameter, SupplyParameterFromQuery(Name = "month")] public string? FromMonth { get; set; }

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
    private string? CalculationOrderNos;
    private int? CalculationPoTaskId;
    private List<BomYarnLineDto> YarnLines = new();
    private bool IsCalculating = false;
    private string? CalculationError;

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
        // Add first, round after: the per-order figures stay exact and the line is rounded up to a whole kg once,
        // on their sum (0.600 + 0.300 -> 1 kg), never order by order.
        public decimal OrderedKg => OrderQty.Values.Sum() is var s && s > 0 ? Math.Ceiling(s) : 0m;

        // What is saved per order: the line's rounded kg shared out in proportion to each order's exact kg
        // (2 decimals; the rounding leftover goes to the biggest order so the rows add up to OrderedKg).
        public Dictionary<string, decimal> AllocatedKg()
        {
            var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
            var total = OrderQty.Values.Sum();
            var ordered = OrderedKg;
            if (total <= 0 || ordered <= 0) return result;

            foreach (var (order, kg) in OrderQty) result[order] = Math.Round(ordered * kg / total, 2);
            var biggest = OrderQty.OrderByDescending(o => o.Value).First().Key;
            result[biggest] += ordered - result.Values.Sum();
            return result;
        }
        public decimal NeedKg => NeedQty.Values.Sum();
        public IEnumerable<string> Orders => OrderQty.Keys;
        public string Display => string.IsNullOrWhiteSpace(YarnName) ? ProductId : YarnName;
    }

    private IEnumerable<MonthlyOrderDetailDto> FilteredOrders =>
        Orders
            .Where(o => !PlacedOrders.Contains(o.OrderNo.Trim()))
            .Where(o => string.IsNullOrWhiteSpace(OrderSearch)
                        || o.OrderNo.Contains(OrderSearch, StringComparison.OrdinalIgnoreCase));

    // The import input shows and keeps 2 decimals (1.21, never 1.2099999). Applied when the user leaves the field.
    private static void SetOrderQty(BomYarnLineDto line, string? text)
    {
        if (decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var kg))
            line.OrderQtyKg = Math.Round(kg, 2);
    }

    // ===== Column 2 roll-ups =====
    private decimal TotalNeed => YarnLines.Sum(l => l.TotalNeed);
    private decimal TotalSelf => YarnLines.Sum(l => l.SelfWt);
    private decimal TotalStock => YarnLines.Sum(l => l.StockQty);
    private decimal TotalImport => YarnLines.Where(l => l.IsImport).Sum(l => l.OrderQtyKg);
    private int ImportLineCount => YarnLines.Count(l => l.IsImport);
    private bool IsImportAlreadyRequested =>
        HasRequestedImport(CalculationOrderNos, SelectedOrderNo, PlacedOrders);

    public static bool HasRequestedImport(
        string? calculationOrderNos,
        string? selectedOrderNo,
        IReadOnlySet<string> requestedOrders)
    {
        var orderNos = string.IsNullOrWhiteSpace(calculationOrderNos)
            ? selectedOrderNo
            : calculationOrderNos;

        return !string.IsNullOrWhiteSpace(orderNos)
               && orderNos
                   .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                   .Any(requestedOrders.Contains);
    }

    // Orders that already have import lines in the basket (trimmed, case-insensitive) - highlighted in the list.
    private HashSet<string> OrdersInBasket =>
        new(Basket.SelectMany(b => b.OrderQty.Keys).Select(o => o.Trim()), StringComparer.OrdinalIgnoreCase);

    // ===== Column 3 roll-ups =====
    private decimal BasketOrderedKg => Basket.Sum(l => l.OrderedKg);  // user-edited order qty
    private decimal BasketNeedKg => Basket.Sum(l => l.NeedKg);        // actual need
    private int BasketOrderCount => Basket.SelectMany(l => l.Orders).Distinct(StringComparer.OrdinalIgnoreCase).Count();

    // ===== Lifecycle =====
    protected override async Task OnInitializedAsync()
    {
        if (!PermSvc.IsLoaded)
            await PermSvc.LoadPermissionsAsync();

        if (!PermSvc.CanView("yarn-orders"))
        {
            AccessDenied = true;
            return;
        }

        // Set the month BEFORE the list loads, so the deep link's month is the one queried
        // rather than today's and then re-fetched.
        if (!string.IsNullOrWhiteSpace(FromMonth)
            && DateTime.TryParse(FromMonth, CultureInfo.InvariantCulture, DateTimeStyles.None, out var linkedMonth))
            SelectedMonth = linkedMonth;

        await LoadPlacedOrdersAsync();
        await LoadOrdersAsync();

        // Arrived from a BOM task card — show that order's yarn requirement right away.
        // SelectOrderAsync calls the requirement API directly, so it works even though a
        // placed order is normally filtered out of the left-hand list.
        if (!string.IsNullOrWhiteSpace(FromOrderNo))
        {
            await SelectOrderAsync(FromOrderNo.Trim(), FromPoTaskId, FromOrderNos);
            // Deep link only. A row the user clicked is already on screen; the linked one can
            // be anywhere in a 38-order list. The scroll itself waits for OnAfterRenderAsync,
            // because the row doesn't exist in the DOM until this render lands.
            _scrollToSelected = true;
        }
    }

    private bool _scrollToSelected;

    private ElementReference _pipe;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // Resizable panels. Not rendered when access is denied, and a missing element is harmless.
            try { await JS.InvokeVoidAsync("bomSplit.init", _pipe); }
            catch (Exception) { /* no pipe on screen (access denied) or JS not ready: layout stays at defaults */ }
        }

        if (!_scrollToSelected) return;
        _scrollToSelected = false;   // cleared first, so this runs once even if the call throws
        await JS.InvokeVoidAsync("scrollElementIntoView", "bom-selected-order");
    }

    private async Task LoadOrdersAsync()
    {
        IsLoadingOrders = true;
        _loading.Show("Loading orders…");
        StateHasChanged();
        try
        {
            var response = await ThirdPartyApiManager.GetBomOrdersAsync(SelectedMonth);
            if (response.Succeeded && response.Data is not null)
                Orders = response.Data;
            else
            {
                Orders = new();
                ToastService.ShowInfo("Orders could not be loaded from PLM right now. Please try again shortly.");
            }
        }
        finally
        {
            _loading.Hide();
            IsLoadingOrders = false;
            StateHasChanged();
        }
    }

    private async Task LoadPlacedOrdersAsync()
    {
        var placed = await BomApi.GetYarnOrderedOrdersAsync();
        PlacedOrders = new HashSet<string>(placed.Select(p => p.Trim()), StringComparer.OrdinalIgnoreCase);
    }

    // ===== Column 2 — calculate =====
    private async Task SelectOrderAsync(string orderNo, int? poTaskId = null, string? orderNos = null)
    {
        SelectedOrderNo = orderNo;
        CalculationPoTaskId = poTaskId;
        CalculationOrderNos = string.IsNullOrWhiteSpace(orderNos) ? orderNo : orderNos.Trim();
        IsCalculating = true;
        CalculationError = null;
        YarnLines = new();
        StateHasChanged();

        _loading.Show("Calculating yarn requirement…");
        try
        {
            var response = await BomApi.GetYarnRequirementAsync(orderNo, flag: 1, poTaskId: CalculationPoTaskId);
            if (response.Succeeded)
                YarnLines = response.Data ?? new();
            else
                CalculationError = string.IsNullOrWhiteSpace(response.Messages)
                    ? "The BOM calculation failed without an error message."
                    : response.Messages;
        }
        finally
        {
            _loading.Hide();
            IsCalculating = false;
            StateHasChanged();
        }
    }

    private Task RecalculateAsync() =>
        string.IsNullOrEmpty(SelectedOrderNo)
            ? Task.CompletedTask
            : SelectOrderAsync(SelectedOrderNo, CalculationPoTaskId, CalculationOrderNos);

    // ===== Column 3 — basket =====
    private static string LineKey(string productId, string color) => $"{productId}|{color}".ToLowerInvariant();
    private static string LineKey(BomYarnLineDto l) => LineKey(l.ProductId, l.OrderColor);
    private static string LineKey(BasketLine l) => LineKey(l.ProductId, l.OrderColor);

    // All order numbers in this BOM calculation batch (e.g. OTO-5924, OTO-5925, OTO-5926, OTO-5927)
    private List<string> OrdersInBom()
    {
        var orders = (CalculationOrderNos ?? SelectedOrderNo ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (orders.Count == 0 && !string.IsNullOrEmpty(SelectedOrderNo)) orders.Add(SelectedOrderNo);
        return orders;
    }

    // True once every import line of the current calculation is in the basket for this batch's orders.
    // Clearing the basket or removing a line makes it false again, so the button comes back.
    private bool AreImportLinesInBasket
    {
        get
        {
            var imports = YarnLines.Where(l => l.IsImport).ToList();
            if (imports.Count == 0) return false;
            var orders = OrdersInBom();
            return imports.All(l => Basket.Any(b => LineKey(b) == LineKey(l) && orders.Any(o => b.OrderQty.ContainsKey(o))));
        }
    }

    // Instant: everything it needs (including each order's own share of a yarn) is already in YarnLines,
    // so nothing is fetched.
    private void AddImportLinesToBasket()
    {
        if (string.IsNullOrEmpty(SelectedOrderNo)) return;
        if (IsImportAlreadyRequested)
        {
            ToastService.ShowInfo($"Import already requested for {CalculationOrderNos ?? SelectedOrderNo}.");
            return;
        }
        if (AreImportLinesInBasket) return;

        var ordersInBom = OrdersInBom();

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

            // Orders that use this yarn, weighted by their own requirement (all orders, evenly, if unknown).
            var share = ordersInBom.Count == 1
                ? new Dictionary<string, decimal> { [ordersInBom[0]] = 1m }
                : line.OrderShares.Where(x => x.Value > 0 && ordersInBom.Contains(x.Key, StringComparer.OrdinalIgnoreCase))
                      .ToDictionary(x => x.Key, x => x.Value, StringComparer.OrdinalIgnoreCase);
            if (share.Count == 0) share = ordersInBom.ToDictionary(o => o, _ => 1m, StringComparer.OrdinalIgnoreCase);
            var totalShare = share.Values.Sum();
            var users = share.Keys.ToList();

            // Exact (unrounded) kg per order: the whole-kg rounding happens once, on the basket line's sum
            // (BasketLine.OrderedKg), not per order and not per add.
            foreach (var orderNo in users)
            {
                bl.OrderQty[orderNo] = line.OrderQtyKg * share[orderNo] / totalShare;
                bl.NeedQty[orderNo] = line.ImportKg * share[orderNo] / totalShare;
            }

            touched++;
        }
        if (touched > 0)
            ToastService.ShowSuccess($"Added/updated {touched} line(s) for {string.Join(", ", ordersInBom)}.");
        else
            ToastService.ShowInfo("No import lines to add.");
    }

    private void RemoveFromBasket(BasketLine line) => Basket.RemoveAll(b => LineKey(b) == LineKey(line));

    private void ClearBasket() => Basket.Clear();

    private bool IsPlacing = false;

    private async Task PlaceYarnOrderAsync()
    {
        if (Basket.Count == 0) { ShowStatus("Basket is empty.", true); return; }

        // Expand each summed cart line into per-order rows (order_no tracking).
        var lines = Basket.SelectMany(b => b.AllocatedKg().Select(o => new YarnOrderLineDto
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

        var response = await BomApi.PlaceYarnOrderAsync(new PlaceYarnOrderRequest { Lines = lines });
        var result = response.Data;

        IsPlacing = false;

        if (response.Succeeded && result is { IsSuccess: true })
        {
            var placedNos = lines.Select(l => l.OrderNo.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            ShowStatus(string.IsNullOrWhiteSpace(result.Message)
                ? $"{result.YoNo} saved — {result.TotalKg:N2} kg across {result.OrderCount} order(s)."
                : result.Message, false);
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
            ShowStatus($"Could not request yarn import: {response.Messages ?? result?.Message ?? "no response"}", true);
        }
    }

    // Messages go to the app toaster (no banner on the page).
    private void ShowStatus(string message, bool isError)
    {
        if (isError) ToastService.ShowError(message);
        else ToastService.ShowSuccess(message);
    }
}
