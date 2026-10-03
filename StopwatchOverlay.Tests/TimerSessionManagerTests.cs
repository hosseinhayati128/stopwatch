using System;
using Xunit;

namespace StopwatchOverlay.Tests
{
    public class TimerSessionManagerTests
    {
        [Fact]
        public void EmptyManager_AllowsZeroSessions()
        {
            var manager = new TimerSessionManager();

            Assert.Empty(manager.Sessions);
            Assert.Equal(0, manager.Count);
            Assert.Null(manager.Active);
            Assert.Null(manager.CycleNext());
            Assert.False(manager.CloseActive());
        }

        [Fact]
        public void Create_AssignsMonotonicNumbersAndActivatesNewest()
        {
            var manager = new TimerSessionManager();

            var first = manager.Create();
            var second = manager.Create();
            var third = manager.Create();

            Assert.Equal(new[] { 1, 2, 3 }, new[] { first.Number, second.Number, third.Number });
            Assert.Same(third, manager.Active);
        }

        [Fact]
        public void ClosedTimerNumber_IsNeverReused()
        {
            var manager = new TimerSessionManager();
            manager.Create();
            var second = manager.Create();
            manager.Create();

            Assert.True(manager.Close(second));
            var fourth = manager.Create();

            Assert.Equal(4, fourth.Number);
            Assert.Equal(new[] { 1, 3, 4 }, new[]
            {
                manager.Sessions[0].Number,
                manager.Sessions[1].Number,
                manager.Sessions[2].Number
            });
        }

        [Fact]
        public void CycleNext_UsesCreationOrderAndWraps()
        {
            var manager = new TimerSessionManager();
            var first = manager.Create();
            var second = manager.Create();
            var third = manager.Create();

            Assert.Same(first, manager.CycleNext());
            Assert.Same(second, manager.CycleNext());
            Assert.Same(third, manager.CycleNext());
        }

        [Fact]
        public void Activate_RejectsForeignSessionWithoutChangingActive()
        {
            var manager = new TimerSessionManager();
            var owned = manager.Create();
            var foreign = new TimerSession(99);

            Assert.False(manager.Activate(foreign));
            Assert.False(manager.Activate(null));
            Assert.Same(owned, manager.Active);
        }

        [Fact]
        public void CloseActive_SelectsNextNeighborAndEventuallyAllowsZero()
        {
            var manager = new TimerSessionManager();
            var first = manager.Create();
            var second = manager.Create();
            var third = manager.Create();
            manager.Activate(second);

            Assert.True(manager.CloseActive());
            Assert.Same(third, manager.Active);

            Assert.True(manager.CloseActive());
            Assert.Same(first, manager.Active);

            Assert.True(manager.CloseActive());
            Assert.Null(manager.Active);
            Assert.Empty(manager.Sessions);
        }

        [Fact]
        public void ClosingInactiveTimer_PreservesActiveTimer()
        {
            var manager = new TimerSessionManager();
            var first = manager.Create();
            var active = manager.Create();

            Assert.True(manager.Close(first));

            Assert.Same(active, manager.Active);
            Assert.Single(manager.Sessions);
        }

        [Fact]
        public void Sessions_KeepIndependentRuntimeAndPresentationState()
        {
            var manager = new TimerSessionManager();
            var first = manager.Create();
            var second = manager.Create();

            first.IsRunning = true;
            first.Mode = 2;
            first.CountdownRemaining = System.TimeSpan.FromSeconds(30);
            first.Name = "Tea";
            first.LapTimes.Add("Lap 1: 00:01");

            Assert.False(second.IsRunning);
            Assert.Equal(0, second.Mode);
            Assert.Equal(System.TimeSpan.Zero, second.CountdownRemaining);
            Assert.Equal(string.Empty, second.Name);
            Assert.Empty(second.LapTimes);
        }

        [Fact]
        public void ResetForProjectSwitch_ResetsPausedRuntimeAndClearsSessionDetails()
        {
            var timer = new TimerSession(1)
            {
                Name = "New project",
                IsRunning = false,
                CountdownInitialized = true,
                LastCountdownUpdateUtc = DateTime.UtcNow,
                LapCount = 1,
                RecBlinkVisible = true
            };
            timer.RestoreElapsed(TimeSpan.FromMinutes(12), start: false);
            timer.LapTimes.Add("Lap 1: 00:12:00");

            Assert.True(timer.HasAccumulatedTime);

            timer.ResetForProjectSwitch();

            Assert.Equal(TimeSpan.Zero, timer.Elapsed);
            Assert.False(timer.HasAccumulatedTime);
            Assert.False(timer.IsRunning);
            Assert.False(timer.Stopwatch.IsRunning);
            Assert.Equal("New project", timer.Name);
            Assert.Empty(timer.LapTimes);
            Assert.Equal(0, timer.LapCount);
            Assert.False(timer.CountdownInitialized);
            Assert.Equal(default, timer.LastCountdownUpdateUtc);
            Assert.False(timer.RecBlinkVisible);
        }

        [Fact]
        public void ResetForProjectSwitch_RestartsUnderlyingClockWhenTimerIsRunning()
        {
            var timer = new TimerSession(1)
            {
                Name = "New project",
                IsRunning = true
            };
            timer.RestoreElapsed(TimeSpan.FromMinutes(12), start: true);

            timer.ResetForProjectSwitch();

            Assert.True(timer.IsRunning);
            Assert.True(timer.Stopwatch.IsRunning);
            Assert.Equal(TimeSpan.Zero, timer.ElapsedOffset);
        }

        [Fact]
        public void SeparateAndMerge_UpdatesHerdAndSeparatedCollections()
        {
            var manager = new TimerSessionManager();
            var t1 = manager.Create();
            var t2 = manager.Create();
            var t3 = manager.Create();

            Assert.Equal(3, manager.HerdSessions.Count);
            Assert.Empty(manager.SeparatedSessions);

            Assert.True(manager.Separate(t2));
            Assert.True(t2.IsSeparated);
            Assert.Equal(2, manager.HerdSessions.Count);
            Assert.Single(manager.SeparatedSessions);
            Assert.Same(t2, manager.SeparatedSessions[0]);

            Assert.True(manager.Merge(t2));
            Assert.False(t2.IsSeparated);
            Assert.Equal(3, manager.HerdSessions.Count);
            Assert.Empty(manager.SeparatedSessions);
        }

        [Fact]
        public void CycleNextHerd_SkipsSeparatedTimers()
        {
            var manager = new TimerSessionManager();
            var t1 = manager.Create();
            var t2 = manager.Create();
            var t3 = manager.Create();

            manager.Separate(t2);
            manager.Activate(t1);

            Assert.Same(t3, manager.CycleNextHerd());
            Assert.Same(t1, manager.CycleNextHerd());
            Assert.Same(t3, manager.CycleNextHerd());
        }

        [Fact]
        public void CycleNextSeparatedOrHerd_CyclesBetweenSeparatedAndHerd()
        {
            var manager = new TimerSessionManager();
            var t1 = manager.Create();
            var t2 = manager.Create();
            var t3 = manager.Create();

            manager.Separate(t2);
            manager.Separate(t3);
            manager.Activate(t1);

            Assert.Same(t2, manager.CycleNextSeparatedOrHerd());
            Assert.Same(t3, manager.CycleNextSeparatedOrHerd());
            Assert.Same(t1, manager.CycleNextSeparatedOrHerd());
            Assert.Same(t2, manager.CycleNextSeparatedOrHerd());
        }
    }
}
