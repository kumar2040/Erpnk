namespace NkplmErp.Blazor.Pages.TaskManagement.Shared
{
    // Drives the per-column colors, header icon and status badge of a TaskCard.
    public enum TaskVariant
    {
        Todo,     // Scheduled
        Progress, // In Progress
        Complete, // Completed
        Due,      // Over Due
        OnHold    // On Hold
    }
}
