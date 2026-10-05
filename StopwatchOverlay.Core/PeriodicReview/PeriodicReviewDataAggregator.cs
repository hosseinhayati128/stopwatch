using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StopwatchOverlay.ActivityWatch;

namespace StopwatchOverlay.PeriodicReview;

public sealed record RawActivityEvent(
    DateTimeOffset StartUtc,
    DateTimeOffset EndUtc,
    string App,
    string Details,
    string Type);

public sealed class PeriodicReviewActivityItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTime StartLocal { get; set; }
    public DateTime EndLocal { get; set; }
    public DateTime StartUtc => StartLocal.ToUniversalTime();
    public DateTime EndUtc => EndLocal.ToUniversalTime();
    public TimeSpan Duration => EndLocal > StartLocal ? EndLocal - StartLocal : TimeSpan.Zero;
    public string App { get; set; } = "";
    public string Details { get; set; } = "";
    public string Type { get; set; } = "App"; // "App", "Web", "Idle"
    public bool IsIdle => string.Equals(Type, "Idle", StringComparison.OrdinalIgnoreCase);

    public bool IsSelected { get; set; }
    public List<string> AssignedProjects { get; set; } = [];

    public string TimeDisplay => $"{StartLocal:HH:mm} – {EndLocal:HH:mm}";
    public string DurationDisplay
    {
        get
        {
            var d = Duration;
            int totalMin = (int)Math.Round(d.TotalMinutes);
            if (totalMin <= 0) return $"{Math.Max(1, (int)d.TotalSeconds)}s";
            int h = totalMin / 60;
            int m = totalMin % 60;
            return h > 0 ? (m > 0 ? $"{h}h {m}m" : $"{h}h") : $"{m}m";
        }
    }
}

public sealed class PeriodicReviewStopwatchSlot
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid? ExistingIntervalId { get; set; }
    public Guid? SourceActivityId { get; set; }
    public string? SourceActivityName { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string? OriginalProjectName { get; set; }
    public string? SelectedProjectName { get; set; }
    public bool IsTracked { get; set; }
    public bool IsOpenTimer { get; set; }
    public bool IsManuallyAdded { get; set; }

    public DateTime StartLocal => StartUtc.ToLocalTime();
    public DateTime EndLocal => EndUtc.ToLocalTime();
    public TimeSpan Duration => EndUtc > StartUtc ? EndUtc - StartUtc : TimeSpan.Zero;

    public string TimeDisplay => $"{StartLocal:HH:mm} – {EndLocal:HH:mm}";
    public string DurationDisplay
    {
        get
        {
            var d = Duration;
            int totalMin = (int)Math.Round(d.TotalMinutes);
            if (totalMin <= 0) return $"{Math.Max(1, (int)d.TotalSeconds)}s";
            int h = totalMin / 60;
            int m = totalMin % 60;
            return h > 0 ? (m > 0 ? $"{h}h {m}m" : $"{h}h") : $"{m}m";
        }
    }

    public string StatusBadge => IsTracked
        ? (IsOpenTimer ? "⏱️ Running" : "⏱️ Tracked")
        : "⚪ Untracked / Off";

    public bool HasChanged => !string.Equals(OriginalProjectName?.Trim() ?? "", SelectedProjectName?.Trim() ?? "", StringComparison.OrdinalIgnoreCase)
                              || (IsManuallyAdded && !string.IsNullOrWhiteSpace(SelectedProjectName));
}

public sealed class ActivitySummaryGroup
{
    public string App { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "App"; // "App", "Web", "Idle"
    public TimeSpan TotalDuration { get; set; }
    public double Percentage { get; set; } // e.g. 35.0
    public int EventCount { get; set; }

    public bool IsIdle => string.Equals(Category, "Idle", StringComparison.OrdinalIgnoreCase);
    public bool IsOther => string.Equals(Category, "Other", StringComparison.OrdinalIgnoreCase);
    public List<ActivitySummaryGroup> SubItems { get; set; } = [];

    public string DurationDisplay
    {
        get
        {
            int totalMin = (int)Math.Round(TotalDuration.TotalMinutes);
            if (totalMin <= 0) return $"{Math.Max(1, (int)TotalDuration.TotalSeconds)}s";
            int h = totalMin / 60;
            int m = totalMin % 60;
            return h > 0 ? (m > 0 ? $"{h}h {m}m" : $"{h}h") : $"{m}m";
        }
    }

    public string PercentageDisplay => $"{Percentage:0.#}%";
}

public sealed class ReviewProjectSelectionItem
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string ProjectName { get; set; } = "";
    public bool IsBreak { get; set; }
    public int SelectionOrder { get; set; } // 1, 2, 3... 0 if unselected
    public bool IsSelected => SelectionOrder > 0;
    public double AllocatedMinutes { get; set; }
    public DateTime? StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public bool IsExpanded { get; set; }

    public DateTime CalculatedStartUtc
    {
        get => StartUtc ?? DateTime.MinValue;
        set => StartUtc = value;
    }

