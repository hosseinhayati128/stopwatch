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
    public void IsBrowserApp_IdentifiesBrowsers()
    {
        Assert.True(ActivityWatchSync.IsBrowserApp("chrome"));
        Assert.True(ActivityWatchSync.IsBrowserApp("chrome.exe"));
        Assert.True(ActivityWatchSync.IsBrowserApp("Google Chrome"));
        Assert.True(ActivityWatchSync.IsBrowserApp("msedge"));
        Assert.True(ActivityWatchSync.IsBrowserApp("firefox"));
        Assert.True(ActivityWatchSync.IsBrowserApp("brave"));
        Assert.True(ActivityWatchSync.IsBrowserApp("opera"));
        Assert.True(ActivityWatchSync.IsBrowserApp("vivaldi"));
        Assert.True(ActivityWatchSync.IsBrowserApp("arc"));

        Assert.False(ActivityWatchSync.IsBrowserApp("Telegram"));
        Assert.False(ActivityWatchSync.IsBrowserApp("Obsidian"));
        Assert.False(ActivityWatchSync.IsBrowserApp("Code"));
        Assert.False(ActivityWatchSync.IsBrowserApp("vlc"));
        Assert.False(ActivityWatchSync.IsBrowserApp(""));
        Assert.False(ActivityWatchSync.IsBrowserApp(null));
    }

    [Fact]
    public void MergeIntervals_MergesOverlappingAndAdjacentIntervals()
    {
        var baseTime = new DateTimeOffset(2026, 9, 26, 10, 0, 0, TimeSpan.Zero);
        var intervals = new List<(DateTimeOffset Start, DateTimeOffset End)>
        {
            (baseTime, baseTime.AddMinutes(30)),
            (baseTime.AddMinutes(20), baseTime.AddMinutes(50)), // overlaps
            (baseTime.AddMinutes(50), baseTime.AddMinutes(60)), // adjacent
            (baseTime.AddHours(2), baseTime.AddHours(3))        // separate
        };

        var merged = ActivityWatchSync.MergeIntervals(intervals);

        Assert.Equal(2, merged.Count);
        Assert.Equal(baseTime, merged[0].Start);
        Assert.Equal(baseTime.AddMinutes(60), merged[0].End);
        Assert.Equal(baseTime.AddHours(2), merged[1].Start);
        Assert.Equal(baseTime.AddHours(3), merged[1].End);
    }

    [Fact]
    public void SubtractIntervals_RemovesExclusionsAndSplitsSegments()
    {
        var baseTime = new DateTimeOffset(2026, 9, 26, 1, 0, 0, TimeSpan.Zero);
        var start = baseTime;
        var end = baseTime.AddHours(4); // 01:00 - 05:00

        var exclusions = new List<(DateTimeOffset Start, DateTimeOffset End)>
        {
            (baseTime.AddHours(1), baseTime.AddHours(2)), // 02:00 - 03:00 (middle)
            (baseTime.AddHours(3).AddMinutes(30), baseTime.AddHours(5)) // 04:30 - 06:00 (overlaps end)
        };

        var result = ActivityWatchSync.SubtractIntervals(start, end, exclusions);

        Assert.Equal(2, result.Count);
        Assert.Equal(baseTime, result[0].Start);
        Assert.Equal(baseTime.AddHours(1), result[0].End); // 01:00 - 02:00

        Assert.Equal(baseTime.AddHours(2), result[1].Start);
        Assert.Equal(baseTime.AddHours(3).AddMinutes(30), result[1].End); // 03:00 - 04:30
    }

    [Fact]
    public void SubtractIntervals_WhenEventCompletelyInsideExclusion_ReturnsEmpty()
    {
        var baseTime = new DateTimeOffset(2026, 9, 26, 2, 0, 0, TimeSpan.Zero);
        var start = baseTime.AddMinutes(15);
        var end = baseTime.AddMinutes(45);

        var exclusions = new List<(DateTimeOffset Start, DateTimeOffset End)>
        {
            (baseTime, baseTime.AddHours(1))
        };

        var result = ActivityWatchSync.SubtractIntervals(start, end, exclusions);
        Assert.Empty(result);
    }

    [Fact]
    public void IntersectIntervals_OnlyReturnsOverlappingRanges()
    {
        var baseTime = new DateTimeOffset(2026, 9, 26, 14, 0, 0, TimeSpan.Zero);
        var webStart = baseTime;
        var webEnd = baseTime.AddHours(2); // 14:00 - 16:00 (e.g. background tab open for 2 hours)

        // Active browser window time was only 14:05-14:20 and 15:10-15:30 (user was in Telegram/IDE the rest)
        var browserActive = new List<(DateTimeOffset Start, DateTimeOffset End)>
        {
            (baseTime.AddMinutes(5), baseTime.AddMinutes(20)),
            (baseTime.AddMinutes(70), baseTime.AddMinutes(90))
        };

        var result = ActivityWatchSync.IntersectIntervals(webStart, webEnd, browserActive);

        Assert.Equal(2, result.Count);
        Assert.Equal(baseTime.AddMinutes(5), result[0].Start);
        Assert.Equal(baseTime.AddMinutes(20), result[0].End);

        Assert.Equal(baseTime.AddMinutes(70), result[1].Start);
        Assert.Equal(baseTime.AddMinutes(90), result[1].End);
    }

    [Fact]
    public void IntersectIntervals_WhenNoOverlap_ReturnsEmpty()
    {
        var baseTime = new DateTimeOffset(2026, 9, 26, 0, 0, 0, TimeSpan.Zero);
        // Website event (e.g. localhost overnight: 00:00 - 10:13)
        var webStart = baseTime;
        var webEnd = baseTime.AddHours(10).AddMinutes(13);

        // Browser was only in foreground from 10:13 - 10:20
        var browserActive = new List<(DateTimeOffset Start, DateTimeOffset End)>
        {
            (baseTime.AddHours(10).AddMinutes(13), baseTime.AddHours(10).AddMinutes(20))
        };

        var result = ActivityWatchSync.IntersectIntervals(webStart, webEnd, browserActive);
        Assert.Empty(result);
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
        // Ensure the bug where localhost was 10h 14m across the whole day is resolved
        Assert.DoesNotContain("| 2026-09-26 | **localhost** | ActivityWatch | 00:00 | 10:13 | 614 | 10h 14m | Web |", content);
    }
}
