using System;
using System.Collections.Generic;
using System.Linq;
using StopwatchOverlay.PeriodicReview;
using Xunit;

namespace StopwatchOverlay.Tests;

public sealed class ReviewTimelineEditorTests
{
    private static readonly DateTime Start = new(2026, 10, 13, 10, 0, 0, DateTimeKind.Utc);
    private static ReviewProjectSelectionItem Item(string name, double start, double end, bool fixedRecord = false, bool isBreak = false)
        => new() { ProjectName = name, StartUtc = Start.AddMinutes(start), EndUtc = Start.AddMinutes(end),
            AllocatedMinutes = end - start, IsFixedTracked = fixedRecord, IsBreak = isBreak };
    private static void Resize(List<ReviewProjectSelectionItem> items, ReviewProjectSelectionItem item, double minutes,
        string mode = "StopAtFixed") => ReviewTimelineEditor.Resize(items, item, minutes, Start, Start.AddMinutes(60), mode);
    private static void Range(ReviewProjectSelectionItem item, double start, double end)
    {
        Assert.Equal(Start.AddMinutes(start), item.StartUtc);
        Assert.Equal(Start.AddMinutes(end), item.EndUtc);
        Assert.Equal(end - start, item.AllocatedMinutes, 6);
    }

    [Fact]
    public void GrowthUsesRightNeighborNeverLargestLeftDonor()
    {
        var left = Item("Left", 0, 30); var changed = Item("Edited", 30, 40); var right = Item("Right", 40, 60);
        List<ReviewProjectSelectionItem> items = [left, changed, right];
        Resize(items, changed, 20);
        Range(left, 0, 30); Range(changed, 30, 50); Range(right, 50, 60);
    }

    [Fact]
    public void GrowthConsumesNearbyGapBeforeRightSession()
    {
        var changed = Item("Edited", 0, 10); var right = Item("Right", 20, 40);
        List<ReviewProjectSelectionItem> items = [changed, right];
        Resize(items, changed, 15); Range(right, 20, 40);
        Resize(items, changed, 25); Range(changed, 0, 25); Range(right, 25, 40);
    }

    [Fact]
    public void ShrinkLeavesGapAndDoesNotMoveOtherSessions()
    {
        var changed = Item("Edited", 0, 30); var right = Item("Right", 30, 60);
        List<ReviewProjectSelectionItem> items = [changed, right];
        Resize(items, changed, 10); Range(right, 30, 60);
        var gap = Assert.Single(ReviewTimelineEditor.Snapshot(Start, Start.AddMinutes(60), items), s => s.IsUnallocated);
        Assert.Equal(Start.AddMinutes(10), gap.StartUtc); Assert.Equal(Start.AddMinutes(30), gap.EndUtc);
    }

    [Fact]
    public void GrowthReplacesBreakAndCapsAtPeriodEnd()
    {
        var project = Item("Project", 0, 10); var pause = Item("Break", 10, 60, isBreak: true);
        List<ReviewProjectSelectionItem> items = [project, pause];
        Resize(items, project, 90); Range(project, 0, 60); Range(pause, 10, 10);
    }

    [Theory]
    [InlineData("StopAtFixed", 10)]
    [InlineData("CutAndContinue", 30)]
    public void FixedCollisionModeIsAppliedWithoutMovingFixedOrLeftRecords(string mode, int expectedMinutes)
    {
        var project = Item("Project", 0, 5); var fixedItem = Item("Fixed", 10, 20, true); var right = Item("Right", 20, 60);
        List<ReviewProjectSelectionItem> items = [project, fixedItem, right];
        Resize(items, project, 30, mode);
        Range(fixedItem, 10, 20);
        Assert.Equal(expectedMinutes, items.Where(it => it.ProjectName == "Project").Sum(it => it.AllocatedMinutes));
        if (mode == "CutAndContinue") { Range(right, 40, 60); Assert.Equal(2, items.Count(it => it.ProjectName == "Project")); }
        else Range(right, 20, 60);
    }

    [Fact]
    public void ContinuationCanCrossMultipleFixedRecordsWithoutDoubleCounting()
    {
        var project = Item("Project", 0, 5);
        List<ReviewProjectSelectionItem> items = [project, Item("Fixed 1", 10, 20, true), Item("Fixed 2", 30, 40, true)];
        Resize(items, project, 40, "CutAndContinue");
        Assert.Equal(40, items.Where(it => !it.IsFixedTracked).Sum(it => it.AllocatedMinutes));
        Assert.Equal(60, ReviewTimelineEditor.Snapshot(Start, Start.AddMinutes(60), items).Sum(it => it.DurationMinutes));
        Assert.DoesNotContain(items, it => it.IsAutoContinuation);
    }

