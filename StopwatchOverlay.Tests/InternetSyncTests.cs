using System;
using System.IO;
using StopwatchOverlay.Internet;
using Xunit;

namespace StopwatchOverlay.Tests;

public class InternetSyncTests
{
    [Fact]
    public void ParseNetshWlanOutput_WithConnectedInterface_ReturnsSsidAndSignal()
    {
        string sampleOutput = @"
There is 1 interface on the system: 

    Name                   : Wi-Fi
    Description            : Intel(R) Dual Band Wireless-AC 8265
    GUID                   : 761a5656-e902-43ba-aa17-fd7a387b45da
    State                  : connected
    SSID                   : MyHomeWifi_5G
    Radio type             : 802.11ac
    Authentication         : WPA2-Personal
    Cipher                 : CCMP
    Connection mode        : Profile
    Signal                 : 88% 
    Profile                : MyHomeWifi_5G 
";

        var result = NetworkInfoDetector.ParseNetshWlanOutput(sampleOutput);

        Assert.NotNull(result);
        Assert.Equal("MyHomeWifi_5G", result.Value.Ssid);
        Assert.Equal(88, result.Value.Signal);
    }

    [Fact]
    public void ParseNetshWlanOutput_WithDisconnectedInterface_ReturnsNull()
    {
        string sampleOutput = @"
There is 1 interface on the system: 

    Name                   : Wi-Fi
    Description            : Intel(R) Dual Band Wireless-AC 8265
    State                  : disconnected
";

        var result = NetworkInfoDetector.ParseNetshWlanOutput(sampleOutput);

        Assert.Null(result);
    }

    [Fact]
    public void ParseNetshWlanOutput_WithEmptyOutput_ReturnsNull()
    {
        var result = NetworkInfoDetector.ParseNetshWlanOutput(string.Empty);
        Assert.Null(result);
    }

    [Fact]
    public void FormatRow_FormatsCorrectMarkdownColumns()
    {
        var netInfo = new NetworkConnectionInfo(
            IsConnected: true,
            ConnectionType: "Wi-Fi",
            NetworkName: "CoffeeShop",
            SignalPercent: 75,
            AdapterDescription: "Intel Wi-Fi",
            DisplayText: "📶 CoffeeShop (75%)");

        var check = new InternetCheckResult(
            Timestamp: new DateTime(2026, 9, 26, 14, 30, 0),
            Status: InternetStatus.Online,
            PingMs: 15,
            DownloadMbps: 35.4,
            NetworkInfo: netInfo,
            Notes: "Stable");

        string row = InternetLogSync.FormatRow(check);

        Assert.Equal("| 14:30 | 🟢 Online | 📶 CoffeeShop (75%) | 15 ms | 35.4 Mbps | Stable |", row);
    }

    [Fact]
    public void FormatRow_WithOfflineStatus_FormatsDashes()
    {
        var netInfo = new NetworkConnectionInfo(
            IsConnected: false,
            ConnectionType: "Offline",
            NetworkName: "Disconnected",
            SignalPercent: null,
            AdapterDescription: string.Empty,
            DisplayText: "❌ Disconnected");

        var check = new InternetCheckResult(
            Timestamp: new DateTime(2026, 9, 26, 15, 0, 0),
            Status: InternetStatus.Offline,
            PingMs: null,
            DownloadMbps: null,
            NetworkInfo: netInfo,
            Notes: "Ping timed out");

        string row = InternetLogSync.FormatRow(check);

        Assert.Equal("| 15:00 | 🔴 Offline | ❌ Disconnected | - | - | Ping timed out |", row);
    }

    [Fact]
    public void InsertRowIntoContent_NewDay_AppendsSectionAndTable()
    {
        string existing = "# 🌐 Internet Connection Log\n\nAutomated network monitoring.\n\n## 📅 2026-09-25\n\n| Time | Status | Network / Wi-Fi | Ping | Speed | Notes |\n| :--- | :--- | :--- | :--- | :--- | :--- |\n| 10:00 | 🟢 Online | 📶 Office | 12 ms | 50.0 Mbps | Stable |\n";
        string newDateHeader = "## 📅 2026-09-26";
        string newRow = "| 09:15 | 🟢 Online | 📶 Home_WiFi (90%) | 8 ms | 42.1 Mbps | Stable |";

        string updated = InternetLogSync.InsertRowIntoContent(existing, newDateHeader, newRow);

        Assert.Contains("## 📅 2026-09-25", updated);
        Assert.Contains("## 📅 2026-09-26", updated);
        Assert.Contains(newRow, updated);
    }

