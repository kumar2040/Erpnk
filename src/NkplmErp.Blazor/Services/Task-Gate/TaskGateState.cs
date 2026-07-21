using NkplmErp.Blazor.Model.Task_Gate;

namespace NkplmErp.Blazor.Services.Task_Gate
{
    // Per-circuit state for the login task gate. TaskGateModal drives it;
    // TaskGateBadge reads from it, so the header count stays correct without a
    // second fetch.
    //
    // This class holds no HttpClient and no JS interop on purpose — the modal owns
    // the I/O, this owns the sequencing. "Started" is a server-backed fact;
    // "skipped" is session-only and is persisted to sessionStorage by the modal.
    public class TaskGateState
    {
        private readonly HashSet<int> _started = new();
        private readonly HashSet<int> _skipped = new();

        private List<TaskGateResponseModel> _run = new();
        private int _runIndex;

        public event Action? Changed;

        // Everything the server said is pending for this user, FIFO.
        public List<TaskGateResponseModel> Queue { get; private set; } = new();

        public bool HasLoaded { get; private set; }
        public bool IsOpen { get; private set; }
        public bool IsBusy { get; set; }

        // The task currently on screen.
        public TaskGateResponseModel? Current =>
            _runIndex >= 0 && _runIndex < _run.Count ? _run[_runIndex] : null;

        // 1-based position within THIS run, not within the whole queue.
        public int Position => _run.Count == 0 ? 0 : _runIndex + 1;
        public int Total => _run.Count;

        // What the header badge shows: everything still not started.
        public int PendingCount => Queue.Count - _started.Count;

        public int StartedCount => _started.Count;
        public int SkippedCount => _skipped.Count;

        // Persisted by the modal so a mid-queue refresh resumes where it left off.
        public string SkippedCsv => string.Join(",", _skipped);

        public void Load(IEnumerable<TaskGateResponseModel> queue, IEnumerable<int> skipped)
        {
            // TaskId is nullable on the shared model because the same class also
            // carries the 'S' write result. Queue rows always have one; drop
            // anything that does not rather than tracking it under a bogus id.
            Queue = (queue ?? Enumerable.Empty<TaskGateResponseModel>())
                .Where(t => t.TaskId.HasValue)
                .ToList();

            _started.Clear();
            _skipped.Clear();
            foreach (var id in skipped ?? Enumerable.Empty<int>())
                _skipped.Add(id);

            HasLoaded = true;
            Notify();
        }

        // Build the sequence this run will show: not already started, and not
        // skipped earlier in the same session. Returns false when there is nothing
        // to show, so the caller can close the gate without ever painting it.
        public bool OpenRun()
        {
            _run = Queue
                .Where(t => !_started.Contains(t.TaskId!.Value) && !_skipped.Contains(t.TaskId!.Value))
                .ToList();

            _runIndex = 0;
            IsOpen = _run.Count > 0;
            Notify();
            return IsOpen;
        }

        // Badge replay: previously skipped tasks come back into the run. Started
        // tasks never do — they are done with the gate.
        public bool Replay()
        {
            _skipped.Clear();
            return OpenRun();
        }

        public void MarkStarted(int taskId)
        {
            _started.Add(taskId);
            Advance();
        }

        public void MarkSkipped(int taskId)
        {
            _skipped.Add(taskId);
            Advance();
        }

        public void Close()
        {
            IsOpen = false;
            Notify();
        }

        public void Notify() => Changed?.Invoke();

        private void Advance()
        {
            _runIndex++;
            if (_runIndex >= _run.Count) IsOpen = false;
            Notify();
        }
    }
}
