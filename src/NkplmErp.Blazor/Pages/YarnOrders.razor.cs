using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using NkplmErp.Blazor.Services.Bom;
using NkplmErp.Blazor.Services.RoleManagement;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Pages;

public partial class YarnOrders
{
    [Inject] private BomApiClient BomApi { get; set; } = default!;
    [Inject] private PermissionService PermSvc { get; set; } = default!;
    [Inject] private IJSRuntime JS { get; set; } = default!;

    private bool AccessDenied = false;

    private List<YarnOrderHeaderDto> Orders = new();
    private string Search = "";
    private bool IsLoadingList = false;

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

        if (!PermSvc.CanView("Bom"))
        {
            AccessDenied = true;
            return;
        }

        await LoadOrdersAsync();
    }

    private async Task LoadOrdersAsync()
    {
        IsLoadingList = true;
        StateHasChanged();
        Orders = await BomApi.GetYarnOrdersAsync();
        IsLoadingList = false;
        StateHasChanged();
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
        if (!DateTime.TryParse(DepartureEdit.GetValueOrDefault(v.VyoId), out var d)) return;
        if (await BomApi.SetDepartureAsync(v.VyoId, d))
        {
            StatusMessage = $"Departure {d:dd MMM yyyy} saved for {v.VyoNo}. Task created.";
            IsError = false;
            await ReloadVendorOrdersAsync();
        }
    }

    private async Task SaveArrivalAsync(YarnVendorOrderDto v)
    {
        if (!DateTime.TryParse(ArrivalEdit.GetValueOrDefault(v.VyoId), out var d)) return;
        if (await BomApi.SetArrivalAsync(v.VyoId, d))
        {
            StatusMessage = $"Arrival {d:dd MMM yyyy} saved for {v.VyoNo}. Task created.";
            IsError = false;
            await ReloadVendorOrdersAsync();
        }
    }

    private async Task DropColorAsync(YarnVendorOrderDto v)
    {
        var color = await JS.InvokeAsync<string>("prompt", $"Color the vendor dropped on {v.VyoNo}:");
        if (string.IsNullOrWhiteSpace(color)) return;
        if (await BomApi.DropColorAsync(v.VyoId, color.Trim()))
        {
            StatusMessage = $"Dropped color '{color.Trim()}' flagged on {v.VyoNo}. Yarn-issue task raised.";
            IsError = false;
            StateHasChanged();
        }
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
