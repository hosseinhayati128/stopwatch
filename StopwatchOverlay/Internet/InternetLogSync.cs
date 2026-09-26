using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace StopwatchOverlay.Internet;

public sealed record InternetSyncResult(
    bool Success,
    string TargetFilePath,
    string? Message = null);

public static class InternetLogSync
{
    public const string DefaultFileName = "Internet Log.md";
    private static readonly object FileLock = new();

    public static string FormatRow(InternetCheckResult result)
    {
        string time = result.Timestamp.ToString("HH:mm");
        string status = result.StatusBadge;
        string netName = string.IsNullOrWhiteSpace(result.NetworkInfo.DisplayText)
            ? "Unknown"
            : result.NetworkInfo.DisplayText.Replace("|", "\\|");
        string ping = result.PingDisplay;
        string speed = result.SpeedDisplay;
        string notes = string.IsNullOrWhiteSpace(result.Notes) ? "-" : result.Notes.Replace("|", "\\|");

        return $"| {time} | {status} | {netName} | {ping} | {speed} | {notes} |";
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

        string folder = !string.IsNullOrWhiteSpace(customFolderPath)
            ? customFolderPath.Trim()
            : settings.ObsidianVaultFolder.Trim();

        if (string.IsNullOrWhiteSpace(folder))
        {
            return new InternetSyncResult(false, string.Empty, "Obsidian vault folder is not configured.");
        }

        string fileName = !string.IsNullOrWhiteSpace(customFileName)
            ? customFileName.Trim()
            : (!string.IsNullOrWhiteSpace(settings.InternetLogFileName) ? settings.InternetLogFileName.Trim() : DefaultFileName);

        string targetPath = Path.Combine(folder, fileName);

        lock (FileLock)
        {
            try
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }

                string dateHeader = $"## 📅 {checkResult.Timestamp:yyyy-MM-dd}";
                string row = FormatRow(checkResult);

                if (!File.Exists(targetPath))
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(GetHeaderBlock());
                    sb.AppendLine(dateHeader);
                    sb.AppendLine();
                    sb.Append(GetTableHeader());
                    sb.AppendLine(row);

                    File.WriteAllText(targetPath, sb.ToString(), Encoding.UTF8);
                    return new InternetSyncResult(true, targetPath, "Created file and recorded entry.");
                }

                string content = File.ReadAllText(targetPath, Encoding.UTF8);
                string updated = InsertRowIntoContent(content, dateHeader, row);

                File.WriteAllText(targetPath, updated, Encoding.UTF8);
                return new InternetSyncResult(true, targetPath, "Appended entry.");
            }
            catch (Exception ex)
            {
                return new InternetSyncResult(false, targetPath, $"Failed to log internet check: {ex.Message}");
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
