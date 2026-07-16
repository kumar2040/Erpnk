using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using NkplmErp.Blazor.Components;
using NkplmErp.Blazor.Services.PoTask;
using NkplmErp.Blazor.Services.RoleManagement;
using NkplmErp.Blazor.Services.TaskManagement.Manager.Interface;
using NkplmErp.Blazor.Services.TaskManagement.Model;
using NkplmErp.Shared.DTOs;

namespace NkplmErp.Blazor.Pages.PoTasks
{
    public partial class PoTasks
    {
        [Inject] private PoTaskApiClient Api { get; set; } = default!;
        [Inject] private RoleManagementApiClient Roles { get; set; } = default!;
        [Inject] private NkplmErp.Blazor.Services.RoleManagement.PermissionService PermSvc { get; set; } = default!;
        [Inject] private ITaskManagementManager TaskMgr { get; set; } = default!;

        private const string PageKey = "PoTask";

        private bool AccessDenied;
        private bool CanEdit;
        private bool loading = true;

        // Filters
        private bool MineOnly;
        private string orderNo = "";

        // Board buckets, keyed by display flag S/P/C/O/H.
        private readonly Dictionary<string, List<PoTaskCardDto>> _buckets = new()
        {
            ["S"] = new(), ["P"] = new(), ["C"] = new(), ["O"] = new(), ["H"] = new()
        };

        // Column definitions rendered left-to-right.
        private record Col(string Title, string Flag, string Css);
        private readonly List<Col> Columns = new()
        {
            new("Scheduled",   "S", "col-sched"),
            new("In Progress", "P", "col-prog"),
            new("On Hold",     "H", "col-hold"),
            new("Completed",   "C", "col-done"),
            new("Over Due",    "O", "col-due"),
        };

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

            // Staff + groups for the Add Task form (deduped staff by user id).
            if (CanEdit)
            {
                groups = await Api.GetGroupsAsync();
                staff = (await Roles.GetAllUsersWithRolesAsync())
                    .GroupBy(u => u.UserId).Select(g => g.First()).ToList();
            }

            await LoadBoardAsync();
        }

        // -------------------------------------------------------------- board ----

        private async Task LoadBoardAsync()
        {
            loading = true;
            StateHasChanged();

            var search = string.IsNullOrWhiteSpace(orderNo) ? null : orderNo.Trim();
            foreach (var flag in _buckets.Keys.ToList())
            {
                _buckets[flag] = MineOnly
                    ? await Api.GetMyTasksAsync(flag, orderNo: search)
                    : await Api.GetBoardAsync(flag, orderNo: search);
            }

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
                    orderSummary = await TaskMgr.GetKnitterSummaryAsync(refId);
                    if (orderSummary is not null)
                    {
                        orderHasLine = true;
                        var pts = await TaskMgr.GetKnitterReturnSeriesAsync(orderSummary.RId);
                        returnPoints = pts.Select(p => new ReturnPacePoint { Date = p.ReturnAt, Count = p.ReturnCount }).ToList();
                        orderStyles = await TaskMgr.GetOrderStylesAsync(refId);
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

        private static string StatusName(string? s) => s switch
        {
            "S" => "Scheduled",
            "P" => "In progress",
            "C" => "Completed",
            "H" => "On hold",
            "X" => "Cancelled",
            _ => s ?? ""
        };

        private static string RuleName(byte rule) => rule switch
        {
            2 => "any one completes",
            3 => "quorum",
            _ => "all must complete"
        };
    }
}
