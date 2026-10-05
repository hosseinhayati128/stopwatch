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

    [Fact]
    public void BuildFullTimeline_HandlesIntermediateAndTrailingUnallocatedGaps()
    {
        // 20-minute period from 10:00 to 10:20 UTC
        DateTime startUtc = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);
        DateTime endUtc = startUtc.AddMinutes(20);

        // User scenario:
        // Item A: 10:00 - 10:10 (10 min)
        // Item B moved to 10:12 - 10:17 (5 min)
        // Resulting in: 2 min unallocated gap before B, and 3 min unallocated gap after B
        var items = new List<ReviewProjectSelectionItem>
        {
            new()
            {
                ProjectName = "Project A",
                AllocatedMinutes = 10,
                StartUtc = startUtc,
                EndUtc = startUtc.AddMinutes(10),
                SelectionOrder = 1
            },
            new()
            {
                ProjectName = "Project B",
                AllocatedMinutes = 5,
                StartUtc = startUtc.AddMinutes(12),
                EndUtc = startUtc.AddMinutes(17),
                SelectionOrder = 2
            }
        };

        var fullTimeline = PeriodicReviewDataAggregator.BuildFullTimeline(startUtc, endUtc, items);

        // Expected 4 contiguous slots:
        // 1. Project A: 10:00 - 10:10 (10 min)
        // 2. Unallocated: 10:10 - 10:12 (2 min)
        // 3. Project B: 10:12 - 10:17 (5 min)
        // 4. Unallocated: 10:17 - 10:20 (3 min)
        Assert.Equal(4, fullTimeline.Count);

        // Slot 1
        Assert.False(fullTimeline[0].IsUnallocated);
        Assert.Equal("Project A", fullTimeline[0].Item!.ProjectName);
        Assert.Equal(startUtc, fullTimeline[0].StartUtc);
        Assert.Equal(startUtc.AddMinutes(10), fullTimeline[0].EndUtc);
        Assert.Equal(10, fullTimeline[0].DurationMinutes);

        // Slot 2: 2m gap on the left of B
        Assert.True(fullTimeline[1].IsUnallocated);
        Assert.Null(fullTimeline[1].Item);
        Assert.Equal(startUtc.AddMinutes(10), fullTimeline[1].StartUtc);
        Assert.Equal(startUtc.AddMinutes(12), fullTimeline[1].EndUtc);
        Assert.Equal(2, fullTimeline[1].DurationMinutes);

        // Slot 3: Project B
        Assert.False(fullTimeline[2].IsUnallocated);
        Assert.Equal("Project B", fullTimeline[2].Item!.ProjectName);
        Assert.Equal(startUtc.AddMinutes(12), fullTimeline[2].StartUtc);
        Assert.Equal(startUtc.AddMinutes(17), fullTimeline[2].EndUtc);
        Assert.Equal(5, fullTimeline[2].DurationMinutes);

        // Slot 4: 3m gap on the right of B
        Assert.True(fullTimeline[3].IsUnallocated);
        Assert.Null(fullTimeline[3].Item);
        Assert.Equal(startUtc.AddMinutes(17), fullTimeline[3].StartUtc);
        Assert.Equal(endUtc, fullTimeline[3].EndUtc);
        Assert.Equal(3, fullTimeline[3].DurationMinutes);
    }

    [Fact]
    public void FilterAndGroupActivitiesSummary_GroupsBelowDefaultThreshold()
    {
        var totalPeriod = TimeSpan.FromMinutes(30); // 1800s
        var groups = new List<ActivitySummaryGroup>
        {
            new() { App = "ChatGPT", Title = "Chat A", Category = "Web", TotalDuration = TimeSpan.FromMinutes(10), Percentage = 33.3, EventCount = 5 },
            new() { App = "VS Code", Title = "StopwatchOverlay", Category = "App", TotalDuration = TimeSpan.FromMinutes(8), Percentage = 26.7, EventCount = 4 },
            new() { App = "Spotify", Title = "Music", Category = "App", TotalDuration = TimeSpan.FromSeconds(45), Percentage = 2.5, EventCount = 1 },
            new() { App = "Calculator", Title = "Calc", Category = "App", TotalDuration = TimeSpan.FromSeconds(20), Percentage = 1.1, EventCount = 1 },
            new() { App = "Terminal", Title = "git", Category = "App", TotalDuration = TimeSpan.FromSeconds(15), Percentage = 0.8, EventCount = 1 }
        };

        // Default threshold 3.0%: Spotify (2.5%), Calc (1.1%), and Terminal (0.8%) should be bundled into "Others"
        var filtered = PeriodicReviewDataAggregator.FilterAndGroupActivitiesSummary(groups, totalPeriod, 3.0);

        Assert.Equal(3, filtered.Count);
        Assert.Equal("ChatGPT", filtered[0].App);
        Assert.Equal("VS Code", filtered[1].App);

        // "Others" group
        var others = filtered[2];
        Assert.Equal("Others", others.App);
        Assert.Equal("Other", others.Category);
        Assert.True(others.IsOther);
        Assert.Equal(3, others.SubItems.Count);
        Assert.Equal(3, others.EventCount);
        Assert.Equal(TimeSpan.FromSeconds(80), others.TotalDuration);
        Assert.Contains("minor activities", others.Title);
        Assert.True(others.Percentage > 0);
    }

    [Fact]
    public void FilterAndGroupActivitiesSummary_AdjustableThreshold_VariousPercentages()
    {
        var totalPeriod = TimeSpan.FromMinutes(30);
        var groups = new List<ActivitySummaryGroup>
        {
            new() { App = "App A", Title = "Task 1", Category = "App", TotalDuration = TimeSpan.FromMinutes(12), Percentage = 40.0, EventCount = 5 },
            new() { App = "App B", Title = "Task 2", Category = "App", TotalDuration = TimeSpan.FromMinutes(6), Percentage = 20.0, EventCount = 3 },
            new() { App = "App C", Title = "Task 3", Category = "App", TotalDuration = TimeSpan.FromMinutes(2), Percentage = 6.7, EventCount = 2 },
            new() { App = "App D", Title = "Task 4", Category = "App", TotalDuration = TimeSpan.FromSeconds(30), Percentage = 1.7, EventCount = 1 }
        };

        // Threshold = 0.0 (Off): all 4 items shown individually, no "Others"
        var off = PeriodicReviewDataAggregator.FilterAndGroupActivitiesSummary(groups, totalPeriod, 0.0);
        Assert.Equal(4, off.Count);
        Assert.DoesNotContain(off, g => g.IsOther);

        // Threshold = 5.0%: App D (1.7%) is grouped into Others
        var at5 = PeriodicReviewDataAggregator.FilterAndGroupActivitiesSummary(groups, totalPeriod, 5.0);
        Assert.Equal(4, at5.Count); // A, B, C, Others
        Assert.Equal("Others", at5[3].App);
        Assert.Single(at5[3].SubItems);

        // Threshold = 10.0%: App C (6.7%) and App D (1.7%) grouped into Others
        var at10 = PeriodicReviewDataAggregator.FilterAndGroupActivitiesSummary(groups, totalPeriod, 10.0);
        Assert.Equal(3, at10.Count); // A, B, Others
        Assert.Equal("Others", at10[2].App);
        Assert.Equal(2, at10[2].SubItems.Count);
    }

    [Fact]
    public void ProjectPersistence_WhenProjectRegistered_IsPersistedPermanently()
    {
        var history = new ProjectTimeHistory();

        // Register a project as added from Periodic Review
        string registered = history.RegisterProject("New Review Project");

        Assert.Equal("New Review Project", registered);
        Assert.Contains("New Review Project", history.ProjectNames);

        // Verify it remains unless deleted manually
        var view = history.CreateView(DateTime.UtcNow);
        Assert.Contains(view.Projects, p => p.Name == "New Review Project");

        // Now test manual deletion
        var delResult = history.DeleteProject("New Review Project");
        Assert.Equal(ProjectDeletionStatus.Success, delResult.Status);
        Assert.DoesNotContain("New Review Project", history.ProjectNames);
    }

    [Fact]
    public void TruncateToMinute_StripsSecondsAndMilliseconds()
    {
        DateTime dt = new(2026, 9, 30, 10, 14, 45, 789, DateTimeKind.Utc);
        DateTime truncated = PeriodicReviewDataAggregator.TruncateToMinute(dt);

        Assert.Equal(0, truncated.Second);
        Assert.Equal(0, truncated.Millisecond);
        Assert.Equal(10, truncated.Hour);
        Assert.Equal(14, truncated.Minute);
        Assert.Equal(DateTimeKind.Utc, truncated.Kind);
    }

    [Fact]
    public void BuildFullTimeline_TruncatesSecondsAndSynchronizes()
    {
        // Start and end with non-zero seconds
        DateTime startUtc = new(2026, 9, 30, 10, 0, 35, DateTimeKind.Utc);
        DateTime endUtc = new(2026, 9, 30, 10, 30, 50, DateTimeKind.Utc);

        var items = new List<ReviewProjectSelectionItem>
        {
            new()
            {
                ProjectName = "Project Alpha",
                AllocatedMinutes = 10,
                StartUtc = new(2026, 9, 30, 10, 5, 22, DateTimeKind.Utc),
                EndUtc = new(2026, 9, 30, 10, 15, 22, DateTimeKind.Utc),
                SelectionOrder = 1
            },
            new()
            {
                ProjectName = "Project Beta",
                AllocatedMinutes = 10,
                StartUtc = new(2026, 9, 30, 10, 18, 11, DateTimeKind.Utc),
                EndUtc = new(2026, 9, 30, 10, 28, 11, DateTimeKind.Utc),
                SelectionOrder = 2
            }
        };

        var fullTimeline = PeriodicReviewDataAggregator.BuildFullTimeline(startUtc, endUtc, items);

        // Expected slots:
        // 1. Unallocated gap 10:00 - 10:05 (5m)
        // 2. Project Alpha 10:05 - 10:15 (10m)
        // 3. Unallocated gap 10:15 - 10:18 (3m)
        // 4. Project Beta 10:18 - 10:28 (10m)
        // 5. Unallocated gap 10:28 - 10:30 (2m)
        Assert.Equal(5, fullTimeline.Count);

        Assert.All(fullTimeline, slot =>
        {
            Assert.Equal(0, slot.StartUtc.Second);
            Assert.Equal(0, slot.StartUtc.Millisecond);
            Assert.Equal(0, slot.EndUtc.Second);
            Assert.Equal(0, slot.EndUtc.Millisecond);
            Assert.Equal(slot.DurationMinutes, Math.Round(slot.DurationMinutes));
        });

        // Slot 1: Unallocated 10:00 - 10:05
        Assert.True(fullTimeline[0].IsUnallocated);
        Assert.Equal(5, fullTimeline[0].DurationMinutes);

        // Slot 2: Project Alpha 10:05 - 10:15
        Assert.Equal("Project Alpha", fullTimeline[1].Item!.ProjectName);
        Assert.Equal(10, fullTimeline[1].DurationMinutes);

        // Slot 3: Unallocated 10:15 - 10:18
        Assert.True(fullTimeline[2].IsUnallocated);
        Assert.Equal(3, fullTimeline[2].DurationMinutes);

        // Slot 4: Project Beta 10:18 - 10:28
        Assert.Equal("Project Beta", fullTimeline[3].Item!.ProjectName);
        Assert.Equal(10, fullTimeline[3].DurationMinutes);

        // Slot 5: Unallocated 10:28 - 10:30
        Assert.True(fullTimeline[4].IsUnallocated);
        Assert.Equal(2, fullTimeline[4].DurationMinutes);
    }

    [Fact]
    public void BuildFullTimeline_WhenItemsPushed_PreservesAllocatedDurations()
    {
        DateTime startUtc = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);
        DateTime endUtc = new(2026, 9, 30, 10, 30, 0, DateTimeKind.Utc);

        // Item 1 has 15 minutes, starting at 10:00 (ends 10:15)
        // Item 2 has 10 minutes, but its StartUtc was overlapping at 10:10
        var items = new List<ReviewProjectSelectionItem>
        {
            new()
            {
                ProjectName = "Task 1",
                AllocatedMinutes = 15,
                StartUtc = startUtc,
                EndUtc = startUtc.AddMinutes(15),
                SelectionOrder = 1
            },
            new()
            {
                ProjectName = "Task 2",
                AllocatedMinutes = 10,
                StartUtc = startUtc.AddMinutes(10), // overlaps Task 1!
                EndUtc = startUtc.AddMinutes(20),
                SelectionOrder = 2
            }
        };

        var fullTimeline = PeriodicReviewDataAggregator.BuildFullTimeline(startUtc, endUtc, items);

        // Task 2 should be pushed to 10:15, and keep its 10 minutes (ends 10:25)!
        Assert.Equal(3, fullTimeline.Count); // Task 1, Task 2, trailing gap

        // Task 1: 10:00 - 10:15 (15m)
        Assert.Equal("Task 1", fullTimeline[0].Item!.ProjectName);
        Assert.Equal(startUtc, fullTimeline[0].StartUtc);
        Assert.Equal(startUtc.AddMinutes(15), fullTimeline[0].EndUtc);
        Assert.Equal(15, fullTimeline[0].DurationMinutes);

        // Task 2: 10:15 - 10:25 (10m) - duration preserved!
        Assert.Equal("Task 2", fullTimeline[1].Item!.ProjectName);
        Assert.Equal(startUtc.AddMinutes(15), fullTimeline[1].StartUtc);
        Assert.Equal(startUtc.AddMinutes(25), fullTimeline[1].EndUtc);
        Assert.Equal(10, fullTimeline[1].DurationMinutes);
        Assert.Equal(10, items[1].AllocatedMinutes);

        // Trailing gap: 10:25 - 10:30 (5m)
        Assert.True(fullTimeline[2].IsUnallocated);
        Assert.Equal(startUtc.AddMinutes(25), fullTimeline[2].StartUtc);
        Assert.Equal(endUtc, fullTimeline[2].EndUtc);
        Assert.Equal(5, fullTimeline[2].DurationMinutes);
    }

    [Fact]
    public void RebalanceAllocatedMinutes_WhenOneItemTakesAllTime_BorrowingAllowsZeroMinuteItemToBeIncreased()
    {
        int totalPeriodMinutes = 30;
        var p1 = new ReviewProjectSelectionItem { ProjectName = "P1", AllocatedMinutes = 30, SelectionOrder = 1 };
        var p2 = new ReviewProjectSelectionItem { ProjectName = "P2", AllocatedMinutes = 0, SelectionOrder = 2 };
        var items = new List<ReviewProjectSelectionItem> { p1, p2 };

        // User increases P2 from 0 to 5 minutes
        PeriodicReviewDataAggregator.RebalanceAllocatedMinutes(items, p2, 5, totalPeriodMinutes);

        // P2 should be 5, and P1 should be reduced to 25
        Assert.Equal(5, p2.AllocatedMinutes);
        Assert.Equal(25, p1.AllocatedMinutes);
        Assert.Equal(30, items.Sum(it => it.AllocatedMinutes));

        // Now user increases P2 to 30 minutes (full duration)
        PeriodicReviewDataAggregator.RebalanceAllocatedMinutes(items, p2, 30, totalPeriodMinutes);

        // P2 should be 30, P1 should be 0
        Assert.Equal(30, p2.AllocatedMinutes);
        Assert.Equal(0, p1.AllocatedMinutes);

        // Now user increases P1 from 0 to 10 minutes (P1 was at 0 and stuck previously)
        PeriodicReviewDataAggregator.RebalanceAllocatedMinutes(items, p1, 10, totalPeriodMinutes);

        // P1 should be 10, P2 should be reduced to 20
        Assert.Equal(10, p1.AllocatedMinutes);
        Assert.Equal(20, p2.AllocatedMinutes);
    }

    [Fact]
    public void RebalanceAllocatedMinutes_MultipleDonors_BorrowsFromLargestDonorFirst()
    {
        int totalPeriodMinutes = 30;
        var p1 = new ReviewProjectSelectionItem { ProjectName = "P1", AllocatedMinutes = 20, SelectionOrder = 1 };
        var p2 = new ReviewProjectSelectionItem { ProjectName = "P2", AllocatedMinutes = 10, SelectionOrder = 2 };
        var p3 = new ReviewProjectSelectionItem { ProjectName = "P3", AllocatedMinutes = 0, SelectionOrder = 3 };
        var items = new List<ReviewProjectSelectionItem> { p1, p2, p3 };

        // User increases P3 from 0 to 15m. Excess is 15m.
        // P1 (20m) is the largest donor, so 15m should be deducted from P1!
        PeriodicReviewDataAggregator.RebalanceAllocatedMinutes(items, p3, 15, totalPeriodMinutes);

        Assert.Equal(15, p3.AllocatedMinutes);
        Assert.Equal(5, p1.AllocatedMinutes); // 20 - 15 = 5
        Assert.Equal(10, p2.AllocatedMinutes); // untouched
    }

    [Fact]
    public void RebalanceAllocatedMinutes_ConsumesUnallocatedGapFirstBeforeBorrowing()
    {
        int totalPeriodMinutes = 30;
        var p1 = new ReviewProjectSelectionItem { ProjectName = "P1", AllocatedMinutes = 10, SelectionOrder = 1 };
        var p2 = new ReviewProjectSelectionItem { ProjectName = "P2", AllocatedMinutes = 5, SelectionOrder = 2 };
        var items = new List<ReviewProjectSelectionItem> { p1, p2 };
        // Total allocated is 15m, unallocated gap is 15m.

        // User increases P2 from 5m to 12m (delta = 7m <= 15m gap)
        PeriodicReviewDataAggregator.RebalanceAllocatedMinutes(items, p2, 12, totalPeriodMinutes);

        // P1 should remain completely untouched!
        Assert.Equal(12, p2.AllocatedMinutes);
        Assert.Equal(10, p1.AllocatedMinutes);
    }

    [Fact]
    public void RebalanceAllocatedMinutes_DecreasingAllocation_DoesNotTouchOtherItems()
    {
        int totalPeriodMinutes = 30;
        var p1 = new ReviewProjectSelectionItem { ProjectName = "P1", AllocatedMinutes = 20, SelectionOrder = 1 };
        var p2 = new ReviewProjectSelectionItem { ProjectName = "P2", AllocatedMinutes = 10, SelectionOrder = 2 };
        var items = new List<ReviewProjectSelectionItem> { p1, p2 };

        // User reduces P1 from 20 to 5
        PeriodicReviewDataAggregator.RebalanceAllocatedMinutes(items, p1, 5, totalPeriodMinutes);

        Assert.Equal(5, p1.AllocatedMinutes);
        Assert.Equal(10, p2.AllocatedMinutes); // P2 untouched, difference is freed as gap
    }

    [Fact]
    public void BuildFullTimeline_IncludesZeroMinuteSlotsForSelectedProjects()
    {
        DateTime startUtc = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);
        DateTime endUtc = startUtc.AddMinutes(30);

        var p1 = new ReviewProjectSelectionItem { ProjectName = "P1", AllocatedMinutes = 30, SelectionOrder = 1 };
        var p2 = new ReviewProjectSelectionItem { ProjectName = "P2", AllocatedMinutes = 0, SelectionOrder = 2 };
        var items = new List<ReviewProjectSelectionItem> { p1, p2 };

        var fullTimeline = PeriodicReviewDataAggregator.BuildFullTimeline(startUtc, endUtc, items);

        // Should return 2 slots: P1 (30m) and P2 (0m pin)
        Assert.Equal(2, fullTimeline.Count);

        Assert.Equal("P1", fullTimeline[0].Item!.ProjectName);
        Assert.Equal(30, fullTimeline[0].DurationMinutes);

        Assert.Equal("P2", fullTimeline[1].Item!.ProjectName);
        Assert.Equal(0, fullTimeline[1].DurationMinutes);

        // Ensure AllocatedMinutes was not corrupted
        Assert.Equal(30, p1.AllocatedMinutes);
        Assert.Equal(0, p2.AllocatedMinutes);
    }

    [Fact]
    public void BuildFullTimeline_WhenAllItemsZeroMinutes_GeneratesSlotsAndFullUnallocatedGap()
    {
        DateTime startUtc = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);
        DateTime endUtc = startUtc.AddMinutes(30);

        var p1 = new ReviewProjectSelectionItem { ProjectName = "P1", AllocatedMinutes = 0, SelectionOrder = 1 };
        var p2 = new ReviewProjectSelectionItem { ProjectName = "P2", AllocatedMinutes = 0, SelectionOrder = 2 };
        var items = new List<ReviewProjectSelectionItem> { p1, p2 };

        var fullTimeline = PeriodicReviewDataAggregator.BuildFullTimeline(startUtc, endUtc, items);

        // Should return 3 slots: P1 (0m), P2 (0m), and Unallocated gap (30m)
        Assert.Equal(3, fullTimeline.Count);

        Assert.Equal("P1", fullTimeline[0].Item!.ProjectName);
        Assert.Equal(0, fullTimeline[0].DurationMinutes);

        Assert.Equal("P2", fullTimeline[1].Item!.ProjectName);
        Assert.Equal(0, fullTimeline[1].DurationMinutes);

        Assert.True(fullTimeline[2].IsUnallocated);
        Assert.Equal(30, fullTimeline[2].DurationMinutes);
    }

    [Fact]
    public void AutoSelectStopwatchProjects_SelectsTrackedProjectsInOrder()
    {
        var available = new List<ReviewProjectSelectionItem>
        {
            new() { ProjectName = "Break / Empty", IsBreak = true },
            new() { ProjectName = "Project Alpha" },
            new() { ProjectName = "Project Beta" },
            new() { ProjectName = "Project Gamma" }
        };
        var selected = new List<ReviewProjectSelectionItem>();

        var slots = new List<PeriodicReviewStopwatchSlot>
        {
            new()
            {
                IsTracked = true,
                SelectedProjectName = "Project Beta",
                StartUtc = DateTime.UtcNow.AddMinutes(-30),
                EndUtc = DateTime.UtcNow.AddMinutes(-15)
            },
            new()
            {
                IsTracked = false,
                SelectedProjectName = "(Untracked / Off)",
                StartUtc = DateTime.UtcNow.AddMinutes(-15),
                EndUtc = DateTime.UtcNow.AddMinutes(-10)
            },
            new()
            {
                IsTracked = true,
                SelectedProjectName = "Project Alpha",
                StartUtc = DateTime.UtcNow.AddMinutes(-10),
                EndUtc = DateTime.UtcNow
            }
        };

        PeriodicReviewDataAggregator.AutoSelectStopwatchProjects(available, selected, slots);

        Assert.Equal(2, selected.Count);
        Assert.Equal("Project Beta", selected[0].ProjectName);
        Assert.Equal(1, selected[0].SelectionOrder);
        Assert.True(selected[0].IsSelected);

        Assert.Equal("Project Alpha", selected[1].ProjectName);
        Assert.Equal(2, selected[1].SelectionOrder);
        Assert.True(selected[1].IsSelected);

        // Gamma was not tracked, should remain unselected
        var gamma = available.First(p => p.ProjectName == "Project Gamma");
        Assert.False(gamma.IsSelected);
        Assert.Equal(0, gamma.SelectionOrder);
    }

    [Fact]
    public void AutoSelectStopwatchProjects_DoesNotDuplicateAlreadySelectedProjects()
    {
        var alreadySelected = new ReviewProjectSelectionItem { ProjectName = "Project Alpha", SelectionOrder = 1 };
        var available = new List<ReviewProjectSelectionItem>
        {
            alreadySelected,
            new() { ProjectName = "Project Beta" }
        };
        var selected = new List<ReviewProjectSelectionItem> { alreadySelected };

        var slots = new List<PeriodicReviewStopwatchSlot>
        {
            new()
            {
                IsTracked = true,
                SelectedProjectName = "Project Alpha",
                StartUtc = DateTime.UtcNow.AddMinutes(-30),
                EndUtc = DateTime.UtcNow.AddMinutes(-20)
            },
            new()
            {
                IsTracked = true,
                SelectedProjectName = "Project Beta",
                StartUtc = DateTime.UtcNow.AddMinutes(-20),
                EndUtc = DateTime.UtcNow
            }
        };

        PeriodicReviewDataAggregator.AutoSelectStopwatchProjects(available, selected, slots);

        // Alpha should not be added again, Beta should be added as 2nd item
        Assert.Equal(2, selected.Count);
        Assert.Equal("Project Alpha", selected[0].ProjectName);
        Assert.Equal(1, selected[0].SelectionOrder);

        Assert.Equal("Project Beta", selected[1].ProjectName);
        Assert.Equal(2, selected[1].SelectionOrder);
    }

    [Fact]
    public void PrePopulateStopwatchAllocations_AssignsTrackedDurationsAndSplitsRemainder()
    {
        DateTime startUtc = new(2026, 9, 30, 10, 0, 0, DateTimeKind.Utc);
        DateTime endUtc = startUtc.AddMinutes(60);

        var pAlpha = new ReviewProjectSelectionItem { ProjectName = "Project Alpha", SelectionOrder = 1 };
        var pBeta = new ReviewProjectSelectionItem { ProjectName = "Project Beta", SelectionOrder = 2 };
        var pManual = new ReviewProjectSelectionItem { ProjectName = "Manual Project", SelectionOrder = 3 };
        var selected = new List<ReviewProjectSelectionItem> { pAlpha, pBeta, pManual };

        var slots = new List<PeriodicReviewStopwatchSlot>
        {
            // Alpha: 20 minutes
            new()
            {
                IsTracked = true,
                SelectedProjectName = "Project Alpha",
                StartUtc = startUtc,
                EndUtc = startUtc.AddMinutes(20)
            },
            // Beta: 15 minutes
            new()
            {
                IsTracked = true,
                SelectedProjectName = "Project Beta",
                StartUtc = startUtc.AddMinutes(20),
                EndUtc = startUtc.AddMinutes(35)
            }
        };

        PeriodicReviewDataAggregator.PrePopulateStopwatchAllocations(selected, slots, startUtc, endUtc);

        // Alpha: 20m, Beta: 15m, Remaining 25m goes to Manual Project
        Assert.Equal(20, pAlpha.AllocatedMinutes);
        Assert.Equal(15, pBeta.AllocatedMinutes);
        Assert.Equal(25, pManual.AllocatedMinutes);

        // Intervals must be contiguous and packed within period
        Assert.Equal(startUtc, pAlpha.StartUtc);
        Assert.Equal(startUtc.AddMinutes(20), pAlpha.EndUtc);

        Assert.Equal(startUtc.AddMinutes(20), pBeta.StartUtc);
        Assert.Equal(startUtc.AddMinutes(35), pBeta.EndUtc);

        Assert.Equal(startUtc.AddMinutes(35), pManual.StartUtc);
        Assert.Equal(endUtc, pManual.EndUtc);
    }

    [Fact]
    public void PrePopulateStopwatchAllocations_HandlesNullAndEmptySafely()
    {
        DateTime startUtc = DateTime.UtcNow;
        DateTime endUtc = startUtc.AddMinutes(30);

        // Should not throw on nulls or empty collections
        PeriodicReviewDataAggregator.PrePopulateStopwatchAllocations(null!, null, startUtc, endUtc);
        PeriodicReviewDataAggregator.PrePopulateStopwatchAllocations([], [], startUtc, endUtc);
        PeriodicReviewDataAggregator.AutoSelectStopwatchProjects(null!, null!, null);
    }
}