    [Fact]
    public void ZeroMinuteSessionCanReplaceMiddleOfAnotherSession()
    {
        var other = Item("Other", 0, 60); var changed = Item("New", 20, 20);
        List<ReviewProjectSelectionItem> items = [other, changed];
        Resize(items, changed, 10);
        Range(changed, 20, 30); Range(other, 0, 20);
        Range(Assert.Single(items, it => it.ProjectName == "Other" && it != other), 30, 60);
    }

    [Fact]
    public void NewSessionUsesFirstGapAndIsEditableWhenPeriodIsFull()
    {
        List<ReviewProjectSelectionItem> items = [Item("Other", 0, 60)];
        var added = new ReviewProjectSelectionItem { ProjectName = "New" };
        ReviewTimelineEditor.PlaceNew(items, added, Start, Start.AddMinutes(60));
        Resize(items, added, 10); Range(added, 0, 10);
        Resize(items, added, 5);
        var next = new ReviewProjectSelectionItem { ProjectName = "Next" };
        ReviewTimelineEditor.PlaceNew(items, next, Start, Start.AddMinutes(60));
        Range(next, 5, 5);
    }

    [Fact]
    public void FullyConsumedSessionCanBeIncreasedAgainAtItsOriginalPosition()
    {
        var first = Item("First", 0, 30); var second = Item("Second", 30, 60);
        List<ReviewProjectSelectionItem> items = [first, second];
        Resize(items, first, 60); Range(second, 30, 30);
        Resize(items, second, 10); Range(second, 30, 40); Range(first, 0, 30);
        Range(Assert.Single(items, it => it != first && it.ProjectName == "First"), 40, 60);
    }

    [Fact]
    public void NewSessionInFullPeriodStartsOnManualTimeRatherThanInsideFixedRecord()
    {
        List<ReviewProjectSelectionItem> items = [Item("Fixed", 0, 20, true), Item("Manual", 20, 60)];
        var added = new ReviewProjectSelectionItem { ProjectName = "New" };
        ReviewTimelineEditor.PlaceNew(items, added, Start, Start.AddMinutes(60));
        Resize(items, added, 10); Range(added, 20, 30);
        Range(Assert.Single(items, it => it.IsFixedTracked), 0, 20);
    }

    [Fact]
    public void SwapPreservesBothDurationsAndTheirGap()
    {
        var first = Item("First", 0, 10); var second = Item("Second", 15, 35);
        List<ReviewProjectSelectionItem> items = [first, second];
        Assert.True(ReviewTimelineEditor.Swap(items, second, -1));
        Range(second, 0, 20); Range(first, 25, 35);
    }

    [Fact]
    public void FixedRecordsCannotBeResizedMovedOrSwapped()
    {
        var fixedItem = Item("Fixed", 10, 20, true); var manual = Item("Manual", 20, 30);
        List<ReviewProjectSelectionItem> items = [fixedItem, manual];
        Resize(items, fixedItem, 45);
        ReviewTimelineEditor.Move(items, fixedItem, Start, Start, Start.AddMinutes(60));
        Assert.False(ReviewTimelineEditor.Swap(items, manual, -1)); Range(fixedItem, 10, 20);
    }

    [Fact]
    public void MovePreservesDurationAndStopsAtNeighborsIgnoringZeroMarkers()
    {
        var item = Item("Moving", 10, 20);
        List<ReviewProjectSelectionItem> items = [Item("Left", 0, 5), Item("Zero", 7, 7), item, Item("Right", 30, 40)];
        ReviewTimelineEditor.Move(items, item, Start, Start, Start.AddMinutes(60)); Range(item, 5, 15);
        ReviewTimelineEditor.Move(items, item, Start.AddMinutes(40), Start, Start.AddMinutes(60)); Range(item, 20, 30);
    }

    [Fact]
    public void RenderingIsReadOnlyAndPreservesOverlappingFixedRecordsWithSeconds()
    {
        var a = Item("A", 0.5, 10.5, true); var b = Item("B", 5.5, 20.5, true);
        List<ReviewProjectSelectionItem> items = [a, b];
        for (int i = 0; i < 3; i++) ReviewTimelineEditor.Snapshot(Start, Start.AddMinutes(60), items);
        Range(a, 0.5, 10.5); Range(b, 5.5, 20.5);
    }

