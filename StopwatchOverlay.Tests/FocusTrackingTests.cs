using System;
using System.IO;
using System.Linq;
using Xunit;

namespace StopwatchOverlay.Tests;

public class FocusTrackingTests
{
    [Fact]
    public void AppSettings_FocusTrackingDefaults_AreConfiguredCorrectly()
    {
        var settings = new AppSettings();

        // Option MUST be disabled by default as specified in requirements
        Assert.False(settings.FocusTrackingEnabled);
        Assert.Equal(5.0, settings.FocusDistractionThresholdMinutes);
        Assert.Equal(10, settings.FocusMinimumPauseSeconds);
        Assert.Equal(12, settings.FocusPromptTimeoutSeconds);
        Assert.Equal("Focus Log.md", settings.FocusLogFileName);
    }

    [Fact]
    public void AppSettings_FocusTrackingNormalization_ClampsExtremeValues()
    {
        var settings = new AppSettings
        {
            FocusDistractionThresholdMinutes = -5,
            FocusMinimumPauseSeconds = -10,
            FocusPromptTimeoutSeconds = 200,
            FocusLogFileName = "   "
        };

        settings.NormalizeForRuntime();

        Assert.Equal(5.0, settings.FocusDistractionThresholdMinutes);
        Assert.Equal(10, settings.FocusMinimumPauseSeconds);
        Assert.Equal(120, settings.FocusPromptTimeoutSeconds);
        Assert.Equal("Focus Log.md", settings.FocusLogFileName);
    }

    [Theory]
    [InlineData(FocusPauseReason.ValidBreak, "☕", "Valid Break")]
    [InlineData(FocusPauseReason.Distraction, "⚡", "Distraction")]
    [InlineData(FocusPauseReason.IdeaBrainstorming, "💡", "Idea / Brainstorming")]
    [InlineData(FocusPauseReason.TaskSwitch, "🔀", "Switch Between Tasks")]
    public void FocusPauseRecord_ReasonProperties_MatchExpectedMetadata(
        FocusPauseReason reason,
        string expectedIcon,
        string expectedDisplayName)
    {
        var now = DateTime.UtcNow;
        var record = new FocusPauseRecord(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "ProjectX",
            now.AddMinutes(-4),
            now,
            reason,
            "test note");

        Assert.Equal(expectedIcon, record.ReasonIcon);
        Assert.Equal(expectedDisplayName, record.ReasonDisplayName);
        Assert.Equal(TimeSpan.FromMinutes(4).TotalSeconds, record.Duration.TotalSeconds, 1);
        Assert.Equal("test note", record.Note);
    }

    [Fact]
    public void ProjectTimeHistory_RecordFocusPause_AddsAndExposesInDocument()
    {
        var history = new ProjectTimeHistory();
        var now = DateTime.UtcNow;
        var pauseStart = now.AddMinutes(-3);
        var resume = now;
        var sessionId = Guid.NewGuid();

        var record = history.RecordFocusPause(
            sessionId,
            "ProjectAlpha",
            pauseStart,
            resume,
            FocusPauseReason.Distraction,
            "Lost focus checking notifications");

        Assert.NotNull(record);
        Assert.Single(history.FocusPauses);
        Assert.Equal("ProjectAlpha", history.FocusPauses[0].ProjectName);
        Assert.Equal(FocusPauseReason.Distraction, history.FocusPauses[0].Reason);
        Assert.Equal("Lost focus checking notifications", history.FocusPauses[0].Note);

        // Document roundtrip
        var doc = history.CreateDocument(DateTime.UtcNow);
        Assert.NotNull(doc.FocusPauses);
        Assert.Single(doc.FocusPauses);
        Assert.Equal("ProjectAlpha", doc.FocusPauses[0].ProjectName);
        Assert.Equal("Distraction", doc.FocusPauses[0].Reason);

        // Recreate history from document
        var restoredHistory = ProjectTimeHistory.FromDocument(doc);
        Assert.Single(restoredHistory.FocusPauses);
        Assert.Equal(record.Id, restoredHistory.FocusPauses[0].Id);
        Assert.Equal("ProjectAlpha", restoredHistory.FocusPauses[0].ProjectName);
        Assert.Equal(FocusPauseReason.Distraction, restoredHistory.FocusPauses[0].Reason);
    }

