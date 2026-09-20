using System;
using System.IO;
using System.Linq;
using StopwatchOverlay;
using Xunit;

namespace StopwatchOverlay.Tests;

public sealed class ObsidianLogSyncTests : IDisposable
{
    private readonly string _testDirectory;

    public ObsidianLogSyncTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "StopwatchOverlayObsidianTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
                Directory.Delete(_testDirectory, recursive: true);
        }
        catch
        {
            // Best effort cleanup
        }
    }

    [Fact]
    public void FormatDuration_FormatsExpectedStrings()
    {
        Assert.Equal("< 1m", ObsidianLogSync.FormatDuration(TimeSpan.FromSeconds(20)));
        Assert.Equal("5m", ObsidianLogSync.FormatDuration(TimeSpan.FromMinutes(5)));
        Assert.Equal("1h", ObsidianLogSync.FormatDuration(TimeSpan.FromHours(1)));
        Assert.Equal("1h 30m", ObsidianLogSync.FormatDuration(TimeSpan.FromMinutes(90)));
        Assert.Equal("2h 15m", ObsidianLogSync.FormatDuration(TimeSpan.FromMinutes(135)));
    }

    [Fact]
    public void SyncHistory_CreatesNewFileWithFrontmatterAndTable()
    {
        var history = new ProjectTimeHistory();
        DateTime baseUtc = new(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);
        history.AddManualInterval("Client Alpha", baseUtc, baseUtc.AddMinutes(90));

        var settings = new AppSettings
        {
            ObsidianVaultFolder = _testDirectory,
            ObsidianExportFileName = "Stopwatch Log.md"
        };

        var result = ObsidianLogSync.SyncHistory(history.CreateView(baseUtc.AddHours(2)), settings);

        Assert.True(result.Success);
        Assert.Equal(1, result.RecordCount);
        string expectedFilePath = Path.Combine(_testDirectory, "Stopwatch Log.md");
        Assert.True(File.Exists(expectedFilePath));

        string content = File.ReadAllText(expectedFilePath);
        Assert.Contains("---", content);
        Assert.Contains("type: time-tracking-log", content);
        Assert.Contains("tags:", content);
        Assert.Contains("- time-tracking", content);
        Assert.Contains("- stopwatch", content);
        Assert.Contains("# Time Tracking Log", content);
        Assert.Contains("| Date | Project | Start | End | Duration (min) | Duration | Notes |", content);
        Assert.Contains("| :--- | :--- | :--- | :--- | :--- | :--- | :--- |", content);
        Assert.Contains("Client Alpha", content);
        Assert.Contains("90", content);
        Assert.Contains("1h 30m", content);
    }

    [Fact]
    public void SyncHistory_PreservesNotesInExistingFile()
    {
        var history = new ProjectTimeHistory();
        DateTime baseUtc = new(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);
        var interval1 = history.AddManualInterval("Client Alpha", baseUtc, baseUtc.AddMinutes(60));

        var settings = new AppSettings
        {
            ObsidianVaultFolder = _testDirectory,
            ObsidianExportFileName = "Stopwatch Log.md"
        };

        var result1 = ObsidianLogSync.SyncHistory(history.CreateView(baseUtc.AddHours(2)), settings);
        Assert.True(result1.Success);

        string expectedFilePath = Path.Combine(_testDirectory, "Stopwatch Log.md");
        string originalContent = File.ReadAllText(expectedFilePath);

        // Simulate user or Gemini adding a note to the Notes column
        DateTime startLocal = interval1.StartUtc.ToLocalTime();
        DateTime endLocal = interval1.EndUtc!.Value.ToLocalTime();
        string oldRow = $"| {startLocal:yyyy-MM-dd} | Client Alpha | {startLocal:HH:mm} | {endLocal:HH:mm} | 60 | 1h |  |";
        string updatedRow = $"| {startLocal:yyyy-MM-dd} | Client Alpha | {startLocal:HH:mm} | {endLocal:HH:mm} | 60 | 1h | Fixed authentication bug |";

        string modifiedContent = originalContent.Replace(oldRow, updatedRow);
        File.WriteAllText(expectedFilePath, modifiedContent);

        // Add a second interval
        history.AddManualInterval("Research", baseUtc.AddHours(2), baseUtc.AddHours(3));

        // Sync again
        var result2 = ObsidianLogSync.SyncHistory(history.CreateView(baseUtc.AddHours(4)), settings);
        Assert.True(result2.Success);
        Assert.Equal(2, result2.RecordCount);

        string finalContent = File.ReadAllText(expectedFilePath);
        Assert.Contains("Fixed authentication bug", finalContent);
        Assert.Contains("Research", finalContent);
    }

    [Fact]
    public void SyncHistory_PreservesCustomFooterSections()
    {
        var history = new ProjectTimeHistory();
        DateTime baseUtc = new(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);
        history.AddManualInterval("Client Alpha", baseUtc, baseUtc.AddMinutes(45));

        var settings = new AppSettings
        {
            ObsidianVaultFolder = _testDirectory,
            ObsidianExportFileName = "Stopwatch Log.md"
        };

        var result1 = ObsidianLogSync.SyncHistory(history.CreateView(baseUtc.AddHours(2)), settings);
        Assert.True(result1.Success);

        string expectedFilePath = Path.Combine(_testDirectory, "Stopwatch Log.md");
        string originalContent = File.ReadAllText(expectedFilePath);

        // Simulate user adding a mermaid chart or notes at the bottom of the file
        string customFooter = "## Weekly Summary\n\n```mermaid\npie title Hours\n\"Client Alpha\": 45\n```";
        File.WriteAllText(expectedFilePath, originalContent + Environment.NewLine + customFooter);

        // Add another interval and sync
        history.AddManualInterval("Planning", baseUtc.AddHours(1), baseUtc.AddHours(2));
        var result2 = ObsidianLogSync.SyncHistory(history.CreateView(baseUtc.AddHours(3)), settings);
        Assert.True(result2.Success);

        string finalContent = File.ReadAllText(expectedFilePath);
        Assert.Contains("## Weekly Summary", finalContent);
        Assert.Contains("```mermaid", finalContent);
        Assert.Contains("Planning", finalContent);
    }

    [Fact]
    public void SyncHistory_RejectsEmptyFolder()
    {
        var history = new ProjectTimeHistory();
        var settings = new AppSettings
        {
            ObsidianVaultFolder = "",
            ObsidianExportFileName = "Stopwatch Log.md"
        };

        var result = ObsidianLogSync.SyncHistory(history.CreateView(DateTime.UtcNow), settings);
        Assert.False(result.Success);
        Assert.Contains("No Obsidian vault or target folder", result.Message);
    }

    [Fact]
    public void TryAutoSync_DoesNotRunWhenDisabled()
    {
        var history = new ProjectTimeHistory();
        DateTime baseUtc = new(2026, 9, 19, 10, 0, 0, DateTimeKind.Utc);
        history.AddManualInterval("Client Alpha", baseUtc, baseUtc.AddMinutes(45));

        var settings = new AppSettings
        {
            ObsidianAutoSyncEnabled = false,
            ObsidianVaultFolder = _testDirectory,
            ObsidianExportFileName = "Stopwatch Log.md"
        };

        bool synced = ObsidianLogSync.TryAutoSync(history.CreateView(baseUtc.AddHours(1)), settings);
        Assert.False(synced);
        Assert.False(File.Exists(Path.Combine(_testDirectory, "Stopwatch Log.md")));
    }
}
