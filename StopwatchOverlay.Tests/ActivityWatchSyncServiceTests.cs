using System;
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
}
