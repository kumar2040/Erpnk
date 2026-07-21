namespace NkplmErp.API.Model.TaskManagement
{
    // One size row (style, colour, size, qty) for an In Progress card's line. Returned by
    // spTaskManagement @Flag='KS' for the return-detail modal's items table.
    public class OrderStyleResponseModel
    {
        public string? StyleNo { get; set; }
        public string? Color { get; set; }
        public string? Size { get; set; }
        public decimal Qty { get; set; }
    }
}
