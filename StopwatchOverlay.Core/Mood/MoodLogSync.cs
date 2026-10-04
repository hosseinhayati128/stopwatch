using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StopwatchOverlay.Mood;

public sealed record MoodSyncResult(
    bool Success,
    string TargetFilePath,
    string? Message = null);

/// <summary>
/// Handles non-destructive synchronization of periodic mood ratings and keywords
/// into a user's Obsidian vault as a structured Markdown log.
/// </summary>
public static class MoodLogSync
{
    public const string DefaultFileName = "Mood Log.md";
    private static readonly object FileLock = new();

    public static string FormatRow(MoodEntry entry, string? customNote = null)
    {
        string date = entry.TimestampLocal.ToString("yyyy-MM-dd");
        string time = entry.TimestampLocal.ToString("HH:mm");
        string period = $"{entry.PeriodStartLocal:HH:mm} – {entry.PeriodEndLocal:HH:mm}";
        string mood = entry.MoodBadge;
        string score = entry.ScoreDisplay;

        string keywords = entry.Keywords != null && entry.Keywords.Count > 0
            ? string.Join(", ", entry.Keywords.Select(k => k.Trim())).Replace("|", "\\|")
            : "-";

        string projects = entry.AssociatedProjects != null && entry.AssociatedProjects.Count > 0
            ? string.Join(", ", entry.AssociatedProjects.Select(p => p.Trim())).Replace("|", "\\|")
            : "-";

        string finalNote = !string.IsNullOrWhiteSpace(customNote)
            ? customNote
            : (!string.IsNullOrWhiteSpace(entry.Note) ? entry.Note : "");

        finalNote = finalNote.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

        return $"| {date} | {time} | {period} | {mood} | {score} | {keywords} | {projects} | {finalNote} |";
    }

    public static string GetHeaderBlock()
    {
        string tz = TimeZoneInfo.Local.DisplayName;
        return "---\n" +
               "type: mood-log\n" +
               $"last_updated: {DateTime.Now:yyyy-MM-ddTHH:mm:sszzz}\n" +
               $"timezone: \"{tz}\"\n" +
               "tags:\n" +
               "  - mood-tracking\n" +
               "  - feelings\n" +
               "  - periodic-review\n" +
               "---\n\n" +
               "# 😊 Mood & Feelings Log\n\n" +
               "> [!NOTE]\n" +
               "> Recorded periodically during activity reviews. Custom notes are preserved.\n";
    }

    public static string GetTableHeader()
    {
        return "| Date | Time | Period | Mood | Score | Keywords | Projects | Notes |\n" +
               "| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :--- |\n";
    }

    public static MoodSyncResult AppendMood(
        MoodEntry entry,
        AppSettings settings,
        string? customFolderPath = null,
        string? customFileName = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(settings);

        lock (FileLock)
        {
            // 1. Save to local JSON store
            MoodHistoryStore.Add(entry);

            // 2. Synchronize to Markdown file
            return SyncLogUnderLock(settings, customFolderPath, customFileName);
        }
    }

    public static MoodSyncResult SyncLog(
        AppSettings settings,
        string? customFolderPath = null,
        string? customFileName = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        lock (FileLock)
        {
            return SyncLogUnderLock(settings, customFolderPath, customFileName);
        }
    }

    private static MoodSyncResult SyncLogUnderLock(
        AppSettings settings,
        string? customFolderPath = null,
        string? customFileName = null)
    {
        string folder = !string.IsNullOrWhiteSpace(customFolderPath)
            ? customFolderPath.Trim()
            : settings.ObsidianVaultFolder.Trim();

        if (string.IsNullOrWhiteSpace(folder))
        {
            return new MoodSyncResult(false, string.Empty, "Obsidian vault folder is not configured.");
        }

        string fileName = !string.IsNullOrWhiteSpace(customFileName)
            ? customFileName.Trim()
            : (!string.IsNullOrWhiteSpace(settings.MoodLogFileName) ? settings.MoodLogFileName.Trim() : DefaultFileName);

        if (!fileName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
            fileName += ".md";

        string targetPath = Path.Combine(folder, fileName);

        try
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var existingNotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            if (File.Exists(targetPath))
            {
                try
                {
                    string existingText = File.ReadAllText(targetPath, Encoding.UTF8);
                    ParseExistingNotes(existingText, existingNotes);
                }
                catch (Exception ex)
                {
                    CrashLogger.LogRecoverable(ex, "MoodLogSync.ParseNotes");
                }
            }

            var allRecords = MoodHistoryStore.GetAll()
                .Where(r => !r.IsSkipped)
                .OrderBy(r => r.TimestampUtc)
                .ToList();

            string markdown = BuildMarkdownDocument(allRecords, existingNotes);

            // Safe atomic write
            string tempFile = targetPath + $".tmp.{Guid.NewGuid():N}";
            File.WriteAllText(tempFile, markdown, new UTF8Encoding(false));
            File.Move(tempFile, targetPath, overwrite: true);

            return new MoodSyncResult(true, targetPath, $"Successfully synced {allRecords.Count} mood entries to {Path.GetFileName(targetPath)}.");
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "MoodLogSync.SyncLogUnderLock");
            return new MoodSyncResult(false, targetPath, $"Failed to write mood markdown: {ex.Message}");
        }
    }

    public static string BuildMarkdownDocument(
        IReadOnlyList<MoodEntry> records,
        Dictionary<string, string>? existingNotes = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(GetHeaderBlock());

        if (records == null || records.Count == 0)
        {
            return sb.ToString();
        }

        var groups = records
            .OrderBy(r => r.TimestampUtc)
            .GroupBy(r => r.TimestampLocal.ToString("yyyy-MM-dd"))
            .ToList();

        foreach (var group in groups)
        {
            string dateStr = group.Key;
            sb.AppendLine($"## 📅 {dateStr}");
            sb.AppendLine();
            sb.Append(GetTableHeader());

            foreach (var item in group)
            {
                string noteKey = $"{dateStr}_{item.TimestampLocal:HH:mm}";
                string? customNote = null;
                if (existingNotes != null && existingNotes.TryGetValue(noteKey, out var note))
                {
                    customNote = note;
                }

                sb.AppendLine(FormatRow(item, customNote));
            }

            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static void ParseExistingNotes(string markdown, Dictionary<string, string> existingNotes)
    {
        if (string.IsNullOrWhiteSpace(markdown)) return;

        using var reader = new StringReader(markdown);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            line = line.Trim();
            if (!line.StartsWith('|') || !line.EndsWith('|'))
                continue;

            var parts = line.Split('|');
            // Expecting at least: empty, date, time, period, mood, score, keywords, projects, note, empty (10 parts)
            if (parts.Length < 10)
                continue;

            string date = parts[1].Trim();
            string time = parts[2].Trim();
            string note = parts[8].Trim();

            // Ignore header row and separator line
            if (date.Equals("Date", StringComparison.OrdinalIgnoreCase) || date.StartsWith(':') || date.StartsWith('-'))
                continue;

            if (!string.IsNullOrWhiteSpace(note) && note != "-")
            {
                existingNotes[$"{date}_{time}"] = note.Replace("\\|", "|");
            }
        }
    }
}

