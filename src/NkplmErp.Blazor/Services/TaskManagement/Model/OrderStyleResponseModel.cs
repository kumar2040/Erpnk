namespace NkplmErp.Blazor.Services.TaskManagement.Model
{
    // Mirrors the API's OrderStyleResponseModel: one (style, colour) pair for the
    // return-detail modal's Style/Color table.
    public class OrderStyleResponseModel
    {
        public string? StyleNo { get; set; }
        public string? Color { get; set; }
    }
}
