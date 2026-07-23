namespace NkplmErp.Shared.DTOs.Task_Gate
{
    // Body for POST api/v1/TaskGate/start.
    // TaskId travels as a string; sp_ManageTaskGate converts it with TRY_CONVERT.
    // There is deliberately no UserId here — the acting user comes from the JWT
    // in the controller, never from the caller.
    public class TaskGateRequestModel
    {
        public string? TaskId { get; set; }
    }
}
