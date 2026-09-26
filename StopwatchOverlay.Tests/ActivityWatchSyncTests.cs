using System;
using System.Collections.Generic;
using System.IO;
using StopwatchOverlay;
using StopwatchOverlay.ActivityWatch;
using Xunit;

namespace StopwatchOverlay.Tests;

public sealed class ActivityWatchSyncTests : IDisposable
{
    private readonly string _testDirectory;

    public ActivityWatchSyncTests()
    {
        _testDirectory = Path.Combine(
            Path.GetTempPath(),
            "StopwatchOverlayAwTests_" + Guid.NewGuid().ToString("N"));
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
    public void NormalizeBaseUrl_HandlesVariousFormats()
    {
        Assert.Equal("http://localhost:5600", ActivityWatchClient.NormalizeBaseUrl(""));
        Assert.Equal("http://localhost:5600", ActivityWatchClient.NormalizeBaseUrl(null));
        Assert.Equal("http://localhost:5600", ActivityWatchClient.NormalizeBaseUrl("http://localhost:5600/"));
        Assert.Equal("http://localhost:5600", ActivityWatchClient.NormalizeBaseUrl("localhost:5600"));
        Assert.Equal("http://127.0.0.1:5600", ActivityWatchClient.NormalizeBaseUrl("http://127.0.0.1:5600"));
        Assert.Equal("https://my-aw-host:5600", ActivityWatchClient.NormalizeBaseUrl("https://my-aw-host:5600/"));
    }

    [Fact]
    public void FormatDuration_FormatsExpectedStrings()
    {
        Assert.Equal("30s", ActivityWatchSync.FormatDuration(TimeSpan.FromSeconds(30)));
        Assert.Equal("5m", ActivityWatchSync.FormatDuration(TimeSpan.FromMinutes(5)));
        Assert.Equal("1h", ActivityWatchSync.FormatDuration(TimeSpan.FromHours(1)));
        Assert.Equal("1h 30m", ActivityWatchSync.FormatDuration(TimeSpan.FromMinutes(90)));
    }

    [Fact]
    public void FormatReportToMarkdown_ProducesStructuredMarkdown()
    {
        var settings = new AppSettings
        {
            ActivityWatchIncludeTitles = true,
            ActivityWatchIncludeWeb = true
        };

        var report = new AwDailyReport
        {
            Date = new DateOnly(2026, 9, 25),
            TotalActiveDuration = TimeSpan.FromHours(2),
            TotalAfkDuration = TimeSpan.FromMinutes(30),
            Applications =
            [
                new AwAppSummary
                {
                    AppName = "Obsidian",
                    TotalDuration = TimeSpan.FromHours(1.5),
                    Titles = { ["Notes - Vault - Obsidian"] = TimeSpan.FromHours(1.5) }
                },
                new AwAppSummary
                {
                    AppName = "vlc",
                    TotalDuration = TimeSpan.FromMinutes(30),
                    Titles = { ["Documentary.mkv - VLC media player"] = TimeSpan.FromMinutes(30) }
                }
            ],
            WebSites =
            [
                new AwWebSummary
                {
                    Domain = "github.com",
                    TotalDuration = TimeSpan.FromMinutes(25),
                    Pages = { ["GitHub - Repository"] = TimeSpan.FromMinutes(25) }
                }
            ],
            Intervals =
            [
                new AwTimelineInterval(
                    new DateOnly(2026, 9, 25),
                    "Obsidian",
                    "Notes - Vault - Obsidian",
                    new TimeOnly(14, 0),
                    new TimeOnly(15, 30),
                    TimeSpan.FromMinutes(90),
                    "App")
            ]
        };

        string markdown = ActivityWatchSync.FormatReportToMarkdown(report, settings);

        Assert.Contains("## 📅 2026-09-25", markdown);
        Assert.Contains("**Total Active Time:** 2h", markdown);
        Assert.Contains("**Idle / Away:** 30m", markdown);
        Assert.Contains("| **Obsidian** | 1h 30m | `Notes - Vault - Obsidian` (1h 30m) |", markdown);
        Assert.Contains("| **vlc** | 30m | `Documentary.mkv - VLC media player` (30m) |", markdown);
        Assert.Contains("| `github.com` | 25m | GitHub - Repository (25m) |", markdown);
        Assert.Contains("### ⏱️ Activity Timeline Intervals", markdown);
        Assert.Contains("| 2026-09-25 | **Obsidian** | Notes - Vault - Obsidian | 14:00 | 15:30 | 90 | 1h 30m | App |", markdown);
    }

    [Fact]
    public void AppSettings_NormalizesActivityWatchProperties()
    {
        var settings = new AppSettings
        {
            ActivityWatchServerUrl = "localhost:5600/",
            ActivityWatchExportFileName = "   ",
            ActivityWatchMinDurationSeconds = -5
        };

        settings.NormalizeForRuntime();

        Assert.Equal("http://localhost:5600", settings.ActivityWatchServerUrl);
        Assert.Equal("ActivityWatch Log.md", settings.ActivityWatchExportFileName);
        Assert.Equal(1, settings.ActivityWatchMinDurationSeconds); // Clamped to minimum 1s
    }

    [Fact]
    public async System.Threading.Tasks.Task SyncAsync_WithLiveServer_ProducesExpectedMarkdown()
    {
        var settings = new AppSettings
        {
            ObsidianVaultFolder = @"C:\Users\h128\Documents\HCourses\SecondBrain\Time",
            ActivityWatchExportFileName = "ActivityWatch Log.md",
            ActivityWatchServerUrl = "http://localhost:5600",
            ActivityWatchEnabled = true
        };

        var res1 = await ActivityWatchSync.SyncAsync(settings, new DateOnly(2026, 9, 25));
        Assert.True(res1.Success);
        var res2 = await ActivityWatchSync.SyncAsync(settings, new DateOnly(2026, 9, 26));
        Assert.True(res2.Success);

        string content = await System.IO.File.ReadAllTextAsync(res2.TargetFilePath);
        Assert.Contains("### ⏱️ Activity Timeline Intervals", content);
        Assert.Contains("| Date | Activity | Details | Start | End | Duration (min) | Duration | Type |", content);
    }
}
