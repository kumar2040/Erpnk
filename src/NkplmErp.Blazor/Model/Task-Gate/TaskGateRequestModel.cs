namespace NkplmErp.Blazor.Model.Task_Gate
{
    // Client-side twin of the API's TaskGateRequestModel. Blazor has no project
    // reference to NkplmErp.API, so each side keeps its own copy of the shape.
    public class TaskGateRequestModel
    {
        public string? TaskId { get; set; }
    }
}