    [Fact]
    public void ProjectTimeHistory_RecordTaskSwitch_PersistsWithMiddleTaskNote()
    {
        var history = new ProjectTimeHistory();
        var now = DateTime.UtcNow;
        var pauseStart = now.AddMinutes(-15);
        var resume = now;
        var sessionId = Guid.NewGuid();

        var record = history.RecordFocusPause(
            sessionId,
            "ProjectAlpha",
            pauseStart,
            resume,
            FocusPauseReason.TaskSwitch,
            "Switched to Docs (14m 20s)");

        Assert.NotNull(record);
        Assert.Equal(FocusPauseReason.TaskSwitch, record.Reason);
        Assert.Equal("Switch Between Tasks", record.ReasonDisplayName);
        Assert.Equal("🔀", record.ReasonIcon);
        Assert.Equal("Switched to Docs (14m 20s)", record.Note);

        var doc = history.CreateDocument(DateTime.UtcNow);
        Assert.NotNull(doc.FocusPauses);
        Assert.Single(doc.FocusPauses);
        Assert.Equal("TaskSwitch", doc.FocusPauses[0].Reason);

        var restored = ProjectTimeHistory.FromDocument(doc);
        Assert.Single(restored.FocusPauses);
        Assert.Equal(FocusPauseReason.TaskSwitch, restored.FocusPauses[0].Reason);
        Assert.Equal("Switched to Docs (14m 20s)", restored.FocusPauses[0].Note);
    }

    [Fact]
    public void ProjectTimeStore_SavesAndLoadsFocusPauses()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "FocusTrackTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string filePath = Path.Combine(tempDir, "history.json");

        try
        {
            var store = new ProjectTimeStore(filePath);
            var history = new ProjectTimeHistory();
            var start = DateTime.UtcNow.AddMinutes(-10);
            var resume = DateTime.UtcNow.AddMinutes(-8);
            var sessionId = Guid.NewGuid();

            history.RecordFocusPause(sessionId, "Work", start, resume, FocusPauseReason.ValidBreak, "Coffee break");

            store.Save(history.CreateDocument(DateTime.UtcNow));

            bool loaded = store.TryReadDocument(out var loadedDoc);
            Assert.True(loaded);
            Assert.Equal(ProjectTimeReadStatus.Success, store.LastReadStatus);
            Assert.NotNull(loadedDoc);
            Assert.NotNull(loadedDoc.FocusPauses);
            Assert.Single(loadedDoc.FocusPauses);
            Assert.Equal("Work", loadedDoc.FocusPauses[0].ProjectName);
            Assert.Equal("ValidBreak", loadedDoc.FocusPauses[0].Reason);
            Assert.Equal("Coffee break", loadedDoc.FocusPauses[0].Note);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }

    [Fact]
    public void ObsidianLogSync_SyncFocusLog_WritesMarkdownTable()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "ObsidianFocusSyncTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        try
        {
            var history = new ProjectTimeHistory();
            var settings = new AppSettings
            {
                FocusLogFileName = "Focus Log.md"
            };

            var start = new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);
            var resume = new DateTime(2026, 9, 27, 10, 4, 30, DateTimeKind.Utc);
            var sessionId = Guid.NewGuid();

            history.RecordFocusPause(sessionId, "Coding", start, resume, FocusPauseReason.Distraction, "Checked phone");
            history.RecordFocusPause(sessionId, "Coding", start.AddMinutes(20), resume.AddMinutes(35), FocusPauseReason.ValidBreak, "Walk");
            history.RecordFocusPause(sessionId, "Coding", start.AddMinutes(60), resume.AddMinutes(80), FocusPauseReason.TaskSwitch, "Switched to Research (18m 00s)");

            ObsidianLogSync.SyncFocusLog(history.CreateView(DateTime.UtcNow), settings, tempDir);

            string targetFile = Path.Combine(tempDir, "Focus Log.md");
            Assert.True(File.Exists(targetFile));

            string content = File.ReadAllText(targetFile);
            Assert.Contains("# Focus & Interruption Log", content);
            Assert.Contains("| Date | Project | Pause Start | Resume | Duration (min) | Duration | Category | Reason | Notes |", content);
            Assert.Contains("Coding", content);
            Assert.Contains("⚡ Distraction", content);
            Assert.Contains("Checked phone", content);
            Assert.Contains("☕ Valid Break", content);
            Assert.Contains("Walk", content);
            Assert.Contains("🔀 Switch Between Tasks", content);
            Assert.Contains("Switched to Research (18m 00s)", content);
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, true);
            }
        }
    }
}
