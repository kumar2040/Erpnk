namespace NkplmErp.Blazor.Services.Task_Gate.Manager.Route
{
    // Central place for the TaskGate API routes.
    public static class TaskGateEndpoint
    {
        public const string Base = "api/v1/TaskGate";

        // The caller's not-yet-started assignments, oldest first (GET).
        public const string Queue = Base + "/queue";

        // Accept one task: Scheduled -> In progress on the caller's own row (POST).
        public const string Start = Base + "/start";
    }
}
