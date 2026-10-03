using System;
using Xunit;

namespace StopwatchOverlay.Tests;

public class OverlayPresentationPolicyTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(0.35, 0.35)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    public void BackgroundOpacity_IsClampedToSurfaceRange(double value, double expected)
        => Assert.Equal(expected, OverlayPresentationPolicy.ClampBackgroundOpacity(value));

    [Fact]
    public void NonFiniteBackgroundOpacity_UsesSafeDefault()
        => Assert.Equal(0.5, OverlayPresentationPolicy.ClampBackgroundOpacity(double.NaN));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("  ", false)]
    [InlineData("Programming", true)]
    public void ProjectNameRow_CollapsesWhenNameIsMissing(string? name, bool expected)
        => Assert.Equal(expected, OverlayPresentationPolicy.ShouldShowProjectName(name));

    [Fact]
    public void CombinedOverlay_SelectsOnlyTheActiveOwnedTimer()
    {
        var manager = new TimerSessionManager();
        TimerSession first = manager.Create();
        TimerSession second = manager.Create();
        manager.Activate(first);

        TimerSession? selected = OverlayPresentationPolicy.SelectCombinedTimer(
            manager.Sessions,
            manager.Active);

        Assert.Same(first, selected);
        Assert.NotSame(second, selected);
    }

    [Fact]
    public void CombinedOverlay_RejectsAnUnownedActiveTimer()
    {
        var manager = new TimerSessionManager();
        manager.Create();
        var foreign = new TimerSession(99);

        Assert.Null(OverlayPresentationPolicy.SelectCombinedTimer(manager.Sessions, foreign));
    }

    [Theory]
    [InlineData(5, 10)]
    [InlineData(10, 10)]
    [InlineData(35, 35)]
    [InlineData(100, 100)]
    [InlineData(150, 100)]
    public void InactiveSeparatedOpacity_IsClampedToRange(double value, double expected)
        => Assert.Equal(expected, OverlayPresentationPolicy.ClampInactiveSeparatedOpacity(value));

    [Fact]
    public void NonFiniteInactiveSeparatedOpacity_UsesSafeDefault()
        => Assert.Equal(35.0, OverlayPresentationPolicy.ClampInactiveSeparatedOpacity(double.NaN));

    [Fact]
    public void CombinedOverlay_SelectsFirstHerdTimer_WhenActiveTimerIsSeparated()
    {
        var manager = new TimerSessionManager();
        TimerSession first = manager.Create();
        TimerSession second = manager.Create();
        manager.Separate(first);
        manager.Activate(first);

        TimerSession? selected = OverlayPresentationPolicy.SelectCombinedTimer(
            manager.Sessions,
            manager.Active);

        Assert.Same(second, selected);
    }

    [Fact]
    public void CombinedOverlay_ReturnsNull_WhenAllTimersAreSeparated()
    {
        var manager = new TimerSessionManager();
        TimerSession first = manager.Create();
        manager.Separate(first);
        manager.Activate(first);

        TimerSession? selected = OverlayPresentationPolicy.SelectCombinedTimer(
            manager.Sessions,
            manager.Active);

        Assert.Null(selected);
    }
}
