using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using NkplmErp.Blazor.Pages.TaskManagement.Shared;
using NkplmErp.Blazor.Components;
using NkplmErp.Blazor.Services.TaskManagement.Manager.Interface;
using NkplmErp.Blazor.Services.TaskManagement.Model;

namespace NkplmErp.Blazor.Pages.TaskManagement
{
    public partial class TaskManagement
    {
        [Inject] private ITaskManagementManager TaskManager { get; set; } = default!;

        // ---- Filter state ----
        // Selected date window (the CompactDateRangeFilter binds these). Seeded in
        // OnInitializedAsync to today/today — the same window as the old Daily/today default.
        private DateTime? selectedStartDate;
        private DateTime? selectedEndDate;
        private string selectedOrderNo = "";          // order-no search (empty = all)

        // ---- Factory / gauge scope ----
        // scope tells us whether the user is admin (editable factory dropdown) or
        // gauge-restricted (locked to one factory_type). selectedFactoryType is the
        // current dropdown value (null/"" = all factories; only admins can change it).
        private TaskScopeResponseModel? scope;
        private string? selectedFactoryType;

        // ---- Cascading sub-category (gauge method) filter ----
        // availableSubCategories: options for the ACTIVE factory (empty when no single
        // factory is active, e.g. an admin on "All Factories"). selectedSubCategories:
        // the checked options (empty = "All" = no sub-filter). ActiveFactory is the
        // factory the sub-options cascade from (a restricted user's gauge, else the pick).
        private List<string> availableSubCategories = new();
        private readonly HashSet<string> selectedSubCategories = new(StringComparer.OrdinalIgnoreCase);
        private string? ActiveFactory =>
            scope?.IsRestricted == true ? scope.AssignedGauge
            : (string.IsNullOrWhiteSpace(selectedFactoryType) ? null : selectedFactoryType);

        // Monotonic token so a slow in-flight board load can't overwrite a newer one.
        // Every LoadBoardAsync bumps it; results are applied only if still the latest.
        private int _loadSeq;

        // ---- Which columns are visible (driven by the stat cards) ----
        private bool Box1 { get; set; }
        private bool Box2 { get; set; }
        private bool Box3 { get; set; }
        private bool Box4 { get; set; }
        private bool Box5 { get; set; }

        // Which stat card is currently selected (mirrors the ShowBubble id) so the UI can
        // highlight it. Starts at 6 (Work Load) to match the initial ShowBubble(6) default.
        private int activeBubble = 6;

        // How many board columns are visible (driven by the stat-card selection): a single
        // category shows one column, Work Load shows four. When only one is showing it takes
        // the full row and flows its cards into a grid; Work Load uses a 4-up column grid
        // (2-up on tablets) on laptop and above.
        private int VisibleBoxCount =>
            (Box1 ? 1 : 0) + (Box2 ? 1 : 0) + (Box3 ? 1 : 0) + (Box4 ? 1 : 0) + (Box5 ? 1 : 0);
        private bool SingleFullWidth => VisibleBoxCount == 1;
        private string BoardColClass => SingleFullWidth ? "col-12" : "col-md-6 col-lg-3";

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

            // Default window = today/today (same as the old Daily/today default; equals the
            // date picker's "Today" preset).
            selectedStartDate = DateTime.Today;
            selectedEndDate = DateTime.Today;

            // Resolve the user's factory scope. A gauge-restricted user is pinned to their
            // own factory_type; an admin starts on "all factories" (null) and may change it.
            scope = await TaskManager.GetScopeAsync();
            if (scope.IsRestricted)
                selectedFactoryType = scope.AssignedGauge;

            await LoadBoardAsync();   // also (re)loads the cascading sub-category options
        }

