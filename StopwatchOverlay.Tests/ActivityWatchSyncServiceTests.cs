using System;
using System.Threading;
using System.Threading.Tasks;
using StopwatchOverlay.ActivityWatch;
using Xunit;

namespace StopwatchOverlay.Tests;

public class ActivityWatchSyncServiceTests
{
    [Fact]
    public void Constructor_ThrowsOnNullSettingsProvider()
    {
        Assert.Throws<ArgumentNullException>(() => new ActivityWatchSyncService(null!));
    }

    [Fact]
    public void Constructor_DoesNotThrowWithValidProvider()
    {
        var settings = new AppSettings
        {
            ActivityWatchEnabled = false,
            ActivityWatchPeriodicSyncEnabled = true
        };

        using var service = new ActivityWatchSyncService(() => settings);
        Assert.NotNull(service);
    }

    [Fact]
    public void RestartTimer_WhenDisabled_DoesNotThrow()
    {
        var settings = new AppSettings
        {
            ActivityWatchEnabled = false,
            ActivityWatchPeriodicSyncEnabled = true
        };

        using var service = new ActivityWatchSyncService(() => settings);
        service.RestartTimer();
    }

    [Fact]
    public void RestartTimer_WhenEnabled_StartsTimerWithoutError()
    {
        var settings = new AppSettings
        {
            ActivityWatchEnabled = true,
            ActivityWatchPeriodicSyncEnabled = true,
            ActivityWatchPeriodicSyncIntervalMinutes = 60
        };

        using var service = new ActivityWatchSyncService(() => settings);
        service.RestartTimer();
    }

    [Fact]
    public void Dispose_CanBeCalledMultipleTimes()
    {
        var settings = new AppSettings
        {
            ActivityWatchEnabled = true,
            ActivityWatchPeriodicSyncEnabled = true
        };

        var service = new ActivityWatchSyncService(() => settings);
        service.Dispose();
        service.Dispose(); // second call should be no-op
    }
    private static ActivityWatchSyncService Create(AppSettings settings,
        Func<AppSettings, CancellationToken, Task<ActivityWatchSyncResult>> sync, FakeClock clock)
    {
        var constructor = typeof(ActivityWatchSyncService).GetConstructor(new[] {
            typeof(Func<AppSettings>), typeof(Func<AppSettings, CancellationToken, Task<ActivityWatchSyncResult>>), typeof(TimeProvider) });
        Assert.True(constructor != null, "Automatic sync needs an isolated operation and timer seam.");
        return (ActivityWatchSyncService)constructor!.Invoke(new object[] { new Func<AppSettings>(() => settings), sync, clock });
    }

    [Fact]
    public void Schedule_UsesFrozenSettingsAndStopsWhenDisabled()
    {
        var settings = new AppSettings { ActivityWatchEnabled = true, ActivityWatchPeriodicSyncEnabled = true,
            ActivityWatchPeriodicSyncIntervalMinutes = 30, ObsidianVaultFolder = "sample" };
        var clock = new FakeClock(); int calls = 0; string? folder = null;
        using var service = Create(settings, (s, _) => { calls++; folder = s.ObsidianVaultFolder; return Task.FromResult(new ActivityWatchSyncResult(true, 1, 0, "sample")); }, clock);
        Assert.Equal(TimeSpan.FromMinutes(1), clock.Timer!.Due); Assert.Equal(TimeSpan.FromMinutes(30), clock.Timer.Period);
        settings.ObsidianVaultFolder = "edited"; clock.Timer.Fire(); Assert.Equal(1, calls); Assert.Equal("sample", folder);
        settings.ActivityWatchEnabled = false; service.RestartTimer(); clock.Timer.Fire(); Assert.Equal(1, calls); Assert.True(clock.Timer.Disposed);
    }

    [Fact]
    public async Task Restart_CancelsOldWorkAndDoesNotPublishItsResult()
    {
        var settings = new AppSettings { ActivityWatchEnabled = true, ObsidianVaultFolder = "sample" };
        var pending = new TaskCompletionSource<ActivityWatchSyncResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default; int notifications = 0;
        using var service = Create(settings, (_, ct) => { token = ct; return pending.Task; }, new());
        service.SyncCompleted += _ => notifications++;
        var run = service.TriggerSyncOnceAsync();
        Assert.False((await service.TriggerSyncOnceAsync()).Success);
        settings.ActivityWatchEnabled = false; service.RestartTimer(); Assert.True(token.IsCancellationRequested);
        pending.SetResult(new(true, 1, 0, "sample")); await run; Assert.Equal(0, notifications);
    }

    [Fact]
    public async Task Dispose_CancelsAndSuppressesLateCompletion()
    {
        var pending = new TaskCompletionSource<ActivityWatchSyncResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken token = default; int notifications = 0;
        var service = Create(new(), (_, ct) => { token = ct; return pending.Task; }, new());
        service.SyncCompleted += _ => notifications++; var run = service.TriggerSyncOnceAsync();
        service.Dispose(); service.Dispose(); Assert.True(token.IsCancellationRequested);
        pending.SetResult(new(true, 0, 0, "sample")); await run;
        Assert.Equal(0, notifications); Assert.False((await service.TriggerSyncOnceAsync()).Success);
    }

    [Theory]
    [InlineData(false, true, "sample", 0)]
    [InlineData(true, false, "sample", 0)]
    [InlineData(true, true, "", 0)]
    [InlineData(true, true, "sample", 1)]
    public void AfterExport_ObeysIntegrationPreferenceAndFolder(bool enabled, bool afterExport, string folder, int expected)
    {
        int calls = 0; var settings = new AppSettings { ActivityWatchEnabled = enabled,
            ActivityWatchSyncOnStopwatchSync = afterExport, ObsidianVaultFolder = folder };
        using var service = Create(settings, (_, _) => { calls++; return Task.FromResult(new ActivityWatchSyncResult(true, 0, 0, "sample")); }, new());
        typeof(ActivityWatchSyncService).GetMethod("SyncAfterStopwatchExport")!.Invoke(service, null);
        Assert.Equal(expected, calls);
    }

    private sealed class FakeClock : TimeProvider
    {
        internal FakeTimer? Timer;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
            => Timer = new(callback, state, dueTime, period);
    }
    private sealed class FakeTimer(TimerCallback callback, object? state, TimeSpan due, TimeSpan period) : ITimer
    {
        internal TimeSpan Due = due, Period = period; internal bool Disposed;
        internal void Fire() { if (!Disposed) callback(state); }
        public bool Change(TimeSpan dueTime, TimeSpan period) { Due = dueTime; Period = period; return !Disposed; }
        public void Dispose() => Disposed = true;
        public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
    }

}
