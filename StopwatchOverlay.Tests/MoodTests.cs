using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StopwatchOverlay;
using StopwatchOverlay.Mood;
using Xunit;

namespace StopwatchOverlay.Tests;

public class MoodTests
{
    [Fact]
    public void AppSettings_MoodDefaults_AreCorrect()
    {
        var settings = new AppSettings();

        Assert.True(settings.PeriodicReviewFeelingsEnabled);
        Assert.Equal("Mood Log.md", settings.MoodLogFileName);
        Assert.Equal(7.0, settings.DefaultMoodScore);
        Assert.NotNull(settings.MoodPresetKeywords);

        // Check required feelings keywords from user request
        Assert.Contains("Anxious", settings.MoodPresetKeywords);
        Assert.Contains("Depressed", settings.MoodPresetKeywords);
        Assert.Contains("Sad", settings.MoodPresetKeywords);
        Assert.Contains("Happy", settings.MoodPresetKeywords);
        Assert.Contains("Thrilled", settings.MoodPresetKeywords);
    }

    [Fact]
    public void AppSettings_NormalizeForRuntime_ClampsMoodScoreAndEnsuresPresets()
    {
        var settings = new AppSettings
        {
            DefaultMoodScore = 15.5,
            MoodLogFileName = "   ",
            MoodPresetKeywords = []
        };

        settings.NormalizeForRuntime();

        Assert.Equal(10.0, settings.DefaultMoodScore);
        Assert.Equal("Mood Log.md", settings.MoodLogFileName);
        Assert.NotEmpty(settings.MoodPresetKeywords);
        Assert.Contains("Happy", settings.MoodPresetKeywords);

        settings.DefaultMoodScore = -3.2;
        settings.NormalizeForRuntime();
        Assert.Equal(1.0, settings.DefaultMoodScore);

        // Checks decimal precision
        settings.DefaultMoodScore = 4.84;
        settings.NormalizeForRuntime();
        Assert.Equal(4.8, settings.DefaultMoodScore);

        settings.DefaultMoodScore = 5.14;
        settings.NormalizeForRuntime();
        Assert.Equal(5.1, settings.DefaultMoodScore);
    }

    [Fact]
    public void MoodEntry_ScoreDisplayAndBadges_AreAccurate()
    {
        var lowMood = new MoodEntry
        {
            Score = 2.4,
            Keywords = ["Depressed", "Sad"]
        };
        Assert.Equal("2.4", lowMood.ScoreDisplay);
        Assert.Equal("🔴 Distressed", lowMood.MoodBadge);
        Assert.Equal("😭", lowMood.MoodEmoji);

        var downMood = new MoodEntry
        {
            Score = 4.8,
            Keywords = ["Anxious", "Tired"]
        };
        Assert.Equal("4.8", downMood.ScoreDisplay);
        Assert.Equal("🟠 Down", downMood.MoodBadge);
        Assert.Equal("🙁", downMood.MoodEmoji);

        var neutralMood = new MoodEntry
        {
            Score = 5.1,
            Keywords = ["Calm"]
        };
        Assert.Equal("5.1", neutralMood.ScoreDisplay);
        Assert.Equal("🟡 Neutral", neutralMood.MoodBadge);
        Assert.Equal("😐", neutralMood.MoodEmoji);

        var goodMood = new MoodEntry
        {
            Score = 7.5,
            Keywords = ["Happy", "Focused"]
        };
        Assert.Equal("7.5", goodMood.ScoreDisplay);
        Assert.Equal("🟢 Good", goodMood.MoodBadge);
        Assert.Equal("😊", goodMood.MoodEmoji);

        var thrilledMood = new MoodEntry
        {
            Score = 9.5,
            Keywords = ["Thrilled", "Energetic"]
        };
        Assert.Equal("9.5", thrilledMood.ScoreDisplay);
        Assert.Equal("🌟 Thrilled", thrilledMood.MoodBadge);
        Assert.Equal("🤩", thrilledMood.MoodEmoji);
    }

    [Fact]
    public void MoodHistoryStore_AddAndRetrieve_WorksAtomically()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "MoodStoreTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string testFile = Path.Combine(tempDir, "test-mood.json");

