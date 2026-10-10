using System;
using StopwatchOverlay;
using Xunit;

namespace StopwatchOverlay.Tests;

public class RunawayTimerTests
{
    [Fact]
    public void ShouldPauseRunaway_WhenElapsedExceedsCeilingAndUserIdle_ReturnsTrue()
    {
        bool shouldPause = RunawayTimerGuard.ShouldPauseRunaway(
            TimeSpan.FromHours(9),
            TimeSpan.FromMinutes(20),
            8.0);

        Assert.True(shouldPause);
    }

    [Fact]
    public void ShouldPauseRunaway_WhenElapsedBelowCeiling_ReturnsFalse()
    {
        bool shouldPause = RunawayTimerGuard.ShouldPauseRunaway(
            TimeSpan.FromHours(7),
            TimeSpan.FromHours(3),
            8.0);

        Assert.False(shouldPause);
    }

    [Fact]
    public void ShouldPauseRunaway_WhenUserNotIdle_ReturnsFalse()
    {
        bool shouldPause = RunawayTimerGuard.ShouldPauseRunaway(
            TimeSpan.FromHours(9),
            TimeSpan.FromMinutes(5),
            8.0);

        Assert.False(shouldPause);
    }

    [Fact]
    public void CalculateExcess_ComputesCorrectDifference()
    {
        var excess = RunawayTimerGuard.CalculateExcess(TimeSpan.FromHours(10), 8.0);
        Assert.Equal(TimeSpan.FromHours(2), excess);

        var noExcess = RunawayTimerGuard.CalculateExcess(TimeSpan.FromHours(6), 8.0);
        Assert.Equal(TimeSpan.Zero, noExcess);
    }

    [Fact]
    public void CrossedMidnight_DetectsDifferentCalendarDays()
    {
        DateTime start = new(2026, 9, 30, 16, 0, 0, DateTimeKind.Utc);
        DateTime end = new(2026, 10, 1, 6, 0, 0, DateTimeKind.Utc);

        Assert.True(RunawayTimerGuard.CrossedMidnight(start, end));
    }
}
