namespace NkplmErp.Blazor.Services.TaskManagement.Model
{
    // Mirrors the API's OrderStyleResponseModel: one size row (style, colour, size, qty)
    // for the return-detail modal's items table.
    public class OrderStyleResponseModel
    {
        public string? StyleNo { get; set; }
        public string? Color { get; set; }
        public string? Size { get; set; }
        public decimal Qty { get; set; }
    }
}
