using Microsoft.AspNetCore.Components;
using NkplmErp.Blazor.Pages.TaskManagement.Shared;
using NkplmErp.Blazor.Services.TaskManagement.Manager.Interface;
using NkplmErp.Blazor.Services.TaskManagement.Model;

namespace NkplmErp.Blazor.Pages.TaskManagement
{
    public partial class TaskManagement
    {
        [Inject] private ITaskManagementManager TaskManager { get; set; } = default!;

        // ---- Filter state ----
        private string selectedOption = "1";          // 1 Daily, 2 Weekly, 3 Fortnightly, 4 Monthly
        private DateTime selectedDate = DateTime.Now;
        private string selectedOrderNo = "";          // order-no search (empty = all)

        // ---- Which columns are visible (driven by the stat cards) ----
        private bool Box1 { get; set; }
        private bool Box2 { get; set; }
        private bool Box3 { get; set; }
        private bool Box4 { get; set; }
        private bool Box5 { get; set; }

        // ---- Board data (loaded from the API) ----
        private List<TaskCardItem> todotasks = new();
        private List<TaskCardItem> inprogresstasks = new();
        private List<TaskCardItem> completedtasks = new();
        private List<TaskCardItem> overduetasks = new();   // SP flag "O"
        private List<TaskCardItem> onholdtasks = new();     // no SP flag yet

        // ---- Computed counts for the stat cards ----
        private int Scheduled => todotasks.Count;
        private int InProgress => inprogresstasks.Count;
        private int Completed => completedtasks.Count;
        private int OverDue => overduetasks.Count;
        private int OnHoldCount => onholdtasks.Count;
        private int Workload => Scheduled + InProgress + Completed + OverDue;

        protected override async Task OnInitializedAsync()
        {
            // Default view: Scheduled + In Progress + Completed + Over Due (On Hold hidden).
            ShowBubble(6);
            await LoadBoardAsync();
        }

        // ======================================================================
        // Load the board columns from the API (S / P / C / O).
        // All four honour the selected date window by overlap. For Overdue the SP
        // adds a one-day grace at the window start so a task that ended just before
        // the window -- e.g. yesterday, on today's daily view -- still shows up.
        // ======================================================================
        private async Task LoadBoardAsync()
        {
            var (start, end) = GetDateRange();
            var orderNo = string.IsNullOrWhiteSpace(selectedOrderNo) ? null : selectedOrderNo.Trim();

            var scheduled = await TaskManager.GetTasksAsync("S", start, end, orderNo);
            var progress = await TaskManager.GetTasksAsync("P", start, end, orderNo);
            var completed = await TaskManager.GetTasksAsync("C", start, end, orderNo);
            var overdue = await TaskManager.GetTasksAsync("O", start, end, orderNo);

            todotasks = scheduled.Select(Map).ToList();
            inprogresstasks = progress.Select(Map).ToList();
            completedtasks = completed.Select(Map).ToList();
            overduetasks = overdue.Select(Map).ToList();

            StateHasChanged();
        }

        // Turn the period selector + selected date into a [start, end] window.
        //   1 Daily, 2 Weekly (+6d), 3 Fortnightly (+13d), 4 Monthly (whole month)
        private (DateTime start, DateTime end) GetDateRange() => selectedOption switch
        {
            "2" => (selectedDate, selectedDate.AddDays(6)),
            "3" => (selectedDate, selectedDate.AddDays(13)),
            "4" => (new DateTime(selectedDate.Year, selectedDate.Month, 1),
                    new DateTime(selectedDate.Year, selectedDate.Month, 1).AddMonths(1).AddDays(-1)),
            _ => (selectedDate, selectedDate) // Daily
        };

        // Map a production plan line onto the generic card model.
        // Knitting view: OrderNo = title, machine COUNT where the staff name used
        // to be, ProductionType as the "assignee", PlaningStatus + Qty as the badges.
        private static TaskCardItem Map(TaskManagementResponseModel r) => new()
        {
            TaskId = r.TaskId,
            TaskName = string.IsNullOrWhiteSpace(r.OrderNo) ? "(no order)" : r.OrderNo,
            OrderNo = r.OrderNo,
            Guage = r.Guage,
            MachineCount = r.MachineCount,
            Assignee = !string.IsNullOrWhiteSpace(r.ProductionType) ? r.ProductionType : r.FactoryType,
            TaskStartDate = r.StartDate ?? DateTime.Now,
            TaskEndDate = r.EndDate ?? DateTime.Now,
            RecurringTypeId = 1, // show the status flat badge
            StatusName = !string.IsNullOrWhiteSpace(r.PlaningStatus) ? r.PlaningStatus : r.OrderStatus,
            PriorityId = 0,      // knitting has no priority flag
            Qty = r.Qty
        };

        // ======================================================================
        // Stat-card toggle (mirrors the original ShowBubble behavior)
        // ======================================================================
        private void ShowBubble(int id)
        {
            Box1 = Box2 = Box3 = Box4 = Box5 = false;
            switch (id)
            {
                case 1: Box1 = true; break;
                case 2: Box2 = true; break;
                case 3: Box3 = true; break;
                case 4: Box4 = true; break;
                case 5: Box5 = true; break;
                case 6: Box1 = Box2 = Box3 = Box4 = true; break;
            }
        }

        // ======================================================================
        // Filter / date controls. Each change recomputes the window and reloads
        // the board from the API. (Staff is not an SP parameter yet, so it only
        // updates state.)
        // ======================================================================
        private async Task OnDateRangeChange(ChangeEventArgs e)
        {
            selectedOption = e.Value?.ToString() ?? "1";
            await LoadBoardAsync();
        }

        private async Task OnDateChanged(ChangeEventArgs e)
        {
            if (DateTime.TryParse(e.Value?.ToString(), out var d))
            {
                selectedDate = d;
            }
            await LoadBoardAsync();
        }

        private async Task OnOrderNoChanged(ChangeEventArgs e)
        {
            selectedOrderNo = e.Value?.ToString() ?? "";
            await LoadBoardAsync();
        }

        private async Task ChangeDate(string dir)
        {
            selectedDate = dir == "prev" ? selectedDate.AddDays(-1) : selectedDate.AddDays(1);
            await LoadBoardAsync();
        }

        private async Task RightWeekDate(string dir)
        {
            selectedDate = dir == "prev" ? selectedDate.AddDays(-7) : selectedDate.AddDays(7);
            await LoadBoardAsync();
        }

        private async Task ChangeMonth(string dir)
        {
            selectedDate = dir == "prev" ? selectedDate.AddMonths(-1) : selectedDate.AddMonths(1);
            await LoadBoardAsync();
        }

        // ======================================================================
        // Opening a card (read-only board; no edit/hold/delete/add actions).
        // ======================================================================
        private void OnItemClicked(int taskId) { }
    }
}
