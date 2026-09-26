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
        var afkIntervals = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        double totalAfkSeconds = 0;
        if (afkBucket != null)
        {
            var afkEvents = await client.GetEventsAsync(afkBucket.Id, startUtc, endUtc, 50000, ct).ConfigureAwait(false);
            foreach (var evt in afkEvents)
            {
                if (string.Equals(evt.Status, "afk", StringComparison.OrdinalIgnoreCase))
                {
                    var start = evt.Timestamp;
                    var end = start.AddSeconds(Math.Max(0, evt.DurationSeconds));
                    afkIntervals.Add((start, end));
                    totalAfkSeconds += evt.DurationSeconds;
                }
            }
        }

        // Helper to subtract AFK periods from an active event
        double ComputeActiveSeconds(DateTimeOffset eventStart, double durationSec)
        {
            if (durationSec <= 0) return 0;
            var eventEnd = eventStart.AddSeconds(durationSec);
            double activeSec = durationSec;

            foreach (var (afkStart, afkEnd) in afkIntervals)
            {
                if (eventStart >= afkEnd || eventEnd <= afkStart)
                    continue; // No overlap

                var overlapStart = eventStart > afkStart ? eventStart : afkStart;
                var overlapEnd = eventEnd < afkEnd ? eventEnd : afkEnd;
                double overlap = (overlapEnd - overlapStart).TotalSeconds;
                if (overlap > 0)
                {
                    activeSec -= overlap;
                }
            }

            return Math.Max(0, activeSec);
        }

        // 2. Fetch and aggregate window events
        var appMap = new Dictionary<string, AwAppSummary>(StringComparer.OrdinalIgnoreCase);
        double totalActiveSeconds = 0;
        var allRawWindowEvents = new List<AwEvent>();

        if (windowBucket != null)
        {
            var windowEvents = await client.GetEventsAsync(windowBucket.Id, startUtc, endUtc, 50000, ct).ConfigureAwait(false);
            allRawWindowEvents.AddRange(windowEvents);
            foreach (var evt in windowEvents)
            {
                double effectiveSeconds = ComputeActiveSeconds(evt.Timestamp, evt.DurationSeconds);
                if (effectiveSeconds <= 0) continue;

                totalActiveSeconds += effectiveSeconds;
                string app = string.IsNullOrWhiteSpace(evt.App) ? "Unknown" : CleanAppName(evt.App);
                string title = string.IsNullOrWhiteSpace(evt.Title) ? "Untitled" : evt.Title.Trim();

                if (!appMap.TryGetValue(app, out var appSummary))
                {
                    appSummary = new AwAppSummary { AppName = app };
                    appMap[app] = appSummary;
                }

                appSummary.TotalDuration += TimeSpan.FromSeconds(effectiveSeconds);
                if (settings.ActivityWatchIncludeTitles)
                {
                    if (appSummary.Titles.TryGetValue(title, out var currentTitleDur))
                        appSummary.Titles[title] = currentTitleDur + TimeSpan.FromSeconds(effectiveSeconds);
                    else
                        appSummary.Titles[title] = TimeSpan.FromSeconds(effectiveSeconds);
                }
            }
        }

        // 3. Fetch and aggregate web events
        var webMap = new Dictionary<string, AwWebSummary>(StringComparer.OrdinalIgnoreCase);
        var allRawWebEvents = new List<AwEvent>();
        if (settings.ActivityWatchIncludeWeb)
        {
            foreach (var webBucket in webBuckets)
            {
                var webEvents = await client.GetEventsAsync(webBucket.Id, startUtc, endUtc, 50000, ct).ConfigureAwait(false);
                allRawWebEvents.AddRange(webEvents);
                foreach (var evt in webEvents)
                {
                    double effectiveSeconds = ComputeActiveSeconds(evt.Timestamp, evt.DurationSeconds);
                    if (effectiveSeconds <= 0) continue;

                    string domain = ExtractDomain(evt.Url);
                    string pageTitle = string.IsNullOrWhiteSpace(evt.Title) ? domain : evt.Title.Trim();

                    if (!webMap.TryGetValue(domain, out var webSummary))
                    {
                        webSummary = new AwWebSummary { Domain = domain };
                        webMap[domain] = webSummary;
                    }

                    webSummary.TotalDuration += TimeSpan.FromSeconds(effectiveSeconds);
                    if (webSummary.Pages.TryGetValue(pageTitle, out var currPageDur))
                        webSummary.Pages[pageTitle] = currPageDur + TimeSpan.FromSeconds(effectiveSeconds);
                    else
                        webSummary.Pages[pageTitle] = TimeSpan.FromSeconds(effectiveSeconds);
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

        var intervals = BuildTimelineIntervals(date, allRawWindowEvents, allRawWebEvents, afkIntervals, settings);

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

    private static List<AwTimelineInterval> BuildTimelineIntervals(
        DateOnly date,
        List<AwEvent> rawWindowEvents,
        List<AwEvent> rawWebEvents,
        List<(DateTimeOffset Start, DateTimeOffset End)> afkIntervals,
        AppSettings settings)
    {
        var rawIntervals = new List<(DateTimeOffset Start, DateTimeOffset End, string Activity, string Details, string Type)>();

        // 1. Window events
        foreach (var evt in rawWindowEvents)
        {
            if (evt.DurationSeconds <= 0) continue;
            var start = evt.Timestamp;
            var end = start.AddSeconds(evt.DurationSeconds);

            // Skip if fully during AFK
            bool isAfk = false;
            foreach (var (afkStart, afkEnd) in afkIntervals)
            {
                if (start >= afkStart && end <= afkEnd)
                {
                    isAfk = true;
                    break;
                }
            }
            if (isAfk) continue;

            string app = string.IsNullOrWhiteSpace(evt.App) ? "Unknown" : CleanAppName(evt.App);
            string title = string.IsNullOrWhiteSpace(evt.Title) ? "Untitled" : evt.Title.Trim();
            rawIntervals.Add((start, end, app, title, "App"));
        }

        // 2. Web events
        if (settings.ActivityWatchIncludeWeb)
        {
            foreach (var evt in rawWebEvents)
            {
                if (evt.DurationSeconds <= 0) continue;
                var start = evt.Timestamp;
                var end = start.AddSeconds(evt.DurationSeconds);

                bool isAfk = false;
                foreach (var (afkStart, afkEnd) in afkIntervals)
                {
                    if (start >= afkStart && end <= afkEnd)
                    {
                        isAfk = true;
                        break;
                    }
                }
                if (isAfk) continue;

                string domain = ExtractDomain(evt.Url);
                string pageTitle = string.IsNullOrWhiteSpace(evt.Title) ? domain : evt.Title.Trim();
                rawIntervals.Add((start, end, domain, pageTitle, "Web"));
            }
        }

        // 3. AFK intervals (significant ones >= 2 minutes)
        foreach (var (afkStart, afkEnd) in afkIntervals)
        {
            if ((afkEnd - afkStart).TotalMinutes >= 2)
            {
                rawIntervals.Add((afkStart, afkEnd, "AFK / Idle", "Away from keyboard", "Idle"));
            }
        }

        if (rawIntervals.Count == 0)
            return [];

        // Sort chronologically
        var sorted = rawIntervals.OrderBy(i => i.Start).ToList();
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
