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
    public DateTime StartLocal { get; set; }
    public DateTime EndLocal { get; set; }
    public TimeSpan Duration => EndLocal > StartLocal ? EndLocal - StartLocal : TimeSpan.Zero;
    public string App { get; set; } = "";
    public string Details { get; set; } = "";
    public string Type { get; set; } = "App"; // "App" or "Web"

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
    public Guid? ExistingIntervalId { get; set; }
    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }
    public string? OriginalProjectName { get; set; }
    public string? SelectedProjectName { get; set; }
    public bool IsTracked { get; set; }
    public bool IsOpenTimer { get; set; }

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

    public bool HasChanged => !string.Equals(OriginalProjectName?.Trim() ?? "", SelectedProjectName?.Trim() ?? "", StringComparison.OrdinalIgnoreCase);
}

public sealed class PeriodicReviewModel
{
    public DateTime StartUtc { get; init; }
    public DateTime EndUtc { get; init; }
    public List<PeriodicReviewActivityItem> Activities { get; init; } = [];
    public List<PeriodicReviewStopwatchSlot> StopwatchSlots { get; init; } = [];
    public List<string> KnownProjects { get; init; } = [];
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

        // Step 2: Smart bridging:
        // If list[i] is App A, followed by one or more brief activities B (each <= threshold),
        // followed by list[k] which is also App A, bridge them together!
        bool changed;
        do
        {
            changed = false;
            for (int i = 0; i < list.Count - 1; i++)
            {
                var first = list[i];
                int k = i + 1;
                bool canBridge = true;

                while (k < list.Count)
                {
                    var mid = list[k];
                    if (string.Equals(mid.App, first.App, StringComparison.OrdinalIgnoreCase))
                    {
                        // Found same app after brief interruptions!
                        break;
                    }

                    // Check if mid is short enough to be considered a brief glance / interruption
                    if (mid.Duration.TotalSeconds > threshold)
                    {
                        canBridge = false;
                        break;
                    }

                    k++;
                }

                if (canBridge && k < list.Count && string.Equals(list[k].App, first.App, StringComparison.OrdinalIgnoreCase))
                {
                    // Bridge from i through k!
                    var target = list[k];
                    first.EndLocal = target.EndLocal > first.EndLocal ? target.EndLocal : first.EndLocal;
                    if (string.IsNullOrWhiteSpace(first.Details) && !string.IsNullOrWhiteSpace(target.Details))
                    {
                        first.Details = target.Details;
                    }

                    // Remove bridged intermediate elements and the target element
                    int removeCount = k - i;
                    list.RemoveRange(i + 1, removeCount);
                    changed = true;
                    break;
                }
            }
        } while (changed);

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
                var client = new ActivityWatchClient(settings.ActivityWatchServerUrl);
                var test = await client.TestConnectionAsync(ct).ConfigureAwait(false);

                if (test.Success)
                {
                    awAvailable = true;
                    var buckets = await client.GetBucketsAsync(ct).ConfigureAwait(false);
                    var windowBuckets = buckets.Values.Where(b => b.Type == "currentwindow").ToList();

                    foreach (var bucket in windowBuckets)
                    {
                        var events = await client.GetEventsAsync(bucket.Id, startUtc, endUtc, 50000, ct).ConfigureAwait(false);
                        foreach (var evt in events)
                        {
                            var evtStart = evt.Timestamp.UtcDateTime;
                            var evtEnd = evtStart.AddSeconds(Math.Max(0, evt.DurationSeconds));
                            if (evtEnd <= startUtc || evtStart >= endUtc) continue;

                            var clampedStart = evtStart < startUtc ? startUtc : evtStart;
                            var clampedEnd = evtEnd > endUtc ? endUtc : evtEnd;
                            if (clampedEnd <= clampedStart) continue;

                            rawEvents.Add(new RawActivityEvent(
                                clampedStart,
                                clampedEnd,
                                evt.App,
                                evt.Title,
                                "App"));
                        }
                    }
                }
                else
                {
                    awMessage = test.ErrorMessage ?? "Could not connect to ActivityWatch.";
                }
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
        var knownProjects = history.Projects.Select(p => p.Name)
            .Concat(history.Intervals.Select(i => i.ProjectName))
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        return new PeriodicReviewModel
        {
            StartUtc = startUtc,
            EndUtc = endUtc,
            Activities = activities,
            StopwatchSlots = slots,
            KnownProjects = knownProjects,
            ActivityWatchAvailable = awAvailable,
            ActivityWatchMessage = awMessage
        };
    }
}
