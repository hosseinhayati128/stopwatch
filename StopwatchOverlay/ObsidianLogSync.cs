using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace StopwatchOverlay;

public sealed record ObsidianSyncResult(
    bool Success,
    int RecordCount,
    string TargetFilePath,
    string? Message = null);

/// <summary>
/// Handles non-destructive synchronization of project time intervals
/// into a user's Obsidian vault or folder as a structured Markdown table.
/// </summary>
public static class ObsidianLogSync
{
    public const string DefaultFileName = "Stopwatch Log.md";

    public static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            duration = TimeSpan.Zero;

        int totalMinutes = (int)Math.Round(duration.TotalMinutes);
        if (totalMinutes <= 0)
            return "< 1m";

        int hours = totalMinutes / 60;
        int minutes = totalMinutes % 60;

        if (hours == 0)
            return $"{minutes}m";
        if (minutes == 0)
            return $"{hours}h";

        return $"{hours}h {minutes}m";
    }

    public static ObsidianSyncResult SyncHistory(
        ProjectHistoryView history,
        AppSettings settings,
        string? customFolderPath = null,
        string? customFileName = null)
    {
        ArgumentNullException.ThrowIfNull(history);
        ArgumentNullException.ThrowIfNull(settings);

        string folder = !string.IsNullOrWhiteSpace(customFolderPath)
            ? customFolderPath.Trim()
            : settings.ObsidianVaultFolder.Trim();

        if (string.IsNullOrWhiteSpace(folder))
        {
            return new ObsidianSyncResult(
                false,
                0,
                "",
                "No Obsidian vault or target folder has been selected.");
        }

        try
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
        }
        catch (Exception ex)
        {
            return new ObsidianSyncResult(
                false,
                0,
                folder,
                $"Could not create or access directory: {ex.Message}");
        }

        string fileName = !string.IsNullOrWhiteSpace(customFileName)
            ? customFileName.Trim()
            : (!string.IsNullOrWhiteSpace(settings.ObsidianExportFileName)
                ? settings.ObsidianExportFileName.Trim()
                : DefaultFileName);

        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            fileName += ".md";

        string targetFilePath = Path.Combine(folder, fileName);

        try
        {
            var closedIntervals = history.Intervals
                .Where(i => !i.IsOpen)
                .OrderBy(i => i.StartUtc)
                .ThenBy(i => i.Id)
                .ToList();

            var existingNotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            string? preservedFooter = null;

            if (File.Exists(targetFilePath))
            {
                string existingText = File.ReadAllText(targetFilePath, Encoding.UTF8);
                ParseExistingFile(existingText, existingNotes, out preservedFooter);
            }

            string markdownContent = BuildMarkdownDocument(
                closedIntervals,
                existingNotes,
                preservedFooter);

            string tempFile = targetFilePath + $".tmp.{Guid.NewGuid():N}";
            File.WriteAllText(tempFile, markdownContent, new UTF8Encoding(false));
            File.Move(tempFile, targetFilePath, overwrite: true);

            return new ObsidianSyncResult(
                true,
                closedIntervals.Count,
                targetFilePath,
                $"Successfully saved {closedIntervals.Count} session{(closedIntervals.Count == 1 ? "" : "s")} to {Path.GetFileName(targetFilePath)}.");
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "ObsidianLogSync");
            return new ObsidianSyncResult(
                false,
                0,
                targetFilePath,
                $"Failed to write markdown file: {ex.Message}");
        }
    }

    public static bool TryAutoSync(ProjectHistoryView history, AppSettings settings)
    {
        if (history == null || settings == null)
            return false;

        if (!settings.ObsidianAutoSyncEnabled || string.IsNullOrWhiteSpace(settings.ObsidianVaultFolder))
            return false;

        try
        {
            var result = SyncHistory(history, settings);
            return result.Success;
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "ObsidianAutoSync");
            return false;
        }
    }

    private static void ParseExistingFile(
        string existingText,
        Dictionary<string, string> existingNotes,
        out string? footer)
    {
        footer = null;
        if (string.IsNullOrWhiteSpace(existingText))
            return;

        var lines = existingText.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        int lastTableLineIndex = -1;

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i].Trim();
            if (line.StartsWith('|') && line.EndsWith('|'))
            {
                lastTableLineIndex = i;
                if (!line.Contains("---") && !line.Contains("Duration (min)", StringComparison.OrdinalIgnoreCase))
                {
                    string[] parts = line.Split('|');
                    // Expected format: | Date | Project | Start | End | Duration (min) | Duration | Notes |
                    if (parts.Length >= 8)
                    {
                        string date = parts[1].Trim();
                        string project = parts[2].Trim();
                        string start = parts[3].Trim();
                        string end = parts[4].Trim();
                        string note = parts[7].Trim();

                        if (!string.IsNullOrWhiteSpace(note))
                        {
                            string key = BuildRecordKey(date, project, start, end);
                            existingNotes[key] = note;
                        }
                    }
                }
            }
        }

        if (lastTableLineIndex >= 0 && lastTableLineIndex < lines.Length - 1)
        {
            var footerLines = lines.Skip(lastTableLineIndex + 1).ToArray();
            string remaining = string.Join(Environment.NewLine, footerLines).Trim();
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                footer = remaining;
            }
        }
    }

    private static string BuildRecordKey(string date, string project, string start, string end)
        => $"{date}|{project}|{start}|{end}";

    private static string BuildMarkdownDocument(
        IReadOnlyList<ProjectWorkIntervalView> intervals,
        Dictionary<string, string> existingNotes,
        string? preservedFooter)
    {
        var sb = new StringBuilder();
        DateTime nowLocal = DateTime.Now;
        string timezone = TimeZoneInfo.Local.DisplayName;

        sb.AppendLine("---");
        sb.AppendLine("type: time-tracking-log");
        sb.AppendLine($"last_updated: {nowLocal:yyyy-MM-ddTHH:mm:sszzz}");
        sb.AppendLine($"timezone: \"{timezone}\"");
        sb.AppendLine("tags:");
        sb.AppendLine("  - time-tracking");
        sb.AppendLine("  - stopwatch");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine("# Time Tracking Log");
        sb.AppendLine();
        sb.AppendLine("> [!NOTE]");
        sb.AppendLine("> Auto-generated and maintained by StopwatchOverlay. Custom text in the Notes column and sections below the table are preserved.");
        sb.AppendLine();
        sb.AppendLine("| Date | Project | Start | End | Duration (min) | Duration | Notes |");
        sb.AppendLine("| :--- | :--- | :--- | :--- | :--- | :--- | :--- |");

        foreach (var interval in intervals)
        {
            if (!interval.EndUtc.HasValue)
                continue;

            DateTime startLocal = interval.StartUtc.ToLocalTime();
            DateTime endLocal = interval.EndUtc.Value.ToLocalTime();
            TimeSpan duration = interval.EndUtc.Value - interval.StartUtc;

            string dateStr = startLocal.ToString("yyyy-MM-dd");
            string startStr = startLocal.ToString("HH:mm");
            string endStr = endLocal.ToString("HH:mm");
            int durationMin = (int)Math.Max(1, Math.Round(duration.TotalMinutes));
            string durationFormatted = FormatDuration(duration);

            string key = BuildRecordKey(dateStr, interval.ProjectName, startStr, endStr);
            string note = existingNotes.TryGetValue(key, out string? existingNote) ? existingNote : "";

            sb.AppendLine($"| {dateStr} | {EscapeMarkdown(interval.ProjectName)} | {startStr} | {endStr} | {durationMin} | {durationFormatted} | {EscapeMarkdown(note)} |");
        }

        if (!string.IsNullOrWhiteSpace(preservedFooter))
        {
            sb.AppendLine();
            sb.AppendLine(preservedFooter);
        }
        else
        {
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeMarkdown(string text)
    {
        if (string.IsNullOrEmpty(text))
            return "";

        return text.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
    }
}
