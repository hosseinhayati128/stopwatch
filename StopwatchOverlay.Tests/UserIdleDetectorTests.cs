using System;
using System.Collections.Generic;
using System.Text.Json;
using StopwatchOverlay;
using Xunit;

namespace StopwatchOverlay.Tests;

public sealed class UserIdleDetectorTests
{
    [Fact]
    public void CustomIdleTimeProvider_OverridesDefaultDetection()
    {
        try
        {
            UserIdleDetector.CustomIdleTimeProvider = () => TimeSpan.FromMinutes(12.5);
            TimeSpan idle = UserIdleDetector.GetIdleTime();
            Assert.Equal(TimeSpan.FromMinutes(12.5), idle);
        }
        finally
        {
            UserIdleDetector.CustomIdleTimeProvider = null;
        }
    }

    [Fact]
    public void DefaultIdleDetection_ReturnsNonNegativeSpan()
    {
        UserIdleDetector.CustomIdleTimeProvider = null;
        TimeSpan idle = UserIdleDetector.GetIdleTime();
        Assert.True(idle >= TimeSpan.Zero, "Idle time should never be negative");
    }

    [Fact]
    public void DefaultSettings_IdleStopIsDeactive()
    {
        var settings = new AppSettings();

        Assert.False(settings.IdleStopUnnamedTimers, "Unnamed timers should be deactive by default");
        Assert.Equal(5, settings.DefaultIdleStopTimeoutMinutes);
        Assert.True(settings.IdleStopSubtractDuration);
        Assert.NotNull(settings.ProjectIdleRules);
        Assert.Empty(settings.ProjectIdleRules);

        // Check project lookup
        Assert.False(settings.IsIdleStopActiveForProject(null, out int timeout1, out bool subtract1));
        Assert.Equal(5, timeout1);
        Assert.True(subtract1);

        Assert.False(settings.IsIdleStopActiveForProject("ClientWork", out int timeout2, out bool subtract2));
        Assert.Equal(5, timeout2);
        Assert.True(subtract2);
    }

    [Fact]
    public void UnnamedTimers_CanBeActivatedIndependently()
    {
        var settings = new AppSettings
        {
            IdleStopUnnamedTimers = true,
            DefaultIdleStopTimeoutMinutes = 15,
            IdleStopSubtractDuration = false
        };

        Assert.True(settings.IsIdleStopActiveForProject(null, out int t1, out bool s1));
        Assert.Equal(15, t1);
        Assert.False(s1);

        Assert.True(settings.IsIdleStopActiveForProject("", out int t2, out bool s2));
        Assert.Equal(15, t2);
        Assert.False(s2);

        Assert.True(settings.IsIdleStopActiveForProject("   ", out int t3, out bool s3));
        Assert.Equal(15, t3);
        Assert.False(s3);

        // Named projects are still deactive unless configured
        Assert.False(settings.IsIdleStopActiveForProject("Writing", out int t4, out _));
        Assert.Equal(15, t4);
    }

    [Fact]
    public void SetProjectIdleRule_EnablesAndDisablesPerProject()
    {
        var settings = new AppSettings();

        // Activate for "Deep Work" with 8 minutes timeout
        settings.SetProjectIdleRule("Deep Work", enabled: true, idleMinutes: 8);

        Assert.True(settings.IsIdleStopActiveForProject("Deep Work", out int t1, out bool sub1));
        Assert.Equal(8, t1);
        Assert.True(sub1);

        // Case insensitive match
        Assert.True(settings.IsIdleStopActiveForProject("deep work", out int t2, out _));
        Assert.Equal(8, t2);

        Assert.True(settings.IsIdleStopActiveForProject("  DEEP WORK  ", out int t3, out _));
        Assert.Equal(8, t3);

        // Another project is still deactive
        Assert.False(settings.IsIdleStopActiveForProject("Email", out int t4, out _));
        Assert.Equal(5, t4);

        // Deactivate "Deep Work"
        settings.SetProjectIdleRule("Deep Work", enabled: false, idleMinutes: 8);
        Assert.False(settings.IsIdleStopActiveForProject("Deep Work", out int t5, out _));
        Assert.Equal(8, t5);
    }