    [Fact]
    public void RepeatedEditsRemainBoundedConsistentAndNonOverlapping()
    {
        var fixedItem = Item("Fixed", 20, 30, true);
        List<ReviewProjectSelectionItem> items = [Item("A", 0, 20), fixedItem, Item("B", 30, 60)];
        var random = new Random(1234);
        for (int i = 0; i < 100; i++)
        {
            var candidates = items.Where(it => !it.IsFixedTracked).ToArray();
            Resize(items, candidates[random.Next(candidates.Length)], random.Next(61), i % 2 == 0 ? "CutAndContinue" : "StopAtFixed");
            Range(fixedItem, 20, 30);
            DateTime previousEnd = Start;
            foreach (var item in items.Where(it => it.AllocatedMinutes > 0).OrderBy(it => it.StartUtc))
            {
                Assert.True(item.StartUtc >= previousEnd);
                Assert.True(item.EndUtc <= Start.AddMinutes(60));
                Assert.Equal((item.EndUtc!.Value - item.StartUtc!.Value).TotalMinutes, item.AllocatedMinutes, 6);
                previousEnd = item.EndUtc.Value;
            }
        }
    }

    [Fact]
    public void LeftEdgeGrowthKeepsEndAndReplacesOnlyLeftNeighbor()
    {
        var left = Item("Left", 0, 20); var changed = Item("Edited", 20, 30); var right = Item("Right", 30, 60);
        List<ReviewProjectSelectionItem> items = [left, changed, right];
        ReviewTimelineEditor.ResizeFromStart(items, changed, 15, Start, Start.AddMinutes(60), "StopAtFixed");
        Range(left, 0, 15); Range(changed, 15, 30); Range(right, 30, 60);
        ReviewTimelineEditor.ResizeFromStart(items, changed, 5, Start, Start.AddMinutes(60), "StopAtFixed");
        Range(changed, 25, 30); Range(left, 0, 15); Range(right, 30, 60);
    }

    [Theory]
    [InlineData("StopAtFixed", 10)]
    [InlineData("CutAndContinue", 30)]
    public void LeftEdgeHonorsFixedCollisionPolicy(string mode, int expectedMinutes)
    {
        var changed = Item("Edited", 30, 40); var fixedItem = Item("Fixed", 10, 30, true);
        List<ReviewProjectSelectionItem> items = [fixedItem, changed];
        ReviewTimelineEditor.ResizeFromStart(items, changed, 30, Start, Start.AddMinutes(60), mode);
        Range(fixedItem, 10, 30);
        // Only 20 manual minutes exist to the left of the anchored end (10 before + 10 after fixed).
        Assert.Equal(Math.Min(expectedMinutes, 20), items.Where(it => !it.IsFixedTracked).Sum(it => it.AllocatedMinutes));
        Assert.Equal(Start.AddMinutes(40), changed.EndUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ReversingResizeDragRestoresNeighbors(bool fromStart)
    {
        var left = Item("Left", 0, 20); var changed = Item("Edited", 20, 40); var right = Item("Right", 40, 60);
        List<ReviewProjectSelectionItem> items = [left, changed, right];
        var drag = new ReviewTimelineResize(items, changed, fromStart, Start, Start.AddMinutes(60), "StopAtFixed");
        drag.Preview(fromStart ? -10 : 10);
        Assert.Equal(30, changed.AllocatedMinutes);
        drag.Preview(0);
        Range(left, 0, 20); Range(changed, 20, 40); Range(right, 40, 60);
        Assert.Equal(3, items.Count);
    }

    [Fact]
    public void CancelResizeRemovesTemporaryContinuationPartsAndRestoresOriginalObjects()
    {
        var changed = Item("Edited", 0, 10); var fixedItem = Item("Fixed", 10, 20, true); var right = Item("Right", 20, 60);
        List<ReviewProjectSelectionItem> items = [changed, fixedItem, right];
        var drag = new ReviewTimelineResize(items, changed, false, Start, Start.AddMinutes(60), "CutAndContinue");
        drag.Preview(25);
        Assert.Equal(4, items.Count);
        drag.Preview(15);
        Assert.Equal(4, items.Count); // Preview never accumulates extra continuation parts.
        drag.Cancel();
        Assert.Equal(new[] { changed, fixedItem, right }, items);
        Range(changed, 0, 10); Range(fixedItem, 10, 20); Range(right, 20, 60);
    }

    [Fact]
    public void ResizeDragClampsToPeriodAndFixedRecordsIgnoreEitherEdge()
    {
        var changed = Item("Edited", 20, 40);
        List<ReviewProjectSelectionItem> items = [changed];
        var drag = new ReviewTimelineResize(items, changed, true, Start, Start.AddMinutes(60), "StopAtFixed");
        drag.Preview(-100); Range(changed, 0, 40);
        drag.Preview(100); Range(changed, 40, 40);
        drag.Cancel();
        changed.IsFixedTracked = true;
        foreach (bool fromStart in new[] { false, true })
        {
            new ReviewTimelineResize(items, changed, fromStart, Start, Start.AddMinutes(60), "CutAndContinue").Preview(10);
            Range(changed, 20, 40);
        }
    }
}
