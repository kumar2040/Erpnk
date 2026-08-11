using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.Wrapper;

namespace NkplmErp.Application.Interfaces;

/// <summary>
/// Bill of Materials — yarn requirement calculation. Compares the yarn a
/// new order needs (plus open-order backlog) against main-store stock to
/// decide whether yarn must be imported from a supplier.
/// </summary>
public interface IBomService
{
    /// <summary>
    /// Yarn requirement per yarn × color for an order.
    /// Flag 1 = import-decision rows for this order (qty &gt; 0),
    /// Flag 2 = full picture incl. backlog-only yarns.
    /// </summary>
    Task<IResponse<List<BomYarnLineDto>>> GetYarnRequirementAsync(string? orderNo, int flag = 1, int? poTaskId = null);

    /// <summary>
    /// Save a yarn order (one header + per-order detail rows) and return the
    /// generated reference, e.g. "Natureknit Yarn-001".
    /// </summary>
    Task<PlaceYarnOrderResult> PlaceYarnOrderAsync(PlaceYarnOrderRequest request, string? createdBy);

    /// <summary>All saved yarn orders (headers), newest first.</summary>
    Task<List<YarnOrderHeaderDto>> GetYarnOrdersAsync(string? status = null);

    /// <summary>Detail lines of a saved yarn order.</summary>
    Task<List<YarnOrderDetailLineDto>> GetYarnOrderDetailAsync(int yoId);

    /// <summary>Production order numbers that already have a yarn order placed.</summary>
    Task<List<string>> GetYarnOrderedOrdersAsync();

    /// <summary>Place a vendor sub-order under a parent yarn order.</summary>
    Task<SaveYarnVendorOrderResult> PlaceYarnVendorOrderAsync(SaveYarnVendorOrderRequest request, string? createdBy);

    /// <summary>Vendor sub-orders already placed under a parent yarn order.</summary>
    Task<List<YarnVendorOrderDto>> GetYarnVendorOrdersAsync(int yoId);

    /// <summary>One vendor sub-order with its lines (for the Excel PO export).</summary>
    Task<YarnVendorOrderExport> GetYarnVendorOrderAsync(int vyoId);

    /// <summary>Set a vendor sub-order's departure or arrival date. Kind = "departure" | "arrival".</summary>
    Task<bool> SetYarnVendorOrderDateAsync(int vyoId, string kind, DateTime date);

    /// <summary>
    /// Flag colors as dropped by the vendor on a vendor sub-order (sp_ManageYarnOrder flag 'D').
    /// Sets is_dropped/drop_date/drop_by/drop_note on the parent detail lines, queues outbox
    /// mails in tblMailLog and writes in-app PoTaskNotification rows — all in one transaction.
    /// </summary>
    Task<DropColorResult> DropYarnColorsAsync(int vyoId, List<string> colors, string? note, string? droppedBy);
}
