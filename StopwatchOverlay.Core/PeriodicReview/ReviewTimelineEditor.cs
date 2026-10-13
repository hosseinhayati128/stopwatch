using System;
using System.Collections.Generic;
using System.Linq;

namespace StopwatchOverlay.PeriodicReview;

/// <summary>Edits positioned sessions without redistributing unrelated time.</summary>
public static class ReviewTimelineEditor
{
    private static void SetRange(ReviewProjectSelectionItem item, DateTime start, DateTime end)
    {
        item.StartUtc = start;
        item.EndUtc = end;
        item.AllocatedMinutes = Math.Max(0, (end - start).TotalMinutes);
        item.IsAutoContinuation = false;
        item.CollisionWarning = null;
    }

    private static ReviewProjectSelectionItem Copy(ReviewProjectSelectionItem item, DateTime start, DateTime end)
    {
        var copy = new ReviewProjectSelectionItem { ProjectName = item.ProjectName, IsBreak = item.IsBreak };
        SetRange(copy, start, end);
        return copy;
    }

    public static void Sort(IList<ReviewProjectSelectionItem> items)
    {
        var ordered = items.OrderBy(it => it.StartUtc).ThenBy(it => it.EndUtc).ToList();
        items.Clear();
        foreach (var item in ordered) items.Add(item);
        foreach (var group in ordered.GroupBy(it => it.ProjectName, StringComparer.OrdinalIgnoreCase))
        {
            int part = 0;
            foreach (var item in group) { item.PartIndex = ++part; item.TotalParts = group.Count(); }
        }
        for (int i = 0; i < items.Count; i++)
        { items[i].SelectionOrder = i + 1; items[i].SelectionBadgeText = (i + 1).ToString(); }
    }

    public static void PlaceNew(IList<ReviewProjectSelectionItem> items, ReviewProjectSelectionItem item,
        DateTime periodStart, DateTime periodEnd)
    {
        DateTime cursor = periodStart;
        foreach (var other in items.Where(it => it != item && it.EndUtc > it.StartUtc).OrderBy(it => it.StartUtc))
        {
            if (other.StartUtc > cursor) break;
            if (other.EndUtc > cursor) cursor = other.EndUtc.Value;
        }
        // With a full timeline, a zero-length marker at the beginning can be grown explicitly.
        if (cursor >= periodEnd)
            cursor = items.Where(it => it != item && !it.IsFixedTracked && it.EndUtc > it.StartUtc)
                .OrderBy(it => it.StartUtc).FirstOrDefault()?.StartUtc ?? periodEnd;
        SetRange(item, cursor, cursor);
        if (!items.Contains(item)) items.Add(item);
        Sort(items);
    }

