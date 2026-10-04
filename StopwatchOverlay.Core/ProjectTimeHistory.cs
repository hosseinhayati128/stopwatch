using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace StopwatchOverlay
{
    public enum ProjectTrackingChange
    {
        NoChange,
        Started,
        Switched
    }

    public enum ProjectRecordMutationStatus
    {
        Success,
        NotFound,
        OpenInterval,
        Overlap
    }

    public sealed record ProjectRecordMutationResult(
        ProjectRecordMutationStatus Status,
        ProjectWorkIntervalView? Record = null);

    public enum ProjectDeletionStatus
    {
        Success,
        NotFound,
        ActiveTimerRunning
    }

    public sealed record ProjectDeletionResult(
        ProjectDeletionStatus Status,
        string ProjectName,
        int RecordsDeleted);

    /// <summary>
    /// The part of a timer session that determines whether project time should
    /// remain open when persisted timer state and project history are reconciled.
    /// </summary>
    public sealed record ProjectTimerState(
        Guid TimerSessionId,
        string? ProjectName,
        bool IsRunning);

    public sealed record ProjectInfoView(string Key, string Name, string Category = "", double ScorePerHour = 1.0);

    /// <summary>
    /// An immutable, UTC work interval suitable for dashboard queries.
    /// EndUtc is null while its timer is actively tracking the project.
    /// </summary>
    public sealed record ProjectWorkIntervalView(
        Guid Id,
        Guid TimerSessionId,
        string ProjectKey,
        string ProjectName,
        DateTime StartUtc,
        DateTime? EndUtc)
    {
        public bool IsOpen => !EndUtc.HasValue;

        public TimeSpan Duration(DateTime asOfUtc)
        {
            asOfUtc = ProjectTimeHistory.NormalizeUtc(asOfUtc);
            DateTime effectiveEnd = EndUtc ?? asOfUtc;
            return effectiveEnd > StartUtc
                ? effectiveEnd - StartUtc
                : TimeSpan.Zero;
        }
    }

    /// <summary>
    /// A detached, immutable point-in-time view of all projects and intervals.
    /// The backing collections cannot change when the live tracker is updated.
    /// </summary>
    public sealed class ProjectHistoryView
    {
        internal ProjectHistoryView(
            DateTime asOfUtc,
            IEnumerable<ProjectInfoView> projects,
            IEnumerable<ProjectWorkIntervalView> intervals,
            IEnumerable<FocusPauseRecord>? focusPauses = null)
        {
            AsOfUtc = ProjectTimeHistory.NormalizeUtc(asOfUtc);
            Projects = new ReadOnlyCollection<ProjectInfoView>(projects.ToArray());
            Intervals = new ReadOnlyCollection<ProjectWorkIntervalView>(intervals.ToArray());
            FocusPauses = new ReadOnlyCollection<FocusPauseRecord>((focusPauses ?? []).ToArray());
        }

        public DateTime AsOfUtc { get; }
        public IReadOnlyList<ProjectInfoView> Projects { get; }
        public IReadOnlyList<ProjectWorkIntervalView> Intervals { get; }
        public IReadOnlyList<FocusPauseRecord> FocusPauses { get; }
    }

    /// <summary>
    /// Thread-safe project registry and work-interval model. Project identity is
    /// case-insensitive; the spelling from the first registration is retained
    /// permanently for display and historical reports.
    /// </summary>
    public sealed class ProjectTimeHistory
    {
        private const int MaximumProjectNameLength = 200;
        private readonly object _gate = new();
        private readonly List<ProjectEntry> _projects = new();
        private readonly List<WorkIntervalEntry> _intervals = new();
        private readonly List<FocusPauseRecord> _focusPauses = new();

        public IReadOnlyList<FocusPauseRecord> FocusPauses
        {
            get
            {
                lock (_gate)
                {
                    return new ReadOnlyCollection<FocusPauseRecord>(_focusPauses.ToArray());
                }
            }
        }

        public IReadOnlyList<string> ProjectNames
        {
            get
            {
                lock (_gate)
                {
                    return new ReadOnlyCollection<string>(
                        _projects.Select(project => project.Name).ToArray());
                }
            }
        }

        public string RegisterProject(string projectName, string? category = null, double? scorePerHour = null)
        {
            string displayName = NormalizeProjectName(projectName);
            string key = CreateProjectKey(displayName);

            lock (_gate)
            {
                return RegisterProjectCore(key, displayName, category, scorePerHour).Name;
            }
        }

        public string GetProjectCategory(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName)) return "";
            string key = CreateProjectKey(projectName.Trim());
            lock (_gate)
            {
                var match = _projects.FirstOrDefault(p => ProjectKeysEqual(p.Key, key));
                return match?.Category ?? "";
            }
        }

        public double GetProjectScorePerHour(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName)) return 1.0;
            string key = CreateProjectKey(projectName.Trim());
            lock (_gate)
            {
                var match = _projects.FirstOrDefault(p => ProjectKeysEqual(p.Key, key));
                return match != null ? match.ScorePerHour : 1.0;
            }
        }

        public bool SetProjectScorePerHour(string projectName, double scorePerHour)
        {
            if (string.IsNullOrWhiteSpace(projectName)) return false;
            string displayName = NormalizeProjectName(projectName);
            string key = CreateProjectKey(displayName);
            if (double.IsNaN(scorePerHour) || double.IsInfinity(scorePerHour) || scorePerHour < 0)
                scorePerHour = 1.0;

            lock (_gate)
            {
                var match = _projects.FirstOrDefault(p => ProjectKeysEqual(p.Key, key));
                if (match != null)
                {
                    match.ScorePerHour = scorePerHour;
                    return true;
                }
                RegisterProjectCore(key, displayName, null, scorePerHour);
                return true;
            }
        }

        public bool SetProjectCategory(string projectName, string category)
        {
            if (string.IsNullOrWhiteSpace(projectName)) return false;
            string displayName = NormalizeProjectName(projectName);
            string key = CreateProjectKey(displayName);
            lock (_gate)
            {
                var match = _projects.FirstOrDefault(p => ProjectKeysEqual(p.Key, key));
                if (match != null)
                {
                    match.Category = (category ?? "").Trim();
                    return true;
                }
                RegisterProjectCore(key, displayName, category);
                return true;
            }
        }

        /// <summary>
        /// Adds a closed work record that is independent from every live timer.
        /// A fresh timer identity lets manually entered records overlap records
        /// produced by other independent timers without weakening the per-timer
        /// overlap invariant used for automatic tracking.
        /// </summary>
        public ProjectWorkIntervalView AddManualInterval(
            string projectName,
            DateTime startUtc,
            DateTime endUtc)
        {
            string displayName = NormalizeProjectName(projectName);
            string key = CreateProjectKey(displayName);
            startUtc = NormalizeUtc(startUtc);
            endUtc = NormalizeUtc(endUtc);
            ValidateClosedIntervalRange(startUtc, endUtc, nameof(endUtc));

            lock (_gate)
            {
                Guid intervalId;
                do
                {
                    intervalId = Guid.NewGuid();
                }
                while (intervalId == Guid.Empty
                       || _intervals.Any(interval => interval.Id == intervalId));

                Guid timerSessionId;
                do
                {
                    timerSessionId = Guid.NewGuid();
                }
                while (timerSessionId == Guid.Empty
                       || _intervals.Any(interval =>
                           interval.TimerSessionId == timerSessionId));

                ProjectEntry project = RegisterProjectCore(key, displayName);
                var interval = new WorkIntervalEntry(
                    intervalId,
                    timerSessionId,
                    project.Key,
                    project.Name,
                    startUtc,
                    endUtc);
                _intervals.Add(interval);
                return ToView(interval);
            }
        }

        /// <summary>
        /// Replaces a closed record while preserving both its record and timer
        /// identities. Live/open records remain owned by timer reconciliation and
        /// therefore cannot be changed here. Failed edits have no side effects.
        /// </summary>
        public ProjectRecordMutationResult UpdateClosedInterval(
            Guid id,
            string projectName,
            DateTime startUtc,
            DateTime endUtc)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("A record id must be non-empty.", nameof(id));

            string displayName = NormalizeProjectName(projectName);
            string key = CreateProjectKey(displayName);
            startUtc = NormalizeUtc(startUtc);
            endUtc = NormalizeUtc(endUtc);
            ValidateClosedIntervalRange(startUtc, endUtc, nameof(endUtc));

            lock (_gate)
            {
                int index = _intervals.FindIndex(interval => interval.Id == id);
                if (index < 0)
                {
                    return new ProjectRecordMutationResult(
                        ProjectRecordMutationStatus.NotFound);
                }

                WorkIntervalEntry existing = _intervals[index];
                if (!existing.EndUtc.HasValue)
                {
                    return new ProjectRecordMutationResult(
                        ProjectRecordMutationStatus.OpenInterval);
                }

                bool overlaps = _intervals.Any(interval =>
                    interval.Id != id
                    && interval.TimerSessionId == existing.TimerSessionId
                    && interval.StartUtc < endUtc
                    && startUtc < (interval.EndUtc ?? DateTime.MaxValue));
                if (overlaps)
                {
                    return new ProjectRecordMutationResult(
                        ProjectRecordMutationStatus.Overlap);
                }

                ProjectEntry project = RegisterProjectCore(key, displayName);
                var replacement = new WorkIntervalEntry(
                    existing.Id,
                    existing.TimerSessionId,
                    project.Key,
                    project.Name,
                    startUtc,
                    endUtc);
                _intervals[index] = replacement;
                return new ProjectRecordMutationResult(
                    ProjectRecordMutationStatus.Success,
                    ToView(replacement));
            }
        }

        /// <summary>
        /// Permanently removes one closed record. Live/open records remain owned by
        /// timer reconciliation and cannot be deleted from the records editor.
        /// The project registry is intentionally retained for future timers and
        /// manual entries, even when its final record is removed.
        /// </summary>
        public ProjectRecordMutationResult DeleteClosedInterval(Guid id)
        {
            if (id == Guid.Empty)
                throw new ArgumentException("A record id must be non-empty.", nameof(id));

            lock (_gate)
            {
                int index = _intervals.FindIndex(interval => interval.Id == id);
                if (index < 0)
                {
                    return new ProjectRecordMutationResult(
                        ProjectRecordMutationStatus.NotFound);
                }

                WorkIntervalEntry existing = _intervals[index];
                if (!existing.EndUtc.HasValue)
                {
                    return new ProjectRecordMutationResult(
                        ProjectRecordMutationStatus.OpenInterval);
                }

                ProjectWorkIntervalView deleted = ToView(existing);
                _intervals.RemoveAt(index);
                return new ProjectRecordMutationResult(
                    ProjectRecordMutationStatus.Success,
                    deleted);
            }
        }

        /// <summary>
        /// Permanently deletes an entire project and all of its recorded intervals.
        /// If an active (open) interval is currently tracking this project, deletion is
        /// blocked with ActiveTimerRunning to prevent concurrent state corruption.
        /// </summary>
        public ProjectDeletionResult DeleteProject(string projectName)
        {
            if (string.IsNullOrWhiteSpace(projectName))
                return new ProjectDeletionResult(ProjectDeletionStatus.NotFound, projectName ?? string.Empty, 0);

            string displayName = NormalizeProjectName(projectName);
            string key = CreateProjectKey(displayName);

            lock (_gate)
            {
                int projectIndex = _projects.FindIndex(p => string.Equals(p.Key, key, StringComparison.OrdinalIgnoreCase));
                bool hasIntervals = _intervals.Any(i => string.Equals(i.ProjectKey, key, StringComparison.OrdinalIgnoreCase));

                if (projectIndex < 0 && !hasIntervals)
                {
                    return new ProjectDeletionResult(ProjectDeletionStatus.NotFound, displayName, 0);
                }

                bool hasOpenInterval = _intervals.Any(i => string.Equals(i.ProjectKey, key, StringComparison.OrdinalIgnoreCase) && !i.EndUtc.HasValue);
                if (hasOpenInterval)
                {
                    return new ProjectDeletionResult(ProjectDeletionStatus.ActiveTimerRunning, displayName, 0);
                }

                int removedCount = _intervals.RemoveAll(i => string.Equals(i.ProjectKey, key, StringComparison.OrdinalIgnoreCase));
                if (projectIndex >= 0)
                {
                    _projects.RemoveAt(projectIndex);
                }

                return new ProjectDeletionResult(ProjectDeletionStatus.Success, displayName, removedCount);
            }
        }

        /// <summary>
        /// Starts tracking a project for a timer. Repeating the same project is
        /// idempotent. Switching projects closes the previous interval and opens
        /// the replacement at the exact same timestamp.
        /// </summary>
        public ProjectTrackingChange StartTracking(
            Guid timerSessionId,
            string projectName,
            DateTime utcNow)
        {
            ValidateTimerId(timerSessionId);
            string displayName = NormalizeProjectName(projectName);
            string key = CreateProjectKey(displayName);
            utcNow = NormalizeUtc(utcNow);

            lock (_gate)
            {
                WorkIntervalEntry? current = FindOpenIntervalCore(timerSessionId);
                if (current != null && ProjectKeysEqual(current.ProjectKey, key))
                    return ProjectTrackingChange.NoChange;

                ProjectEntry project = RegisterProjectCore(key, displayName);
                DateTime transitionUtc;
                if (current != null)
                {
                    transitionUtc = CloseIntervalCore(current, utcNow);
                }
                else
                {
                    transitionUtc = ClampNewIntervalStartCore(timerSessionId, utcNow);
                }

                _intervals.Add(new WorkIntervalEntry(
                    Guid.NewGuid(),
                    timerSessionId,
                    project.Key,
                    project.Name,
                    transitionUtc,
                    endUtc: null));

                return current == null
                    ? ProjectTrackingChange.Started
                    : ProjectTrackingChange.Switched;
            }
        }

        public bool StopTracking(Guid timerSessionId, DateTime utcNow)
        {
            ValidateTimerId(timerSessionId);
            utcNow = NormalizeUtc(utcNow);

            lock (_gate)
            {
                WorkIntervalEntry? current = FindOpenIntervalCore(timerSessionId);
                if (current == null)
                    return false;

                CloseIntervalCore(current, utcNow);
                return true;
            }
        }

        /// <summary>
        /// Discards the active open interval or the most recent closed interval for
        /// the specified timer session without saving its time to project records.
        /// </summary>
        public bool DiscardRecentInterval(Guid timerSessionId)
        {
            ValidateTimerId(timerSessionId);
            lock (_gate)
            {
                // First check for an open/active interval
                int openIndex = _intervals.FindIndex(interval =>
                    interval.TimerSessionId == timerSessionId && interval.EndUtc == null);
                if (openIndex >= 0)
                {
                    _intervals.RemoveAt(openIndex);
                    return true;
                }

                // If no open interval, find the most recent closed interval for this timer
                int recentIndex = -1;
                DateTime latestTime = DateTime.MinValue;
                for (int i = 0; i < _intervals.Count; i++)
                {
                    var interval = _intervals[i];
                    if (interval.TimerSessionId == timerSessionId && interval.EndUtc.HasValue)
                    {
                        if (interval.EndUtc.Value > latestTime)
                        {
                            latestTime = interval.EndUtc.Value;
                            recentIndex = i;
                        }
                    }
                }

                if (recentIndex >= 0)
                {
                    _intervals.RemoveAt(recentIndex);
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Updates the start timestamp of the active (or most recent) work interval for
        /// the specified timer session, keeping project records aligned with manual adjustments.
        /// </summary>
        public bool UpdateActiveIntervalStartTime(Guid timerSessionId, DateTime newStartUtc)
        {
            ValidateTimerId(timerSessionId);
            newStartUtc = NormalizeUtc(newStartUtc);

            lock (_gate)
            {
                WorkIntervalEntry? current = FindOpenIntervalCore(timerSessionId);
                if (current != null)
                {
                    current.StartUtc = newStartUtc;
                    return true;
                }

                // If paused, check the most recent closed interval
                WorkIntervalEntry? mostRecent = null;
                DateTime latestEnd = DateTime.MinValue;
                foreach (var interval in _intervals)
                {
                    if (interval.TimerSessionId == timerSessionId && interval.EndUtc.HasValue)
                    {
                        if (interval.EndUtc.Value > latestEnd)
                        {
                            latestEnd = interval.EndUtc.Value;
                            mostRecent = interval;
                        }
                    }
                }

                if (mostRecent != null && mostRecent.EndUtc.HasValue && newStartUtc < mostRecent.EndUtc.Value)
                {
                    mostRecent.StartUtc = newStartUtc;
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Makes open history agree with restored timer state. Stale intervals
        /// are closed, missing intervals for named running timers are started,
        /// and a changed timer name is treated as an exact-timestamp switch.
        /// </summary>
        public bool Reconcile(
            IEnumerable<ProjectTimerState> timerStates,
            DateTime utcNow)
        {
            ArgumentNullException.ThrowIfNull(timerStates);
            utcNow = NormalizeUtc(utcNow);

            var states = timerStates.ToArray();
            if (states.Any(state => state == null))
                throw new ArgumentException("Timer state cannot be null.", nameof(timerStates));
            if (states.Any(state => state.TimerSessionId == Guid.Empty))
                throw new ArgumentException("Timer state ids must be non-empty.", nameof(timerStates));
            if (states.Select(state => state.TimerSessionId).Distinct().Count() != states.Length)
                throw new ArgumentException("Timer state ids must be unique.", nameof(timerStates));

            lock (_gate)
            {
                bool changed = false;
                var byId = states.ToDictionary(state => state.TimerSessionId);

                WorkIntervalEntry[] intervalsToClose = _intervals
                    .Where(item => item.EndUtc == null)
                    .Where(interval =>
                    {
                        return !byId.TryGetValue(interval.TimerSessionId, out var state)
                            || !state.IsRunning
                            || !TryNormalizeProjectName(state.ProjectName, out string? displayName)
                            || !ProjectKeysEqual(interval.ProjectKey, CreateProjectKey(displayName!));
                    })
                    .ToArray();

                // Close stale intervals first. A running timer whose project was
                // renamed will be reopened below at precisely the same instant.
                var transitionTimes = new Dictionary<Guid, DateTime>();
                foreach (WorkIntervalEntry interval in intervalsToClose)
                {
                    transitionTimes[interval.TimerSessionId] =
                        CloseIntervalCore(interval, utcNow);
                    changed = true;
                }

                foreach (ProjectTimerState state in states)
                {
                    if (!state.IsRunning
                        || !TryNormalizeProjectName(state.ProjectName, out string? displayName))
                    {
                        continue;
                    }

                    string key = CreateProjectKey(displayName!);
                    ProjectEntry project = RegisterProjectCore(key, displayName!);
                    WorkIntervalEntry? current = FindOpenIntervalCore(state.TimerSessionId);
                    if (current != null)
                        continue;

                    DateTime startUtc = transitionTimes.TryGetValue(
                        state.TimerSessionId,
                        out DateTime transitionUtc)
                            ? transitionUtc
                            : ClampNewIntervalStartCore(state.TimerSessionId, utcNow);

                    _intervals.Add(new WorkIntervalEntry(
                        Guid.NewGuid(),
                        state.TimerSessionId,
                        project.Key,
                        project.Name,
                        startUtc,
                        endUtc: null));
                    changed = true;
                }

                return changed;
            }
        }

        public ProjectWorkIntervalView? GetOpenInterval(Guid timerSessionId)
        {
            ValidateTimerId(timerSessionId);
            lock (_gate)
            {
                WorkIntervalEntry? interval = FindOpenIntervalCore(timerSessionId);
                return interval == null ? null : ToView(interval);
            }
        }

        public IReadOnlyList<ProjectWorkIntervalView> GetIntervalsForTimer(Guid timerSessionId)
        {
            ValidateTimerId(timerSessionId);
            lock (_gate)
            {
                return _intervals
                    .Where(i => i.TimerSessionId == timerSessionId)
                    .OrderBy(i => i.StartUtc)
                    .ThenBy(i => i.Id)
                    .Select(ToView)
                    .ToList();
            }
        }

        /// <summary>
        /// Adjusts the recorded time intervals for a timer session by a given delta.
        /// If delta > 0, adds time to the most recent interval (or creates one if none exists).
        /// If delta < 0, reduces intervals starting from the most recent backwards, deleting
        /// intervals that are completely eliminated and trimming the last partially reduced interval.
        /// </summary>
        public void AdjustIntervalsForTimer(
            Guid timerSessionId,
            TimeSpan delta,
            string projectName,
            DateTime utcNow,
            bool isRunning)
        {
            ValidateTimerId(timerSessionId);
            utcNow = NormalizeUtc(utcNow);
            if (delta == TimeSpan.Zero) return;

            lock (_gate)
            {
                var timerIntervals = _intervals
                    .Where(i => i.TimerSessionId == timerSessionId)
                    .OrderBy(i => i.StartUtc)
                    .ToList();

                if (delta > TimeSpan.Zero)
                {
                    // Adding time
                    if (timerIntervals.Count > 0)
                    {
                        var latest = timerIntervals.Last();
                        TimeSpan remaining = delta;

                        // Step 1: Forward addition on latest interval up to utcNow (if closed)
                        if (latest.EndUtc.HasValue)
                        {
                            if (latest.EndUtc.Value < utcNow)
                            {
                                TimeSpan forwardAvailable = utcNow - latest.EndUtc.Value;
                                TimeSpan forwardAdd = remaining < forwardAvailable ? remaining : forwardAvailable;
                                latest.EndUtc = latest.EndUtc.Value + forwardAdd;
                                remaining -= forwardAdd;
                            }
                        }

                        // Step 2: If added time exceeds current time, add remaining backward into previous gaps and start
                        if (remaining > TimeSpan.Zero)
                        {
                            for (int i = timerIntervals.Count - 1; i >= 0 && remaining > TimeSpan.Zero; i--)
                            {
                                var current = timerIntervals[i];
                                if (i > 0)
                                {
                                    var prev = timerIntervals[i - 1];
                                    DateTime prevEnd = prev.EndUtc ?? utcNow;
                                    if (current.StartUtc > prevEnd)
                                    {
                                        TimeSpan gap = current.StartUtc - prevEnd;
                                        TimeSpan backwardAdd = remaining < gap ? remaining : gap;
                                        current.StartUtc -= backwardAdd;
                                        remaining -= backwardAdd;
                                    }
                                }
                                else
                                {
                                    // Earliest interval: no previous interval blocking, add all remaining time before start
                                    current.StartUtc -= remaining;
                                    remaining = TimeSpan.Zero;
                                    break;
                                }
                            }
                        }

                        // Step 3: Merge contiguous (connected) or overlapping intervals for this timer session
                        MergeConnectedIntervalsCore(timerSessionId);
                    }
                    else
                    {
                        // No intervals exist for this timer yet: create one
                        if (TryNormalizeProjectName(projectName, out string? displayName))
                        {
                            string key = CreateProjectKey(displayName!);
                            ProjectEntry project = RegisterProjectCore(key, displayName!);
                            DateTime start = utcNow - delta;
                            if (isRunning)
                            {
                                _intervals.Add(new WorkIntervalEntry(
                                    Guid.NewGuid(),
                                    timerSessionId,
                                    project.Key,
                                    project.Name,
                                    start,
                                    endUtc: null));
                            }
                            else
                            {
                                _intervals.Add(new WorkIntervalEntry(
                                    Guid.NewGuid(),
                                    timerSessionId,
                                    project.Key,
                                    project.Name,
                                    start,
                                    endUtc: utcNow));
                            }
                        }
                    }
                }
                else
                {
                    // Reducing time: eliminate from the end
                    TimeSpan toReduce = -delta;
                    bool hadOpenInterval = false;

                    for (int i = timerIntervals.Count - 1; i >= 0 && toReduce > TimeSpan.Zero; i--)
                    {
                        var interval = timerIntervals[i];
                        bool isOpen = interval.EndUtc == null;
                        if (isOpen) hadOpenInterval = true;

                        DateTime effectiveEnd = interval.EndUtc ?? utcNow;
                        TimeSpan duration = effectiveEnd - interval.StartUtc;
                        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;

                        if (duration <= toReduce)
                        {
                            _intervals.Remove(interval);
                            toReduce -= duration;
                        }
                        else
                        {
                            // Partially reduce interval from end
                            if (interval.EndUtc.HasValue)
                            {
                                interval.EndUtc = interval.EndUtc.Value - toReduce;
                            }
                            else
                            {
                                // Shorten open interval by pushing StartUtc forward
                                interval.StartUtc = interval.StartUtc + toReduce;
                            }
                            toReduce = TimeSpan.Zero;
                            break;
                        }
                    }

                    // If an open interval was eliminated but the timer is still running,
                    // start a new open interval at utcNow so future tracking continues.
                    if (isRunning && hadOpenInterval && !_intervals.Any(i => i.TimerSessionId == timerSessionId && i.EndUtc == null))
                    {
                        if (TryNormalizeProjectName(projectName, out string? displayName))
                        {
                            string key = CreateProjectKey(displayName!);
                            ProjectEntry project = RegisterProjectCore(key, displayName!);
                            _intervals.Add(new WorkIntervalEntry(
                                Guid.NewGuid(),
                                timerSessionId,
                                project.Key,
                                project.Name,
                                utcNow,
                                endUtc: null));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Updates the start timestamp of the most recent interval for this timer session.
        /// </summary>
        public bool UpdateLatestIntervalStartTime(Guid timerSessionId, DateTime newStartUtc)
        {
            ValidateTimerId(timerSessionId);
            newStartUtc = NormalizeUtc(newStartUtc);

            lock (_gate)
            {
                var timerIntervals = _intervals
                    .Where(i => i.TimerSessionId == timerSessionId)
                    .OrderBy(i => i.StartUtc)
                    .ToList();

                if (timerIntervals.Count == 0) return false;
                var latest = timerIntervals.Last();

                if (latest.EndUtc.HasValue && newStartUtc >= latest.EndUtc.Value)
                {
                    return false;
                }

                latest.StartUtc = newStartUtc;
                return true;
            }
        }

        /// <summary>
        /// Discards all intervals associated with the specified timer session.
        /// </summary>
        public int DiscardAllIntervalsForTimer(Guid timerSessionId)
        {
            ValidateTimerId(timerSessionId);
            lock (_gate)
            {
                return _intervals.RemoveAll(i => i.TimerSessionId == timerSessionId);
            }
        }

        /// <summary>
        /// Captures a point-in-time snapshot of all intervals belonging to a specific timer session.
        /// </summary>
        public List<WorkIntervalDocumentEntry> CaptureTimerIntervalsSnapshot(Guid timerSessionId)
        {
            ValidateTimerId(timerSessionId);
            lock (_gate)
            {
                return _intervals
                    .Where(i => i.TimerSessionId == timerSessionId)
                    .OrderBy(i => i.StartUtc)
                    .Select(i => new WorkIntervalDocumentEntry
                    {
                        Id = i.Id,
                        TimerSessionId = i.TimerSessionId,
                        ProjectKey = i.ProjectKey,
                        ProjectName = i.ProjectName,
                        StartUtc = i.StartUtc,
                        EndUtc = i.EndUtc
                    })
                    .ToList();
            }
        }

        /// <summary>
        /// Restores a previously captured snapshot of intervals for a specific timer session,
        /// replacing all existing intervals for that session.
        /// </summary>
        public void RestoreTimerIntervalsSnapshot(Guid timerSessionId, IEnumerable<WorkIntervalDocumentEntry> snapshot)
        {
            ValidateTimerId(timerSessionId);
            ArgumentNullException.ThrowIfNull(snapshot);

            lock (_gate)
            {
                _intervals.RemoveAll(i => i.TimerSessionId == timerSessionId);

                foreach (var entry in snapshot)
                {
                    RegisterProjectCore(entry.ProjectKey, entry.ProjectName);
                    _intervals.Add(new WorkIntervalEntry(
                        entry.Id,
                        entry.TimerSessionId,
                        entry.ProjectKey,
                        entry.ProjectName,
                        NormalizeUtc(entry.StartUtc),
                        entry.EndUtc.HasValue ? NormalizeUtc(entry.EndUtc.Value) : null));
                }
            }
        }

        private void MergeConnectedIntervalsCore(Guid timerSessionId)
        {
            bool mergedAny;
            do
            {
                mergedAny = false;
                var timerIntervals = _intervals
                    .Where(i => i.TimerSessionId == timerSessionId)
                    .OrderBy(i => i.StartUtc)
                    .ToList();

                for (int i = 0; i < timerIntervals.Count - 1; i++)
                {
                    var first = timerIntervals[i];
                    var second = timerIntervals[i + 1];

                    if (!first.EndUtc.HasValue)
                    {
                        _intervals.Remove(second);
                        mergedAny = true;
                        break;
                    }

                    if (second.StartUtc <= first.EndUtc.Value)
                    {
                        if (second.EndUtc.HasValue)
                        {
                            first.EndUtc = second.EndUtc.Value > first.EndUtc.Value
                                ? second.EndUtc.Value
                                : first.EndUtc.Value;
                        }
                        else
                        {
                            first.EndUtc = null;
                        }

                        if (!string.IsNullOrWhiteSpace(second.ProjectName))
                        {
                            first.ProjectKey = second.ProjectKey;
                            first.ProjectName = second.ProjectName;
                        }

                        _intervals.Remove(second);
                        mergedAny = true;
                        break;
                    }
                }
            } while (mergedAny);
        }

        public FocusPauseRecord RecordFocusPause(
            Guid timerSessionId,
            string projectName,
            DateTime pauseStartUtc,
            DateTime resumeUtc,
            FocusPauseReason reason,
            string? note = null)
        {
            ValidateTimerId(timerSessionId);
            string displayName = NormalizeProjectName(projectName);
            pauseStartUtc = NormalizeUtc(pauseStartUtc);
            resumeUtc = NormalizeUtc(resumeUtc);

            var record = new FocusPauseRecord(
                Guid.NewGuid(),
                timerSessionId,
                displayName,
                pauseStartUtc,
                resumeUtc,
                reason,
                note);

            lock (_gate)
            {
                _focusPauses.Add(record);
                return record;
            }
        }

        public ProjectHistoryView CreateView(DateTime asOfUtc)
        {
            asOfUtc = NormalizeUtc(asOfUtc);
            lock (_gate)
            {
                return new ProjectHistoryView(
                    asOfUtc,
                    _projects.Select(project => new ProjectInfoView(project.Key, project.Name, project.Category, project.ScorePerHour)),
                    _intervals
                        .OrderBy(interval => interval.StartUtc)
                        .ThenBy(interval => interval.Id)
                        .Select(ToView),
                    _focusPauses
                        .OrderBy(p => p.PauseStartUtc)
                        .ThenBy(p => p.Id));
            }
        }

        internal ProjectHistoryDocument CreateDocument(DateTime savedAtUtc)
        {
            savedAtUtc = NormalizeUtc(savedAtUtc);
            lock (_gate)
            {
                return new ProjectHistoryDocument
                {
                    Version = ProjectTimeStore.CurrentVersion,
                    SavedAtUtc = savedAtUtc,
                    Projects = _projects.Select(project => new ProjectDocumentEntry
                    {
                        Key = project.Key,
                        Name = project.Name,
                        Category = project.Category,
                        ScorePerHour = project.ScorePerHour
                    }).ToList(),
                    Intervals = _intervals.Select(interval => new WorkIntervalDocumentEntry
                    {
                        Id = interval.Id,
                        TimerSessionId = interval.TimerSessionId,
                        ProjectKey = interval.ProjectKey,
                        ProjectName = interval.ProjectName,
                        StartUtc = interval.StartUtc,
                        EndUtc = interval.EndUtc
                    }).ToList(),
                    FocusPauses = _focusPauses.Select(pause => new FocusPauseDocumentEntry
                    {
                        Id = pause.Id,
                        TimerSessionId = pause.TimerSessionId,
                        ProjectName = pause.ProjectName,
                        PauseStartUtc = pause.PauseStartUtc,
                        ResumeUtc = pause.ResumeUtc,
                        Reason = pause.Reason.ToString(),
                        Note = pause.Note
                    }).ToList()
                };
            }
        }

        internal static ProjectTimeHistory FromDocument(ProjectHistoryDocument document)
        {
            ProjectTimeStore.Validate(document);
            var result = new ProjectTimeHistory();

            foreach (ProjectDocumentEntry project in document.Projects)
            {
                result._projects.Add(new ProjectEntry(
                    project.Key,
                    project.Name,
                    project.Category ?? "",
                    project.ScorePerHour > 0 ? project.ScorePerHour : (project.ScorePerHour == 0 ? 0 : 1.0)));
            }

            foreach (WorkIntervalDocumentEntry interval in document.Intervals)
            {
                result._intervals.Add(new WorkIntervalEntry(
                    interval.Id,
                    interval.TimerSessionId,
                    interval.ProjectKey,
                    interval.ProjectName,
                    NormalizeUtc(interval.StartUtc),
                    interval.EndUtc.HasValue
                        ? NormalizeUtc(interval.EndUtc.Value)
                        : null));
            }

            if (document.FocusPauses != null)
            {
                foreach (FocusPauseDocumentEntry entry in document.FocusPauses)
                {
                    if (!Enum.TryParse<FocusPauseReason>(entry.Reason, true, out var reason))
                    {
                        reason = FocusPauseReason.Distraction;
                    }

                    result._focusPauses.Add(new FocusPauseRecord(
                        entry.Id == Guid.Empty ? Guid.NewGuid() : entry.Id,
                        entry.TimerSessionId,
                        string.IsNullOrWhiteSpace(entry.ProjectName) ? "General" : entry.ProjectName.Trim(),
                        NormalizeUtc(entry.PauseStartUtc),
                        NormalizeUtc(entry.ResumeUtc),
                        reason,
                        entry.Note));
                }
            }

            return result;
        }

        internal static string NormalizeProjectName(string projectName)
        {
            if (projectName == null)
                throw new ArgumentNullException(nameof(projectName));

            string normalized = projectName.Trim();
            if (normalized.Length == 0)
                throw new ArgumentException("A project name cannot be empty.", nameof(projectName));
            if (normalized.Length > MaximumProjectNameLength)
                throw new ArgumentException(
                    $"A project name cannot exceed {MaximumProjectNameLength} characters.",
                    nameof(projectName));
            if (normalized.Any(char.IsControl))
                throw new ArgumentException("A project name cannot contain control characters.", nameof(projectName));

            return normalized;
        }

        internal static bool TryNormalizeProjectName(
            string? projectName,
            out string? normalized)
        {
            normalized = null;
            if (projectName == null)
                return false;

            try
            {
                normalized = NormalizeProjectName(projectName);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        internal static string CreateProjectKey(string normalizedProjectName)
            => normalizedProjectName.ToUpperInvariant();

        internal static DateTime NormalizeUtc(DateTime value)
        {
            if (value == default)
                throw new ArgumentOutOfRangeException(nameof(value), "A timestamp is required.");

            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private static bool ProjectKeysEqual(string left, string right)
            => StringComparer.OrdinalIgnoreCase.Equals(left, right);

        private ProjectEntry RegisterProjectCore(string key, string displayName, string? category = null, double? scorePerHour = null)
        {
            ProjectEntry? existing = _projects.FirstOrDefault(
                project => ProjectKeysEqual(project.Key, key));
            if (existing != null)
            {
                if (!string.IsNullOrWhiteSpace(category))
                {
                    existing.Category = category.Trim();
                }
                if (scorePerHour.HasValue && !double.IsNaN(scorePerHour.Value) && !double.IsInfinity(scorePerHour.Value) && scorePerHour.Value >= 0)
                {
                    existing.ScorePerHour = scorePerHour.Value;
                }
                return existing;
            }

            double score = (scorePerHour.HasValue && !double.IsNaN(scorePerHour.Value) && !double.IsInfinity(scorePerHour.Value) && scorePerHour.Value >= 0)
                ? scorePerHour.Value
                : 1.0;
            var project = new ProjectEntry(key, displayName, (category ?? "").Trim(), score);
            _projects.Add(project);
            return project;
        }

        private WorkIntervalEntry? FindOpenIntervalCore(Guid timerSessionId)
            => _intervals.FirstOrDefault(interval =>
                interval.TimerSessionId == timerSessionId && interval.EndUtc == null);

        private DateTime ClampNewIntervalStartCore(Guid timerSessionId, DateTime requestedUtc)
        {
            DateTime latestEnd = _intervals
                .Where(interval => interval.TimerSessionId == timerSessionId)
                .Where(interval => interval.EndUtc.HasValue)
                .Select(interval => interval.EndUtc!.Value)
                .DefaultIfEmpty(DateTime.MinValue)
                .Max();

            return requestedUtc < latestEnd ? latestEnd : requestedUtc;
        }

        private static DateTime CloseIntervalCore(WorkIntervalEntry interval, DateTime utcNow)
        {
            DateTime effectiveEnd = utcNow < interval.StartUtc
                ? interval.StartUtc
                : utcNow;
            interval.EndUtc = effectiveEnd;
            return effectiveEnd;
        }

        private static void ValidateClosedIntervalRange(
            DateTime startUtc,
            DateTime endUtc,
            string endParameterName)
        {
            if (endUtc <= startUtc)
            {
                throw new ArgumentException(
                    "A closed work interval must end after it starts.",
                    endParameterName);
            }
        }

        private static ProjectWorkIntervalView ToView(WorkIntervalEntry interval)
            => new(
                interval.Id,
                interval.TimerSessionId,
                interval.ProjectKey,
                interval.ProjectName,
                interval.StartUtc,
                interval.EndUtc);

        private static void ValidateTimerId(Guid timerSessionId)
        {
            if (timerSessionId == Guid.Empty)
                throw new ArgumentException("A timer id must be non-empty.", nameof(timerSessionId));
        }

        private sealed class ProjectEntry
        {
            public ProjectEntry(string key, string name, string category = "", double scorePerHour = 1.0)
            {
                Key = key;
                Name = name;
                Category = category;
                ScorePerHour = (double.IsNaN(scorePerHour) || double.IsInfinity(scorePerHour) || scorePerHour < 0) ? 1.0 : scorePerHour;
            }

            public string Key { get; }
            public string Name { get; }
            public string Category { get; set; }
            public double ScorePerHour { get; set; } = 1.0;
        }

        private sealed class WorkIntervalEntry
        {
            public WorkIntervalEntry(
                Guid id,
                Guid timerSessionId,
                string projectKey,
                string projectName,
                DateTime startUtc,
                DateTime? endUtc)
            {
                Id = id;
                TimerSessionId = timerSessionId;
                ProjectKey = projectKey;
                ProjectName = projectName;
                StartUtc = startUtc;
                EndUtc = endUtc;
            }

            public Guid Id { get; }
            public Guid TimerSessionId { get; }
            public string ProjectKey { get; set; }
            public string ProjectName { get; set; }
            public DateTime StartUtc { get; set; }
            public DateTime? EndUtc { get; set; }
        }
    }
}
