using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace NkplmErp.Blazor.Pages;

public partial class Index : ComponentBase
{
    private readonly List<ActivityItem> MockActivities = new()
    {
        new("MFA enabled for admin@nkplm.erp", "2 mins ago", "#22c55e"),
        new("New product 'Industrial Fan' added", "15 mins ago", "#38bdf8"),
        new("Unauthorized login attempt blocked", "1 hour ago", "#ef4444"),
        new("System maintenance completed", "3 hours ago", "#f59e0b"),
        new("Database migration to MS SQL success", "5 hours ago", "#22c55e")
    };

    public record ActivityItem(string Title, string Time, string Color);
}
