using Microsoft.AspNetCore.Components;

namespace NkplmErp.Blazor.Pages.TaskManagement.Shared
{
    public partial class TaskCard : ComponentBase
    {
        // --- Core parameters ---
        // 1. Title section
        [Parameter] public string Title { get; set; } = "Tasks";

        // 2. Data the card renders
        [Parameter] public List<TaskCardItem>? Items { get; set; }

        // 3. Drives the per-column look (colors, header icon, status badge)
        [Parameter] public TaskVariant Variant { get; set; } = TaskVariant.Todo;

        // Opening a card (read-only board; no edit/hold/delete/add actions).
        [Parameter] public EventCallback<int> OnItemClick { get; set; }

        private int Count => Items?.Count ?? 0;

        // data-status attribute used by the drag/drop script (1..5)
        private int DataStatus => Variant switch
        {
            TaskVariant.Todo => 1,
            TaskVariant.Progress => 2,
            TaskVariant.Complete => 3,
            TaskVariant.Due => 4,
            TaskVariant.OnHold => 5,
            _ => 1
        };

        private string BoxClass => Variant switch
        {
            TaskVariant.Todo => "TodoBox",
            TaskVariant.Progress => "ProgressBox",
            TaskVariant.Complete => "CompleteBox",
            TaskVariant.Due => "DueBox",
            TaskVariant.OnHold => "OnHoldBox",
            _ => "TodoBox"
        };

        // Over Due / On Hold use a distinct header: title-only pill + a separate
        // circular count badge (matches the original boxHead dueHead/onholdHead layout).
        private bool UsesCountBadge => Variant is TaskVariant.Due or TaskVariant.OnHold;

        private string HeadClass => Variant switch
        {
            TaskVariant.Due => "boxHead dueHead",
            TaskVariant.OnHold => "boxHead onholdHead",
            _ => "boxHead"
        };

        // matches the .flat / .flat.progress / .flat.complete / ... CSS variants
        private string FlatClass => Variant switch
        {
            TaskVariant.Progress => "flat progress",
            TaskVariant.Complete => "flat complete",
            TaskVariant.Due => "flat overdue",
            TaskVariant.OnHold => "flat onhold",
            _ => "flat"
        };

        private static string PriorityClass(int priorityId) => priorityId switch
        {
            3 => "stat statRed",
            2 => "stat Warn",
            _ => "stat statGreen"
        };

        // Header icon per column (inline SVG path, no icon font needed).
        private string HeaderIconPath => Variant switch
        {
            // Scheduled - paper plane / send
            TaskVariant.Todo => "M2.01 21L23 12 2.01 3 2 10l15 2-15 2z",
            // In progress - clock
            TaskVariant.Progress => "M11.99 2C6.47 2 2 6.48 2 12s4.47 10 9.99 10C17.52 22 22 17.52 22 12S17.52 2 11.99 2zM12 20c-4.42 0-8-3.58-8-8s3.58-8 8-8 8 3.58 8 8-3.58 8-8 8zm.5-13H11v6l5.25 3.15.75-1.23-4.5-2.67z",
            // Completed - check circle
            TaskVariant.Complete => "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z",
            // Over due - event busy
            TaskVariant.Due => "M9.31 17l2.44-2.44L14.19 17l1.06-1.06-2.44-2.44 2.44-2.44L14.19 10l-2.44 2.44L9.31 10l-1.06 1.06 2.44 2.44-2.44 2.44L9.31 17zM19 3h-1V1h-2v2H8V1H6v2H5c-1.11 0-1.99.9-1.99 2L3 19c0 1.1.89 2 2 2h14c1.1 0 2-.9 2-2V5c0-1.1-.9-2-2-2zm0 16H5V8h14v11z",
            // On hold - play circle
            TaskVariant.OnHold => "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 14.5v-9l6 4.5-6 4.5z",
            _ => "M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"
        };
    }
}
