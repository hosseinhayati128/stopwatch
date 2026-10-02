using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace StopwatchOverlay.ActivityWatch;

public static class ActivityWatchSync
{
    public const string DefaultFileName = "ActivityWatch Log.md";

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        int totalMinutes = (int)Math.Round(duration.TotalMinutes);
        if (totalMinutes <= 0)
        {
            int totalSeconds = (int)Math.Round(duration.TotalSeconds);
            return totalSeconds > 0 ? $"{totalSeconds}s" : "< 1m";
        }

        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;

        if (hours == 0)
            return $"{minutes}m";
        if (minutes == 0)
            return $"{hours}h";

        return $"{hours}h {minutes}m";
    }

    public static async Task<AwDailyReport> GenerateDailyReportAsync(
        AppSettings settings,
        DateOnly date,
        CancellationToken ct = default)
    {
        var client = new ActivityWatchClient(settings.ActivityWatchServerUrl);

        var startOfDayLocal = date.ToDateTime(TimeOnly.MinValue);
        var endOfDayLocal = date.ToDateTime(TimeOnly.MaxValue);
        DateTimeOffset startUtc = new DateTimeOffset(startOfDayLocal, DateTimeOffset.Now.Offset).ToUniversalTime();
        DateTimeOffset endUtc = new DateTimeOffset(endOfDayLocal, DateTimeOffset.Now.Offset).ToUniversalTime();

        var buckets = await client.GetBucketsAsync(ct).ConfigureAwait(false);

        // Find relevant buckets
        var afkBucket = buckets.Values.FirstOrDefault(b =>
            b.Type == "afkstatus" || b.Id.StartsWith("aw-watcher-afk", StringComparison.OrdinalIgnoreCase));

        var windowBucket = buckets.Values.FirstOrDefault(b =>
            b.Type == "currentwindow" || b.Id.StartsWith("aw-watcher-window", StringComparison.OrdinalIgnoreCase));

        var webBuckets = buckets.Values.Where(b =>
            b.Type == "web.tab.current" || b.Id.StartsWith("aw-watcher-web", StringComparison.OrdinalIgnoreCase)).ToList();

        // 1. Fetch AFK events to build idle intervals
        var rawAfkIntervals = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        if (afkBucket != null)
        {
            var afkEvents = await client.GetEventsAsync(afkBucket.Id, startUtc, endUtc, 50000, ct).ConfigureAwait(false);
            foreach (var evt in afkEvents)
            {
                if (string.Equals(evt.Status, "afk", StringComparison.OrdinalIgnoreCase))
                {
                    var rawStart = evt.Timestamp;
                    var rawEnd = rawStart.AddSeconds(Math.Max(0, evt.DurationSeconds));
                    var start = rawStart < startUtc ? startUtc : rawStart;
                    var end = rawEnd > endUtc ? endUtc : rawEnd;
                    if (end > start)
                    {
                        rawAfkIntervals.Add((start, end));
                    }
                }
            }
        }
        var mergedAfk = MergeIntervals(rawAfkIntervals);
        double totalAfkSeconds = mergedAfk.Sum(i => (i.End - i.Start).TotalSeconds);

        // 2. Fetch and aggregate window events (excluding AFK periods)
        var appMap = new Dictionary<string, AwAppSummary>(StringComparer.OrdinalIgnoreCase);
        var activeWindowIntervals = new List<(DateTimeOffset Start, DateTimeOffset End, string Activity, string Details, string Type)>();
        var allBrowserActiveIntervals = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        var browserSpecificIntervals = new Dictionary<string, List<(DateTimeOffset Start, DateTimeOffset End)>>(StringComparer.OrdinalIgnoreCase);

        if (windowBucket != null)
        {
            var windowEvents = await client.GetEventsAsync(windowBucket.Id, startUtc, endUtc, 50000, ct).ConfigureAwait(false);
            foreach (var evt in windowEvents)
            {
                var rawStart = evt.Timestamp;
                var rawEnd = rawStart.AddSeconds(Math.Max(0, evt.DurationSeconds));
                var start = rawStart < startUtc ? startUtc : rawStart;
                var end = rawEnd > endUtc ? endUtc : rawEnd;
                if (end <= start) continue;

                string app = string.IsNullOrWhiteSpace(evt.App) ? "Unknown" : CleanAppName(evt.App);
                string title = string.IsNullOrWhiteSpace(evt.Title) ? "Untitled" : evt.Title.Trim();

                var activeSegments = SubtractIntervals(start, end, mergedAfk);
                foreach (var (segStart, segEnd) in activeSegments)
                {
                    var segDuration = segEnd - segStart;
                    if (segDuration.TotalSeconds <= 0) continue;

                    if (!appMap.TryGetValue(app, out var appSummary))
                    {
                        appSummary = new AwAppSummary { AppName = app };
                        appMap[app] = appSummary;
                    }

                    appSummary.TotalDuration += segDuration;
                    if (settings.ActivityWatchIncludeTitles)
                    {
                        if (appSummary.Titles.TryGetValue(title, out var currentTitleDur))
                            appSummary.Titles[title] = currentTitleDur + segDuration;
                        else
                            appSummary.Titles[title] = segDuration;
                    }

                    activeWindowIntervals.Add((segStart, segEnd, app, title, "App"));

                    string? bKey = GetBrowserKey(app);
                    if (bKey != null)
                    {
                        allBrowserActiveIntervals.Add((segStart, segEnd));
                        if (!browserSpecificIntervals.TryGetValue(bKey, out var bList))
                        {
                            bList = [];
                            browserSpecificIntervals[bKey] = bList;
                        }
                        bList.Add((segStart, segEnd));
                    }
                }
            }
        }

        var mergedBrowserActive = MergeIntervals(allBrowserActiveIntervals);
        var mergedBrowserSpecific = new Dictionary<string, List<(DateTimeOffset Start, DateTimeOffset End)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kvp in browserSpecificIntervals)
        {
            mergedBrowserSpecific[kvp.Key] = MergeIntervals(kvp.Value);
        }

        double totalActiveSeconds = activeWindowIntervals.Count > 0
            ? MergeIntervals(activeWindowIntervals.Select(w => (w.Start, w.End))).Sum(i => (i.End - i.Start).TotalSeconds)
            : 0;

        // 3. Fetch and aggregate web events (intersected with active foreground browser window time)
        var webMap = new Dictionary<string, AwWebSummary>(StringComparer.OrdinalIgnoreCase);
        var activeWebIntervals = new List<(DateTimeOffset Start, DateTimeOffset End, string Activity, string Details, string Type)>();

        if (settings.ActivityWatchIncludeWeb)
        {
            foreach (var webBucket in webBuckets)
            {
                string? bucketBrowser = GetBrowserNameFromBucketId(webBucket.Id);
                List<(DateTimeOffset Start, DateTimeOffset End)> activeBrowserRanges;

                if (bucketBrowser != null && mergedBrowserSpecific.TryGetValue(bucketBrowser, out var specificList) && specificList.Count > 0)
                {
                    activeBrowserRanges = specificList;
                }
                else if (mergedBrowserActive.Count > 0)
                {
                    activeBrowserRanges = mergedBrowserActive;
                }
                else
                {
                    activeBrowserRanges = [];
                }

                var webEvents = await client.GetEventsAsync(webBucket.Id, startUtc, endUtc, 50000, ct).ConfigureAwait(false);
                foreach (var evt in webEvents)
                {
                    var rawStart = evt.Timestamp;
                    var rawEnd = rawStart.AddSeconds(Math.Max(0, evt.DurationSeconds));
                    var start = rawStart < startUtc ? startUtc : rawStart;
                    var end = rawEnd > endUtc ? endUtc : rawEnd;
                    if (end <= start) continue;

                    string domain = ExtractDomain(evt.Url);
                    string pageTitle = string.IsNullOrWhiteSpace(evt.Title) ? domain : evt.Title.Trim();

                    // Web activity is only active when the browser was actually active in the foreground!
                    // Fall back to subtracting AFK only if no browser window events exist in the window bucket.
                    var activeWebSegments = activeBrowserRanges.Count > 0
                        ? IntersectIntervals(start, end, activeBrowserRanges)
                        : SubtractIntervals(start, end, mergedAfk);

                    foreach (var (segStart, segEnd) in activeWebSegments)
                    {
                        var segDuration = segEnd - segStart;
                        if (segDuration.TotalSeconds <= 0) continue;

                        if (!webMap.TryGetValue(domain, out var webSummary))
                        {
                            webSummary = new AwWebSummary { Domain = domain };
                            webMap[domain] = webSummary;
                        }

                        webSummary.TotalDuration += segDuration;
                        if (webSummary.Pages.TryGetValue(pageTitle, out var currPageDur))
                            webSummary.Pages[pageTitle] = currPageDur + segDuration;
                        else
                            webSummary.Pages[pageTitle] = segDuration;

                        activeWebIntervals.Add((segStart, segEnd, domain, pageTitle, "Web"));
                    }
                }
            }
        }

        // Filter out applications shorter than minimum threshold
        TimeSpan minThreshold = TimeSpan.FromSeconds(Math.Max(1, settings.ActivityWatchMinDurationSeconds));
        var filteredApps = appMap.Values
            .Where(a => a.TotalDuration >= minThreshold)
            .OrderByDescending(a => a.TotalDuration)
            .ToList();

        var filteredWeb = webMap.Values
            .Where(w => w.TotalDuration >= minThreshold)
            .OrderByDescending(w => w.TotalDuration)
            .ToList();

        var intervals = BuildTimelineIntervals(date, activeWindowIntervals, activeWebIntervals, mergedAfk, settings);

        return new AwDailyReport
        {
            Date = date,
            TotalActiveDuration = TimeSpan.FromSeconds(totalActiveSeconds),
            TotalAfkDuration = TimeSpan.FromSeconds(totalAfkSeconds),
            Applications = filteredApps,
            WebSites = filteredWeb,
            Intervals = intervals
        };
    }

    public static async Task<ActivityWatchSyncResult> SyncAsync(
        AppSettings settings,
        DateOnly? targetDate = null,
        CancellationToken ct = default)
    {
        string folder = (settings.ObsidianVaultFolder ?? "").Trim();
        if (string.IsNullOrWhiteSpace(folder))
        {
            return new ActivityWatchSyncResult(
                false, 0, 0, "", "No Obsidian vault folder has been selected in Settings.");
        }

        if (!Directory.Exists(folder))
        {
            try
            {
                Directory.CreateDirectory(folder);
            }
            catch (Exception ex)
            {
                return new ActivityWatchSyncResult(
                    false, 0, 0, folder, $"Could not create folder: {ex.Message}");
            }
        }

        string fileName = !string.IsNullOrWhiteSpace(settings.ActivityWatchExportFileName)
            ? settings.ActivityWatchExportFileName.Trim()
            : DefaultFileName;

        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            fileName += ".md";

        string targetFilePath = Path.Combine(folder, fileName);
        DateOnly date = targetDate ?? DateOnly.FromDateTime(DateTime.Now);

        try
        {
            var report = await GenerateDailyReportAsync(settings, date, ct).ConfigureAwait(false);
            string markdownSection = FormatReportToMarkdown(report, settings);

            await UpdateMarkdownFileAsync(targetFilePath, date, markdownSection).ConfigureAwait(false);

            return new ActivityWatchSyncResult(
                true,
                report.Applications.Count,
                report.WebSites.Count,
                targetFilePath);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "ActivityWatchSync");
            return new ActivityWatchSyncResult(
                false, 0, 0, targetFilePath, $"ActivityWatch sync failed: {ex.Message}");
        }
    }

    public static void TryAutoSync(AppSettings settings)
    {
        if (!settings.ActivityWatchEnabled || !settings.ActivityWatchSyncOnStopwatchSync)
            return;

        if (string.IsNullOrWhiteSpace(settings.ObsidianVaultFolder))
            return;

        Task.Run(async () =>
        {
            try
            {
                await SyncAsync(settings).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                CrashLogger.LogRecoverable(ex, "ActivityWatchAutoSync");
            }
        });
    }

    public static string FormatReportToMarkdown(AwDailyReport report, AppSettings settings)
    {
        var sb = new StringBuilder();
        string dateStr = report.Date.ToString("yyyy-MM-dd");

        sb.AppendLine($"## 📅 {dateStr}");
        sb.AppendLine();
        sb.AppendLine($"> **Total Active Time:** {FormatDuration(report.TotalActiveDuration)} | **Idle / Away:** {FormatDuration(report.TotalAfkDuration)}");
        sb.AppendLine();

        // 1. Applications Table
        sb.AppendLine("### 🖥️ Applications & Windows");
        sb.AppendLine();
        if (report.Applications.Count == 0)
        {
            sb.AppendLine("_No application activity recorded for this period._");
        }
        else
        {
            sb.AppendLine("| Application | Active Time | Top Window / Details |");
            sb.AppendLine("| :--- | :--- | :--- |");
            foreach (var app in report.Applications)
            {
                string topDetail = "";
                if (settings.ActivityWatchIncludeTitles && app.Titles.Count > 0)
                {
                    var top = app.Titles.OrderByDescending(t => t.Value).First();
                    string sanitizedTitle = EscapeMarkdownPipe(Truncate(top.Key, 60));
                    topDetail = $"`{sanitizedTitle}` ({FormatDuration(top.Value)})";
                }
                sb.AppendLine($"| **{EscapeMarkdownPipe(app.AppName)}** | {FormatDuration(app.TotalDuration)} | {topDetail} |");
            }
        }
        sb.AppendLine();

        // 2. Web Sites Table
        if (settings.ActivityWatchIncludeWeb)
        {
            sb.AppendLine("### 🌐 Web Activity (Browser)");
            sb.AppendLine();
            if (report.WebSites.Count == 0)
            {
                sb.AppendLine("_No browser activity recorded (ensure the ActivityWatch Chrome extension is installed)._");
            }
            else
            {
                sb.AppendLine("| Domain / Site | Time | Top Page / Tab |");
                sb.AppendLine("| :--- | :--- | :--- |");
                foreach (var web in report.WebSites)
                {
                    string topPage = "";
                    if (web.Pages.Count > 0)
                    {
                        var top = web.Pages.OrderByDescending(p => p.Value).First();
                        string sanitizedPage = EscapeMarkdownPipe(Truncate(top.Key, 60));
                        topPage = $"{sanitizedPage} ({FormatDuration(top.Value)})";
                    }
                    sb.AppendLine($"| `{EscapeMarkdownPipe(web.Domain)}` | {FormatDuration(web.TotalDuration)} | {topPage} |");
                }
            }
            sb.AppendLine();
        }

        // 3. Timeline Intervals Table (for timeline charting and stopwatch correlation)
        sb.AppendLine("### ⏱️ Activity Timeline Intervals");
        sb.AppendLine();
        if (report.Intervals.Count == 0)
        {
            sb.AppendLine("_No activity timeline intervals recorded._");
        }
        else
        {
            sb.AppendLine("| Date | Activity | Details | Start | End | Duration (min) | Duration | Type |");
            sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |");
            foreach (var interval in report.Intervals)
            {
                string activity = EscapeMarkdownPipe(interval.Activity);
                string details = EscapeMarkdownPipe(Truncate(interval.Details, 70));
                sb.AppendLine($"| {interval.Date:yyyy-MM-dd} | **{activity}** | {details} | {interval.Start:HH:mm} | {interval.End:HH:mm} | {interval.DurationMinutes} | {FormatDuration(interval.Duration)} | {interval.Type} |");
            }
        }
        sb.AppendLine();

        return sb.ToString();
    }

    public static bool IsBrowserApp(string? appName)
    {
        return GetBrowserKey(appName) != null;
    }

    public static string? GetBrowserKey(string? appName)
    {
        if (string.IsNullOrWhiteSpace(appName)) return null;
        string clean = CleanAppName(appName).ToLowerInvariant();
        if (clean.Contains("chrome")) return "chrome";
        if (clean.Contains("msedge") || clean.Contains("edge")) return "msedge";
        if (clean.Contains("firefox")) return "firefox";
        if (clean.Contains("brave")) return "brave";
        if (clean.Contains("opera")) return "opera";
        if (clean.Contains("vivaldi")) return "vivaldi";
        if (clean.Contains("arc")) return "arc";
        if (clean.Contains("zen")) return "zen";
        if (clean.Contains("chromium")) return "chromium";
        if (clean.Contains("safari")) return "safari";
        return null;
    }

    public static string? GetBrowserNameFromBucketId(string? bucketId)
    {
        if (string.IsNullOrWhiteSpace(bucketId)) return null;
        string lower = bucketId.ToLowerInvariant();
        if (lower.Contains("chrome")) return "chrome";
        if (lower.Contains("msedge") || lower.Contains("edge")) return "msedge";
        if (lower.Contains("firefox")) return "firefox";
        if (lower.Contains("brave")) return "brave";
        if (lower.Contains("opera")) return "opera";
        if (lower.Contains("vivaldi")) return "vivaldi";
        if (lower.Contains("arc")) return "arc";
        if (lower.Contains("zen")) return "zen";
        if (lower.Contains("chromium")) return "chromium";
        if (lower.Contains("safari")) return "safari";
        return null;
    }

    public static List<(DateTimeOffset Start, DateTimeOffset End)> MergeIntervals(
        IEnumerable<(DateTimeOffset Start, DateTimeOffset End)> intervals)
    {
        var sorted = intervals
            .Where(i => i.End > i.Start)
            .OrderBy(i => i.Start)
            .ThenBy(i => i.End)
            .ToList();

        if (sorted.Count <= 1)
            return sorted;

        var merged = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        var current = sorted[0];

        for (int i = 1; i < sorted.Count; i++)
        {
            var next = sorted[i];
            if (next.Start <= current.End)
            {
                if (next.End > current.End)
                {
                    current = (current.Start, next.End);
                }
            }
            else
            {
                merged.Add(current);
                current = next;
            }
        }
        merged.Add(current);
        return merged;
    }

    public static List<(DateTimeOffset Start, DateTimeOffset End)> SubtractIntervals(
        DateTimeOffset start,
        DateTimeOffset end,
        List<(DateTimeOffset Start, DateTimeOffset End)> exclusions)
    {
        if (end <= start)
            return [];

        var result = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        var currentStart = start;

        foreach (var (exStart, exEnd) in exclusions)
        {
            if (exEnd <= currentStart)
                continue;

            if (exStart >= end)
                break;

            if (exStart > currentStart)
            {
                var segEnd = exStart < end ? exStart : end;
                if (segEnd > currentStart)
                {
                    result.Add((currentStart, segEnd));
                }
            }

            if (exEnd > currentStart)
            {
                currentStart = exEnd;
            }

            if (currentStart >= end)
                break;
        }

        if (currentStart < end)
        {
            result.Add((currentStart, end));
        }

        return result;
    }

    public static List<(DateTimeOffset Start, DateTimeOffset End)> IntersectIntervals(
        DateTimeOffset start,
        DateTimeOffset end,
        List<(DateTimeOffset Start, DateTimeOffset End)> inclusions)
    {
        if (end <= start || inclusions.Count == 0)
            return [];

        var result = new List<(DateTimeOffset Start, DateTimeOffset End)>();

        foreach (var (incStart, incEnd) in inclusions)
        {
            if (incEnd <= start)
                continue;
            if (incStart >= end)
                break;

            var overlapStart = incStart > start ? incStart : start;
            var overlapEnd = incEnd < end ? incEnd : end;

            if (overlapEnd > overlapStart)
            {
                result.Add((overlapStart, overlapEnd));
            }
        }

        return result;
    }

    private static List<AwTimelineInterval> BuildTimelineIntervals(
        DateOnly date,
        List<(DateTimeOffset Start, DateTimeOffset End, string Activity, string Details, string Type)> activeWindowIntervals,
        List<(DateTimeOffset Start, DateTimeOffset End, string Activity, string Details, string Type)> activeWebIntervals,
        List<(DateTimeOffset Start, DateTimeOffset End)> mergedAfk,
        AppSettings settings)
    {
        var rawIntervals = new List<(DateTimeOffset Start, DateTimeOffset End, string Activity, string Details, string Type)>();
        rawIntervals.AddRange(activeWindowIntervals);
        rawIntervals.AddRange(activeWebIntervals);

        // Significant AFK intervals (>= 2 minutes)
        foreach (var (afkStart, afkEnd) in mergedAfk)
        {
            if ((afkEnd - afkStart).TotalMinutes >= 2)
            {
                rawIntervals.Add((afkStart, afkEnd, "AFK / Idle", "Away from keyboard", "Idle"));
            }
        }

        if (rawIntervals.Count == 0)
            return [];

        // Sort chronologically
        var sorted = rawIntervals.OrderBy(i => i.Start).ThenBy(i => i.End).ToList();
        var merged = new List<(DateTimeOffset Start, DateTimeOffset End, string Activity, string Details, string Type)>();

        foreach (var item in sorted)
        {
            if (merged.Count > 0)
            {
                var prev = merged[^1];
                // Merge if same activity, same type, and gap between them <= 60s
                if (string.Equals(prev.Activity, item.Activity, StringComparison.OrdinalIgnoreCase) &&
                    prev.Type == item.Type &&
                    (item.Start - prev.End).TotalSeconds <= 60 &&
                    item.Start >= prev.Start)
                {
                    var extendedEnd = item.End > prev.End ? item.End : prev.End;
                    merged[^1] = (prev.Start, extendedEnd, prev.Activity, prev.Details, prev.Type);
                    continue;
                }
            }
            merged.Add(item);
        }

        double minSec = Math.Max(5, settings.ActivityWatchMinDurationSeconds);
        var result = new List<AwTimelineInterval>();

        foreach (var (startUtc, endUtc, activity, details, type) in merged)
        {
            var startLocal = startUtc.ToLocalTime();
            var endLocal = endUtc.ToLocalTime();
            var dur = endLocal - startLocal;

            if (dur.TotalSeconds < minSec && type != "Idle")
                continue;

            result.Add(new AwTimelineInterval(
                date,
                activity,
                details,
                TimeOnly.FromDateTime(startLocal.DateTime),
                TimeOnly.FromDateTime(endLocal.DateTime),
                dur,
                type));
        }

        return result;
    }

    private static async Task UpdateMarkdownFileAsync(string filePath, DateOnly date, string newSectionContent)
    {
        string dateHeading = $"## 📅 {date:yyyy-MM-dd}";
        string fullContent;

        if (File.Exists(filePath))
        {
            fullContent = await File.ReadAllTextAsync(filePath, Encoding.UTF8).ConfigureAwait(false);
        }
        else
        {
            fullContent = "# 📊 ActivityWatch Activity Log\n\nAutomated daily activity logs synced from ActivityWatch.\n\n";
        }

        // Check if date section already exists
        int headingIndex = fullContent.IndexOf(dateHeading, StringComparison.OrdinalIgnoreCase);
        if (headingIndex >= 0)
        {
            // Find end of this section (either next "## 📅" heading or end of file)
            int nextHeadingIndex = fullContent.IndexOf("\n## 📅 ", headingIndex + dateHeading.Length, StringComparison.OrdinalIgnoreCase);
            if (nextHeadingIndex >= 0)
            {
                fullContent = fullContent.Substring(0, headingIndex) + newSectionContent.TrimEnd() + "\n\n" + fullContent.Substring(nextHeadingIndex + 1);
            }
            else
            {
                fullContent = fullContent.Substring(0, headingIndex) + newSectionContent.TrimEnd() + "\n";
            }
        }
        else
        {
            // Append as a new date section
            fullContent = fullContent.TrimEnd() + "\n\n" + newSectionContent;
        }

        await File.WriteAllTextAsync(filePath, fullContent, Encoding.UTF8).ConfigureAwait(false);
    }

    private static string CleanAppName(string raw)
    {
        if (raw.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            raw = raw[..^4];
        return raw;
    }

    private static string ExtractDomain(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "unknown";
        try
        {
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                url = "https://" + url;
            }
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return url.Split('/')[0];
        }
    }

    private static string EscapeMarkdownPipe(string text) =>
        text.Replace("|", " - ").Replace("\r", "").Replace("\n", " ");

    private static string Truncate(string text, int maxLen)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLen)
            return text;
        return text[..(maxLen - 3)] + "...";
    }
}