    public static void Resize(IList<ReviewProjectSelectionItem> items, ReviewProjectSelectionItem item,
        double minutes, DateTime periodStart, DateTime periodEnd, string collisionBehavior)
    {
        if (item.IsFixedTracked || !items.Contains(item) || !double.IsFinite(minutes)) return;
        DateTime start = item.StartUtc ?? periodStart;
        start = start < periodStart ? periodStart : start > periodEnd ? periodEnd : start;
        double remaining = Math.Clamp(Math.Round(minutes), 0, (periodEnd - start).TotalMinutes);
        var ranges = new List<(DateTime Start, DateTime End)>();
        DateTime cursor = start;
        bool stop = string.Equals(collisionBehavior, "StopAtFixed", StringComparison.OrdinalIgnoreCase);
        foreach (var fixedItem in items.Where(it => it.IsFixedTracked && it.EndUtc > cursor)
                     .OrderBy(it => it.StartUtc).ToList())
        {
            if (!fixedItem.StartUtc.HasValue || !fixedItem.EndUtc.HasValue || remaining <= 0) continue;
            DateTime boundary = fixedItem.StartUtc.Value < periodEnd ? fixedItem.StartUtc.Value : periodEnd;
            if (boundary > cursor)
            {
                double take = Math.Min(remaining, (boundary - cursor).TotalMinutes);
                ranges.Add((cursor, cursor.AddMinutes(take)));
                cursor = cursor.AddMinutes(take);
                remaining -= take;
            }
            if (remaining <= 0 || stop) { remaining = 0; break; }
            if (fixedItem.EndUtc > cursor) cursor = fixedItem.EndUtc.Value;
        }
        if (remaining > 0 && cursor < periodEnd)
            ranges.Add((cursor, cursor.AddMinutes(Math.Min(remaining, (periodEnd - cursor).TotalMinutes))));
        if (ranges.Count == 0) ranges.Add((start, start));

        // Only overwrite the portions actually occupied by the resized session.
        // Left-hand sessions, gaps outside these ranges and fixed records stay put.
        foreach (var range in ranges.Where(r => r.End > r.Start))
        foreach (var other in items.Where(it => it != item && !it.IsFixedTracked &&
                     it.StartUtc < range.End && it.EndUtc > range.Start).ToList())
        {
            DateTime oldStart = other.StartUtc!.Value, oldEnd = other.EndUtc!.Value;
            if (oldStart < range.Start && oldEnd > range.End)
            {
                SetRange(other, oldStart, range.Start);
                items.Add(Copy(other, range.End, oldEnd));
            }
            else if (oldStart < range.Start) SetRange(other, oldStart, range.Start);
            else if (oldEnd > range.End) SetRange(other, range.End, oldEnd);
            // Keep an exhausted session at its original anchor so it can be
            // increased again, even when another session currently fills the period.
            else SetRange(other, oldStart, oldStart);
        }
        SetRange(item, ranges[0].Start, ranges[0].End);
        foreach (var range in ranges.Skip(1)) items.Add(Copy(item, range.Start, range.End));
        if (ranges.Count > 1) item.CollisionWarning = "Split around fixed records into separate editable parts.";
        Sort(items);
    }

    public static void ResizeFromStart(IList<ReviewProjectSelectionItem> items, ReviewProjectSelectionItem item,
        double minutes, DateTime periodStart, DateTime periodEnd, string collisionBehavior)
    {
        if (item.IsFixedTracked || !items.Contains(item) || !double.IsFinite(minutes)) return;
        // Reverse the time axis to apply exactly the same collision and replacement
        // rules to the left edge, with the original end held in place.
        DateTime Reflect(DateTime time) => periodStart + (periodEnd - time);
        var originals = new Dictionary<ReviewProjectSelectionItem, ReviewProjectSelectionItem>();
        foreach (var original in items)
        {
            var mirrored = Copy(original, Reflect(original.EndUtc ?? periodEnd), Reflect(original.StartUtc ?? periodStart));
            mirrored.IsFixedTracked = original.IsFixedTracked;
            originals.Add(mirrored, original);
        }
        var reversed = originals.Keys.OrderBy(it => it.StartUtc).ToList();
        var changed = originals.Single(pair => pair.Value == item).Key;
        Resize(reversed, changed, minutes, periodStart, periodEnd, collisionBehavior);
        foreach (var mirrored in reversed)
        {
            if (mirrored.IsFixedTracked) continue;
            if (!originals.TryGetValue(mirrored, out var original))
            {
                original = Copy(mirrored, Reflect(mirrored.EndUtc!.Value), Reflect(mirrored.StartUtc!.Value));
                items.Add(original);
            }
            else SetRange(original, Reflect(mirrored.EndUtc!.Value), Reflect(mirrored.StartUtc!.Value));
            original.CollisionWarning = mirrored.CollisionWarning;
        }
        Sort(items);
    }

