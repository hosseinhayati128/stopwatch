using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace StopwatchOverlay.Internet;

/// <summary>
/// Thread-safe local JSON persistence store for internet speed and connectivity check results.
/// Prevents data loss if Markdown files are deleted, renamed, or modified externally.
/// </summary>
public static class InternetHistoryStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string? _customFilePath;
    private static List<InternetCheckResult>? _cachedRecords;

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StopwatchOverlay",
        "internet-history.json");

    public static string DefaultBackupPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StopwatchOverlay",
        "internet-history.json.bak");

    public static string FilePath => _customFilePath ?? DefaultFilePath;

    public static string BackupPath => _customFilePath != null
        ? _customFilePath + ".bak"
        : DefaultBackupPath;

    public static void SetCustomFilePath(string? path)
    {
        lock (Gate)
        {
            _customFilePath = path;
            _cachedRecords = null;
        }
    }

    public static IReadOnlyList<InternetCheckResult> GetAll()
    {
        lock (Gate)
        {
            EnsureLoadedUnderLock();
            return _cachedRecords!.ToList().AsReadOnly();
        }
    }

    public static void Add(InternetCheckResult check)
    {
        ArgumentNullException.ThrowIfNull(check);

        lock (Gate)
        {
            EnsureLoadedUnderLock();
            UpsertRecordUnderLock(check);
            SaveUnderLock();
        }
    }

    public static void AddRange(IEnumerable<InternetCheckResult> checks)
    {
        ArgumentNullException.ThrowIfNull(checks);

        lock (Gate)
        {
            EnsureLoadedUnderLock();
            bool changed = false;
            foreach (var check in checks)
            {
                if (check != null && UpsertRecordUnderLock(check))
                {
                    changed = true;
                }
            }

            if (changed)
            {
                SaveUnderLock();
            }
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            _cachedRecords = new List<InternetCheckResult>();
            SaveUnderLock();
        }
    }

    public static int PurgeOlderThanDays(int days)
    {
        if (days <= 0) return 0;

        lock (Gate)
        {
            EnsureLoadedUnderLock();
            DateTime cutoff = DateTime.Now.Date.AddDays(-days);
            int initialCount = _cachedRecords!.Count;
            _cachedRecords.RemoveAll(r => r.Timestamp < cutoff);
            int removed = initialCount - _cachedRecords.Count;

            if (removed > 0)
            {
                SaveUnderLock();
            }

            return removed;
        }
    }

    /// <summary>
    /// Imports existing records from an Obsidian Internet Log.md file content.
    /// Non-destructively absorbs all parsed checks without overwriting more accurate records.
    /// </summary>
    public static int ImportFromMarkdown(string markdownContent)
    {
        if (string.IsNullOrWhiteSpace(markdownContent))
            return 0;

        var parsed = ParseMarkdownContent(markdownContent);
        if (parsed.Count == 0)
            return 0;

        lock (Gate)
        {
            EnsureLoadedUnderLock();
            int imported = 0;
            foreach (var check in parsed)
            {
                if (UpsertRecordUnderLock(check))
                {
                    imported++;
                }
            }

            if (imported > 0)
            {
                SaveUnderLock();
            }

            return imported;
        }
    }

    public static List<InternetCheckResult> ParseMarkdownContent(string markdownContent)
    {
        var results = new List<InternetCheckResult>();
        if (string.IsNullOrWhiteSpace(markdownContent))
            return results;

        var lines = markdownContent.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
        string? currentDateStr = null;
        var dateRegex = new Regex(@"^##\s+📅\s+(\d{4}-\d{2}-\d{2})", RegexOptions.Compiled);
        var signalRegex = new Regex(@"\((\d+)%\)", RegexOptions.Compiled);

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

            if (!line.StartsWith('|') || !line.EndsWith('|'))
                continue;

            // Skip table header and separator rows
            if (line.Contains("---") || line.Contains("Time", StringComparison.OrdinalIgnoreCase) && line.Contains("Status", StringComparison.OrdinalIgnoreCase))
                continue;

            string[] parts = line.Split('|');
            // Format: | Time | Status | Network / Wi-Fi | Ping | Speed | Notes |
            // parts[0] is empty (before first |)
            if (parts.Length < 7)
                continue;

            string timeStr = parts[1].Trim();
            string statusStr = parts[2].Trim();
            string networkStr = parts[3].Trim();
            string pingStr = parts[4].Trim();
            string speedStr = parts[5].Trim();
            string notesStr = parts[6].Trim();

            if (!DateTime.TryParseExact($"{currentDateStr} {timeStr}", "yyyy-MM-dd HH:mm",
                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime timestamp))
            {
                continue;
            }

            InternetStatus status = InternetStatus.Slow;
            if (statusStr.Contains("Online", StringComparison.OrdinalIgnoreCase) || statusStr.Contains("🟢"))
            {
                status = InternetStatus.Online;
            }
            else if (statusStr.Contains("Offline", StringComparison.OrdinalIgnoreCase) || statusStr.Contains("🔴"))
            {
                status = InternetStatus.Offline;
            }

            long? pingMs = null;
            string pingDigits = Regex.Replace(pingStr, @"[^\d]", "");
            if (long.TryParse(pingDigits, out long parsedPing))
            {
                pingMs = parsedPing;
            }

            double? speedMbps = null;
            string speedClean = speedStr.Replace("Mbps", "", StringComparison.OrdinalIgnoreCase).Trim();
            if (double.TryParse(speedClean, NumberStyles.Any, CultureInfo.InvariantCulture, out double parsedSpeed))
            {
                speedMbps = parsedSpeed;
            }

            int? signal = null;
            var sigMatch = signalRegex.Match(networkStr);
            if (sigMatch.Success && int.TryParse(sigMatch.Groups[1].Value, out int sigVal))
            {
                signal = sigVal;
            }

            bool isConnected = status != InternetStatus.Offline &&
                               !networkStr.Contains("Disconnected", StringComparison.OrdinalIgnoreCase) &&
                               !networkStr.Contains("❌");

            string connectionType = networkStr.Contains("📶") ? "Wi-Fi"
                : (networkStr.Contains("🔌") ? "Ethernet" : (isConnected ? "Network" : "Offline"));

            string cleanNetName = networkStr
                .Replace("📶", "")
                .Replace("🔌", "")
                .Replace("❌", "");
            if (sigMatch.Success)
            {
                cleanNetName = cleanNetName.Replace(sigMatch.Value, "");
            }
            cleanNetName = cleanNetName.Trim();

            var netInfo = new NetworkConnectionInfo(
                IsConnected: isConnected,
                ConnectionType: connectionType,
                NetworkName: cleanNetName,
                SignalPercent: signal,
                AdapterDescription: string.Empty,
                DisplayText: networkStr);

            string notes = (notesStr == "-" || string.IsNullOrWhiteSpace(notesStr)) ? string.Empty : notesStr;

            results.Add(new InternetCheckResult(
                Timestamp: timestamp,
                Status: status,
                PingMs: pingMs,
                DownloadMbps: speedMbps,
                NetworkInfo: netInfo,
                Notes: notes));
        }

        return results;
    }

    private static bool UpsertRecordUnderLock(InternetCheckResult check)
    {
        string minuteKey = check.Timestamp.ToString("yyyy-MM-dd HH:mm");
        int existingIndex = _cachedRecords!.FindIndex(r => r.Timestamp.ToString("yyyy-MM-dd HH:mm") == minuteKey);

        if (existingIndex >= 0)
        {
            var existing = _cachedRecords[existingIndex];
            // If existing has valid speed and new doesn't, keep existing
            if (existing.DownloadMbps.HasValue && !check.DownloadMbps.HasValue)
            {
                return false;
            }

            _cachedRecords[existingIndex] = check;
            return true;
        }

        _cachedRecords.Add(check);
        _cachedRecords.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
        return true;
    }

    private static void EnsureLoadedUnderLock()
    {
        if (_cachedRecords != null)
            return;

        string target = FilePath;
        string backup = BackupPath;

        if (File.Exists(target))
        {
            try
            {
                string json = File.ReadAllText(target);
                _cachedRecords = JsonSerializer.Deserialize<List<InternetCheckResult>>(json, JsonOptions) ?? new List<InternetCheckResult>();
                _cachedRecords.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                return;
            }
            catch (Exception ex)
            {
                CrashLogger.LogRecoverable(ex, "InternetHistoryStore.LoadPrimary");
            }
        }

        if (File.Exists(backup))
        {
            try
            {
                string json = File.ReadAllText(backup);
                _cachedRecords = JsonSerializer.Deserialize<List<InternetCheckResult>>(json, JsonOptions) ?? new List<InternetCheckResult>();
                _cachedRecords.Sort((a, b) => a.Timestamp.CompareTo(b.Timestamp));
                return;
            }
            catch (Exception ex)
            {
                CrashLogger.LogRecoverable(ex, "InternetHistoryStore.LoadBackup");
            }
        }

        _cachedRecords = new List<InternetCheckResult>();
    }

    private static void SaveUnderLock()
    {
        try
        {
            string target = FilePath;
            string dir = Path.GetDirectoryName(target) ?? "";
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string tmp = target + $".tmp.{Guid.NewGuid():N}";
            string json = JsonSerializer.Serialize(_cachedRecords, JsonOptions);
            File.WriteAllText(tmp, json);

            string backup = BackupPath;
            if (File.Exists(target))
            {
                try
                {
                    File.Copy(target, backup, overwrite: true);
                }
                catch
                {
                    // Non-critical if backup copy fails
                }
            }

            File.Move(tmp, target, overwrite: true);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "InternetHistoryStore.Save");
        }
    }
}
