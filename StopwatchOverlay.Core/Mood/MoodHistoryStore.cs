using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StopwatchOverlay.Mood;

/// <summary>
/// Thread-safe local JSON persistence store for periodic review mood and feelings entries.
/// Saves atomically with a .bak backup file to prevent data loss.
/// </summary>
public static class MoodHistoryStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static string? _customFilePath;
    private static List<MoodEntry>? _cachedRecords;

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StopwatchOverlay",
        "mood-history.json");

    public static string DefaultBackupPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StopwatchOverlay",
        "mood-history.json.bak");

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

    public static IReadOnlyList<MoodEntry> GetAll()
    {
        lock (Gate)
        {
            EnsureLoadedUnderLock();
            return _cachedRecords!.ToList().AsReadOnly();
        }
    }

    public static void Add(MoodEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        lock (Gate)
        {
            EnsureLoadedUnderLock();

            // Prevent exact duplicates
            _cachedRecords!.RemoveAll(r => r.Id == entry.Id);
            _cachedRecords.Add(entry);

            SaveUnderLock();
        }
    }

    public static void SaveAll(IEnumerable<MoodEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        lock (Gate)
        {
            _cachedRecords = entries.ToList();
            SaveUnderLock();
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            _cachedRecords = [];
            SaveUnderLock();
        }
    }

    private static void EnsureLoadedUnderLock()
    {
        if (_cachedRecords != null)
            return;

        _cachedRecords = [];

        string target = FilePath;
        string backup = BackupPath;

        if (File.Exists(target))
        {
            try
            {
                string json = File.ReadAllText(target);
                var loaded = JsonSerializer.Deserialize<List<MoodEntry>>(json, JsonOptions);
                if (loaded != null)
                {
                    _cachedRecords = loaded;
                    return;
                }
            }
            catch (Exception ex)
            {
                CrashLogger.LogRecoverable(ex, "MoodHistoryStore.LoadPrimary");
            }
        }

        // Attempt recovery from backup if primary was corrupt or missing
        if (File.Exists(backup))
        {
            try
            {
                string json = File.ReadAllText(backup);
                var loaded = JsonSerializer.Deserialize<List<MoodEntry>>(json, JsonOptions);
                if (loaded != null)
                {
                    _cachedRecords = loaded;
                    SaveUnderLock();
                    return;
                }
            }
            catch (Exception ex)
            {
                CrashLogger.LogRecoverable(ex, "MoodHistoryStore.LoadBackup");
            }
        }
    }

    private static void SaveUnderLock()
    {
        string target = FilePath;
        string backup = BackupPath;

        try
        {
            string? dir = Path.GetDirectoryName(target);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(_cachedRecords ?? [], JsonOptions);

            // Safe atomic write pattern:
            string tempFile = target + $".tmp.{Guid.NewGuid():N}";
            File.WriteAllText(tempFile, json);

            if (File.Exists(target))
            {
                try
                {
                    File.Copy(target, backup, overwrite: true);
                }
                catch (Exception ex)
                {
                    CrashLogger.LogRecoverable(ex, "MoodHistoryStore.CreateBackup");
                }
            }

            File.Move(tempFile, target, overwrite: true);

            // Ensure backup exists even if this was the first write
            if (!File.Exists(backup))
            {
                try
                {
                    File.Copy(target, backup, overwrite: true);
                }
                catch (Exception ex)
                {
                    CrashLogger.LogRecoverable(ex, "MoodHistoryStore.CreateInitialBackup");
                }
            }
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "MoodHistoryStore.SaveUnderLock");
        }
    }
}