        // ======================================================================
        // Load the board columns from the API (S / P / C / O).
        // All four honour the selected date window by overlap. For Overdue the SP
        // adds a one-day grace at the window start so a task that ended just before
        // the window -- e.g. yesterday, on today's daily view -- still shows up.
        // ======================================================================
        private async Task LoadBoardAsync()
        {
            var token = ++_loadSeq;   // newest load wins; a stale in-flight load is discarded below

            var start = selectedStartDate;
            var end = selectedEndDate;

            // Refresh the cascading sub-category options for the active factory + date window
            // (numeric -> "general", tailor code -> name). A factory change clears the selection
            // beforehand; here we just prune anything no longer available in the new window so
            // the checkboxes stay consistent. ActiveFactory == null (admin "All Factories")
            // aggregates sub-categories across every factory.
            var subs = await TaskManager.GetSubCategoriesAsync(ActiveFactory, start, end);
            if (token != _loadSeq) return;
            availableSubCategories = subs;
            selectedSubCategories.RemoveWhere(s => !subs.Contains(s, StringComparer.OrdinalIgnoreCase));

            var orderNo = string.IsNullOrWhiteSpace(selectedOrderNo) ? null : selectedOrderNo.Trim();
            var factoryType = string.IsNullOrWhiteSpace(selectedFactoryType) ? null : selectedFactoryType;
            // Empty selection = "All" = no sub-filter; otherwise pipe-join the checked options.
            var subCats = selectedSubCategories.Count == 0 ? null : string.Join("|", selectedSubCategories);

            var scheduled = await TaskManager.GetTasksAsync("S", start, end, orderNo, factoryType, subCats);
            var progress = await TaskManager.GetTasksAsync("P", start, end, orderNo, factoryType, subCats);
            var completed = await TaskManager.GetTasksAsync("C", start, end, orderNo, factoryType, subCats);
            var overdue = await TaskManager.GetTasksAsync("O", start, end, orderNo, factoryType, subCats);
            var onhold = await TaskManager.GetTasksAsync("H", start, end, orderNo, factoryType, subCats);  // plan_status = 1

            // A newer filter change started while we awaited -> drop these stale results so the
            // board never shows data that doesn't match the current filters.
            if (token != _loadSeq) return;

            todotasks = scheduled.Select(Map).ToList();
            inprogresstasks = progress.Select(Map).ToList();
            completedtasks = completed.Select(Map).ToList();
            overduetasks = overdue.Select(Map).ToList();
            onholdtasks = onhold.Select(Map).ToList();

            StateHasChanged();
        }

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
            Qty = r.Qty,
            // Knitter-detail fields (populated only for the In Progress 'P' flag).
            KnitterId = r.KnitterId,
            Issue = r.Issue,
            ReturnQty = r.ReturnQty,
            CustomerCode = r.CustomerCode,
            CustomerName = r.CustomerName,
            RId = r.RId
        };

        // ======================================================================
        // Stat-card toggle (mirrors the original ShowBubble behavior)
        // ======================================================================
        private void ShowBubble(int id)
        {
            activeBubble = id;
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
        // Filter controls. Each change recomputes the window and reloads the board.
        // ======================================================================
        // Raised by the CompactDateRangeFilter component after a preset/custom range is
        // applied. selectedStartDate/selectedEndDate are already updated via @bind, so
        // LoadBoardAsync() just reloads with the new window.
        private Task OnFilterChanged() => LoadBoardAsync();

        private async Task OnOrderNoChanged(ChangeEventArgs e)
        {
            selectedOrderNo = e.Value?.ToString() ?? "";
            await LoadBoardAsync();
        }

        // Admin-only: change the active factory_type filter ("" = all factories).
        // Restricted users never reach this (their dropdown is fixed/disabled).
        private async Task OnFactoryTypeChanged(ChangeEventArgs e)
        {
            var value = e.Value?.ToString();
            selectedFactoryType = string.IsNullOrWhiteSpace(value) ? null : value;
            selectedSubCategories.Clear();   // changing factory resets the sub-filter to "All"
            await LoadBoardAsync();           // reloads sub-options for the new factory + the board
        }

        // "All" -> clear specific picks (empty set = no sub-filter). The chip's active state
        // is purely model-driven, so clicking "All" while it is already active is a harmless
        // no-op (it stays active) and can never be left visually "unchecked".
        private async Task OnAllSubCategoriesSelected()
        {
            if (selectedSubCategories.Count == 0) return;   // already "All"
            selectedSubCategories.Clear();
            await LoadBoardAsync();
        }

        // Toggle one sub-category chip; an empty set falls back to "All".
        private async Task ToggleSubCategory(string sub)
        {
            if (!selectedSubCategories.Remove(sub))   // present -> remove; absent -> add
                selectedSubCategories.Add(sub);
            await LoadBoardAsync();
        }

        // ======================================================================
        // Opening a card (read-only board; no edit/hold/delete/add actions).
        // ======================================================================
        private void OnItemClicked(int taskId) { }

        // ======================================================================
        // Order-detail modal (opened from an In Progress card header).
        // showOrderModal toggles the reusable Modal; selectedOrder keeps the
        // clicked card for the next step that fills the modal in. Empty for now.
        // ======================================================================
        private bool showOrderModal;
        private bool chartTableView;   // false = pace graph, true = data table
        private TaskCardItem? selectedOrder;
        private List<ReturnPacePoint> returnPoints = new();
        private List<OrderStyleResponseModel> orderStyles = new();
        private string orderModalTitle =>
            selectedOrder is null ? "Order" : $"Order {selectedOrder.OrderNo}";

        // Open the order modal for the clicked card and lazily load its return data. The
        // ReturnPaceChart component does all the chart math (date-scaling, cumulative line,
        // expected pace); the page just hands it the raw return points + the order's window.
        private async Task OnOrderClicked(TaskCardItem item)
        {
            selectedOrder = item;
            returnPoints = new();
            orderStyles = new();
            chartTableView = false;   // always open on the graph
            showOrderModal = true;
            StateHasChanged();

            var points = await TaskManager.GetKnitterReturnSeriesAsync(item.RId);
            returnPoints = (points ?? new())
                .Select(p => new ReturnPacePoint { Date = p.ReturnAt, Count = p.ReturnCount })
                .ToList();
            orderStyles = await TaskManager.GetOrderStylesAsync(item.TaskId);

            StateHasChanged();
        }

        private void CloseOrderModal()
        {
            showOrderModal = false;
            selectedOrder = null;
            returnPoints = new();
            orderStyles = new();
        }

        // ======================================================================
        // Add / Edit Task modal (UI scaffold, reuses PanelModal). The Scheduled
        // column's header "Add" button and each Scheduled card's "Edit" button
        // open this. No persistence yet — Save only validates and closes.
        // ======================================================================
        private bool showTaskEditor;
        private string taskEditorTitle = "Add Task";
        private string? taskFormError;

        // Form fields (mirror the Add Task design). Reset on every open.
        private string taskFormName = "";
        private DateTime? taskFormStart = DateTime.Today;
        private DateTime? taskFormEnd = DateTime.Today;
        private string taskFormPriority = "";
        private string taskFormDescription = "";
        private DateTime? taskFormNotifyDate = DateTime.Today;
        private string taskFormFrequency = "";
        private bool taskFormAssignGroup;          // false = Individual Staff, true = Group
        private string taskFormStaff = "";
        private string? taskFormFileName;

        private void OpenAddTaskModal()
        {
            ResetTaskForm();
            taskEditorTitle = "Add Task";
            showTaskEditor = true;
        }

        // Edit opens the same form. Blank for now: the Scheduled cards carry knitting
        // data (order no, gauge) that doesn't map to these to-do fields, so there is
        // nothing meaningful to prefill yet.
        private void OpenEditTaskModal(TaskCardItem item)
        {
            ResetTaskForm();
            taskEditorTitle = "Edit Task";
            showTaskEditor = true;
        }

        private void CloseTaskEditor() => showTaskEditor = false;

        private void SaveTask()
        {
            // UI-only scaffold: validate the one required field, then close. No API/DB yet.
            if (string.IsNullOrWhiteSpace(taskFormName))
            {
                taskFormError = "Task Name is required.";
                return;
            }
            showTaskEditor = false;
        }

        private void OnTaskFileSelected(InputFileChangeEventArgs e)
        {
            // Capture the name only — nothing is uploaded or stored in this scaffold.
            taskFormFileName = e.File?.Name;
        }

        private void ResetTaskForm()
        {
            taskFormError = null;
            taskFormName = "";
            taskFormStart = DateTime.Today;
            taskFormEnd = DateTime.Today;
            taskFormPriority = "";
            taskFormDescription = "";
            taskFormNotifyDate = DateTime.Today;
            taskFormFrequency = "";
            taskFormAssignGroup = false;
            taskFormStaff = "";
            taskFormFileName = null;
        }
    }
}
