namespace NkplmErp.API.Controllers.TaskManagement.Model
{
    // One (style, colour) pair for an In Progress card's line. Returned by
    // spTaskManagement @Flag='KS' for the return-detail modal's Style/Color table.
    public class OrderStyleResponseModel
    {
        public string? StyleNo { get; set; }
        public string? Color { get; set; }
    }
}
