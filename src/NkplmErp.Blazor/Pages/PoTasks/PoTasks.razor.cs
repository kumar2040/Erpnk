using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using NkplmErp.Blazor.Components;
using NkplmErp.Blazor.Services.Dropdown.Manager.Interface;
using NkplmErp.Blazor.Services.PoTask;
using NkplmErp.Blazor.Services.RoleManagement;
using NkplmErp.Blazor.Services.TaskManagement.Manager.Interface;
using NkplmErp.Blazor.Services.TaskManagement.Model;
using NkplmErp.Shared.DTOs;
using NkplmErp.Shared.DTOs.Dropdown;

namespace NkplmErp.Blazor.Pages.PoTasks
{
    public partial class PoTasks
    {
        [Inject] private PoTaskApiClient Api { get; set; } = default!;
        [Inject] private RoleManagementApiClient Roles { get; set; } = default!;
        [Inject] private NkplmErp.Blazor.Services.RoleManagement.PermissionService PermSvc { get; set; } = default!;
        [Inject] private ITaskManagementManager TaskMgr { get; set; } = default!;
        [Inject] private IDropdownManager Dropdowns { get; set; } = default!;
        [Inject] private NavigationManager Nav { get; set; } = default!;

        private const string PageKey = "PoTask";

        private bool AccessDenied;
        private bool CanEdit;
        private bool loading = true;

        // Filters
        private bool MineOnly;
        private string orderNo = "";

        // Selected date window (the CompactDateRangeFilter binds these). Left blank on
        // load so the board opens unfiltered — the user opts into a window.
        private DateTime? selectedStartDate;
        private DateTime? selectedEndDate;

        // ---- Facility / gauge scope ----
        // scope tells us whether the user is unrestricted (editable facility dropdown) or
        // gauge-restricted (locked to one factory_type). selectedFactoryType is the current
        // dropdown value (null/"" = all facilities; only unrestricted users can change it).
        // The SP pins a restricted user to their own gauge regardless of what we send, so
        // the locked UI is a courtesy, not the security boundary.
        private TaskScopeResponseModel? scope;
        private string? selectedFactoryType;

        // Options listed under the always-present "All Factories". Empty until the scope
        // call returns (or if it returns nothing) — the dropdown still renders, it just has
        // All Factories as its only choice rather than disappearing.
        private List<string> FactoryOptions => scope?.FactoryTypes ?? new();

        // AutoCompleteSelect (the app's standard dropdown) takes DropDownListModel rows
        // rather than plain strings; a factory type has no separate code, so it is its
        // own Id and Value.
        private List<DropDownListModel> FactoryDropDownModels =>
            FactoryOptions.Select(ft => new DropDownListModel { Id = ft, Value = ft }).ToList();

        // What the control shows as selected. All=1 makes its leading row carry
        // DropdownValues.All ("-1"), which is what selectedFactoryType==null maps to.
        private string FactoryValueForControl => selectedFactoryType ?? DropdownValues.All;

        // Monotonic token so a slow in-flight board load can't overwrite a newer one.
        // Every LoadBoardAsync bumps it; results are applied only if still the latest.
        private int _loadSeq;

        // Board buckets, keyed by the status flags loaded from spDropdown (PoTaskBoardColumn).
        // Built in OnInitializedAsync once the columns are known. The keys are the exact
        // letters sp_GetPoTask matches on (S/P/C/O/H), so the board fetch keys on them directly.
        private Dictionary<string, List<PoTaskCardDto>> _buckets = new();

        // The board columns, left to right, from spDropdown -- Id is the status flag,
        // Value is the column title. The per-column CSS is derived from the flag in the
        // markup (class "col-<flag>"), so no title or class is hardcoded in the UI.
        private List<DropDownListModel> BoardColumns = new();

        // ---- Stat-card view (mirrors /task's ShowBubble) ----
        // "WORKLOAD" = the multi-column group view (default); any other value is a single
        // status flag showing just that column, full width with its cards flowing in a grid.
        private const string WorkloadView = "WORKLOAD";
        private string activeView = WorkloadView;
        private void ShowView(string view) => activeView = view;