    public static void Move(IList<ReviewProjectSelectionItem> items, ReviewProjectSelectionItem item,
        DateTime requestedStart, DateTime periodStart, DateTime periodEnd)
    {
        if (item.IsFixedTracked || !item.StartUtc.HasValue || !item.EndUtc.HasValue) return;
        var duration = item.EndUtc.Value - item.StartUtc.Value;
        DateTime lower = periodStart, upper = periodEnd;
        foreach (var other in items.Where(it => it != item && it.EndUtc > it.StartUtc))
        {
            if (other.EndUtc <= item.StartUtc && other.EndUtc > lower) lower = other.EndUtc.Value;
            if (other.StartUtc >= item.EndUtc && other.StartUtc < upper) upper = other.StartUtc.Value;
        }
        if (upper - lower < duration) return;
        var start = PeriodicReviewDataAggregator.TruncateToMinute(requestedStart);
        if (start < lower) start = lower;
        if (start > upper - duration) start = upper - duration;
        SetRange(item, start, start + duration);
        Sort(items);
    }

    public static bool Swap(IList<ReviewProjectSelectionItem> items, ReviewProjectSelectionItem item, int direction)
    {
        Sort(items);
        int index = items.IndexOf(item), otherIndex = index + direction;
        if (index < 0 || otherIndex < 0 || otherIndex >= items.Count) return false;
        var other = items[otherIndex];
        if (item.IsFixedTracked || other.IsFixedTracked) return false;
        var first = direction < 0 ? other : item;
        var second = direction < 0 ? item : other;
        if (!first.StartUtc.HasValue || !first.EndUtc.HasValue || !second.StartUtc.HasValue || !second.EndUtc.HasValue) return false;
        if (first.EndUtc > second.StartUtc) return false;
        var start = first.StartUtc.Value;
        var end = second.EndUtc.Value;
        var firstDuration = first.EndUtc.Value - start;
        var secondDuration = end - second.StartUtc.Value;
        SetRange(second, start, start + secondDuration);
        SetRange(first, end - firstDuration, end);
        Sort(items);
        return true;
    }
    public static List<TimelineSlot> Snapshot(
        DateTime periodStartUtc,
        DateTime periodEndUtc,
        IReadOnlyList<ReviewProjectSelectionItem> items)
    {
        periodStartUtc = PeriodicReviewDataAggregator.TruncateToMinute(periodStartUtc);
        periodEndUtc = PeriodicReviewDataAggregator.TruncateToMinute(periodEndUtc);
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

        // Initial, unpositioned selections are laid out once. Rendering a positioned
        // timeline must never move records, round fixed timestamps or rewrite edits.
        DateTime initialCursor = periodStartUtc;
        foreach (var item in items)
        {
            if (!item.StartUtc.HasValue || !item.EndUtc.HasValue)
            {
                var end = initialCursor.AddMinutes(Math.Max(0, Math.Round(item.AllocatedMinutes)));
                if (end > periodEndUtc) end = periodEndUtc;
                item.StartUtc = initialCursor;
                item.EndUtc = end;
                item.AllocatedMinutes = (end - initialCursor).TotalMinutes;
            }
            if (item.EndUtc > initialCursor) initialCursor = item.EndUtc.Value;
            if (initialCursor > periodEndUtc) initialCursor = periodEndUtc;
        }

        var slots = new List<TimelineSlot>();
        DateTime cursor = periodStartUtc;
        foreach (var item in items.OrderBy(it => it.StartUtc).ThenBy(it => it.SelectionOrder))
        {
            DateTime start = item.StartUtc!.Value < periodStartUtc ? periodStartUtc : item.StartUtc.Value;
            DateTime end = item.EndUtc!.Value > periodEndUtc ? periodEndUtc : item.EndUtc.Value;
            if (start > periodEndUtc || end < periodStartUtc || end < start) continue;
            if (start > cursor) slots.Add(new TimelineSlot { StartUtc = cursor, EndUtc = start });
            slots.Add(new TimelineSlot { StartUtc = start, EndUtc = end, Item = item });
            if (end > cursor) cursor = end;
        }
        if (cursor < periodEndUtc)
            slots.Add(new TimelineSlot { StartUtc = cursor, EndUtc = periodEndUtc });

        return slots;
    }

}
