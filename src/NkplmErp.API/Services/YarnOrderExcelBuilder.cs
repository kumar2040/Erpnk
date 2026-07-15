using ClosedXML.Excel;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.API.Services;

/// <summary>
/// Builds a purchase-order style .xlsx for a yarn vendor sub-order,
/// ready to email to the supplier.
/// </summary>
public static class YarnOrderExcelBuilder
{
    public static byte[] Build(YarnVendorOrderExport export)
    {
        var h = export.Header;
        using var wb = new XLWorkbook();
        var ws = wb.AddWorksheet("Yarn Order");

        var navy = XLColor.FromHtml("#1a3353");
        var headBg = XLColor.FromHtml("#1a3353");

        // ---- Title ----
        ws.Cell("A1").Value = "NATUREKNIT — YARN PURCHASE ORDER";
        ws.Range("A1:E1").Merge();
        ws.Cell("A1").Style.Font.Bold = true;
        ws.Cell("A1").Style.Font.FontSize = 14;
        ws.Cell("A1").Style.Font.FontColor = navy;

        // ---- Header block ----
        var r = 3;
        void Meta(string k, string v)
        {
            ws.Cell(r, 1).Value = k;
            ws.Cell(r, 1).Style.Font.Bold = true;
            ws.Cell(r, 2).Value = v;
            r++;
        }
        Meta("Order No:", h?.VyoNo ?? "");
        Meta("Vendor:", h?.Vendor ?? "—");
        Meta("Date:", h?.CreatedDate.ToString("dd MMM yyyy") ?? "");
        Meta("Status:", h?.Status ?? "");
        if (h?.DepartureDate is { } dep) Meta("Departure:", dep.ToString("dd MMM yyyy"));
        if (h?.ArrivalDate is { } arr) Meta("Arrival:", arr.ToString("dd MMM yyyy"));

        // ---- Lines table ---- (aggregated per yarn × color)
        r += 1;
        var headerRow = r;
        string[] cols = { "S.N", "Yarn", "Color", "Qty (kg)" };
        for (int c = 0; c < cols.Length; c++)
        {
            var cell = ws.Cell(headerRow, c + 1);
            cell.Value = cols[c];
            cell.Style.Fill.BackgroundColor = headBg;
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Font.Bold = true;
        }

        var lines = export.Lines
            .GroupBy(l => new { l.ProductId, l.Color })
            .Select(g => new
            {
                Yarn = string.IsNullOrWhiteSpace(g.First().YarnName) ? g.First().ProductId : g.First().YarnName,
                g.First().Color,
                Qty = g.Sum(x => x.ImportKg)
            })
            .OrderBy(x => x.Yarn).ThenBy(x => x.Color)
            .ToList();

        r = headerRow + 1;
        int sn = 1;
        foreach (var l in lines)
        {
            ws.Cell(r, 1).Value = sn++;
            ws.Cell(r, 2).Value = l.Yarn;
            ws.Cell(r, 3).Value = l.Color;
            ws.Cell(r, 4).Value = l.Qty;
            ws.Cell(r, 4).Style.NumberFormat.Format = "0.00";
            r++;
        }

        // ---- Total ----
        ws.Cell(r, 3).Value = "TOTAL";
        ws.Cell(r, 3).Style.Font.Bold = true;
        ws.Cell(r, 4).Value = lines.Sum(x => x.Qty);
        ws.Cell(r, 4).Style.NumberFormat.Format = "0.00";
        ws.Cell(r, 4).Style.Font.Bold = true;

        // Borders around the table
        ws.Range(headerRow, 1, r, 4).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
        ws.Range(headerRow, 1, r, 4).Style.Border.InsideBorder = XLBorderStyleValues.Thin;

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