    public DateTime CalculatedEndUtc
    {
        get => EndUtc ?? DateTime.MinValue;
        set => EndUtc = value;
    }

    public string DisplayName => IsBreak ? "☕ Break / Empty" : ProjectName;
}

public sealed class TimelineSlot
{
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public ReviewProjectSelectionItem? Item { get; set; }
    public bool IsUnallocated => Item == null;
    public bool IsBreak => Item?.IsBreak == true;
    public string DisplayName => Item != null ? Item.DisplayName : "⚪ Unallocated (No project)";
    public TimeSpan Duration => EndUtc > StartUtc ? EndUtc - StartUtc : TimeSpan.Zero;
    public double DurationMinutes => Duration.TotalMinutes;

    public DateTime StartLocal => StartUtc.ToLocalTime();
    public DateTime EndLocal => EndUtc.ToLocalTime();
    public string TimeDisplay => $"{StartLocal:HH:mm} – {EndLocal:HH:mm}";
}

public sealed class PeriodicReviewModel
{
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public List<PeriodicReviewActivityItem> Activities { get; set; } = [];
    public List<PeriodicReviewStopwatchSlot> StopwatchSlots { get; set; } = [];
    public List<ActivitySummaryGroup> ActivitySummaries { get; set; } = [];
    public List<ActivitySummaryGroup> RawActivitySummaries { get; set; } = [];
    public List<string> KnownProjects { get; set; } = [];
    public bool ActivityWatchAvailable { get; init; }
    public string? ActivityWatchMessage { get; init; }

    public DateTime StartLocal => StartUtc.ToLocalTime();
    public DateTime EndLocal => EndUtc.ToLocalTime();
    public TimeSpan TotalDuration => EndUtc > StartUtc ? EndUtc - StartUtc : TimeSpan.Zero;
    public string PeriodDisplay => $"{StartLocal:HH:mm} – {EndLocal:HH:mm} ({Math.Max(1, (int)Math.Round(TotalDuration.TotalMinutes))}m)";
}

