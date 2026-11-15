using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StopwatchOverlay.Internet;

public sealed record InternetSyncResult(
    bool Success,
    string TargetFilePath,
    string? Message = null)
{
    public int? CheckCount { get; init; }
}

public sealed record InternetClearResult(bool LocalHistoryCleared, bool ReportCleared, string TargetFilePath, string? Message)
{
    public bool Success => LocalHistoryCleared && (TargetFilePath.Length == 0 || ReportCleared);
}

public static class InternetLogSync
{
    public const string DefaultFileName = "Internet Log.md";
    private static readonly object FileLock = new();

    public static bool TryNormalizeFileName(string? raw, out string fileName) =>
        ActivityWatch.ActivityWatchSync.TryNormalizeExportFileName(string.IsNullOrWhiteSpace(raw) ? DefaultFileName : raw, out fileName);

    public static bool TryGetTargetPath(AppSettings settings, out string targetPath, out string error) =>
        TryResolveTargetPath(settings.ObsidianVaultFolder, settings.InternetLogFileName, out targetPath, out error);

    private static bool TryResolveTargetPath(string? folder, string? rawName, out string targetPath, out string error)
    {
        targetPath = ""; error = "";
        if (!TryNormalizeFileName(rawName, out string name)) { error = "Use a file name without folders or reserved characters."; return false; }
        if (string.IsNullOrWhiteSpace(folder)) { error = "Obsidian vault folder is not configured."; return false; }
        try
        {
            string root = Path.GetFullPath(folder.Trim());
            string path = Path.GetFullPath(Path.Combine(root, name));
            if (!string.Equals(Path.GetDirectoryName(path), Path.TrimEndingDirectorySeparator(root),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            { error = "The report must stay inside the selected folder."; return false; }
            targetPath = path; return true;
        }
        catch (Exception ex) { error = ex.Message; return false; }
    }

    public static InternetClearResult ClearHistory(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        lock (FileLock)
        {
            if (!TryNormalizeFileName(settings.InternetLogFileName, out _))
                return new(false, false, "", "Use a file name without folders or reserved characters.");
            bool hasFolder = !string.IsNullOrWhiteSpace(settings.ObsidianVaultFolder);
            string target = "";
            if (hasFolder)
            {
                if (!TryGetTargetPath(settings, out target, out string error)) return new(false, false, target, error);
                try { WriteDocument(target, BuildMarkdownDocument(Array.Empty<InternetCheckResult>())); }
                catch (Exception ex)
                {
                    CrashLogger.LogRecoverable(ex, "InternetLogSync.ClearReport");
                    return new(false, false, target, "The report could not be emptied. Local history was kept. " + ex.Message);
                }
            }
            if (!InternetHistoryStore.TryClear(out string? localError))
                return new(false, hasFolder, target, (hasFolder ? "The report was emptied, but local history could not be cleared. " : "Local history could not be cleared. ") + localError);
            return new(true, hasFolder, target, hasFolder ? "Local checks removed and the selected report emptied." : "Local checks removed. Existing Markdown copies were not changed.");
        }
    }

    private static void WriteDocument(string target, string content)
    {
        string temporary = target + $".tmp.{Guid.NewGuid():N}";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.WriteAllText(temporary, content, new UTF8Encoding(false));
            File.Move(temporary, target, overwrite: true);
        }
        finally { try { if (File.Exists(temporary)) File.Delete(temporary); } catch { } }
    }

    public static string FormatRow(InternetCheckResult result, string? customNote = null)
    {
        string time = result.Timestamp.ToString("HH:mm");
        string status = result.StatusBadge;
        string netName = string.IsNullOrWhiteSpace(result.NetworkInfo.DisplayText)
            ? "Unknown"
            : result.NetworkInfo.DisplayText.Replace("|", "\\|");
        string ping = result.PingDisplay;
        string speed = result.SpeedDisplay;

        string finalNote = !string.IsNullOrWhiteSpace(customNote)
            ? customNote
            : (!string.IsNullOrWhiteSpace(result.Notes) ? result.Notes : "-");

        finalNote = finalNote.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");

        return $"| {time} | {status} | {netName} | {ping} | {speed} | {finalNote} |";
    }

    public static string GetHeaderBlock()
    {
        return "# 🌐 Internet Connection Log\n\n" +
               "Automated network connection and speed monitoring logged periodically by Stopwatch Overlay.\n";
    }

    public static string GetTableHeader()
    {
        return "| Time | Status | Network / Wi-Fi | Ping | Speed | Notes |\n" +
               "| :--- | :--- | :--- | :--- | :--- | :--- |\n";
    }

    public static InternetSyncResult AppendCheck(
        InternetCheckResult checkResult,
        AppSettings settings,
        string? customFolderPath = null,
        string? customFileName = null)
    {
        ArgumentNullException.ThrowIfNull(checkResult);
        ArgumentNullException.ThrowIfNull(settings);

        lock (FileLock)
        {
            // 1. Immediately store in persistent JSON database
            InternetHistoryStore.Add(checkResult);

            // 2. Synchronize to Markdown atomically
            return SyncLogUnderLock(settings, customFolderPath, customFileName);
        }
    }

    public static InternetSyncResult SyncLog(
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

    private static InternetSyncResult SyncLogUnderLock(
        AppSettings settings,
        string? customFolderPath = null,
        string? customFileName = null)
    {
        string folder = !string.IsNullOrWhiteSpace(customFolderPath)
            ? customFolderPath.Trim()
            : settings.ObsidianVaultFolder.Trim();

        string? rawName = !string.IsNullOrWhiteSpace(customFileName) ? customFileName : settings.InternetLogFileName;
        if (!TryResolveTargetPath(folder, rawName, out string targetPath, out string error))
            return new(false, "", error);

        try
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }

            var existingNotes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // If the file already exists on disk, absorb any entries not yet in the JSON store
            // (e.g. user restored previous days from Obsidian File Recovery or pasted them in)
            if (File.Exists(targetPath))
            {
                try
                {
                    string existingText = File.ReadAllText(targetPath, Encoding.UTF8);
                    InternetHistoryStore.ImportFromMarkdown(existingText);
                    ParseExistingNotes(existingText, existingNotes);
                }
                catch (Exception ex)
                {
                    CrashLogger.LogRecoverable(ex, "InternetLogSync.ImportExisting");
                    return new(false, targetPath, "The existing report could not be read; it was not overwritten. " + ex.Message);
                }
            }

            // Apply retention policy if enabled
            if (settings.InternetLogRetentionDays > 0)
            {
                InternetHistoryStore.PurgeOlderThanDays(settings.InternetLogRetentionDays);
            }

            var allRecords = InternetHistoryStore.GetAll();

            string markdown = BuildMarkdownDocument(allRecords, existingNotes);

            WriteDocument(targetPath, markdown);
            return new InternetSyncResult(true, targetPath, $"Successfully synced {allRecords.Count} checks to {Path.GetFileName(targetPath)}.") { CheckCount = allRecords.Count };
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "InternetLogSync.SyncLogUnderLock");
            return new InternetSyncResult(false, targetPath, $"Failed to write markdown file: {ex.Message}");
        }
    }

    public static string BuildMarkdownDocument(
        IReadOnlyList<InternetCheckResult> records,
        Dictionary<string, string>? existingNotes = null)
    {
        var sb = new StringBuilder();
        sb.AppendLine(GetHeaderBlock());

        if (records == null || records.Count == 0)
        {
            return sb.ToString();
        }

        var groups = records
            .OrderBy(r => r.Timestamp)
            .GroupBy(r => r.Timestamp.ToString("yyyy-MM-dd"))
            .ToList();

        foreach (var group in groups)
        {
            string dateStr = group.Key;
            sb.AppendLine($"## 📅 {dateStr}");
            sb.AppendLine();
            sb.Append(GetTableHeader());

            foreach (var check in group)
            {
                string noteKey = $"{dateStr} {check.Timestamp:HH:mm}";
                string? customNote = null;
                if (existingNotes != null && existingNotes.TryGetValue(noteKey, out var foundNote) && !string.IsNullOrWhiteSpace(foundNote))
                {
                    customNote = foundNote;
                }

                sb.AppendLine(FormatRow(check, customNote));
            }

            sb.AppendLine();
        }

        return sb.ToString().TrimEnd() + Environment.NewLine;
    }

    public static void ParseExistingNotes(string markdownContent, Dictionary<string, string> notesMap)
    {
        if (string.IsNullOrWhiteSpace(markdownContent))
            return;

        var lines = markdownContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        string? currentDateStr = null;
        var dateRegex = new Regex(@"^##\s+📅\s+(\d{4}-\d{2}-\d{2})", RegexOptions.Compiled);

        foreach (var rawLine in lines)
        {
            string line = rawLine.Trim();
            var dateMatch = dateRegex.Match(line);
            if (dateMatch.Success)
            {
                currentDateStr = dateMatch.Groups[1].Value;
                continue;
            }

            if (string.IsNullOrEmpty(currentDateStr))
                continue;

            if (!line.StartsWith('|') || !line.EndsWith('|') || line.Contains("---") ||
                (line.Contains("Time", StringComparison.OrdinalIgnoreCase) && line.Contains("Status", StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            string[] parts = line.Split('|');
            if (parts.Length >= 7)
            {
                string time = parts[1].Trim();
                string note = parts[6].Trim();
                if (!string.IsNullOrWhiteSpace(note) && note != "-")
                {
                    string key = $"{currentDateStr} {time}";
                    notesMap[key] = note;
                }
            }
        }
    }

    public static string InsertRowIntoContent(string content, string dateHeader, string row)
    {
        int dateIdx = content.IndexOf(dateHeader, StringComparison.OrdinalIgnoreCase);

        if (dateIdx < 0)
        {
            // Date header doesn't exist, append at the end
            var sb = new StringBuilder(content.TrimEnd());
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine(dateHeader);
            sb.AppendLine();
            sb.Append(GetTableHeader());
            sb.AppendLine(row);
            return sb.ToString();
        }

        // Find the next section header (e.g. ## ) or end of content
        int nextSectionIdx = content.IndexOf("\n## ", dateIdx + dateHeader.Length, StringComparison.OrdinalIgnoreCase);
        if (nextSectionIdx < 0)
        {
            nextSectionIdx = content.Length;
        }

        string sectionText = content.Substring(dateIdx, nextSectionIdx - dateIdx);

        // Check if table header already exists in this section
        if (sectionText.Contains("| Time | Status |", StringComparison.OrdinalIgnoreCase))
        {
            // Append row at the end of the section
            string before = content.Substring(0, nextSectionIdx).TrimEnd();
            string after = content.Substring(nextSectionIdx);
            return before + "\n" + row + "\n" + after;
        }
        else
        {
            // Section exists but no table, append table header + row
            string before = content.Substring(0, nextSectionIdx).TrimEnd();
            string after = content.Substring(nextSectionIdx);
            return before + "\n\n" + GetTableHeader() + row + "\n" + after;
        }
    }
}
