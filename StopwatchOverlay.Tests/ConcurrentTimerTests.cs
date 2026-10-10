using System;
using StopwatchOverlay;
using Xunit;

namespace StopwatchOverlay.Tests;

public class ConcurrentTimerTests
{
    [Fact]
    public void AppSettings_ExclusiveTimerMode_DefaultsToFalse()
    {
        var settings = new AppSettings();
        Assert.False(settings.ExclusiveTimerMode);
    }

    [Fact]
    public void AppSettings_ExclusiveTimerMode_RoundtripsThroughNormalization()
    {
        var settings = new AppSettings
        {
            ExclusiveTimerMode = true
        };
        settings.NormalizeForRuntime();
        Assert.True(settings.ExclusiveTimerMode);
    }

    [Fact]
    public void AppSettings_TimerSafetyDefaults_MatchSpecification()
    {
        var settings = new AppSettings();
        Assert.Equal(5, settings.MinimumIntervalSeconds);
        Assert.Equal(8.0, settings.MaxContinuousTimerHours);
        Assert.Equal(120, settings.MaxTrackedPauseMinutes);
        Assert.False(settings.ExclusiveTimerMode);
    }
}
