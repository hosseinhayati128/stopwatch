using System;
using System.Collections.Generic;
using System.Linq;
using StopwatchOverlay;
using StopwatchOverlay.PeriodicReview;
using Xunit;

namespace StopwatchOverlay.Tests;

public class PeriodicReviewTests
{
    [Fact]
    public void AppSettings_PeriodicReviewDefaults_AreDisabledByDefault()
    {
        var settings = new AppSettings();

        // Must be disabled by default according to requirements
        Assert.False(settings.PeriodicReviewEnabled);
        Assert.Equal(30, settings.PeriodicReviewIntervalMinutes);
        Assert.Equal(30, settings.PeriodicReviewSnoozeMinutes);
        Assert.Equal(5, settings.PeriodicReviewAutoDismissSeconds);
        Assert.Equal(15, settings.PeriodicReviewMinDurationSeconds);
        Assert.Null(settings.LastPeriodicReviewCompletedUtc);
    }

    [Fact]
    public void AppSettings_PeriodicReviewNormalization_ClampsValues()
    {
        var settings = new AppSettings
        {
            PeriodicReviewIntervalMinutes = -10,
            PeriodicReviewSnoozeMinutes = 0,
            PeriodicReviewAutoDismissSeconds = -5,
            PeriodicReviewMinDurationSeconds = -100
        };

        settings.NormalizeForRuntime();

        Assert.Equal(1, settings.PeriodicReviewIntervalMinutes);
        Assert.Equal(1, settings.PeriodicReviewSnoozeMinutes);
        Assert.Equal(1, settings.PeriodicReviewAutoDismissSeconds);
        Assert.Equal(0, settings.PeriodicReviewMinDurationSeconds);
    }

    [Fact]
    public void FilterAndBridgeActivities_FiltersOutActivitiesBelowThreshold()
    {
        var baseTime = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

        var events = new List<RawActivityEvent>
        {
            // 5 second activity (below 15s threshold, standalone)
            new(
                baseTime,
                baseTime.AddSeconds(5),
                "QuickApp.exe",
                "Flash Window",
                "App"
            ),
            // 120 second activity (above 15s threshold)
            new(
                baseTime.AddSeconds(10),
                baseTime.AddSeconds(130),
                "Code.exe",
                "main.cs - VSCode",
                "App"
            )
        };

        var filtered = PeriodicReviewDataAggregator.FilterAndBridgeActivities(
            events,
            minDurationSeconds: 15);

        Assert.Single(filtered);
        Assert.Equal("Code.exe", filtered[0].App);
        Assert.Equal(TimeSpan.FromSeconds(120), filtered[0].Duration);
    }

    [Fact]
    public void FilterAndBridgeActivities_SmartBridging_CombinesInterruptedActivity()
    {
        // Smart bridging requirement:
        // If app A is interrupted by brief activity B <= S seconds and returns to A,
        // bridge A across B into a single continuous block.
        var baseTime = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

        var events = new List<RawActivityEvent>
        {
            // App A for 300 seconds
            new(
                baseTime,
                baseTime.AddSeconds(300),
                "Code.exe",
                "IDE",
                "App"
            ),
            // Brief interruption B (10 seconds, <= 15s threshold)
            new(
                baseTime.AddSeconds(300),
                baseTime.AddSeconds(310),
                "Telegram.exe",
                "Notification",
                "App"
            ),
            // App A resumes for 200 seconds
            new(
                baseTime.AddSeconds(310),
                baseTime.AddSeconds(510),
                "Code.exe",
                "IDE",
                "App"
            )
        };

        var filtered = PeriodicReviewDataAggregator.FilterAndBridgeActivities(
            events,
            minDurationSeconds: 15);

        // After smart bridging, B (10s <= 15s) is absorbed, and A is bridged into one continuous item
        Assert.Single(filtered);
        Assert.Equal("Code.exe", filtered[0].App);
        Assert.Equal(baseTime.ToLocalTime().DateTime, filtered[0].StartLocal);
        Assert.Equal(baseTime.AddSeconds(510).ToLocalTime().DateTime, filtered[0].EndLocal);
        Assert.Equal(TimeSpan.FromSeconds(510), filtered[0].Duration);
    }