        // Work Load = the active-workflow statuses, matching /task (On Hold is excluded and
        // only appears when its own card is picked).
        private static readonly string[] WorkloadFlags = { "S", "P", "C", "O" };
        private int Workload => WorkloadFlags.Sum(Count);
        private bool SingleView => activeView != WorkloadView;

        // Which columns render for the current selection: the workflow group, or the one
        // picked status.
        private IEnumerable<DropDownListModel> VisibleColumns =>
            activeView == WorkloadView
                ? BoardColumns.Where(c => WorkloadFlags.Contains(c.Id))
                : BoardColumns.Where(c => c.Id == activeView);

        // Add Task modal
        private bool showAdd;
        private bool saving;
        private string? addError;
        private bool assignGroup;
        private int selectedGroupId;
        private readonly List<string> selectedStaff = new();
        private string staffPick = "";              // transient: the dropdown's current pick
        private string newChecklistText = "";       // Description "+" buffer
        private string? attachmentName;             // selected upload file name
        private bool isDragging;                    // drop-zone drag highlight
        private CreatePoTaskRequest newTask = NewBlankTask();
        private List<PoTaskGroupDto> groups = new();
        private List<UserWithRolesDto> staff = new();

        // Detail drawer
        private bool showDetail;
        private PoTaskDetailResult? detail;

        // Per-card step buttons (Scheduled / In progress / Complete), from spDropdown.
        // Id is the S/P/C code MyUpdate sends to the API. Loaded once on init.
        private List<DropDownListModel> AssigneeStatuses = new();

        // Status-letter -> label and rule-id -> label, both from spDropdown, so the
        // status/rule text in the detail drawer isn't hardcoded here either.
        private Dictionary<string, string> _statusLabels = new();   // S/P/C/H/X -> label
        private Dictionary<string, string> _ruleLabels = new();     // 1/2/3     -> label

        // AutoCompleteSelect binds a string; these request fields are byte?. The component's
        // Select ("0") / All ("-1") leading rows and any unparseable value mean "not chosen".
        private string PriorityIdStr
        {
            get => newTask.PriorityId?.ToString() ?? "";
            set => newTask.PriorityId =
                DropdownValues.IsPlaceholder(value) || !byte.TryParse(value, out var b) ? (byte?)null : b;
        }
        private string UpdateFrequencyStr
        {
            get => newTask.UpdateFrequency?.ToString() ?? "";
            set => newTask.UpdateFrequency =
                DropdownValues.IsPlaceholder(value) || !byte.TryParse(value, out var b) ? (byte?)null : b;
        }
        private string CompletionRuleStr
        {
            get => newTask.CompletionRule?.ToString() ?? "";
            set => newTask.CompletionRule =
                DropdownValues.IsPlaceholder(value) || !byte.TryParse(value, out var b) ? (byte?)null : b;
        }

        protected override async Task OnInitializedAsync()
        {
            if (!PermSvc.IsLoaded)
                await PermSvc.LoadPermissionsAsync();
            if (!PermSvc.CanView(PageKey))
            {
                AccessDenied = true;
                return;
            }
            CanEdit = PermSvc.CanEdit(PageKey);

            // All the board's status text comes from spDropdown -- columns, step buttons,
            // and the detail-drawer status/rule labels. The column flags double as the
            // board's bucket keys, so they're loaded before the first board fetch.
            BoardColumns = await Dropdowns.GetDropDownListAsync("PoTaskBoardColumn");
            _buckets = BoardColumns.ToDictionary(c => c.Id, _ => new List<PoTaskCardDto>());
            AssigneeStatuses = await Dropdowns.GetDropDownListAsync("TaskAssigneeStatus");
            _statusLabels = (await Dropdowns.GetDropDownListAsync("TaskStatus"))
                .ToDictionary(x => x.Id, x => x.Value);
            _ruleLabels = (await Dropdowns.GetDropDownListAsync("TaskCompletionRule"))
                .ToDictionary(x => x.Id, x => x.Value);

            // Staff + groups for the Add Task form (deduped staff by user id).
            if (CanEdit)
            {
                groups = await Api.GetGroupsAsync();
                staff = (await Roles.GetAllUsersWithRolesAsync())
                    .GroupBy(u => u.UserId).Select(g => g.First()).ToList();
            }

            // Resolve the user's facility scope. A gauge-restricted user is pinned to their
            // own factory_type; everyone else starts on "All Factories" (null) and may change it.
            scope = (await TaskMgr.GetScopeAsync()).Data;
            if (scope?.IsRestricted == true)
                selectedFactoryType = scope.AssignedGauge;

            await LoadBoardAsync();
        }

