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
    public static bool CanShowVendorOrdering(string? status, bool isYarn) =>
        isYarn && string.Equals(status, "Approved", StringComparison.OrdinalIgnoreCase);

    public static string NormalizeOrderStatus(string? status, bool hasVendorOrders) =>
        string.Equals(status, "Not ordered", StringComparison.OrdinalIgnoreCase)
            ? (hasVendorOrders ? "Ordered" : "Ready for Approval")
            : status?.Trim() ?? string.Empty;

    public static YarnOrderApprovalRequest CreateRequestApprovalPayload() => new()
    {
        Approve = false,
        Action = "NOTIFY"
    };

    public static string StatusAfterRequestApproval(
        bool succeeded,
        string? backendStatus,
        bool hasVendorOrders) =>
        succeeded ? "Pending Approval" : NormalizeOrderStatus(backendStatus, hasVendorOrders);

    [Inject] private BomApiClient BomApi { get; set; } = default!;
    [Inject] private PermissionService PermSvc { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;
    [Inject] private ToastService Toast { get; set; } = default!;
    [Inject] private IYarnOrderManager YarnApi { get; set; } = default!;
    [Inject] private Microsoft.AspNetCore.Components.Authorization.AuthenticationStateProvider AuthStateProvider { get; set; } = default!;

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

    private bool IsYarnControl = false;
    private bool IsYarn = false;
    private bool notifyingYarnControl;
    private bool showApprovalConfirm;
    private bool isApprovingAction;
    private bool approving;
    private string approvalNote = "";

    protected override async Task OnInitializedAsync()
    {
        if (!PermSvc.IsLoaded)
            await PermSvc.LoadPermissionsAsync();

        if (!PermSvc.CanView("yarn-orders"))
        {
            AccessDenied = true;
            return;
        }

        var authState = await AuthStateProvider.GetAuthenticationStateAsync();
        IsYarnControl = CheckIsInRole(authState.User, "YarnControl") || CheckIsInRole(authState.User, "Admin");
        IsYarn = CheckIsInRole(authState.User, "Yarn") || CheckIsInRole(authState.User, "Admin");

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

        // A legacy list procedure can still return "Not ordered". Absence of a vendor
        // order never proves YarnControl approval, so keep that legacy state pre-approval.
        Selected.Status = NormalizeOrderStatus(Selected.Status, VendorOrders.Any());
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

    // ---- "Arrived" = the yarn physically arrived from the vendor: client invoice, weight,
    // pragyapan no, and LC/TT no, all captured together and saved as one call to
    // sp_ManageYarnOrder flag 'I' (the same write path the old invoice-only save used). ----
    private Dictionary<int, string> ArrivedInvoiceEdit = new();
    private Dictionary<int, string> ArrivedWeightEdit = new();
    private Dictionary<int, string> ArrivedPragyapanEdit = new();
    private Dictionary<int, string> ArrivedLcTtEdit = new();

    private bool showArrivedModal;
    private bool showArrivedConfirm;
    private bool arrivedSaving;
    private YarnVendorOrderDto? arrivedVendor;

    private void OpenArrivedModal(YarnVendorOrderDto v)
    {
        arrivedVendor = v;
        ArrivedInvoiceEdit[v.VyoId] = v.InvoiceNo ?? "";
        ArrivedWeightEdit[v.VyoId] = v.Weight?.ToString("0.###") ?? "";
        ArrivedPragyapanEdit[v.VyoId] = v.PragyapanNo ?? "";
        ArrivedLcTtEdit[v.VyoId] = v.LcTtNo ?? "";
        showArrivedModal = true;
    }

    private void CloseArrivedModal() => showArrivedModal = false;

    // Nothing is sent until the user confirms the restated values — same "don't trust a
    // stray keystroke" rule the old invoice-save flow used.
    private void OpenArrivedConfirm()
    {
        if (arrivedVendor is null) return;
        var id = arrivedVendor.VyoId;

        if (string.IsNullOrWhiteSpace(ArrivedInvoiceEdit.GetValueOrDefault(id))
            || string.IsNullOrWhiteSpace(ArrivedWeightEdit.GetValueOrDefault(id))
            || string.IsNullOrWhiteSpace(ArrivedPragyapanEdit.GetValueOrDefault(id))
            || string.IsNullOrWhiteSpace(ArrivedLcTtEdit.GetValueOrDefault(id)))
        {
            Toast.ShowWarning("Fill in all four fields before continuing.");
            return;
        }

        showArrivedConfirm = true;
    }

    private async Task OnArrivedConfirmResult(bool confirmed)
    {
        if (confirmed)
            await SaveArrivedAsync();
        else
            showArrivedConfirm = false;
    }

    private async Task SaveArrivedAsync()
    {
        if (arrivedVendor is null) return;
        var id = arrivedVendor.VyoId;

        arrivedSaving = true;
        StateHasChanged();

        var result = await YarnApi.SaveInvoiceAsync(new YarnOrderRequestModel
        {
            YarnId = id.ToString(),
            InvoiceNo = ArrivedInvoiceEdit.GetValueOrDefault(id, "").Trim(),
            Weight = ArrivedWeightEdit.GetValueOrDefault(id, "").Trim(),
            PragyapanNo = ArrivedPragyapanEdit.GetValueOrDefault(id, "").Trim(),
            LcTtNo = ArrivedLcTtEdit.GetValueOrDefault(id, "").Trim()
        });

        arrivedSaving = false;

        if (result.Succeeded)
        {
            Toast.ShowSuccess(result.Data?.Message ?? "Arrival details saved.");
            showArrivedConfirm = false;
            showArrivedModal = false;
            await ReloadAfterInvoiceAsync();
        }
        else
        {
            Toast.ShowError(result.Messages ?? "Could not save the arrival details.");
            showArrivedConfirm = false;   // close the confirm, leave the modal open to retry
        }
        StateHasChanged();
    }

    // An arrival can move the header between Ordered and Completed, so the left list has to
    // be re-fetched too — under an Ordered filter the order it just completed should leave.
    private async Task ReloadAfterInvoiceAsync()
    {
        if (Selected is null) return;

        var yoId = Selected.YoId;
        await ReloadVendorOrdersAsync();
        await LoadOrdersAsync();

        // Re-point at the refreshed header so the strip's Status reflects the new state.
        // If the filter now excludes it, keep the detail on screen rather than blanking the
        // pane out from under the user — only its own status text goes stale, and the
        // vendor cards below it are freshly loaded.
        var refreshed = Orders.FirstOrDefault(o => o.YoId == yoId);
        if (refreshed is not null) Selected = refreshed;
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

    private void OpenApprovalModal(bool accept)
    {
        isApprovingAction = accept;
        approvalNote = "";
        showApprovalConfirm = true;
    }

    private async Task OnApprovalConfirmResult(bool confirmed)
    {
        if (confirmed)
            await ConfirmApprovalAsync();
        else
            showApprovalConfirm = false;
    }

    private async Task ConfirmApprovalAsync()
    {
        if (Selected is null) return;
        var selectedYoId = Selected.YoId;

        approving = true;
        StateHasChanged();

        var result = await BomApi.ApproveYarnOrderAsync(selectedYoId, new YarnOrderApprovalRequest
        {
            Approve = isApprovingAction,
            Note = string.IsNullOrWhiteSpace(approvalNote) ? null : approvalNote.Trim()
        });

        approving = false;

        if (result is { IsSuccess: true })
        {
            Toast.ShowSuccess(result.Message);
            showApprovalConfirm = false;
            if (Selected != null)
            {
                Selected.Status = isApprovingAction ? "Approved" : "Rejected";
            }
            await LoadOrdersAsync();
            var updated = Orders.FirstOrDefault(o => o.YoId == selectedYoId);
            if (updated is not null)
            {
                if (isApprovingAction && (updated.Status == "Not ordered" || updated.Status == "Pending Approval"))
                    updated.Status = "Approved";
                else if (!isApprovingAction)
                    updated.Status = "Rejected";

                Selected = updated;
            }
            if (Selected != null)
            {
                Detail = await BomApi.GetYarnOrderDetailAsync(Selected.YoId);
                VendorOrders = await BomApi.GetYarnVendorOrdersAsync(Selected.YoId);
            }
        }
        else
        {
            Toast.ShowError(result?.Message ?? "Approval action failed.");
            showApprovalConfirm = false;
        }
        StateHasChanged();
    }

    private async Task NotifyYarnControlAsync()
    {
        if (Selected is null) return;
        notifyingYarnControl = true;
        StateHasChanged();

        var selectedYoId = Selected.YoId;
        var result = await BomApi.ApproveYarnOrderAsync(
            selectedYoId,
            CreateRequestApprovalPayload());

        notifyingYarnControl = false;

        if (result is { IsSuccess: true })
        {
            Toast.ShowSuccess(result.Message);
            Selected.Status = "Pending Approval";
            await LoadOrdersAsync();
            var updated = Orders.FirstOrDefault(o => o.YoId == selectedYoId);
            if (updated is not null)
            {
                updated.Status = StatusAfterRequestApproval(true, updated.Status, VendorOrders.Any());
                Selected = updated;
            }
        }
        else
        {
            Toast.ShowError(result?.Message ?? "Could not send notification to YarnControl.");
        }
        StateHasChanged();
    }

    private static bool CheckIsInRole(System.Security.Claims.ClaimsPrincipal user, string roleName)
    {
        if (user?.Identity?.IsAuthenticated != true) return false;
        return user.IsInRole(roleName)
            || user.HasClaim(c => (string.Equals(c.Type, "role", StringComparison.OrdinalIgnoreCase)
                                || string.Equals(c.Type, System.Security.Claims.ClaimTypes.Role, StringComparison.OrdinalIgnoreCase)
                                || c.Type.EndsWith("/role", StringComparison.OrdinalIgnoreCase))
                               && string.Equals(c.Value, roleName, StringComparison.OrdinalIgnoreCase));
    }
}