    [Fact]
    public void InsertRowIntoContent_SameDay_AppendsRowUnderExistingTable()
    {
        string existing = "# 🌐 Internet Connection Log\n\n## 📅 2026-09-26\n\n| Time | Status | Network / Wi-Fi | Ping | Speed | Notes |\n| :--- | :--- | :--- | :--- | :--- | :--- |\n| 10:00 | 🟢 Online | 📶 Office | 12 ms | 50.0 Mbps | Stable |\n";
        string dateHeader = "## 📅 2026-09-26";
        string secondRow = "| 10:15 | 🟢 Online | 📶 Office | 14 ms | 48.0 Mbps | Stable |";

        string updated = InternetLogSync.InsertRowIntoContent(existing, dateHeader, secondRow);

        Assert.Contains("| 10:00 | 🟢 Online", updated);
        Assert.Contains(secondRow, updated);
        // Only one date header
        Assert.Equal(1, RegexCount(updated, "## 📅 2026-09-26"));
    }

    [Fact]
    public void AppendCheck_WritesToDiskCorrectly()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "Stopwatch_InternetSyncTest_" + Guid.NewGuid().ToString("N"));
        string tempJson = Path.Combine(tempDir, "internet-history.json");
        Directory.CreateDirectory(tempDir);

        try
        {
            InternetHistoryStore.SetCustomFilePath(tempJson);
            var settings = new AppSettings
            {
                ObsidianVaultFolder = tempDir,
                InternetLogFileName = "Internet Log.md"
            };

            var netInfo = new NetworkConnectionInfo(true, "Wi-Fi", "Home", 95, "Adapter", "📶 Home (95%)");
            var check1 = new InternetCheckResult(new DateTime(2026, 9, 26, 11, 0, 0), InternetStatus.Online, 10, 45.0, netInfo, "Stable");
            var check2 = new InternetCheckResult(new DateTime(2026, 9, 26, 11, 15, 0), InternetStatus.Slow, 210, 3.2, netInfo, "High latency");

            var res1 = InternetLogSync.AppendCheck(check1, settings);
            Assert.True(res1.Success);
            Assert.True(File.Exists(res1.TargetFilePath));

            var res2 = InternetLogSync.AppendCheck(check2, settings);
            Assert.True(res2.Success);

            string fileText = File.ReadAllText(res1.TargetFilePath);
            Assert.Contains("## 📅 2026-09-26", fileText);
            Assert.Contains("| 11:00 | 🟢 Online | 📶 Home (95%) | 10 ms | 45.0 Mbps | Stable |", fileText);
            Assert.Contains("| 11:15 | 🟡 Slow | 📶 Home (95%) | 210 ms | 3.2 Mbps | High latency |", fileText);
        }
        finally
        {
            InternetHistoryStore.SetCustomFilePath(null);
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public void InternetHistoryStore_AddAndRetrieve_PersistsToJson()
    {
        string tempJson = Path.Combine(Path.GetTempPath(), "hist_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            InternetHistoryStore.SetCustomFilePath(tempJson);
            var netInfo = new NetworkConnectionInfo(true, "Wi-Fi", "HomeNet", 90, "Adapter", "📶 HomeNet (90%)");
            var check1 = new InternetCheckResult(new DateTime(2026, 9, 28, 10, 0, 0), InternetStatus.Online, 12, 50.0, netInfo, "Fast");
            var check2 = new InternetCheckResult(new DateTime(2026, 9, 28, 10, 15, 0), InternetStatus.Slow, 250, 2.5, netInfo, "Slow");

            InternetHistoryStore.Add(check1);
            InternetHistoryStore.Add(check2);

            var retrieved = InternetHistoryStore.GetAll();
            Assert.Equal(2, retrieved.Count);
            Assert.Equal(50.0, retrieved[0].DownloadMbps);
            Assert.Equal(2.5, retrieved[1].DownloadMbps);

            // Force reload from disk
            InternetHistoryStore.SetCustomFilePath(tempJson);
            var reloaded = InternetHistoryStore.GetAll();
            Assert.Equal(2, reloaded.Count);
            Assert.Equal(12, reloaded[0].PingMs);
            Assert.Equal(250, reloaded[1].PingMs);
        }
        finally
        {
            InternetHistoryStore.SetCustomFilePath(null);
            if (File.Exists(tempJson)) File.Delete(tempJson);
            if (File.Exists(tempJson + ".bak")) File.Delete(tempJson + ".bak");
        }
    }

    [Fact]
    public void InternetHistoryStore_UpsertDeduplication_UpdatesSameMinuteRecord()
    {
        string tempJson = Path.Combine(Path.GetTempPath(), "hist_dedup_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            InternetHistoryStore.SetCustomFilePath(tempJson);
            var netInfo = new NetworkConnectionInfo(true, "Wi-Fi", "HomeNet", 90, "Adapter", "📶 HomeNet (90%)");
            var check1 = new InternetCheckResult(new DateTime(2026, 9, 28, 12, 0, 0), InternetStatus.Online, 10, null, netInfo, "Initial ping");
            var check2 = new InternetCheckResult(new DateTime(2026, 9, 28, 12, 0, 25), InternetStatus.Online, 10, 48.5, netInfo, "Measured speed");

            InternetHistoryStore.Add(check1);
            InternetHistoryStore.Add(check2);

            var all = InternetHistoryStore.GetAll();
            Assert.Single(all);
            Assert.Equal(48.5, all[0].DownloadMbps);
            Assert.Equal("Measured speed", all[0].Notes);
        }
        finally
        {
            InternetHistoryStore.SetCustomFilePath(null);
            if (File.Exists(tempJson)) File.Delete(tempJson);
            if (File.Exists(tempJson + ".bak")) File.Delete(tempJson + ".bak");
        }
    }

    [Fact]
    public void InternetHistoryStore_PurgeOlderThanDays_RemovesOldEntriesOnly()
    {
        string tempJson = Path.Combine(Path.GetTempPath(), "hist_purge_" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            InternetHistoryStore.SetCustomFilePath(tempJson);
            var netInfo = new NetworkConnectionInfo(true, "Wi-Fi", "HomeNet", 90, "Adapter", "📶 HomeNet (90%)");
            var oldCheck = new InternetCheckResult(DateTime.Now.AddDays(-10), InternetStatus.Online, 10, 40.0, netInfo, "Old");
            var recentCheck = new InternetCheckResult(DateTime.Now.AddDays(-2), InternetStatus.Online, 15, 45.0, netInfo, "Recent");

            InternetHistoryStore.AddRange(new[] { oldCheck, recentCheck });
            Assert.Equal(2, InternetHistoryStore.GetAll().Count);

            int purged = InternetHistoryStore.PurgeOlderThanDays(7);
            Assert.Equal(1, purged);
            var remaining = InternetHistoryStore.GetAll();
            Assert.Single(remaining);
            Assert.Equal("Recent", remaining[0].Notes);
        }
        finally
        {
            InternetHistoryStore.SetCustomFilePath(null);
            if (File.Exists(tempJson)) File.Delete(tempJson);
            if (File.Exists(tempJson + ".bak")) File.Delete(tempJson + ".bak");
        }
    }

    [Fact]
    public void InternetHistoryStore_ImportFromMarkdown_ParsesHistoricalEntries()
    {
        string markdown = @"# 🌐 Internet Connection Log

Automated network connection and speed monitoring logged periodically by Stopwatch Overlay.

## 📅 2026-09-27

| Time | Status | Network / Wi-Fi | Ping | Speed | Notes |
| :--- | :--- | :--- | :--- | :--- | :--- |
| 14:00 | 🟢 Online | 📶 Home_5G (85%) | 18 ms | 55.2 Mbps | Stable |
| 14:15 | 🔴 Offline | ❌ Disconnected | - | - | ISP Disconnected |

## 📅 2026-09-28

| Time | Status | Network / Wi-Fi | Ping | Speed | Notes |
| :--- | :--- | :--- | :--- | :--- | :--- |
| 09:30 | 🟡 Slow | 📶 Home_5G (70%) | 220 ms | 4.1 Mbps | High latency |
";

        var parsed = InternetHistoryStore.ParseMarkdownContent(markdown);
        Assert.Equal(3, parsed.Count);

        Assert.Equal(new DateTime(2026, 9, 27, 14, 0, 0), parsed[0].Timestamp);
        Assert.Equal(InternetStatus.Online, parsed[0].Status);
        Assert.Equal("Home_5G", parsed[0].NetworkInfo.NetworkName);
        Assert.Equal(85, parsed[0].NetworkInfo.SignalPercent);
        Assert.Equal(18, parsed[0].PingMs);
        Assert.Equal(55.2, parsed[0].DownloadMbps);
        Assert.Equal("Stable", parsed[0].Notes);

        Assert.Equal(new DateTime(2026, 9, 27, 14, 15, 0), parsed[1].Timestamp);
        Assert.Equal(InternetStatus.Offline, parsed[1].Status);
        Assert.Null(parsed[1].PingMs);
        Assert.Null(parsed[1].DownloadMbps);

        Assert.Equal(new DateTime(2026, 9, 28, 9, 30, 0), parsed[2].Timestamp);
        Assert.Equal(InternetStatus.Slow, parsed[2].Status);
        Assert.Equal(220, parsed[2].PingMs);
        Assert.Equal(4.1, parsed[2].DownloadMbps);
    }

    [Fact]
    public void BuildMarkdownDocument_PreservesCustomNotes()
    {
        var netInfo = new NetworkConnectionInfo(true, "Wi-Fi", "HomeNet", 90, "Adapter", "📶 HomeNet (90%)");
        var check = new InternetCheckResult(new DateTime(2026, 9, 28, 11, 0, 0), InternetStatus.Online, 15, 50.0, netInfo, "Default Note");

        var customNotes = new System.Collections.Generic.Dictionary<string, string>
        {
            ["2026-09-28 11:00"] = "User edited note about Zoom meeting"
        };

        string doc = InternetLogSync.BuildMarkdownDocument(new[] { check }, customNotes);
        Assert.Contains("User edited note about Zoom meeting", doc);
        Assert.DoesNotContain("Default Note", doc);
    }

    [Fact]
    public async System.Threading.Tasks.Task CheckConnectionAsync_MeasuresValidLatencyWhenOnline()
    {
        var result = await InternetSpeedProbe.CheckConnectionAsync(sampleBytes: 0);
        if (result.NetworkInfo.IsConnected && result.Status != InternetStatus.Offline)
        {
            Assert.NotNull(result.PingMs);
            Assert.InRange(result.PingMs.Value, 0, 5000);
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task AppendLiveCheckToTempDir()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "StopwatchInternetLiveTest_" + Guid.NewGuid().ToString("N"));
        string tempJson = Path.Combine(tempDir, "internet-history.json");
        Directory.CreateDirectory(tempDir);
        try
        {
            InternetHistoryStore.SetCustomFilePath(tempJson);
            var settings = new AppSettings
            {
                ObsidianVaultFolder = tempDir,
                InternetLogFileName = "Internet Log.md"
            };
            var check = await InternetSpeedProbe.CheckConnectionAsync(sampleBytes: 0);
            var res = InternetLogSync.AppendCheck(check, settings);
            Assert.True(res.Success);
            Assert.True(File.Exists(res.TargetFilePath));
        }
        finally
        {
            InternetHistoryStore.SetCustomFilePath(null);
            if (Directory.Exists(tempDir))
            {
                try { Directory.Delete(tempDir, true); } catch { }
            }
        }
    }

    [Fact]
    public async System.Threading.Tasks.Task CheckConnectionAsync_WithSampleBytes_DoesNotThrow()
    {
        using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(15));
        var result = await InternetSpeedProbe.CheckConnectionAsync(sampleBytes: 50_000, cancellationToken: cts.Token);
        Assert.NotNull(result);
        Assert.NotNull(result.Notes);
    }

    private static int RegexCount(string input, string pattern)
    {
        return System.Text.RegularExpressions.Regex.Matches(input, System.Text.RegularExpressions.Regex.Escape(pattern)).Count;
    }
}