        // -------------------------------------------------------------- board ----

        // Load every status column for the current filters. All five go out together, so a
        // filter change costs one round-trip's latency instead of five back-to-back.
        private async Task LoadBoardAsync()
        {
            var token = ++_loadSeq;   // newest load wins; a stale in-flight load is discarded below

            loading = true;
            StateHasChanged();

            var search = string.IsNullOrWhiteSpace(orderNo) ? null : orderNo.Trim();
            var factory = string.IsNullOrWhiteSpace(selectedFactoryType) ? null : selectedFactoryType;
            var start = selectedStartDate;
            var end = selectedEndDate;

            // Every filter applies to both scopes, so the facility dropdown behaves the same
            // on All and on My tasks.
            var flags = _buckets.Keys.ToList();
            var results = await Task.WhenAll(flags.Select(flag => MineOnly
                ? Api.GetMyTasksAsync(flag, startDate: start, endDate: end, orderNo: search, factoryType: factory)
                : Api.GetBoardAsync(flag, startDate: start, endDate: end, orderNo: search, factoryType: factory)));

            // A newer filter change started while we awaited -> drop these stale results so the
            // board never shows data that doesn't match the filters currently on screen.
            if (token != _loadSeq) return;

            for (var i = 0; i < flags.Count; i++)
                _buckets[flags[i]] = results[i];

            loading = false;
            StateHasChanged();
        }

        private List<PoTaskCardDto> Cards(string flag) =>
            _buckets.TryGetValue(flag, out var list) ? list : new();

        private int Count(string flag) => Cards(flag).Count;

        private async Task SetScope(bool mine)
        {
            if (MineOnly == mine) return;
            MineOnly = mine;
            await LoadBoardAsync();
        }

        private async Task OnOrderNoChanged(ChangeEventArgs e)
        {
            orderNo = e.Value?.ToString() ?? "";
            await LoadBoardAsync();
        }

        // The date picker applied a preset / custom range (it already pushed the new window
        // through @bind), so just reload.
        private async Task OnFilterChanged() => await LoadBoardAsync();

        // Facility dropdown. DropdownValues.IsPlaceholder catches the leading "-1"
        // (All Factories) row as well as an empty/unset value.
        private async Task OnFactoryTypeSelectedAsync(string id)
        {
            selectedFactoryType = DropdownValues.IsPlaceholder(id) ? null : id;
            await LoadBoardAsync();
        }

        // "Update my side" — moves only the caller's own assignee row.
        private async Task MyUpdate(int taskId, string toStatus)
        {
            await Api.MyUpdateAsync(new MyUpdatePoTaskRequest { PoTaskId = taskId, ToStatus = toStatus });
            await LoadBoardAsync();
        }

        // --------------------------------------------------------- Add Task ----

        private void OpenAdd()
        {
            newTask = NewBlankTask();
            selectedStaff.Clear();
            staffPick = "";
            newChecklistText = "";
            attachmentName = null;
            isDragging = false;
            selectedGroupId = 0;
            assignGroup = false;
            addError = null;
            showAdd = true;
        }

        private void CloseAdd() => showAdd = false;

        // Description "+" — add the typed line as a checklist sub-item.
        private void AddChecklistLine()
        {
            var t = newChecklistText?.Trim();
            if (string.IsNullOrEmpty(t)) return;
            newTask.ChecklistItems.Add(t);
            newChecklistText = "";
        }

        private void RemoveChecklistLine(string item) => newTask.ChecklistItems.Remove(item);