    [Fact]
    public void FilterAndBridgeActivities_DoesNotBridgeWhenInterruptionExceedsThreshold()
    {
        var baseTime = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

        var events = new List<RawActivityEvent>
        {
            // App A for 300 seconds
            new(
                baseTime,
                baseTime.AddSeconds(300),
                "Code.exe",
                "IDE",
                "App"
            ),
            // Interruption B (60 seconds, > 15s threshold)
            new(
                baseTime.AddSeconds(300),
                baseTime.AddSeconds(360),
                "Browser.exe",
                "Research Article",
                "App"
            ),
            // App A resumes for 200 seconds
            new(
                baseTime.AddSeconds(360),
                baseTime.AddSeconds(560),
                "Code.exe",
                "IDE",
                "App"
            )
        };

        var filtered = PeriodicReviewDataAggregator.FilterAndBridgeActivities(
            events,
            minDurationSeconds: 15);

        // Since B was 60s (> 15s), both A blocks and B remain separate
        Assert.Equal(3, filtered.Count);
        Assert.Equal("Code.exe", filtered[0].App);
        Assert.Equal("Browser.exe", filtered[1].App);
        Assert.Equal("Code.exe", filtered[2].App);
    }

    [Fact]
    public void SliceStopwatchIntervals_CreatesContiguousTrackedAndUntrackedSlots()
    {
        DateTime baseTime = new(2026, 9, 28, 10, 0, 0, DateTimeKind.Utc);
        DateTime endTime = baseTime.AddMinutes(60);

        var history = new ProjectTimeHistory();
        // Add a 20-minute interval from 10:15 to 10:35
        history.AddManualInterval("Deep Work", baseTime.AddMinutes(15), baseTime.AddMinutes(35));

        var view = history.CreateView(endTime);
        var slots = PeriodicReviewDataAggregator.SliceStopwatchIntervals(baseTime, endTime, view);

        // Expected 3 slots:
        // 1. Untracked from 10:00 to 10:15 (15 min)
        // 2. Tracked "Deep Work" from 10:15 to 10:35 (20 min)
        // 3. Untracked from 10:35 to 11:00 (25 min)
        Assert.Equal(3, slots.Count);

        Assert.False(slots[0].IsTracked);
        Assert.Equal(baseTime, slots[0].StartUtc);
        Assert.Equal(baseTime.AddMinutes(15), slots[0].EndUtc);
        Assert.Null(slots[0].SelectedProjectName);

        Assert.True(slots[1].IsTracked);
        Assert.Equal(baseTime.AddMinutes(15), slots[1].StartUtc);
        Assert.Equal(baseTime.AddMinutes(35), slots[1].EndUtc);
        Assert.Equal("Deep Work", slots[1].SelectedProjectName);

        Assert.False(slots[2].IsTracked);
        Assert.Equal(baseTime.AddMinutes(35), slots[2].StartUtc);
        Assert.Equal(endTime, slots[2].EndUtc);
        Assert.Null(slots[2].SelectedProjectName);
    }

    [Fact]
    public void PeriodicReviewService_SchedulesCorrectlyOnIgnoredAndCompleted()
    {
        var settings = new AppSettings
        {
            PeriodicReviewEnabled = true,
            PeriodicReviewIntervalMinutes = 45,
            PeriodicReviewSnoozeMinutes = 10
        };

        var service = new PeriodicReviewService(() => settings);

        // Simulate prompt ignored -> should snooze for 10 minutes
        service.OnPromptIgnored();
        Assert.NotNull(service.NextPromptUtc);
        var expectedSnooze = DateTime.UtcNow.AddMinutes(10);
        Assert.True(Math.Abs((service.NextPromptUtc.Value - expectedSnooze).TotalSeconds) < 5);

        // Simulate prompt skipped -> should reschedule for 45 minutes
        service.OnPromptSkipped();
        Assert.NotNull(service.NextPromptUtc);
        var expectedSkip = DateTime.UtcNow.AddMinutes(45);
        Assert.True(Math.Abs((service.NextPromptUtc.Value - expectedSkip).TotalSeconds) < 5);

        // Simulate review completed -> should reschedule for 45 minutes
        service.OnReviewCompleted(DateTime.UtcNow);
        Assert.NotNull(service.NextPromptUtc);
        var expectedComplete = DateTime.UtcNow.AddMinutes(45);
        Assert.True(Math.Abs((service.NextPromptUtc.Value - expectedComplete).TotalSeconds) < 5);

        service.Dispose();
    }
}
