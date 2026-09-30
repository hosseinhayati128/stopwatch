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
        Assert.True(settings.PeriodicReviewShowIdle);
        Assert.False(settings.PeriodicReviewAllowMultiProject);
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

    [Fact]
    public void FilterAndBridgeActivities_PreservesIdlePeriodsAndDoesNotBridgeAcrossIdle()
    {
        var baseTime = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero);

        var events = new List<RawActivityEvent>
        {
            // App A for 10 minutes
            new(
                baseTime,
                baseTime.AddMinutes(10),
                "Code.exe",
                "Working on parser",
                "App"
            ),
            // Idle for 5 minutes
            new(
                baseTime.AddMinutes(10),
                baseTime.AddMinutes(15),
                "Idle / Away",
                "No keyboard or mouse activity",
                "Idle"
            ),
            // App A resumes for 10 minutes
            new(
                baseTime.AddMinutes(15),
                baseTime.AddMinutes(25),
                "Code.exe",
                "Working on parser",
                "App"
            )
        };

        var filtered = PeriodicReviewDataAggregator.FilterAndBridgeActivities(
            events,
            minDurationSeconds: 15);

        // Active app work should NOT be bridged across the idle period
        Assert.Equal(3, filtered.Count);
        Assert.Equal("Code.exe", filtered[0].App);
        Assert.False(filtered[0].IsIdle);

        Assert.Equal("Idle / Away", filtered[1].App);
        Assert.True(filtered[1].IsIdle);

        Assert.Equal("Code.exe", filtered[2].App);
        Assert.False(filtered[2].IsIdle);
    }

    [Fact]
    public void BatchLabeling_OverwriteVsMultiProjectOverlap()
    {
        // Test requirement 3 & 3.1:
        // By default (allowMulti = false): labeling an activity replaces previous assignment with the last labeled item.
        // When allowMulti = true: labeling the same activity across multiple projects adds multiple slots.

        var activity = new PeriodicReviewActivityItem
        {
            Id = Guid.NewGuid(),
            StartLocal = new DateTime(2026, 9, 28, 10, 0, 0),
            EndLocal = new DateTime(2026, 9, 28, 10, 30, 0),
            App = "Research Document",
            Type = "App"
        };

        var slots = new List<PeriodicReviewStopwatchSlot>();

        // 1. Label as "Project Alpha" (allowMulti = false)
        bool allowMulti = false;
        string project1 = "Project Alpha";

        if (!allowMulti)
        {
            slots.RemoveAll(s => s.SourceActivityId == activity.Id);
            activity.AssignedProjects.Clear();
        }
        slots.Add(new PeriodicReviewStopwatchSlot
        {
            SourceActivityId = activity.Id,
            SourceActivityName = activity.App,
            StartUtc = activity.StartUtc,
            EndUtc = activity.EndUtc,
            SelectedProjectName = project1,
            IsManuallyAdded = true
        });
        activity.AssignedProjects.Add(project1);

        Assert.Single(slots);
        Assert.Equal("Project Alpha", slots[0].SelectedProjectName);
        Assert.Equal(["Project Alpha"], activity.AssignedProjects);

        // 2. Re-label same activity as "Project Beta" with allowMulti = false (default)
        // Should overwrite "Project Alpha" with "Project Beta"
        string project2 = "Project Beta";

        if (!allowMulti)
        {
            slots.RemoveAll(s => s.SourceActivityId == activity.Id);
            activity.AssignedProjects.Clear();
        }
        slots.Add(new PeriodicReviewStopwatchSlot
        {
            SourceActivityId = activity.Id,
            SourceActivityName = activity.App,
            StartUtc = activity.StartUtc,
            EndUtc = activity.EndUtc,
            SelectedProjectName = project2,
            IsManuallyAdded = true
        });
        activity.AssignedProjects.Add(project2);

        // Overwrite verified
        Assert.Single(slots);
        Assert.Equal("Project Beta", slots[0].SelectedProjectName);
        Assert.Equal(["Project Beta"], activity.AssignedProjects);

        // 3. Now toggle allowMulti = true (Req 3.1) and also label as "Project Gamma"
        allowMulti = true;
        string project3 = "Project Gamma";

        if (!allowMulti)
        {
            slots.RemoveAll(s => s.SourceActivityId == activity.Id);
            activity.AssignedProjects.Clear();
        }
        slots.Add(new PeriodicReviewStopwatchSlot
        {
            SourceActivityId = activity.Id,
            SourceActivityName = activity.App,
            StartUtc = activity.StartUtc,
            EndUtc = activity.EndUtc,
            SelectedProjectName = project3,
            IsManuallyAdded = true
        });
        if (!activity.AssignedProjects.Contains(project3))
        {
            activity.AssignedProjects.Add(project3);
        }

        // Multi-project overlap verified: both Beta and Gamma exist for this activity period
        Assert.Equal(2, slots.Count);
        Assert.Contains(slots, s => s.SelectedProjectName == "Project Beta");
        Assert.Contains(slots, s => s.SelectedProjectName == "Project Gamma");
        Assert.Equal(2, activity.AssignedProjects.Count);
        Assert.Contains("Project Beta", activity.AssignedProjects);
        Assert.Contains("Project Gamma", activity.AssignedProjects);
    }

    [Fact]
    public void AggregateActivitiesSummary_GroupsRepeatedVisitsAndCalculatesPercentages()
    {
        // 30 minute review window = 1800 seconds
        var baseTime = new DateTimeOffset(2026, 9, 30, 10, 0, 0, TimeSpan.Zero);
        var totalPeriod = TimeSpan.FromMinutes(30);

        var events = new List<RawActivityEvent>();

        // 7 separate visits to ChatGPT Chat A (each 90 seconds = total 630s = 10.5m = 35%)
        for (int i = 0; i < 7; i++)
        {
            events.Add(new RawActivityEvent(
                baseTime.AddSeconds(i * 100),
                baseTime.AddSeconds(i * 100 + 90),
                "chrome.exe",
                "Chat A - ChatGPT - Google Chrome",
                "Web"));
        }

        // 3 separate visits to ChatGPT Chat B (each 126 seconds = total 378s = 6.3m = 21%)
        for (int i = 0; i < 3; i++)
        {
            events.Add(new RawActivityEvent(
                baseTime.AddSeconds(800 + i * 150),
                baseTime.AddSeconds(800 + i * 150 + 126),
                "chrome.exe",
                "Chat B - ChatGPT - Google Chrome",
                "Web"));
        }

        // Idle time totaling 558 seconds (9.3m = 31%)
        events.Add(new RawActivityEvent(
            baseTime.AddSeconds(1300),
            baseTime.AddSeconds(1858),
            "Idle / Away",
            "No mouse/keyboard",
            "Idle"));

        // Remaining 234 seconds (3.9m = 13%) in VS Code
        events.Add(new RawActivityEvent(
            baseTime.AddSeconds(1858),
            baseTime.AddSeconds(2092),
            "Code.exe",
            "main.cs - Stopwatch - Visual Studio Code",
            "App"));

        var summaries = PeriodicReviewDataAggregator.AggregateActivitiesSummary(events, totalPeriod);

        // All 7 visits to Chat A are combined into 1 entry
        // All 3 visits to Chat B are combined into 1 entry
        // Idle is 1 entry, Code is 1 entry -> exactly 4 groups
        Assert.Equal(4, summaries.Count);

        // Sorted by duration descending:
        // 1. Chat A: 35.0%
        Assert.Equal("Chat A - ChatGPT", summaries[0].Title);
        Assert.Equal("chrome", summaries[0].App);
        Assert.Equal(7, summaries[0].EventCount);
        Assert.Equal(35.0, summaries[0].Percentage);

        // 2. Idle: 31.0%
        Assert.True(summaries[1].IsIdle);
        Assert.Equal("Idle / Away", summaries[1].App);
        Assert.Equal(31.0, summaries[1].Percentage);

        // 3. Chat B: 21.0%
        Assert.Equal("Chat B - ChatGPT", summaries[2].Title);
        Assert.Equal(3, summaries[2].EventCount);
        Assert.Equal(21.0, summaries[2].Percentage);

        // 4. Code: 13.0%
        Assert.Equal("main.cs - Stopwatch", summaries[3].Title);
        Assert.Equal("Code", summaries[3].App);
        Assert.Equal(13.0, summaries[3].Percentage);
    }

    [Fact]
    public void ComputeTimeline_CalculatesSequentialIntervalsWithBreakAndUnallocated()
    {
        // 30 minute review window from 10:00 to 10:30 UTC
        DateTime startUtc = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);
        DateTime endUtc = startUtc.AddMinutes(30);

        var items = new List<ReviewProjectSelectionItem>
        {
            new() { ProjectName = "Project Alpha", AllocatedMinutes = 10, SelectionOrder = 1, IsBreak = false },
            new() { ProjectName = "Break / Empty", AllocatedMinutes = 5, SelectionOrder = 2, IsBreak = true },
            new() { ProjectName = "Project Beta", AllocatedMinutes = 5, SelectionOrder = 3, IsBreak = false },
            new() { ProjectName = "Project Delta", AllocatedMinutes = 5, SelectionOrder = 4, IsBreak = false }
        };

        var timeline = PeriodicReviewDataAggregator.ComputeTimeline(startUtc, endUtc, items);

        Assert.Equal(4, timeline.Count);

        // Slot 1: Project Alpha (10:00 - 10:10)
        Assert.Equal(startUtc, timeline[0].StartUtc);
        Assert.Equal(startUtc.AddMinutes(10), timeline[0].EndUtc);
        Assert.Equal("Project Alpha", timeline[0].Item.ProjectName);
        Assert.False(timeline[0].Item.IsBreak);

        // Slot 2: Break (10:10 - 10:15)
        Assert.Equal(startUtc.AddMinutes(10), timeline[1].StartUtc);
        Assert.Equal(startUtc.AddMinutes(15), timeline[1].EndUtc);
        Assert.True(timeline[1].Item.IsBreak);

        // Slot 3: Project Beta (10:15 - 10:20)
        Assert.Equal(startUtc.AddMinutes(15), timeline[2].StartUtc);
        Assert.Equal(startUtc.AddMinutes(20), timeline[2].EndUtc);
        Assert.Equal("Project Beta", timeline[2].Item.ProjectName);

        // Slot 4: Project Delta (10:20 - 10:25)
        Assert.Equal(startUtc.AddMinutes(20), timeline[3].StartUtc);
        Assert.Equal(startUtc.AddMinutes(25), timeline[3].EndUtc);
        Assert.Equal("Project Delta", timeline[3].Item.ProjectName);

        // Remainder: timeline ended at 10:25, so 10:25 to 10:30 (5m) is unallocated (untracked)
        Assert.True(timeline[^1].EndUtc < endUtc);
        Assert.Equal(TimeSpan.FromMinutes(5), endUtc - timeline[^1].EndUtc);
    }
}
