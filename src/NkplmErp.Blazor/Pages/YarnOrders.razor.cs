using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NkplmErp.Blazor.Services.Bom;
using NkplmErp.Blazor.Services.RoleManagement;
using NkplmErp.Blazor.Services.Toast;
using NkplmErp.Shared.DTOs.Yarn_Orders;
using NkplmErp.Blazor.Services.Yarn_Orders.Manager.Interface;
using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.DTOs.Dropdown;

namespace NkplmErp.Blazor.Pages;

public partial class YarnOrders
{
    [Inject] private BomApiClient BomApi { get; set; } = default!;
    [Inject] private PermissionService PermSvc { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;
    [Inject] private IYarnOrderManager YarnApi { get; set; } = default!;

    /// <summary>Route id from /yarn-orders/{YoId} — set when a BOM task card links here.</summary>
    [Parameter] public int? YoId { get; set; }

    private bool AccessDenied = false;

    private List<YarnOrderHeaderDto> Orders = new();
    private string Search = "";
    private bool IsLoadingList = false;

    // Order-state filter: 'O' = ordered, 'N' = not ordered from spDropdown
    // 'YarnOrderStatus', or DropdownValues.All ("-1") for no filter. Applied by
    // sp_GetYarnOrders rather than here, so the "ordered" rule lives in one place
    // next to the data.
    private string StatusFilter = DropdownValues.All;

    private YarnOrderHeaderDto? Selected;
    private List<YarnOrderDetailLineDto> Detail = new();
    private bool IsLoadingDetail = false;

    // Vendor sub-orders placed under the selected parent.
    private List<YarnVendorOrderDto> VendorOrders = new();
    private string? PlacingVendor = null;

    // Per-vendor-order date edit buffers (yyyy-MM-dd).
    private Dictionary<int, string> DepartureEdit = new();
    private Dictionary<int, string> ArrivalEdit = new();
    private string StatusMessage = "";
    private bool IsError = false;

    private const string Unassigned = "— No vendor —";

    // Detail lines grouped by vendor (for placing sub-orders).
    private IEnumerable<IGrouping<string, YarnOrderDetailLineDto>> VendorGroups =>
        Detail.GroupBy(d => string.IsNullOrWhiteSpace(d.Vendor) ? Unassigned : d.Vendor.Trim());

    private bool VendorPlaced(string vendor) =>
        VendorOrders.Any(v => string.Equals(v.Vendor?.Trim(), vendor, StringComparison.OrdinalIgnoreCase));

    private IEnumerable<YarnOrderHeaderDto> FilteredOrders =>
        string.IsNullOrWhiteSpace(Search)
            ? Orders
            : Orders.Where(o => o.YoNo.Contains(Search, StringComparison.OrdinalIgnoreCase));

    private decimal DetailOrderKg => Detail
        .GroupBy(d => $"{d.ProductId}|{d.Color}".ToLowerInvariant())
        .Sum(g => System.Math.Ceiling(g.Sum(x => x.ImportKg)));

    protected override async Task OnInitializedAsync()
    {
        if (!PermSvc.IsLoaded)
            await PermSvc.LoadPermissionsAsync();

        if (!PermSvc.CanView("yarn-orders"))
        {
            AccessDenied = true;
            return;
        }

        await LoadOrdersAsync();

        // Arrived from a BOM task card (/yarn-orders/{yo_id}) — open that order right away,
        // using the same selection path a click on the list would take. An unknown id (order
        // deleted, or the task predates it) just leaves the page on "nothing selected".
        if (YoId is > 0)
        {
            var target = Orders.FirstOrDefault(o => o.YoId == YoId.Value);
            if (target is not null)
            {
                await SelectOrderAsync(target);
                // Deep link only — a card the user clicked is already on screen, the linked
                // one can be anywhere down the list. Deferred to OnAfterRenderAsync because
                // the card isn't in the DOM until this render lands.
                _scrollToSelected = true;
            }
        }
    }

    private bool _scrollToSelected;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_scrollToSelected) return;
        _scrollToSelected = false;   // cleared first, so this runs once even if the call throws
        await JS.InvokeVoidAsync("scrollElementIntoView", "yo-selected-order");
    }

    private async Task LoadOrdersAsync()
    {
        IsLoadingList = true;
        StateHasChanged();
        // "-1" is the All row, not a status. Sending it would reach @Status CHAR(1)
        // as "-" and match nothing, so a leading row goes down as null instead.
        Orders = await BomApi.GetYarnOrdersAsync(
            DropdownValues.IsPlaceholder(StatusFilter) ? null : StatusFilter);
        IsLoadingList = false;
        StateHasChanged();
    }

    private async Task OnStatusFilterChangedAsync(string status)
    {
        var next = status ?? "";

        // The dropdown also raises this when it settles its own default on load.
        // Without this guard that would re-fetch the list we just fetched.
        if (next == StatusFilter) return;

        StatusFilter = next;

        // The selected order can drop out of the filtered list, which would leave
        // the detail pane showing a card no longer on screen.
        Selected = null;
        Detail = new();
        VendorOrders = new();

        await LoadOrdersAsync();
    }

    private async Task SelectOrderAsync(YarnOrderHeaderDto o)
    {
        Selected = o;
        IsLoadingDetail = true;
        Detail = new();
        VendorOrders = new();
        StatusMessage = "";
        StateHasChanged();

        Detail = await BomApi.GetYarnOrderDetailAsync(o.YoId);
        VendorOrders = await BomApi.GetYarnVendorOrdersAsync(o.YoId);
        SeedDateEdits();

        IsLoadingDetail = false;
        StateHasChanged();
    }

    private void SeedDateEdits()
    {
        DepartureEdit = VendorOrders.ToDictionary(v => v.VyoId, v => v.DepartureDate?.ToString("yyyy-MM-dd") ?? "");
        ArrivalEdit = VendorOrders.ToDictionary(v => v.VyoId, v => v.ArrivalDate?.ToString("yyyy-MM-dd") ?? "");
    }

    private async Task SaveDepartureAsync(YarnVendorOrderDto v)
    {
        var departure = DepartureEdit.GetValueOrDefault(v.VyoId);

        if (string.IsNullOrWhiteSpace(departure))
        {
            Toast.ShowWarning("Departure date can't be empty.");
            return;
        }

        await SaveTimelineAsync(v, departure, null);
    }

    private async Task SaveArrivalAsync(YarnVendorOrderDto v)
    {
        var arrival = ArrivalEdit.GetValueOrDefault(v.VyoId);

        if (string.IsNullOrWhiteSpace(arrival))
        {
            Toast.ShowWarning("Arrival date can't be empty.");
            return;
        }

        await SaveTimelineAsync(v, null, arrival);
    }

    // Dates go over as raw strings; sp_ManageYarnOrder flag 'T' converts them and
    // decides the outcome, so the toast text is the procedure's own message. Passing
    // null for the other date leaves that column untouched.
    private async Task SaveTimelineAsync(YarnVendorOrderDto v, string? departureDate, string? arrivalDate)
    {
        var request = new YarnOrderRequestModel
        {
            YarnId        = v.VyoId.ToString(),
            DepartureDate = departureDate,
            ArrivalDate   = arrivalDate
        };

        var result = await YarnApi.UpdateYarnOrderAsync(request);

        if (result.Succeeded)
        {
            Toast.ShowSuccess(result.Data?.Message ?? "Date saved.");
            await ReloadVendorOrdersAsync();
        }
        else
        {
            Toast.ShowError(result.Messages ?? "Could not save the date.");
        }
    }

    // ---- Drop-color modal (multi-select). Server persistence is deferred for now: the
    // endpoint just returns Succeeded=true, so this only drives the UI + a status message. ----
    private bool showDropModal;
    private bool showDropConfirm;
    private YarnVendorOrderDto? dropVendor;
    private List<DropColorItem> dropItems = new();
    private readonly HashSet<string> dropSelected = new(StringComparer.OrdinalIgnoreCase);
    private string dropNote = "";
    private bool dropping;

    private sealed record DropColorItem(string Color, string Yarn, string? Ply, decimal Qty);

    // Open the modal for a placed vendor order, listing its distinct colors — sourced from the
    // parent order's detail lines for this vendor (the same lines already shown under the card).
    private void OpenDropModal(YarnVendorOrderDto v)
    {
        dropVendor = v;
        dropNote = "";
        dropSelected.Clear();
        dropItems = Detail
            .Where(d => !string.IsNullOrWhiteSpace(d.Color)
                     && string.Equals(d.Vendor?.Trim(), v.Vendor?.Trim(), StringComparison.OrdinalIgnoreCase))
            .GroupBy(d => d.Color.Trim())
            .Select(g => new DropColorItem(g.Key, g.First().Display, g.First().Ply, g.Sum(x => x.ImportKg)))
            .OrderBy(i => i.Color)
            .ToList();
        StatusMessage = "";
        showDropModal = true;
    }

    private void ToggleDrop(string color, bool on)
    {
        if (on) dropSelected.Add(color);
        else dropSelected.Remove(color);
    }

    private void CloseDropModal() => showDropModal = false;

    // "Drop selected" → open the shared confirm step (nothing is sent until the user confirms).
    private void OpenDropConfirm()
    {
        if (dropSelected.Count == 0) return;
        showDropConfirm = true;
    }

    // ConfirmModal result: true = Yes (proceed with the drop), false = Cancel (just close it).
    private async Task OnDropConfirmResult(bool confirmed)
    {
        if (confirmed)
            await ConfirmDropAsync();
        else
            showDropConfirm = false;
    }

    private async Task ConfirmDropAsync()
    {
        if (dropVendor is null || dropSelected.Count == 0) return;

        dropping = true;
        StateHasChanged();

        var result = await BomApi.DropColorsAsync(dropVendor.VyoId, new DropColorRequest
        {
            Colors = dropSelected.ToList(),
            Note = string.IsNullOrWhiteSpace(dropNote) ? null : dropNote.Trim()
        });

        dropping = false;

        if (result is { Succeeded: true })
        {
            // Success feedback is a toast (the proc reports how many lines were flagged).
            Toast.ShowSuccess(!string.IsNullOrWhiteSpace(result.Message)
                ? result.Message
                : $"{dropSelected.Count} color(s) dropped on {dropVendor.VyoNo}.");
            showDropConfirm = false;
            showDropModal = false;

            // Reload the detail — sp_GetYarnOrderDetail no longer fetches dropped colors,
            // so they disappear from the vendor group (and any future drop modal) live.
            if (Selected is not null)
                Detail = await BomApi.GetYarnOrderDetailAsync(Selected.YoId);
        }
        else
        {
            Toast.ShowError(!string.IsNullOrWhiteSpace(result?.Message) ? result!.Message : "Could not flag the dropped colors.");
            showDropConfirm = false;   // close the confirm, keep the picker open to retry
        }
        StateHasChanged();
    }

    private async Task ReloadVendorOrdersAsync()
    {
        if (Selected == null) return;
        VendorOrders = await BomApi.GetYarnVendorOrdersAsync(Selected.YoId);
        SeedDateEdits();
        StateHasChanged();
    }

    private async Task PlaceVendorOrderAsync(string vendor, IEnumerable<YarnOrderDetailLineDto> lines)
    {
        if (Selected == null) return;

        var request = new SaveYarnVendorOrderRequest
        {
            YoId = Selected.YoId,
            Vendor = vendor,
            Lines = lines.Select(d => new YarnVendorOrderLineDto
            {
                ProductId = d.ProductId,
                YarnName = d.YarnName,
                Color = d.Color,
                Ply = d.Ply,
                OrderNo = d.OrderNo,
                ImportKg = d.ImportKg
            }).ToList()
        };

        PlacingVendor = vendor;
        StateHasChanged();

        var result = await BomApi.PlaceYarnVendorOrderAsync(Selected.YoId, request);

        PlacingVendor = null;

        if (result is { IsSuccess: true })
        {
            StatusMessage = $"{result.VyoNo} placed for {vendor} — {request.Lines.Count} line(s), {result.TotalKg:N2} kg.";
            IsError = false;
            VendorOrders = await BomApi.GetYarnVendorOrdersAsync(Selected.YoId);
        }
        else
        {
            StatusMessage = $"Could not place vendor order: {result?.Message ?? "no response"}";
            IsError = true;
        }
        StateHasChanged();
    }

    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private async Task DownloadVendorExcelAsync(YarnVendorOrderDto v)
    {
        var file = await BomApi.DownloadVendorOrderExcelAsync(v.VyoId);
        if (file is null)
        {
            StatusMessage = $"Could not generate Excel for {v.VyoNo}.";
            IsError = true;
            StateHasChanged();
            return;
        }
        var base64 = Convert.ToBase64String(file.Value.bytes);
        await JS.InvokeVoidAsync("bomDownloadFile", file.Value.fileName, base64, XlsxContentType);
    }
}
