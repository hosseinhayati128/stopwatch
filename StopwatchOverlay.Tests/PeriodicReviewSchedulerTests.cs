using System;
using System.Threading;
using System.Threading.Tasks;
using StopwatchOverlay.PeriodicReview;
using Xunit;

namespace StopwatchOverlay.Tests;

public class PeriodicReviewSchedulerTests
{
    [Fact]
    public void ReconfigurationReleasesPendingPromptAtomicallyButPreservesAnOpenReview()
    {
        var clock = new Clock(); using var service = new PeriodicReviewService(() => new() { PeriodicReviewEnabled = true }, clock);
        int calls = 0; service.PromptRequested += (_, _) => calls++;
        clock.Now = clock.Now.AddMinutes(30); var pending = clock.Timer!; pending.Fire();
        Assert.True(service.IsPromptShowing);
        service.RestartTimer(cancelPendingPrompt: true);
        Assert.False(service.IsPromptShowing); Assert.Equal(clock.Now.UtcDateTime.AddMinutes(30), service.NextPromptUtc);
        pending.Fire(); Assert.Equal(1, calls);
        service.BeginReview(); service.RestartTimer();
        Assert.True(service.IsPromptShowing);
        clock.Now = clock.Now.AddHours(1); clock.Timer!.Fire(); Assert.Equal(1, calls);
    }

    [Fact]
    public void EditingIntervalUsesFinalValueWhileUnrelatedEditsRetainDeadline()
    {
        var clock = new Clock(); var settings = new AppSettings { PeriodicReviewEnabled = true };
        using var service = new PeriodicReviewService(() => settings, clock);
        foreach (int interval in new[] { 1, 14, 144, 1440 })
        {
            settings.PeriodicReviewIntervalMinutes = interval; service.RestartTimer();
            Assert.Equal(clock.Now.UtcDateTime.AddMinutes(interval), service.NextPromptUtc);
        }
        var due = service.NextPromptUtc; clock.Now = clock.Now.AddMinutes(5);
        settings.PeriodicReviewMinActivityPercent = 5; service.RestartTimer();
        Assert.Equal(due, service.NextPromptUtc);
    }

    [Fact]
    public void TimingUsesConfiguredDeadlinesWithoutPollingOrDuplicateRequests()
    {
        var clock = new Clock();
        var settings = new AppSettings { PeriodicReviewEnabled = true, PeriodicReviewIntervalMinutes = 45, PeriodicReviewSnoozeMinutes = 10 };
        using var service = new PeriodicReviewService(() => settings, clock);
        int calls = 0; DateTime start = default, end = default;
        service.PromptRequested += (a, b) => { calls++; start = a; end = b; };
        Assert.Equal(TimeSpan.FromMinutes(45), clock.Timer!.Due);
        Assert.Equal(Timeout.InfiniteTimeSpan, clock.Timer.Period);
        clock.Now = clock.Now.AddMinutes(45); clock.Timer.Fire(); clock.Timer.Fire();
        Assert.Equal(1, calls); Assert.True(service.IsPromptShowing); Assert.Equal(TimeSpan.FromMinutes(45), end - start);
        service.OnPromptIgnored(); Assert.Equal(clock.Now.UtcDateTime.AddMinutes(10), service.NextPromptUtc);
        clock.Now = clock.Now.AddMinutes(10); clock.Timer.Fire(); Assert.Equal(2, calls);
        service.OnPromptSkipped(); Assert.Equal(clock.Now.UtcDateTime.AddMinutes(45), service.NextPromptUtc);
        service.BeginReview(); Assert.True(service.IsPromptShowing);
        clock.Now = clock.Now.AddHours(1); clock.Timer.Fire(); Assert.Equal(2, calls);
        service.ResetPromptShowing(); Assert.False(service.IsPromptShowing);
        Assert.Equal(clock.Now.UtcDateTime.AddMinutes(45), service.NextPromptUtc);
        service.OnReviewCompleted(clock.Now.UtcDateTime); Assert.Equal(clock.Now.UtcDateTime.AddMinutes(45), service.NextPromptUtc);
    }

