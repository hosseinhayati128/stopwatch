using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Xunit;

namespace StopwatchOverlay.Tests;

public class HistoricalDataMigrationTests : IDisposable
{
    private readonly string _testDirectory;

    public HistoricalDataMigrationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), "Stopwatch_Migration_Tests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, true);
        }
        catch
        {
            // Ignore cleanup failure
        }
    }

    [Fact]
    public void HistoricalDataMigration_PrunesDuplicatesAndMicroIntervals_CreatesBackups_AndResyncsLog()
    {
        // 1. Arrange test history file
        string historyPath = Path.Combine(_testDirectory, "project-history.json");
        string vaultPath = Path.Combine(_testDirectory, "Vault");
        Directory.CreateDirectory(vaultPath);

        string settingsPath = Path.Combine(_testDirectory, "settings.json");
        var settings = new AppSettings
        {
            ObsidianVaultFolder = vaultPath,
            ObsidianExportFileName = "Stopwatch Log.md"
        };
        SettingsStore.Save(settings, settingsPath);

        DateTime start1 = new DateTime(2026, 10, 6, 7, 38, 36, 86, DateTimeKind.Utc);
        DateTime end1 = new DateTime(2026, 10, 6, 8, 15, 22, 140, DateTimeKind.Utc);

        DateTime start2 = new DateTime(2026, 10, 6, 7, 38, 0, 0, DateTimeKind.Utc);
        DateTime end2 = new DateTime(2026, 10, 6, 8, 15, 0, 0, DateTimeKind.Utc);

        DateTime start3 = new DateTime(2026, 10, 6, 9, 0, 0, 0, DateTimeKind.Utc);
        DateTime end3 = new DateTime(2026, 10, 6, 9, 0, 2, 0, DateTimeKind.Utc);

        DateTime start4 = new DateTime(2026, 10, 6, 10, 0, 0, 0, DateTimeKind.Utc);
        DateTime end4 = new DateTime(2026, 10, 6, 11, 0, 0, 0, DateTimeKind.Utc);

        var doc = new ProjectHistoryDocument
        {
            Version = ProjectTimeStore.CurrentVersion,
            SavedAtUtc = DateTime.UtcNow,
            Projects = new List<ProjectDocumentEntry>
            {
                new() { Key = "WORK", Name = "Work", ScorePerHour = 1.0 },
                new() { Key = "CODING", Name = "Coding", ScorePerHour = 1.0 }
            },
            Intervals = new List<WorkIntervalDocumentEntry>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    TimerSessionId = Guid.NewGuid(),
                    ProjectKey = "WORK",
                    ProjectName = "Work",
                    StartUtc = start1,
                    EndUtc = end1
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TimerSessionId = Guid.NewGuid(),
                    ProjectKey = "WORK",
                    ProjectName = "Work",
                    StartUtc = start2,
                    EndUtc = end2
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TimerSessionId = Guid.NewGuid(),
                    ProjectKey = "WORK",
                    ProjectName = "Work",
                    StartUtc = start3,
                    EndUtc = end3
                },
                new()
                {
                    Id = Guid.NewGuid(),
                    TimerSessionId = Guid.NewGuid(),
                    ProjectKey = "CODING",
                    ProjectName = "Coding",
                    StartUtc = start4,
                    EndUtc = end4
                }
            }
        };

        File.WriteAllText(historyPath, JsonSerializer.Serialize(doc));

        // Create initial dummy Stopwatch Log.md
        string logPath = Path.Combine(vaultPath, "Stopwatch Log.md");
        File.WriteAllText(logPath, "# Initial Log\n| Date | Project |\n");

        // 2. Act
        var result = HistoricalDataMigration.Run(historyPath, settingsPath);

        // 3. Assert
        Assert.True(result.Success);
        Assert.Equal(2, result.PrunedIntervalsCount); // 1 duplicate + 1 micro-interval pruned
        Assert.Equal(2, result.RemainingIntervalsCount); // 1 Work + 1 Coding kept
        Assert.True(File.Exists(result.HistoryBackupPath));
        Assert.NotNull(result.LogBackupPath);
        Assert.True(File.Exists(result.LogBackupPath));

        // Verify remaining data in store
        var cleanedStore = new ProjectTimeStore(historyPath);
        Assert.True(cleanedStore.TryLoad(out var cleanedHistory));
        var view = cleanedHistory!.CreateView(DateTime.UtcNow);
        Assert.Equal(2, view.Intervals.Count);

        // Verify Stopwatch Log.md was regenerated
        string logContent = File.ReadAllText(logPath);
        Assert.Contains("Work", logContent);
        Assert.Contains("Coding", logContent);
    }

    [Fact]
    public void MigrateLiveUserData_IfFileExists()
    {
        string liveHistory = ProjectTimeStore.ProjectHistoryPath;
        if (!File.Exists(liveHistory)) return;

        var result = HistoricalDataMigration.Run();
        Assert.True(result.Success, result.Message);
        Assert.True(result.RemainingIntervalsCount > 0);
        Assert.True(File.Exists(result.HistoryBackupPath));
        // Verify no micro-intervals remain (< 3s)
        var store = new ProjectTimeStore(liveHistory);
        Assert.True(store.TryLoad(out var history));
        var view = history!.CreateView(DateTime.UtcNow);
        foreach (var interval in view.Intervals)
        {
            if (interval.EndUtc.HasValue)
            {
                Assert.True((interval.EndUtc.Value - interval.StartUtc).TotalSeconds >= 3.0,
                    $"Interval {interval.Id} for {interval.ProjectName} was < 3s");
            }
        }
    }
}

