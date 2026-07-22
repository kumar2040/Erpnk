namespace NkplmErp.Shared.DTOs.TaskManagement
{
    // One production plan line (a "task") returned by spTaskManagement.
    // Property names match the SP's aliased columns so Dapper maps by name.
    public class TaskManagementResponseModel
    {
        public int TaskId { get; set; }

        public string? OrderNo { get; set; }
        public string? OrderType { get; set; }
        public string? ProductionType { get; set; }
        public string? FactoryType { get; set; }

        public string? Machine { get; set; }
        public int? MachineCount { get; set; }
        public string? Guage { get; set; }
        public int? Qty { get; set; }

        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }

        public string? OrderStatus { get; set; }
        public string? PlaningStatus { get; set; }
        public string? PlanWorkingStatus { get; set; }
    }
}