public static class PeriodicReviewDataAggregator
{
    /// <summary>
    /// Filters out short blips (< minDurationSeconds) and smart-bridges interruptions
    /// where same app A was interrupted by brief activity B (<= minDurationSeconds) and returned to A.
    /// </summary>
    public static List<PeriodicReviewActivityItem> FilterAndBridgeActivities(
        IEnumerable<RawActivityEvent> events,
        int minDurationSeconds)
    {
        ArgumentNullException.ThrowIfNull(events);
        double threshold = Math.Max(1, minDurationSeconds);

        var list = events
            .OrderBy(e => e.StartUtc)
            .Where(e => e.EndUtc > e.StartUtc)
            .Select(e => new PeriodicReviewActivityItem
            {
                StartLocal = e.StartUtc.ToLocalTime().DateTime,
                EndLocal = e.EndUtc.ToLocalTime().DateTime,
                App = string.IsNullOrWhiteSpace(e.App) ? "Unknown" : e.App.Trim(),
                Details = e.Details?.Trim() ?? "",
                Type = e.Type
            })
            .ToList();

        if (list.Count == 0) return list;

        // Step 1: Merge contiguous intervals of the exact same app
        list = MergeConsecutiveSameApp(list);

        // Step 2: Linear smart bridging:
        // For each item, look ahead to see if same app resumes after brief interruptions (each <= threshold).
        var bridged = new List<PeriodicReviewActivityItem>(list.Count);
        int i = 0;
        while (i < list.Count)
        {
            var first = list[i];
            if (first.IsIdle)
            {
                bridged.Add(first);
                i++;
                continue;
            }

            int lookahead = i + 1;
            while (lookahead < list.Count)
            {
                int k = lookahead;
                bool canBridge = true;

                while (k < list.Count)
                {
                    var mid = list[k];
                    if (string.Equals(mid.App, first.App, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }

                    if (mid.Duration.TotalSeconds > threshold || mid.IsIdle)
                    {
                        canBridge = false;
                        break;
                    }

                    k++;
                }

                if (canBridge && k < list.Count && string.Equals(list[k].App, first.App, StringComparison.OrdinalIgnoreCase))
                {
                    var target = list[k];
                    if (target.EndLocal > first.EndLocal)
                    {
                        first.EndLocal = target.EndLocal;
                    }
                    if (string.IsNullOrWhiteSpace(first.Details) && !string.IsNullOrWhiteSpace(target.Details))
                    {
                        first.Details = target.Details;
                    }
                    lookahead = k + 1;
                }
                else
                {
                    break;
                }
            }

            bridged.Add(first);
            i = lookahead;
        }
        list = bridged;

        // Step 3: Merge any newly adjacent same app items
        list = MergeConsecutiveSameApp(list);

        // Step 4: Filter out standalone items with total duration < threshold
        return list.Where(item => item.Duration.TotalSeconds >= threshold).ToList();
    }

    private static List<PeriodicReviewActivityItem> MergeConsecutiveSameApp(List<PeriodicReviewActivityItem> items)
    {
        if (items.Count <= 1) return items;
        var merged = new List<PeriodicReviewActivityItem> { items[0] };

        for (int i = 1; i < items.Count; i++)
        {
            var prev = merged[^1];
            var curr = items[i];

            if (string.Equals(prev.App, curr.App, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(prev.Type, curr.Type, StringComparison.OrdinalIgnoreCase) &&
                (curr.StartLocal - prev.EndLocal).TotalSeconds <= 30 &&
                curr.StartLocal >= prev.StartLocal)
            {
                prev.EndLocal = curr.EndLocal > prev.EndLocal ? curr.EndLocal : prev.EndLocal;
                if (string.IsNullOrWhiteSpace(prev.Details) && !string.IsNullOrWhiteSpace(curr.Details))
                {
                    prev.Details = curr.Details;
                }
            }
            else
            {
                merged.Add(curr);
            }
        }

        return merged;
    }

    /// <summary>
    /// Groups raw ActivityWatch events by normalized App and Title/Details,
    /// combines repeated events (e.g. multiple visits to ChatGPT Chat A or Chat B),
    /// combines all idle events into a single entry, and calculates percentage shares.
    /// </summary>
    public static List<ActivitySummaryGroup> AggregateActivitiesSummary(
        IEnumerable<RawActivityEvent> events,
        TimeSpan totalPeriodDuration)
    {
        ArgumentNullException.ThrowIfNull(events);

        var list = events.ToList();
        if (list.Count == 0) return [];

        double totalPeriodSeconds = Math.Max(1, totalPeriodDuration.TotalSeconds);

        var groups = new Dictionary<string, (string App, string Title, string Category, TimeSpan Duration, int Count)>(StringComparer.OrdinalIgnoreCase);

        foreach (var evt in list)
        {
            TimeSpan duration = evt.EndUtc > evt.StartUtc ? evt.EndUtc - evt.StartUtc : TimeSpan.Zero;
            if (duration <= TimeSpan.Zero) continue;

            string app = evt.App?.Trim() ?? "";
            string title = evt.Details?.Trim() ?? "";
            string category = evt.Type ?? "App";

            if (string.Equals(category, "Idle", StringComparison.OrdinalIgnoreCase) ||
                app.StartsWith("Idle", StringComparison.OrdinalIgnoreCase))
            {
                app = "Idle / Away";
                title = "No user input (keyboard/mouse idle)";
                category = "Idle";
            }
            else
            {
                title = CleanActivityTitle(title, app);
                app = CleanAppName(app);
            }

            string groupKey = $"{category}:{app}::{title}";

            if (groups.TryGetValue(groupKey, out var existing))
            {
                groups[groupKey] = (existing.App, existing.Title, existing.Category, existing.Duration + duration, existing.Count + 1);
            }
            else
            {
                groups[groupKey] = (app, title, category, duration, 1);
            }
        }

        var result = new List<ActivitySummaryGroup>();
        foreach (var entry in groups.Values)
        {
            double pct = Math.Round((entry.Duration.TotalSeconds / totalPeriodSeconds) * 100.0, 1);
            result.Add(new ActivitySummaryGroup
            {
                App = entry.App,
                Title = string.IsNullOrWhiteSpace(entry.Title) ? entry.App : entry.Title,
                Category = entry.Category,
                TotalDuration = entry.Duration,
                Percentage = pct,
                EventCount = entry.Count
            });
        }

        return result.OrderByDescending(g => g.TotalDuration).ToList();
    }

    /// <summary>
    /// Filters activity summary groups by minimum percentage threshold, bundling all items below
    /// the threshold into a single "Others" group.
    /// </summary>
    public static List<ActivitySummaryGroup> FilterAndGroupActivitiesSummary(
        IReadOnlyList<ActivitySummaryGroup> rawGroups,
        TimeSpan totalPeriodDuration,
        double minPercentThreshold)
    {
        ArgumentNullException.ThrowIfNull(rawGroups);
        if (rawGroups.Count == 0) return [];

        if (minPercentThreshold <= 0.0)
        {
            return rawGroups.OrderByDescending(g => g.TotalDuration).ToList();
        }

        var sorted = rawGroups.OrderByDescending(g => g.TotalDuration).ToList();
        var topItems = new List<ActivitySummaryGroup>();
        var otherItems = new List<ActivitySummaryGroup>();

        foreach (var item in sorted)
        {
            if (item.Percentage >= minPercentThreshold)
            {
                topItems.Add(item);
            }
            else
            {
                otherItems.Add(item);
            }
        }

        // If no single activity met the threshold, retain at least the top activity so the user has context
        if (topItems.Count == 0 && otherItems.Count > 0)
        {
            topItems.Add(otherItems[0]);
            otherItems.RemoveAt(0);
        }

        if (otherItems.Count == 0)
        {
            return topItems;
        }

        long totalOtherTicks = otherItems.Sum(o => o.TotalDuration.Ticks);
        TimeSpan otherDuration = TimeSpan.FromTicks(totalOtherTicks);
        double totalPeriodSeconds = Math.Max(1, totalPeriodDuration.TotalSeconds);
        double otherPct = Math.Round((otherDuration.TotalSeconds / totalPeriodSeconds) * 100.0, 1);
        if (otherPct <= 0.0 && otherDuration.TotalSeconds > 0)
        {
            otherPct = 0.1;
        }

        var otherGroup = new ActivitySummaryGroup
        {
            App = "Others",
            Title = $"{otherItems.Count} minor {(otherItems.Count == 1 ? "activity" : "activities")} (< {minPercentThreshold:0.#}%)",
            Category = "Other",
            TotalDuration = otherDuration,
            Percentage = otherPct,
            EventCount = otherItems.Sum(o => o.EventCount),
            SubItems = otherItems.OrderByDescending(o => o.TotalDuration).ToList()
        };

        topItems.Add(otherGroup);
        return topItems;
    }

    public static string CleanActivityTitle(string title, string app)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        string clean = title.Trim();

        string[] suffixes = [
            " - Google Chrome",
            " - Microsoft Edge",
            " - Brave",
            " - Mozilla Firefox",
            " - Visual Studio Code",
            " - Telegram"
        ];

        foreach (var suffix in suffixes)
        {
            if (clean.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                clean = clean[..^suffix.Length].Trim();
            }
        }

        return clean;
    }

    public static string CleanAppName(string app)
    {
        if (string.IsNullOrWhiteSpace(app)) return "Unknown";
        string clean = app.Trim();
        if (clean.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            clean = clean[..^4];
        }
        return clean;
    }

    /// <summary>
    /// Computes chronological timeline intervals starting at startUtc based on the ordered list of items.
    /// If items have explicit StartUtc/EndUtc set, their positions are respected and gaps are handled.
    /// </summary>
    public static List<(DateTime StartUtc, DateTime EndUtc, ReviewProjectSelectionItem Item)> ComputeTimeline(
        DateTime startUtc,
        DateTime endUtc,
        IReadOnlyList<ReviewProjectSelectionItem> orderedItems)
    {
        var slots = BuildFullTimeline(startUtc, endUtc, orderedItems);
        return slots
            .Where(s => s.Item != null && s.DurationMinutes > 0)
            .Select(s => (s.StartUtc, s.EndUtc, s.Item!))
            .ToList();
    }

    internal static DateTime TruncateToMinute(DateTime dt)
    {
        return new DateTime(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, 0, dt.Kind);
    }

    /// <summary>
    /// Rebalances allocated minutes across items when an item's allocation is changed.
    /// If targetMinutes increases and exceeds available unallocated space, minutes are borrowed
    /// from other items (prioritizing the largest donor).
    /// </summary>
    public static void RebalanceAllocatedMinutes(
        IReadOnlyList<ReviewProjectSelectionItem> items,
        ReviewProjectSelectionItem changedItem,
        double targetMinutes,
        int totalPeriodMinutes)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(changedItem);
        if (totalPeriodMinutes <= 0 || items.Count == 0) return;

        double target = Math.Clamp(Math.Round(targetMinutes), 0, totalPeriodMinutes);
        double current = Math.Round(changedItem.AllocatedMinutes);
        double delta = target - current;
        if (Math.Abs(delta) < 0.001) return;

        if (delta > 0)
        {
            double totalAllocatedOthers = items
                .Where(it => it != changedItem)
                .Sum(it => Math.Round(it.AllocatedMinutes));
            double unallocatedGap = Math.Max(0, totalPeriodMinutes - (totalAllocatedOthers + current));

            if (delta <= unallocatedGap)
            {
                changedItem.AllocatedMinutes = target;
            }
            else
            {
                double excess = delta - unallocatedGap;

                while (excess > 0.001)
                {
                    var donor = items
                        .Where(it => it != changedItem && it.AllocatedMinutes > 0)
                        .OrderByDescending(it => it.AllocatedMinutes)
                        .FirstOrDefault();

                    if (donor == null)
                    {
                        target -= excess;
                        excess = 0;
                        break;
                    }

                    double deduct = Math.Min(excess, donor.AllocatedMinutes);
                    donor.AllocatedMinutes -= deduct;
                    excess -= deduct;
                }

                changedItem.AllocatedMinutes = target;
            }
        }
        else
        {
            changedItem.AllocatedMinutes = target;
        }
    }

