using System;
using System.Linq;
using Xunit;

namespace StopwatchOverlay.Tests
{
    public class TimerEditorTests
    {
        [Theory]
        [InlineData("0", 0)]
        [InlineData("00:00", 0)]
        [InlineData("00:00:00", 0)]
        [InlineData("00:05", 5)]
        [InlineData("05:00", 300)]
        [InlineData("01:30:00", 5400)]
        [InlineData("15m", 900)]
        [InlineData("1h", 3600)]
        [InlineData("45s", 45)]
        public void TryParseDuration_ValidInputs_ReturnExpectedSeconds(string input, int expectedSeconds)
        {
            bool success = TimerEditorWindow.TryParseDuration(input, out TimeSpan duration);
            Assert.True(success);
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), duration);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("invalid")]
        [InlineData("-10m")]
        public void TryParseDuration_InvalidInputs_ReturnFalse(string input)
        {
            bool success = TimerEditorWindow.TryParseDuration(input, out _);
            Assert.False(success);
        }

        [Fact]
        public void FormatDuration_FormatsAppropriately()
        {
            Assert.Equal("05:30", TimerEditorWindow.FormatDuration(TimeSpan.FromMinutes(5.5)));
            Assert.Equal("01:15:30", TimerEditorWindow.FormatDuration(TimeSpan.FromHours(1.25) + TimeSpan.FromSeconds(30)));
            Assert.Equal("00:00", TimerEditorWindow.FormatDuration(TimeSpan.Zero));
        }

        [Fact]
        public void ProjectTimeHistory_DiscardRecentInterval_RemovesOpenInterval()
        {
            var history = new ProjectTimeHistory();
            var timerId = Guid.NewGuid();
            var now = DateTime.UtcNow;

            history.StartTracking(timerId, "TestProject", now);
            var open = history.GetOpenInterval(timerId);
            Assert.NotNull(open);

            bool discarded = history.DiscardRecentInterval(timerId);
            Assert.True(discarded);

            var openAfter = history.GetOpenInterval(timerId);
            Assert.Null(openAfter);
        }

        [Fact]
        public void ProjectTimeHistory_DiscardRecentInterval_RemovesClosedIntervalIfNoOpen()
        {
            var history = new ProjectTimeHistory();
            var timerId = Guid.NewGuid();
            var start = DateTime.UtcNow.AddMinutes(-30);
            var end = DateTime.UtcNow;

            history.StartTracking(timerId, "TestProject", start);
            history.StopTracking(timerId, end);

            var view = history.CreateView(DateTime.UtcNow);
            Assert.Single(view.Intervals);

            bool discarded = history.DiscardRecentInterval(timerId);
            Assert.True(discarded);

            var viewAfter = history.CreateView(DateTime.UtcNow);
            Assert.Empty(viewAfter.Intervals);
        }

        [Fact]
        public void ProjectTimeHistory_UpdateActiveIntervalStartTime_UpdatesTimestamp()
        {
            var history = new ProjectTimeHistory();
            var timerId = Guid.NewGuid();
            var start = DateTime.UtcNow.AddMinutes(-30);

            history.StartTracking(timerId, "TestProject", start);
            var open = history.GetOpenInterval(timerId);
            Assert.NotNull(open);

            var newStart = DateTime.UtcNow.AddMinutes(-15);
            bool updated = history.UpdateActiveIntervalStartTime(timerId, newStart);
            Assert.True(updated);

            var openAfter = history.GetOpenInterval(timerId);
            Assert.NotNull(openAfter);
            Assert.Equal(newStart, openAfter.StartUtc);
        }

        [Fact]
        public void ProjectTimeHistory_AdjustIntervals_AddExtendsLatestSegment()
        {
            var history = new ProjectTimeHistory();
            var timerId = Guid.NewGuid();
            var baseDate = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc);

            // Segment 1: 10:00 - 10:05 (5m)
            history.StartTracking(timerId, "ProjectX", baseDate);
            history.StopTracking(timerId, baseDate.AddMinutes(5));

            // Segment 2: 16:00 - 16:20 (20m)
            history.StartTracking(timerId, "ProjectX", baseDate.AddHours(6));
            history.StopTracking(timerId, baseDate.AddHours(6).AddMinutes(20));

            // Segment 3: 18:00 - 18:03 (3m)
            history.StartTracking(timerId, "ProjectX", baseDate.AddHours(8));
            history.StopTracking(timerId, baseDate.AddHours(8).AddMinutes(3));

            // Segment 4: 20:00 - 20:02 (2m)
            history.StartTracking(timerId, "ProjectX", baseDate.AddHours(10));
            history.StopTracking(timerId, baseDate.AddHours(10).AddMinutes(2));

            var intervalsBefore = history.GetIntervalsForTimer(timerId);
            Assert.Equal(4, intervalsBefore.Count);
            TimeSpan totalBefore = intervalsBefore.Aggregate(TimeSpan.Zero, (acc, i) => acc + (i.EndUtc!.Value - i.StartUtc));
            Assert.Equal(TimeSpan.FromMinutes(30), totalBefore);

            // Add 10 minutes when current time is 20:20 (forward addition does not exceed now)
            history.AdjustIntervalsForTimer(timerId, TimeSpan.FromMinutes(10), "ProjectX", baseDate.AddHours(10).AddMinutes(20), isRunning: false);

            var intervalsAfter = history.GetIntervalsForTimer(timerId);
            Assert.Equal(4, intervalsAfter.Count);

            // Latest segment should now end at 20:12 (was 20:02)
            var latest = intervalsAfter.Last();
            Assert.Equal(baseDate.AddHours(10), latest.StartUtc);
            Assert.Equal(baseDate.AddHours(10).AddMinutes(12), latest.EndUtc);
            Assert.NotNull(latest.EndUtc);
            Assert.Equal(TimeSpan.FromMinutes(12), latest.EndUtc!.Value - latest.StartUtc);

            TimeSpan totalAfter = intervalsAfter.Aggregate(TimeSpan.Zero, (acc, i) => acc + (i.EndUtc!.Value - i.StartUtc));
            Assert.Equal(TimeSpan.FromMinutes(40), totalAfter);
        }

        [Fact]
        public void ProjectTimeHistory_AdjustIntervals_SubtractEliminatesFromEnd()
        {
            var history = new ProjectTimeHistory();
            var timerId = Guid.NewGuid();
            var baseDate = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc);

            // Segment 1: 10:00 - 10:05 (5m)
            history.StartTracking(timerId, "ProjectX", baseDate);
            history.StopTracking(timerId, baseDate.AddMinutes(5));

            // Segment 2: 16:00 - 16:20 (20m)
            history.StartTracking(timerId, "ProjectX", baseDate.AddHours(6));
            history.StopTracking(timerId, baseDate.AddHours(6).AddMinutes(20));

            // Segment 3: 18:00 - 18:03 (3m)
            history.StartTracking(timerId, "ProjectX", baseDate.AddHours(8));
            history.StopTracking(timerId, baseDate.AddHours(8).AddMinutes(3));

            // Segment 4: 20:00 - 20:02 (2m)
            history.StartTracking(timerId, "ProjectX", baseDate.AddHours(10));
            history.StopTracking(timerId, baseDate.AddHours(10).AddMinutes(2));

            // Subtract 10 minutes: eliminates seg 4 (2m), seg 3 (3m), and reduces seg 2 by 5m (from 20m down to 15m)
            history.AdjustIntervalsForTimer(timerId, TimeSpan.FromMinutes(-10), "ProjectX", baseDate.AddHours(10).AddMinutes(2), isRunning: false);

            var intervalsAfter = history.GetIntervalsForTimer(timerId);
            Assert.Equal(2, intervalsAfter.Count);

            // Segment 1 untouched (5m)
            Assert.Equal(baseDate, intervalsAfter[0].StartUtc);
            Assert.Equal(baseDate.AddMinutes(5), intervalsAfter[0].EndUtc);

            // Segment 2 reduced from 16:20 down to 16:15 (15m)
            Assert.Equal(baseDate.AddHours(6), intervalsAfter[1].StartUtc);
            Assert.Equal(baseDate.AddHours(6).AddMinutes(15), intervalsAfter[1].EndUtc);

            TimeSpan totalAfter = intervalsAfter.Aggregate(TimeSpan.Zero, (acc, i) => acc + (i.EndUtc!.Value - i.StartUtc));
            Assert.Equal(TimeSpan.FromMinutes(20), totalAfter);
        }

        [Fact]
        public void ProjectTimeHistory_AdjustIntervals_Subtract13Minutes_ReducesNoonTo1612()
        {
            var history = new ProjectTimeHistory();
            var timerId = Guid.NewGuid();
            var baseDate = new DateTime(2026, 9, 24, 10, 0, 0, DateTimeKind.Utc);

            // Segment 1: 10:00 - 10:05 (5m)
            history.StartTracking(timerId, "ProjectX", baseDate);
            history.StopTracking(timerId, baseDate.AddMinutes(5));

            // Segment 2: 16:00 - 16:20 (20m)
            history.StartTracking(timerId, "ProjectX", baseDate.AddHours(6));
            history.StopTracking(timerId, baseDate.AddHours(6).AddMinutes(20));

            // Segment 3: 18:00 - 18:03 (3m)
            history.StartTracking(timerId, "ProjectX", baseDate.AddHours(8));
            history.StopTracking(timerId, baseDate.AddHours(8).AddMinutes(3));

            // Segment 4: 20:00 - 20:02 (2m)
            history.StartTracking(timerId, "ProjectX", baseDate.AddHours(10));
            history.StopTracking(timerId, baseDate.AddHours(10).AddMinutes(2));

            // Subtract 13 minutes: eliminates seg 4 (2m), seg 3 (3m), and reduces seg 2 by 8m (20m - 8m = 12m, ending at 16:12)
            history.AdjustIntervalsForTimer(timerId, TimeSpan.FromMinutes(-13), "ProjectX", baseDate.AddHours(10).AddMinutes(2), isRunning: false);

            var intervalsAfter = history.GetIntervalsForTimer(timerId);
            Assert.Equal(2, intervalsAfter.Count);

            // Segment 2 ends at 16:12
            Assert.Equal(baseDate.AddHours(6), intervalsAfter[1].StartUtc);
            Assert.Equal(baseDate.AddHours(6).AddMinutes(12), intervalsAfter[1].EndUtc);
            Assert.NotNull(intervalsAfter[1].EndUtc);
            Assert.Equal(TimeSpan.FromMinutes(12), intervalsAfter[1].EndUtc!.Value - intervalsAfter[1].StartUtc);

            TimeSpan totalAfter = intervalsAfter.Aggregate(TimeSpan.Zero, (acc, i) => acc + (i.EndUtc!.Value - i.StartUtc));
            Assert.Equal(TimeSpan.FromMinutes(17), totalAfter);
        }

        [Fact]
        public void ProjectTimeHistory_AdjustIntervals_AddBackwardWhenExceedingCurrentTime_UserExample()
        {
            // User example:
            // Records: 12:03 - 12:05 (2m) and 12:07 - 12:08 (1m)
            // Time is now 12:09.
            // Add 6 minutes.
            // 1. Adds 1m forward after 12:08 (up to 12:09)
            // 2. Adds 2m backward before 12:07 (back to 12:05)
            // 3. Adds 3m backward before 12:03 (back to 12:00)
            // 4. Connects all together into 12:00 - 12:09
            var history = new ProjectTimeHistory();
            var timerId = Guid.NewGuid();
            var baseDate = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

            // Record 1: 12:03 - 12:05
            history.StartTracking(timerId, "ProjectX", baseDate.AddMinutes(3));
            history.StopTracking(timerId, baseDate.AddMinutes(5));

            // Record 2: 12:07 - 12:08
            history.StartTracking(timerId, "ProjectX", baseDate.AddMinutes(7));
            history.StopTracking(timerId, baseDate.AddMinutes(8));

            var intervalsBefore = history.GetIntervalsForTimer(timerId);
            Assert.Equal(2, intervalsBefore.Count);

            // Time is now 12:09, add 6 minutes
            DateTime now = baseDate.AddMinutes(9);
            history.AdjustIntervalsForTimer(timerId, TimeSpan.FromMinutes(6), "ProjectX", now, isRunning: false);

            var intervalsAfter = history.GetIntervalsForTimer(timerId);

            // All intervals connected together into one continuous record: 12:00 - 12:09
            Assert.Single(intervalsAfter);
            var merged = intervalsAfter[0];
            Assert.Equal(baseDate.AddMinutes(0), merged.StartUtc);
            Assert.Equal(baseDate.AddMinutes(9), merged.EndUtc);
            Assert.Equal(TimeSpan.FromMinutes(9), merged.EndUtc!.Value - merged.StartUtc);
        }

        [Fact]
        public void ProjectTimeHistory_AdjustIntervals_AddBackward_PartialOverlapDoesNotMergeGaps()
        {
            // Record 1: 12:00 - 12:02 (2m)
            // Record 2: 12:06 - 12:08 (2m)
            // Time is now 12:09.
            // Add 2 minutes:
            // 1m forward (12:08 -> 12:09)
            // 1m backward (12:06 -> 12:05)
            // Gap between 12:02 and 12:05 remains (3m gap), so records do NOT connect/merge.
            var history = new ProjectTimeHistory();
            var timerId = Guid.NewGuid();
            var baseDate = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

            history.StartTracking(timerId, "ProjectX", baseDate);
            history.StopTracking(timerId, baseDate.AddMinutes(2));

            history.StartTracking(timerId, "ProjectX", baseDate.AddMinutes(6));
            history.StopTracking(timerId, baseDate.AddMinutes(8));

            DateTime now = baseDate.AddMinutes(9);
            history.AdjustIntervalsForTimer(timerId, TimeSpan.FromMinutes(2), "ProjectX", now, isRunning: false);

            var intervalsAfter = history.GetIntervalsForTimer(timerId);
            Assert.Equal(2, intervalsAfter.Count);

            // Record 1 untouched: 12:00 - 12:02
            Assert.Equal(baseDate, intervalsAfter[0].StartUtc);
            Assert.Equal(baseDate.AddMinutes(2), intervalsAfter[0].EndUtc);

            // Record 2 expanded to 12:05 - 12:09 (4m)
            Assert.Equal(baseDate.AddMinutes(5), intervalsAfter[1].StartUtc);
            Assert.Equal(baseDate.AddMinutes(9), intervalsAfter[1].EndUtc);
        }

        [Fact]
        public void ProjectTimeHistory_Snapshot_CaptureAndRestore_RestoresSeparatedSegments()
        {
            var history = new ProjectTimeHistory();
            var timerId = Guid.NewGuid();
            var baseDate = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

            // Initial separated records: 12:03 - 12:05 and 12:07 - 12:08
            history.StartTracking(timerId, "ProjectX", baseDate.AddMinutes(3));
            history.StopTracking(timerId, baseDate.AddMinutes(5));
            history.StartTracking(timerId, "ProjectX", baseDate.AddMinutes(7));
            history.StopTracking(timerId, baseDate.AddMinutes(8));

            // Capture snapshot before edit
            var snapshot = history.CaptureTimerIntervalsSnapshot(timerId);
            Assert.Equal(2, snapshot.Count);

            // User mistakenly adds 1 hour -> all data connects together
            DateTime now = baseDate.AddMinutes(9);
            history.AdjustIntervalsForTimer(timerId, TimeSpan.FromHours(1), "ProjectX", now, isRunning: false);

            var intervalsAfterEdit = history.GetIntervalsForTimer(timerId);
            Assert.Single(intervalsAfterEdit); // Connected into 1

            // User undoes the change -> restores separated segments
            history.RestoreTimerIntervalsSnapshot(timerId, snapshot);

            var intervalsRestored = history.GetIntervalsForTimer(timerId);
            Assert.Equal(2, intervalsRestored.Count);
            Assert.Equal(baseDate.AddMinutes(3), intervalsRestored[0].StartUtc);
            Assert.Equal(baseDate.AddMinutes(5), intervalsRestored[0].EndUtc);
            Assert.Equal(baseDate.AddMinutes(7), intervalsRestored[1].StartUtc);
            Assert.Equal(baseDate.AddMinutes(8), intervalsRestored[1].EndUtc);
        }

        [Fact]
        public void ProjectTimeHistory_AdjustIntervals_RunningTimer_AddsBackwardAndMerges()
        {
            var history = new ProjectTimeHistory();
            var timerId = Guid.NewGuid();
            var baseDate = new DateTime(2026, 9, 24, 12, 0, 0, DateTimeKind.Utc);

            // Segment 1: 12:03 - 12:05 (closed)
            history.StartTracking(timerId, "ProjectX", baseDate.AddMinutes(3));
            history.StopTracking(timerId, baseDate.AddMinutes(5));

            // Segment 2: 12:07 - open (running)
            history.StartTracking(timerId, "ProjectX", baseDate.AddMinutes(7));

            DateTime now = baseDate.AddMinutes(9);
            // Add 5 minutes: cannot add forward since running interval ends at now;
            // adds 2m backward to 12:05, and 3m backward before 12:03 to 12:00.
            history.AdjustIntervalsForTimer(timerId, TimeSpan.FromMinutes(5), "ProjectX", now, isRunning: true);

            var intervals = history.GetIntervalsForTimer(timerId);
            Assert.Single(intervals);
            Assert.Equal(baseDate, intervals[0].StartUtc);
            Assert.Null(intervals[0].EndUtc); // Still running
        }
    }
}