    [Fact]
    public void SetProjectIdleRule_ClampsTimeoutBounds()
    {
        var settings = new AppSettings();

        // 0 minutes clamped to 1
        settings.SetProjectIdleRule("ProjectA", enabled: true, idleMinutes: 0);
        settings.IsIdleStopActiveForProject("ProjectA", out int t1, out _);
        Assert.Equal(1, t1);

        // Negative minutes clamped to 1
        settings.SetProjectIdleRule("ProjectB", enabled: true, idleMinutes: -10);
        settings.IsIdleStopActiveForProject("ProjectB", out int t2, out _);
        Assert.Equal(1, t2);

        // Over 1440 minutes clamped to 1440
        settings.SetProjectIdleRule("ProjectC", enabled: true, idleMinutes: 5000);
        settings.IsIdleStopActiveForProject("ProjectC", out int t3, out _);
        Assert.Equal(1440, t3);
    }

    [Fact]
    public void JsonSerialization_PreservesIdleStopRules()
    {
        var original = new AppSettings
        {
            IdleStopUnnamedTimers = true,
            DefaultIdleStopTimeoutMinutes = 10,
            IdleStopSubtractDuration = true
        };
        original.SetProjectIdleRule("Coding", enabled: true, idleMinutes: 7);
        original.SetProjectIdleRule("Reading", enabled: false, idleMinutes: 15);

        string json = JsonSerializer.Serialize(original);
        var restored = JsonSerializer.Deserialize<AppSettings>(json);
        Assert.NotNull(restored);

        restored.NormalizeForRuntime();

        Assert.True(restored.IdleStopUnnamedTimers);
        Assert.Equal(10, restored.DefaultIdleStopTimeoutMinutes);
        Assert.True(restored.IdleStopSubtractDuration);

        Assert.True(restored.IsIdleStopActiveForProject("coding", out int t1, out bool sub1));
        Assert.Equal(7, t1);
        Assert.True(sub1);

        Assert.False(restored.IsIdleStopActiveForProject("Reading", out int t2, out _));
        Assert.Equal(15, t2);

        Assert.False(restored.IsIdleStopActiveForProject("Unconfigured", out int t3, out _));
        Assert.Equal(10, t3);
    }

    [Fact]
    public void TimerSession_SubtractIdleDuration_CorrectlyAdjustsElapsed()
    {
        var timer = new TimerSession(1)
        {
            Name = "ActiveProject"
        };
        timer.Stopwatch.Restore(TimeSpan.FromMinutes(25), start: false);

        // If system was idle for 10 minutes and user stepped away
        TimeSpan idleDuration = TimeSpan.FromMinutes(10);
        TimeSpan currentElapsed = timer.Elapsed;
        TimeSpan adjusted = currentElapsed > idleDuration ? currentElapsed - idleDuration : TimeSpan.Zero;
        timer.Stopwatch.Restore(adjusted, start: false);

        Assert.Equal(TimeSpan.FromMinutes(15), timer.Elapsed);
    }

    [Fact]
    public void TimerSession_SubtractIdleDuration_ClampsAtZero()
    {
        var timer = new TimerSession(1)
        {
            Name = "ActiveProject"
        };
        timer.Stopwatch.Restore(TimeSpan.FromMinutes(5), start: false);

        // Idle duration greater than elapsed
        TimeSpan idleDuration = TimeSpan.FromMinutes(10);
        TimeSpan currentElapsed = timer.Elapsed;
        TimeSpan adjusted = currentElapsed > idleDuration ? currentElapsed - idleDuration : TimeSpan.Zero;
        timer.Stopwatch.Restore(adjusted, start: false);

        Assert.Equal(TimeSpan.Zero, timer.Elapsed);
    }
}
