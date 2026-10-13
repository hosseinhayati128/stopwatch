using System;
using System.Collections.Generic;
using System.Linq;

namespace StopwatchOverlay.PeriodicReview;

/// <summary>Each drag preview starts from the original timeline, so reversing the
/// pointer restores neighboring time instead of accumulating destructive edits.</summary>
public sealed class ReviewTimelineResize
{
    private readonly IList<ReviewProjectSelectionItem> _items;
    private readonly ReviewProjectSelectionItem _item;
    private readonly bool _fromStart;
    private readonly DateTime _periodStart, _periodEnd;
    private readonly string _collisionBehavior;
    private readonly double _originalMinutes;
    private readonly Action[] _restore;
    private readonly ReviewProjectSelectionItem[] _originalItems;

    public ReviewTimelineResize(IList<ReviewProjectSelectionItem> items, ReviewProjectSelectionItem item,
        bool fromStart, DateTime periodStart, DateTime periodEnd, string collisionBehavior)
    {
        _items = items; _item = item; _fromStart = fromStart;
        _periodStart = periodStart; _periodEnd = periodEnd; _collisionBehavior = collisionBehavior;
        _originalMinutes = item.AllocatedMinutes;
        _originalItems = items.ToArray();
        _restore = items.Select(it =>
        {
            var start = it.StartUtc; var end = it.EndUtc; var minutes = it.AllocatedMinutes;
            var auto = it.IsAutoContinuation; var warning = it.CollisionWarning;
            var order = it.SelectionOrder; var badge = it.SelectionBadgeText;
            var part = it.PartIndex; var parts = it.TotalParts;
            return (Action)(() =>
            {
                it.StartUtc = start; it.EndUtc = end; it.AllocatedMinutes = minutes;
                it.IsAutoContinuation = auto; it.CollisionWarning = warning;
                it.SelectionOrder = order; it.SelectionBadgeText = badge;
                it.PartIndex = part; it.TotalParts = parts;
            });
        }).ToArray();
    }

    public void Preview(double deltaMinutes)
    {
        if (!double.IsFinite(deltaMinutes)) return;
        Cancel();
        double minutes = Math.Max(0, _originalMinutes + Math.Round(deltaMinutes) * (_fromStart ? -1 : 1));
        if (_fromStart) ReviewTimelineEditor.ResizeFromStart(_items, _item, minutes, _periodStart, _periodEnd, _collisionBehavior);
        else ReviewTimelineEditor.Resize(_items, _item, minutes, _periodStart, _periodEnd, _collisionBehavior);
    }

    public void Cancel()
    {
        foreach (var restore in _restore) restore();
        _items.Clear();
        foreach (var item in _originalItems) _items.Add(item);
    }
}