        // Upload Documents (< 1 MB) — read into the task's attachment.
        private async Task OnFileSelected(InputFileChangeEventArgs e)
        {
            isDragging = false;
            var file = e.File;
            if (file is null) return;
            if (file.Size > 1_000_000) { addError = "File must be less than 1 MB."; return; }

            using var ms = new MemoryStream();
            await file.OpenReadStream(1_000_000).CopyToAsync(ms);
            newTask.Attachment = new PoTaskAttachmentUpload
            {
                FileName = file.Name,
                ContentType = file.ContentType,
                ContentBase64 = Convert.ToBase64String(ms.ToArray())
            };
            attachmentName = file.Name;
            addError = null;
        }

        // Tag-style staff picker: add the dropdown's pick as a chip, then reset it.
        private void AddStaff(ChangeEventArgs e)
        {
            var id = e.Value?.ToString();
            if (!string.IsNullOrWhiteSpace(id) && !selectedStaff.Contains(id))
                selectedStaff.Add(id);
            staffPick = "";   // reset so the same option can be reselected later
        }

        private void RemoveStaff(string id) => selectedStaff.Remove(id);

        private string StaffName(string id)
        {
            var s = staff.FirstOrDefault(x => x.UserId == id);
            return s is null ? id : (string.IsNullOrWhiteSpace(s.FullName) ? s.Email : s.FullName);
        }