    /// <summary>
    /// Repacks start and end times contiguously across items within [startUtc, endUtc],
    /// respecting existing offsets, available gaps, and item durations.
    /// </summary>
    public static void RepackTimelineIntervals(
        IReadOnlyList<ReviewProjectSelectionItem> items,
        DateTime startUtc,
        DateTime endUtc)
    {
        ArgumentNullException.ThrowIfNull(items);
        DateTime cursor = startUtc;

        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            TimeSpan dur = TimeSpan.FromMinutes(Math.Max(0, Math.Round(it.AllocatedMinutes)));

            DateTime start = it.StartUtc.HasValue && it.StartUtc.Value >= cursor
                ? it.StartUtc.Value
                : cursor;

            if (start > endUtc) start = endUtc;

            if (start + dur > endUtc)
            {
                start = endUtc - dur;
                if (start < cursor) start = cursor;
            }

            DateTime end = start + dur;
            if (end > endUtc) end = endUtc;

            it.StartUtc = start;
            it.EndUtc = end;

            cursor = end;
        }
    }

    /// <summary>
    /// Auto-selects projects from availableProjects that have tracked intervals in stopwatchSlots,
    /// adding them to selectedItems with proper selection order if not already selected.
    /// </summary>
    public static void AutoSelectStopwatchProjects(
        IReadOnlyList<ReviewProjectSelectionItem> availableProjects,
        IList<ReviewProjectSelectionItem> selectedItems,
        IEnumerable<PeriodicReviewStopwatchSlot>? stopwatchSlots)
    {
        if (stopwatchSlots == null || availableProjects == null || selectedItems == null) return;

        var trackedNames = stopwatchSlots
            .Where(s => s.IsTracked &&
                        !string.IsNullOrWhiteSpace(s.SelectedProjectName) &&
                        !string.Equals(s.SelectedProjectName, "(Untracked / Off)", StringComparison.OrdinalIgnoreCase))
            .Select(s => s.SelectedProjectName!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (trackedNames.Count == 0) return;

        foreach (var name in trackedNames)
        {
            var item = availableProjects.FirstOrDefault(p =>
                string.Equals(p.ProjectName, name, StringComparison.OrdinalIgnoreCase));

            if (item == null || item.IsSelected) continue;

            selectedItems.Add(item);
            item.SelectionOrder = selectedItems.Count;
        }
    }

    /// <summary>
    /// Pre-populates time allocations for selectedItems using actual tracked stopwatch interval durations and times.
    /// Items with tracked data receive their measured durations (clamped to period bounds).
    /// Any remaining time is split equally among selected items without tracked intervals.
    /// </summary>
    public static void PrePopulateStopwatchAllocations(
        IReadOnlyList<ReviewProjectSelectionItem> selectedItems,
        IEnumerable<PeriodicReviewStopwatchSlot>? stopwatchSlots,
        DateTime startUtc,
        DateTime endUtc)
    {
        if (selectedItems == null || selectedItems.Count == 0 || stopwatchSlots == null) return;

        int totalMin = Math.Max(1, (int)Math.Round((endUtc - startUtc).TotalMinutes));

        var trackedMinutesByProject = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        var trackedSlotsByProject = new Dictionary<string, List<PeriodicReviewStopwatchSlot>>(StringComparer.OrdinalIgnoreCase);

        foreach (var slot in stopwatchSlots)
        {
            if (!slot.IsTracked || string.IsNullOrWhiteSpace(slot.SelectedProjectName)) continue;

            string key = slot.SelectedProjectName.Trim();
            if (!trackedMinutesByProject.ContainsKey(key))
            {
                trackedMinutesByProject[key] = 0;
                trackedSlotsByProject[key] = [];
            }
            trackedMinutesByProject[key] += slot.Duration.TotalMinutes;
            trackedSlotsByProject[key].Add(slot);
        }

        double trackedTotal = 0;
        var untrackedItems = new List<ReviewProjectSelectionItem>();

        foreach (var item in selectedItems)
        {
            if (trackedMinutesByProject.TryGetValue(item.ProjectName, out double mins) && mins > 0)
            {
                double clamped = Math.Min(mins, totalMin);
                item.AllocatedMinutes = Math.Round(clamped, 1);

                var slots = trackedSlotsByProject[item.ProjectName];
                item.StartUtc = slots.Min(s => s.StartUtc < startUtc ? startUtc : s.StartUtc);
                item.EndUtc = slots.Max(s => s.EndUtc > endUtc ? endUtc : s.EndUtc);

                trackedTotal += item.AllocatedMinutes;
            }
            else
            {
                untrackedItems.Add(item);
            }
        }

        double remainingMin = Math.Max(0, totalMin - trackedTotal);
        if (untrackedItems.Count > 0 && remainingMin > 0)
        {
            double perItem = remainingMin / untrackedItems.Count;
            foreach (var item in untrackedItems)
            {
                item.AllocatedMinutes = Math.Round(perItem, 1);
            }
        }

        RepackTimelineIntervals(selectedItems, startUtc, endUtc);
    }

    /// <summary>
    /// Builds a full, contiguous timeline covering [periodStartUtc, periodEndUtc].
    /// Project intervals (including 0-minute selected items) and intermediate/trailing unallocated gaps
    /// are represented as TimelineSlots.
    /// </summary>
    public static List<TimelineSlot> BuildFullTimeline(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        IReadOnlyList<ReviewProjectSelectionItem> items)
    {
        periodStartUtc = TruncateToMinute(periodStartUtc);
        periodEndUtc = TruncateToMinute(periodEndUtc);
        if (periodEndUtc <= periodStartUtc)
        {
            periodEndUtc = periodStartUtc.AddMinutes(1);
        }

        if (items.Count == 0)
        {
            return
            [
                new TimelineSlot
                {
                    StartUtc = periodStartUtc,
                    EndUtc = periodEndUtc,
                    Item = null
                }
            ];
        }

        // Check if any items have explicit start times; if not, arrange them sequentially
        bool anyExplicit = items.Any(it => it.StartUtc.HasValue && it.StartUtc.Value >= periodStartUtc && it.StartUtc.Value < periodEndUtc);

        if (!anyExplicit)
        {
            DateTime seqCursor = periodStartUtc;
            foreach (var it in items)
            {
                TimeSpan dur = TimeSpan.FromMinutes(Math.Max(0, Math.Round(it.AllocatedMinutes)));
                DateTime s = seqCursor;
                DateTime e = s + dur;
                if (e > periodEndUtc) e = periodEndUtc;
                it.StartUtc = s;
                it.EndUtc = e;
                seqCursor = e;
            }
        }

        // Sort items by StartUtc, preserving selection order for equal start times
        var sorted = items
            .OrderBy(it => it.StartUtc ?? periodStartUtc)
            .ThenBy(it => it.SelectionOrder)
            .ToList();

        var slots = new List<TimelineSlot>();
        DateTime cursor = periodStartUtc;

        for (int i = 0; i < sorted.Count; i++)
        {
            var it = sorted[i];
            TimeSpan duration = TimeSpan.FromMinutes(Math.Max(0, Math.Round(it.AllocatedMinutes)));
            DateTime itemStart = it.StartUtc.HasValue ? TruncateToMinute(it.StartUtc.Value) : cursor;
            if (itemStart < periodStartUtc) itemStart = periodStartUtc;
            if (itemStart < cursor) itemStart = cursor; // prevent overlapping previous item

            DateTime itemEnd = itemStart + duration;
            if (itemEnd > periodEndUtc) itemEnd = periodEndUtc;

            it.StartUtc = itemStart;
            it.EndUtc = itemEnd;

            // Gap before this item? Insert Unallocated slot!
            if (itemStart > cursor)
            {
                slots.Add(new TimelineSlot
                {
                    StartUtc = cursor,
                    EndUtc = itemStart,
                    Item = null
                });
            }

            // Insert project / break slot
            slots.Add(new TimelineSlot
            {
                StartUtc = itemStart,
                EndUtc = itemEnd,
                Item = it
            });

            cursor = itemEnd;
        }

        // Trailing gap after last item?
        if (cursor < periodEndUtc)
        {
            slots.Add(new TimelineSlot
            {
                StartUtc = cursor,
                EndUtc = periodEndUtc,
                Item = null
            });
        }

        return slots;
    }

    /// <summary>
    /// Slices the range [startUtc, endUtc] into contiguous tracked project intervals and untracked intervals.
    /// </summary>
    public static List<PeriodicReviewStopwatchSlot> SliceStopwatchIntervals(
        DateTime startUtc,
        DateTime endUtc,
        ProjectHistoryView history,
        IEnumerable<TimerSession>? runningSessions = null)
    {
        startUtc = ProjectTimeHistory.NormalizeUtc(startUtc);
        endUtc = ProjectTimeHistory.NormalizeUtc(endUtc);

        if (endUtc <= startUtc) return [];

        // Collect all intervals that overlap [startUtc, endUtc]
        var overlapping = new List<(DateTime Start, DateTime End, string ProjectName, Guid? Id, bool IsOpen)>();

        foreach (var interval in history.Intervals)
        {
            DateTime intStart = interval.StartUtc;
            DateTime intEnd = interval.EndUtc ?? endUtc;

            if (intEnd > startUtc && intStart < endUtc)
            {
                DateTime clampedStart = intStart < startUtc ? startUtc : intStart;
                DateTime clampedEnd = intEnd > endUtc ? endUtc : intEnd;

                if (clampedEnd > clampedStart)
                {
                    overlapping.Add((clampedStart, clampedEnd, interval.ProjectName, interval.Id, interval.IsOpen));
                }
            }
        }

        // Sort overlapping intervals by start time
        var sortedTracked = overlapping
            .OrderBy(o => o.Start)
            .ThenBy(o => o.End)
            .ToList();

        // Build contiguous partition of [startUtc, endUtc]
        var slots = new List<PeriodicReviewStopwatchSlot>();
        DateTime cursor = startUtc;

        foreach (var (tStart, tEnd, projName, id, isOpen) in sortedTracked)
        {
            if (tStart > cursor)
            {
                // Gap = Untracked
                slots.Add(new PeriodicReviewStopwatchSlot
                {
                    ExistingIntervalId = null,
                    StartUtc = cursor,
                    EndUtc = tStart,
                    OriginalProjectName = null,
                    SelectedProjectName = null,
                    IsTracked = false,
                    IsOpenTimer = false
                });
            }

            // Tracked interval
            DateTime effectiveStart = tStart < cursor ? cursor : tStart;
            if (tEnd > effectiveStart)
            {
                slots.Add(new PeriodicReviewStopwatchSlot
                {
                    ExistingIntervalId = id,
                    StartUtc = effectiveStart,
                    EndUtc = tEnd,
                    OriginalProjectName = projName,
                    SelectedProjectName = projName,
                    IsTracked = true,
                    IsOpenTimer = isOpen
                });
                cursor = tEnd;
            }
        }

        // Trailing gap = Untracked
        if (cursor < endUtc)
        {
            slots.Add(new PeriodicReviewStopwatchSlot
            {
                ExistingIntervalId = null,
                StartUtc = cursor,
                EndUtc = endUtc,
                OriginalProjectName = null,
                SelectedProjectName = null,
                IsTracked = false,
                IsOpenTimer = false
            });
        }

        return slots;
    }

    /// <summary>
    /// Fetches ActivityWatch events for the review period, applies smart bridging,
    /// slices stopwatch history, and returns the combined PeriodicReviewModel.
    /// </summary>
    public static async Task<PeriodicReviewModel> FetchAndAggregateAsync(
        DateTime startUtc,
        DateTime endUtc,
        AppSettings settings,
        ProjectHistoryView history,
        IEnumerable<TimerSession>? runningSessions = null,
        int? overrideFilterDurationSeconds = null,
        CancellationToken ct = default)
    {
        startUtc = ProjectTimeHistory.NormalizeUtc(startUtc);
        endUtc = ProjectTimeHistory.NormalizeUtc(endUtc);

        int filterSeconds = overrideFilterDurationSeconds ?? settings.PeriodicReviewMinDurationSeconds;
        var rawEvents = new List<RawActivityEvent>();
        bool awAvailable = false;
        string? awMessage = null;

        if (settings.ActivityWatchEnabled)
        {
            try
            {
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                cts.CancelAfter(TimeSpan.FromSeconds(3.5));
                var linkedCt = cts.Token;

                var client = new ActivityWatchClient(settings.ActivityWatchServerUrl);
                var buckets = await client.GetBucketsAsync(linkedCt).ConfigureAwait(false);

                if (buckets.Count > 0)
                {
                    awAvailable = true;
                    var afkBucket = buckets.Values.FirstOrDefault(b =>
                        b.Type == "afkstatus" || b.Id.StartsWith("aw-watcher-afk", StringComparison.OrdinalIgnoreCase));
                    var windowBuckets = buckets.Values.Where(b =>
                        b.Type == "currentwindow" || b.Id.StartsWith("aw-watcher-window", StringComparison.OrdinalIgnoreCase)).ToList();

                    var afkTask = afkBucket != null
                        ? client.GetEventsAsync(afkBucket.Id, startUtc, endUtc, 10000, linkedCt)
                        : Task.FromResult(new List<AwEvent>());

                    var windowTasks = windowBuckets
                        .Select(b => client.GetEventsAsync(b.Id, startUtc, endUtc, 10000, linkedCt))
                        .ToList();

                    var webBuckets = (settings.ActivityWatchIncludeWeb
                        ? buckets.Values.Where(b => b.Type == "web.tab.current" || b.Id.StartsWith("aw-watcher-web", StringComparison.OrdinalIgnoreCase))
                        : Enumerable.Empty<AwBucketInfo>()).ToList();

                    var webTasks = webBuckets
                        .Select(b => client.GetEventsAsync(b.Id, startUtc, endUtc, 10000, linkedCt))
                        .ToList();

                    // Parallel bucket fetching across all watchers
                    await Task.WhenAll(windowTasks.Concat(webTasks).Append(afkTask)).ConfigureAwait(false);

                    var rawAfkIntervals = new List<(DateTimeOffset Start, DateTimeOffset End)>();
                    var afkEvents = await afkTask.ConfigureAwait(false);
                    foreach (var evt in afkEvents)
                    {
                        if (string.Equals(evt.Status, "afk", StringComparison.OrdinalIgnoreCase))
                        {
                            var evtStart = evt.Timestamp;
                            var evtEnd = evtStart.AddSeconds(Math.Max(0, evt.DurationSeconds));
                            if (evtEnd <= startUtc || evtStart >= endUtc) continue;

                            var clampedStart = evtStart < startUtc ? (DateTimeOffset)startUtc : evtStart;
                            var clampedEnd = evtEnd > endUtc ? (DateTimeOffset)endUtc : evtEnd;
                            if (clampedEnd > clampedStart)
                            {
                                rawAfkIntervals.Add((clampedStart, clampedEnd));
                            }
                        }
                    }

                    var mergedAfk = ActivityWatchSync.MergeIntervals(rawAfkIntervals);
                    foreach (var afk in mergedAfk)
                    {
                        rawEvents.Add(new RawActivityEvent(
                            afk.Start,
                            afk.End,
                            "Idle / Away",
                            "No user input (mouse/keyboard idle)",
                            "Idle"));
                    }

                    for (int bIdx = 0; bIdx < windowBuckets.Count; bIdx++)
                    {
                        var events = await windowTasks[bIdx].ConfigureAwait(false);
                        foreach (var evt in events)
                        {
                            var evtStart = evt.Timestamp;
                            var evtEnd = evtStart.AddSeconds(Math.Max(0, evt.DurationSeconds));
                            if (evtEnd <= startUtc || evtStart >= endUtc) continue;

                            var clampedStart = evtStart < startUtc ? (DateTimeOffset)startUtc : evtStart;
                            var clampedEnd = evtEnd > endUtc ? (DateTimeOffset)endUtc : evtEnd;
                            if (clampedEnd <= clampedStart) continue;

                            if (mergedAfk.Count > 0)
                            {
                                var activeSegments = ActivityWatchSync.SubtractIntervals(clampedStart, clampedEnd, mergedAfk);
                                foreach (var (segStart, segEnd) in activeSegments)
                                {
                                    if (segEnd > segStart)
                                    {
                                        rawEvents.Add(new RawActivityEvent(
                                            segStart,
                                            segEnd,
                                            evt.App,
                                            evt.Title,
                                            "App"));
                                    }
                                }
                            }
                            else
                            {
                                rawEvents.Add(new RawActivityEvent(
                                    clampedStart,
                                    clampedEnd,
                                    evt.App,
                                    evt.Title,
                                    "App"));
                            }
                        }
                    }

                    for (int wIdx = 0; wIdx < webBuckets.Count; wIdx++)
                    {
                        var webEvents = await webTasks[wIdx].ConfigureAwait(false);
                        foreach (var evt in webEvents)
                        {
                            var evtStart = evt.Timestamp;
                            var evtEnd = evtStart.AddSeconds(Math.Max(0, evt.DurationSeconds));
                            if (evtEnd <= startUtc || evtStart >= endUtc) continue;

                            var clampedStart = evtStart < startUtc ? (DateTimeOffset)startUtc : evtStart;
                            var clampedEnd = evtEnd > endUtc ? (DateTimeOffset)endUtc : evtEnd;
                            if (clampedEnd <= clampedStart) continue;

                            string domain = "";
                            if (!string.IsNullOrWhiteSpace(evt.Url))
                            {
                                try { domain = new Uri(evt.Url).Host; } catch { domain = evt.Url; }
                            }

                            rawEvents.Add(new RawActivityEvent(
                                clampedStart,
                                clampedEnd,
                                !string.IsNullOrWhiteSpace(domain) ? domain : (string.IsNullOrWhiteSpace(evt.App) ? "Browser" : evt.App),
                                evt.Title,
                                "Web"));
                        }
                    }
                }
                else
                {
                    var test = await client.TestConnectionAsync(ct).ConfigureAwait(false);
                    awMessage = test.ErrorMessage ?? "Could not connect to ActivityWatch.";
                }
            }
            catch (OperationCanceledException)
            {
                awMessage = "ActivityWatch request timed out.";
            }
            catch (Exception ex)
            {
                awMessage = $"ActivityWatch error: {ex.Message}";
            }
        }
        else
        {
            awMessage = "ActivityWatch is disabled in Settings.";
        }

        var activities = FilterAndBridgeActivities(rawEvents, filterSeconds);
        var slots = SliceStopwatchIntervals(startUtc, endUtc, history, runningSessions);

        // Pre-tag activities that already fall within existing tracked stopwatch intervals
        foreach (var act in activities)
        {
            var matchingSlots = slots
                .Where(s => s.IsTracked && !string.IsNullOrWhiteSpace(s.SelectedProjectName)
                            && s.EndUtc > act.StartUtc && s.StartUtc < act.EndUtc)
                .Select(s => s.SelectedProjectName!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase);

            foreach (var proj in matchingSlots)
            {
                if (!act.AssignedProjects.Contains(proj, StringComparer.OrdinalIgnoreCase))
                {
                    act.AssignedProjects.Add(proj);
                }
            }
        }

        var knownProjects = history.Projects.Select(p => p.Name)
            .Concat(history.Intervals.Select(i => i.ProjectName))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        var rawSummaries = AggregateActivitiesSummary(rawEvents, endUtc - startUtc);
        double thresholdPercent = settings.PeriodicReviewMinActivityPercent;
        var filteredSummaries = FilterAndGroupActivitiesSummary(rawSummaries, endUtc - startUtc, thresholdPercent);

        return new PeriodicReviewModel
        {
            StartUtc = startUtc,
            EndUtc = endUtc,
            Activities = activities,
            StopwatchSlots = slots,
            RawActivitySummaries = rawSummaries,
            ActivitySummaries = filteredSummaries,
            KnownProjects = knownProjects,
            ActivityWatchAvailable = awAvailable,
            ActivityWatchMessage = awMessage
        };
    }
}