    [Fact]
    public void StaleTicksCannotPromptAfterReconfigurationOrShutdown()
    {
        var clock = new Clock(); var settings = new AppSettings { PeriodicReviewEnabled = true };
        int reads = 0, calls = 0;
        using var service = new PeriodicReviewService(() => { reads++; return settings; }, clock);
        service.PromptRequested += (_, _) => calls++;
        var old = clock.Timer!; settings.PeriodicReviewIntervalMinutes = 10; service.RestartTimer();
        clock.Now = clock.Now.AddMinutes(30); old.Fire(); Assert.Equal(0, calls);
        int configuredReads = reads; clock.Timer!.Fire(); Assert.Equal(1, calls); Assert.Equal(configuredReads, reads);
        settings.PeriodicReviewEnabled = false; service.RestartTimer(); old = clock.Timer!; old.Fire();
        Assert.False(service.IsPromptShowing); Assert.Null(service.NextPromptUtc); Assert.Equal(1, calls);
        settings.PeriodicReviewEnabled = true; service.RestartTimer(); old = clock.Timer!;
        service.Dispose(); clock.Now = clock.Now.AddHours(1); old.Fire(); service.RestartTimer();
        Assert.Equal(1, calls); Assert.Null(service.NextPromptUtc);
    }

    [Theory]
    [InlineData(-60, 60)]
    [InlineData(-1441, 30)]
    [InlineData(10, 30)]
    public void ReviewPeriodUsesRecentCompletionOnly(int lastMinutes, int expectedMinutes)
    {
        var clock = new Clock(); var settings = new AppSettings { LastPeriodicReviewCompletedUtc = clock.Now.UtcDateTime.AddMinutes(lastMinutes) };
        using var service = new PeriodicReviewService(() => settings, clock);
        service.GetReviewPeriod(out var start, out var end);
        Assert.Equal(clock.Now.UtcDateTime, end); Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), end - start);
    }

    [Fact]
    public void MissingSubscriberDoesNotLeaveSchedulerStuck()
    {
        var clock = new Clock(); using var service = new PeriodicReviewService(() => new() { PeriodicReviewEnabled = true }, clock);
        clock.Now = clock.Now.AddMinutes(30); clock.Timer!.Fire();
        Assert.False(service.IsPromptShowing); Assert.Equal(clock.Now.UtcDateTime.AddMinutes(30), service.NextPromptUtc);
    }

    [Fact]
    public void DisabledSchedulerCannotBeRearmedByPromptOutcomes()
    {
        var settings = new AppSettings();
        using var service = new PeriodicReviewService(() => settings);
        service.OnPromptIgnored();
        Assert.Null(service.NextPromptUtc);
        service.OnPromptSkipped();
        Assert.Null(service.NextPromptUtc);
        service.OnReviewCompleted(DateTime.UtcNow);
        Assert.Null(service.NextPromptUtc);
    }

    [Fact]
    public void DisposedSchedulerCannotBeRestarted()
    {
        var settings = new AppSettings { PeriodicReviewEnabled = true };
        using var service = new PeriodicReviewService(() => settings);
        service.Dispose();
        service.RestartTimer();
        service.SetNextPromptUtc(DateTime.UtcNow);
        service.OnPromptIgnored();
        Assert.Null(service.NextPromptUtc);
        Assert.False(service.IsPromptShowing);
    }

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    public void NonFiniteGroupingRestoresDefault(double value)
    {
        var settings = new AppSettings { PeriodicReviewMinActivityPercent = value };
        settings.NormalizeForRuntime();
        Assert.Equal(3, settings.PeriodicReviewMinActivityPercent);
    }

    private sealed class Clock : TimeProvider
    {
        internal DateTimeOffset Now = new(2026, 11, 15, 10, 0, 0, TimeSpan.Zero);
        internal TestTimer? Timer;
        public override DateTimeOffset GetUtcNow() => Now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => Timer = new(callback, state, dueTime, period);
    }
    private sealed class TestTimer(TimerCallback callback, object? state, TimeSpan due, TimeSpan period) : ITimer
    {
        internal TimeSpan Due = due, Period = period;
        internal void Fire() => callback(state); // Deliberately deliver callbacks already queued before disposal.
        public bool Change(TimeSpan dueTime, TimeSpan period) { Due = dueTime; Period = period; return true; }
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
