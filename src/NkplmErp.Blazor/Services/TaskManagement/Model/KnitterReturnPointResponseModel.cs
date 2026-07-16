namespace NkplmErp.Blazor.Services.TaskManagement.Model
{
    // Mirrors the API's KnitterReturnPointResponseModel: one point of a knitter's
    // return series (pieces received at a date+time) for the return-detail modal chart.
    public class KnitterReturnPointResponseModel
    {
        public DateTime ReturnAt { get; set; }
        public int ReturnCount { get; set; }
    }
}
