namespace NkplmErp.API.Controllers.TaskManagement.Model
{
    // Describes the factory_type scope the current user is allowed to see on the board.
    //   IsRestricted = false -> admin / unrestricted: FactoryTypes lists every factory_type
    //                           in MasterPlanDetail; the user may pick any (or all).
    //   IsRestricted = true  -> the user is locked to AssignedGauge; FactoryTypes holds only it.
    public class TaskScopeResponseModel
    {
        public bool IsRestricted { get; set; }
        public string? AssignedGauge { get; set; }
        public List<string> FactoryTypes { get; set; } = new();
    }
}