        try
        {
            MoodHistoryStore.SetCustomFilePath(testFile);
            MoodHistoryStore.Clear();

            var entry1 = new MoodEntry
            {
                Score = 4.8,
                Keywords = ["Anxious", "Tired"],
                PeriodStartUtc = DateTime.UtcNow.AddMinutes(-30),
                PeriodEndUtc = DateTime.UtcNow,
                AssociatedProjects = ["ProjectA"]
            };

            var entry2 = new MoodEntry
            {
                Score = 8.5,
                Keywords = ["Happy", "Thrilled"],
                PeriodStartUtc = DateTime.UtcNow.AddMinutes(-60),
                PeriodEndUtc = DateTime.UtcNow.AddMinutes(-30),
                AssociatedProjects = ["ProjectB"]
            };

            MoodHistoryStore.Add(entry1);
            MoodHistoryStore.Add(entry2);

            var loaded = MoodHistoryStore.GetAll();
            Assert.Equal(2, loaded.Count);

            var loaded1 = loaded.First(r => r.Id == entry1.Id);
            Assert.Equal(4.8, loaded1.Score);
            Assert.Equal(["Anxious", "Tired"], loaded1.Keywords);

            var loaded2 = loaded.First(r => r.Id == entry2.Id);
            Assert.Equal(8.5, loaded2.Score);
            Assert.Equal(["Happy", "Thrilled"], loaded2.Keywords);
        }
        finally
        {
            MoodHistoryStore.SetCustomFilePath(null);
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void MoodHistoryStore_RecoversFromBackup_WhenPrimaryCorrupted()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "MoodStoreCorruptTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string testFile = Path.Combine(tempDir, "test-mood.json");

        try
        {
            MoodHistoryStore.SetCustomFilePath(testFile);

            var entry = new MoodEntry
            {
                Score = 5.1,
                Keywords = ["Calm", "Focused"]
            };

            MoodHistoryStore.Add(entry);
            Assert.Single(MoodHistoryStore.GetAll());
            Assert.True(File.Exists(testFile + ".bak"));

            // Corrupt the primary file intentionally
            File.WriteAllText(testFile, "{ corrupt invalid json !!!");

            // Reset cache to force reload
            MoodHistoryStore.SetCustomFilePath(testFile);

            var recovered = MoodHistoryStore.GetAll();
            Assert.Single(recovered);
            Assert.Equal(5.1, recovered[0].Score);
            Assert.Equal(["Calm", "Focused"], recovered[0].Keywords);
        }
        finally
        {
            MoodHistoryStore.SetCustomFilePath(null);
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    [Fact]
    public void MoodLogSync_GeneratesMarkdownAndPreservesCustomNotes()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "MoodSyncTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        string jsonFile = Path.Combine(tempDir, "test-mood.json");

        try
        {
            MoodHistoryStore.SetCustomFilePath(jsonFile);
            MoodHistoryStore.Clear();

            var settings = new AppSettings
            {
                ObsidianVaultFolder = tempDir,
                MoodLogFileName = "Mood Log.md"
            };

            var time = new DateTime(2026, 10, 5, 14, 0, 0, DateTimeKind.Local);
            var entry = new MoodEntry
            {
                TimestampUtc = time.ToUniversalTime(),
                PeriodStartUtc = time.AddMinutes(-30).ToUniversalTime(),
                PeriodEndUtc = time.ToUniversalTime(),
                Score = 5.1,
                Keywords = ["Anxious", "Sad"],
                AssociatedProjects = ["Learning"],
                Note = "Initial reflection"
            };

            var syncResult = MoodLogSync.AppendMood(entry, settings);
            Assert.True(syncResult.Success);
            Assert.True(File.Exists(syncResult.TargetFilePath));

            string mdContent = File.ReadAllText(syncResult.TargetFilePath);
            Assert.Contains("type: mood-log", mdContent);
            Assert.Contains("# 😊 Mood & Feelings Log", mdContent);
            Assert.Contains("## 📅 2026-10-05", mdContent);
            Assert.Contains("| 14:00 |", mdContent);
            Assert.Contains("5.1", mdContent);
            Assert.Contains("Anxious, Sad", mdContent);
            Assert.Contains("Learning", mdContent);
            Assert.Contains("Initial reflection", mdContent);

            // User edits custom note in Obsidian directly:
            string edited = mdContent.Replace("Initial reflection", "Custom user thought edited in Obsidian");
            File.WriteAllText(syncResult.TargetFilePath, edited);

            // Add another entry
            var entry2 = new MoodEntry
            {
                TimestampUtc = time.AddMinutes(30).ToUniversalTime(),
                PeriodStartUtc = time.ToUniversalTime(),
                PeriodEndUtc = time.AddMinutes(30).ToUniversalTime(),
                Score = 7.8,
                Keywords = ["Happy", "Thrilled"],
                AssociatedProjects = ["Work"],
                Note = "Felt great after breakthrough"
            };

            var syncResult2 = MoodLogSync.AppendMood(entry2, settings);
            Assert.True(syncResult2.Success);

            string updatedMd = File.ReadAllText(syncResult2.TargetFilePath);
            // Verify custom user edit was preserved!
            Assert.Contains("Custom user thought edited in Obsidian", updatedMd);
            Assert.Contains("Felt great after breakthrough", updatedMd);
            Assert.Contains("7.8", updatedMd);
            Assert.Contains("Happy, Thrilled", updatedMd);
        }
        finally
        {
            MoodHistoryStore.SetCustomFilePath(null);
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }
}
