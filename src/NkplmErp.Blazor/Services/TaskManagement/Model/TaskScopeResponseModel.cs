namespace NkplmErp.Blazor.Services.TaskManagement.Model
{
    // Mirrors the API's TaskScopeResponseModel. Drives the board's factory dropdown:
    //   IsRestricted = false -> admin: FactoryTypes lists every factory_type (editable select).
    //   IsRestricted = true  -> user locked to AssignedGauge; FactoryTypes holds only it (fixed).
    public class TaskScopeResponseModel
    {
        public bool IsRestricted { get; set; }
        public string? AssignedGauge { get; set; }
        public List<string> FactoryTypes { get; set; } = new();
    }
}