        private void OnStaffSelected(ChangeEventArgs e)
        {
            selectedStaff.Clear();
            if (e.Value is string[] arr)
                selectedStaff.AddRange(arr.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        private async Task SaveAsync()
        {
            addError = null;
            if (string.IsNullOrWhiteSpace(newTask.Title)) { addError = "Task name is required."; return; }

            if (assignGroup)
            {
                if (selectedGroupId <= 0) { addError = "Pick a group."; return; }
                newTask.GroupId = selectedGroupId;
                newTask.UserIds = new();
            }
            else
            {
                newTask.GroupId = null;
                if (selectedStaff.Count == 0) { addError = "Pick at least one staff member."; return; }
                newTask.UserIds = selectedStaff.ToList();
            }

            saving = true;
            var id = await Api.CreateAsync(newTask);
            saving = false;

            if (id is null) { addError = "Could not save the task. Please try again."; return; }
            showAdd = false;
            await LoadBoardAsync();
        }

        private static CreatePoTaskRequest NewBlankTask() => new()
        {
            Stage = 20,                 // Manual
            CompletionRule = 1,         // All
            StartDate = DateTime.Today,
            DueDate = DateTime.Today
        };

        // ----------------------------------------------------------- detail ----

        private async Task OpenDetail(int taskId)
        {
            detail = await Api.GetDetailAsync(taskId);
            showDetail = detail?.Task is not null;
        }

        private void CloseDetail() => showDetail = false;

        // Navigate to the URL the procedure built for this card. The board knows no
        // routes — a stage's link is added in sp_GetPoTask, never here. Only called for
        // cards whose LinkUrl is non-empty.
        private void GoToUrl(string url) => Nav.NavigateTo(url);

        private async Task ToggleChecklist(int checklistId)
        {
            await Api.ToggleChecklistAsync(checklistId);
            if (detail?.Task is not null) await OpenDetail(detail.Task.PoTaskId);
        }

        private async Task OverrideAsync(string toStatus)
        {
            if (detail?.Task is null) return;
            await Api.TransitionAsync(new TransitionPoTaskRequest { PoTaskId = detail.Task.PoTaskId, ToStatus = toStatus });
            await AfterDetailWrite(detail.Task.PoTaskId);
        }

        private async Task HoldAsync()
        {
            if (detail?.Task is null) return;
            await Api.HoldAsync(new HoldPoTaskRequest { PoTaskId = detail.Task.PoTaskId, BlockedReason = "Held from board" });
            await AfterDetailWrite(detail.Task.PoTaskId);
        }

        private async Task ResolveAsync()
        {
            if (detail?.Task is null) return;
            await Api.ResolveAsync(detail.Task.PoTaskId);
            await AfterDetailWrite(detail.Task.PoTaskId);
        }

        private async Task CancelAsync()
        {
            if (detail?.Task is null) return;
            await Api.CancelAsync(detail.Task.PoTaskId);
            showDetail = false;
            await LoadBoardAsync();
        }

        private async Task AfterDetailWrite(int taskId)
        {
            await OpenDetail(taskId);
            await LoadBoardAsync();
        }

        // ------------------------------------------- order return-detail modal ----
        // Clicking a card's PO name opens the return-detail modal (buyer + issued/returned
        // + return-pace chart + Styles & Colors) for the task's linked knitter line. The
        // card carries no RefId, so we read the task detail to get its MasterPlanChildId,
        // then pull the summary (KH), chart series (KD) and styles (KS) for that line.
        private bool showOrderView;
        private bool orderViewLoading;
        private string? orderViewNo;
        private bool orderHasLine;                 // false = task has no linked knitter line (BOM/manual)
        private KnitterSummaryResponseModel? orderSummary;
        private List<ReturnPacePoint> returnPoints = new();
        private List<OrderStyleResponseModel> orderStyles = new();

        // Pace-chart X window. Prefers the line's own planned dates, then the task's,
        // then today, so the chart always has a valid axis to draw — even before any
        // pieces are issued or returned (an empty skeleton rather than a blank panel).
        private DateTime chartStart = DateTime.Today;
        private DateTime chartEnd = DateTime.Today;

        private async Task OpenOrderView(PoTaskCardDto card)
        {
            orderViewNo = card.OrderNo;
            orderViewLoading = true;
            showOrderView = true;
            orderHasLine = false;
            orderSummary = null;
            returnPoints = new();
            orderStyles = new();
            chartStart = DateTime.Today;
            chartEnd = DateTime.Today;
            StateHasChanged();

            try
            {
                // The card has no RefId — read the task detail to get its linked line.
                var detail = await Api.GetDetailAsync(card.TaskId);
                var refId = detail?.Task?.RefId ?? 0;
                if (refId > 0)
                {
                    orderSummary = (await TaskMgr.GetKnitterSummaryAsync(refId)).Data;
                    if (orderSummary is not null)
                    {
                        orderHasLine = true;
                        var pts = (await TaskMgr.GetKnitterReturnSeriesAsync(orderSummary.RId)).Data ?? new();
                        returnPoints = pts.Select(p => new ReturnPacePoint { Date = p.ReturnAt, Count = p.ReturnCount }).ToList();
                        orderStyles = (await TaskMgr.GetOrderStylesAsync(refId)).Data ?? new();
                    }
                }

                // Pace-chart X window: line dates first, then the task's planned window,
                // then today — so the chart always has a valid axis, even for a line with
                // nothing issued or returned yet.
                chartStart = orderSummary?.StartDate ?? detail?.Task?.StartDate ?? DateTime.Today;
                chartEnd = orderSummary?.EndDate ?? detail?.Task?.DueDate ?? chartStart;
            }
            catch
            {
                // Leave state empty — the modal shows its "no linked line" message.
            }
            finally
            {
                orderViewLoading = false;
                StateHasChanged();
            }
        }

        private void CloseOrderView() => showOrderView = false;

        // Returned / issued as 0..100% for the progress bar under the Returned tile.
        private int OrderReturnPct
        {
            get
            {
                var issue = orderSummary?.Issue ?? 0;
                var ret = orderSummary?.ReturnQty ?? 0;
                return issue > 0 ? Math.Clamp((int)Math.Round(100.0 * ret / issue), 0, 100) : 0;
            }
        }

        // ---------------------------------------------------------- helpers ----

        // Status letter -> label, from spDropdown (TaskStatus). Unknown -> the raw letter.
        private string StatusName(string? s) =>
            s is not null && _statusLabels.TryGetValue(s, out var v) ? v : (s ?? "");

        // Completion-rule id -> label, from spDropdown (TaskCompletionRule), lower-cased so
        // it reads inline ("rolls up: all must complete"). Unknown -> "".
        private string RuleName(byte rule) =>
            _ruleLabels.TryGetValue(rule.ToString(), out var v) ? v.ToLowerInvariant() : "";
    }
}
