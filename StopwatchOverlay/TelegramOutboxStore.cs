using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace StopwatchOverlay;

public sealed record TelegramOutboxEntry(
    Guid Id,
    NoteType NoteType,
    string Text,
    DateTime Timestamp,
    int RetryCount = 0,
    DateTime? LastAttemptUtc = null,
    string? LastError = null);

public sealed record TelegramSentNoteRecord(
    NoteType NoteType,
    string Text,
    string TimestampMinute,
    DateTime SentAtUtc);

public static class TelegramOutboxStore
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string? _customFilePath;

    public static event Action? OutboxChanged;

    public static string DefaultFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StopwatchOverlay",
        "telegram-outbox.json");

    public static string DefaultSentFilePath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "StopwatchOverlay",
        "telegram-sent-notes.json");

    public static string FilePath => _customFilePath ?? DefaultFilePath;

    public static string SentFilePath => _customFilePath != null
        ? _customFilePath + ".sent.json"
        : DefaultSentFilePath;

    public static void SetCustomFilePath(string? path)
    {
        lock (Gate)
        {
            _customFilePath = path;
        }
    }

    public static int PendingCount
    {
        get
        {
            lock (Gate)
            {
                return LoadInternal().Count;
            }
        }
    }

    public static IReadOnlyList<TelegramOutboxEntry> GetAll()
    {
        lock (Gate)
        {
            return LoadInternal();
        }
    }

    public static TelegramOutboxEntry Enqueue(NoteType noteType, string text, DateTime timestamp)
    {
        lock (Gate)
        {
            var list = LoadInternal();
            string minute = timestamp.ToString("yyyy-MM-dd HH:mm");

            // Avoid duplicate identical note at identical timestamp
            var existing = list.FirstOrDefault(e =>
                e.NoteType == noteType &&
                string.Equals(e.Text.Trim(), text.Trim(), StringComparison.Ordinal) &&
                (e.Timestamp.ToString("yyyy-MM-dd HH:mm") == minute ||
                 Math.Abs((e.Timestamp - timestamp).TotalSeconds) < 90));

            if (existing != null)
            {
                return existing;
            }

            var entry = new TelegramOutboxEntry(Guid.NewGuid(), noteType, text.Trim(), timestamp);
            list.Add(entry);
            SaveInternal(list);
            OutboxChanged?.Invoke();
            return entry;
        }
    }

    public static bool Remove(Guid id)
    {
        lock (Gate)
        {
            var list = LoadInternal();
            int removed = list.RemoveAll(e => e.Id == id);
            if (removed > 0)
            {
                SaveInternal(list);
                OutboxChanged?.Invoke();
                return true;
            }
            return false;
        }
    }

    public static void Update(TelegramOutboxEntry updatedEntry)
    {
        lock (Gate)
        {
            var list = LoadInternal();
            int index = list.FindIndex(e => e.Id == updatedEntry.Id);
            if (index >= 0)
            {
                list[index] = updatedEntry;
                SaveInternal(list);
                OutboxChanged?.Invoke();
            }
        }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            SaveInternal(new List<TelegramOutboxEntry>());
            OutboxChanged?.Invoke();
        }
    }

    public static bool IsSent(NoteType noteType, string text, DateTime timestamp)
    {
        lock (Gate)
        {
            var sent = LoadSentInternal();
            string minute = timestamp.ToString("yyyy-MM-dd HH:mm");
            return sent.Any(s =>
                s.NoteType == noteType &&
                string.Equals(s.Text.Trim(), text.Trim(), StringComparison.Ordinal) &&
                (s.TimestampMinute == minute ||
                 (DateTime.TryParse(s.TimestampMinute, out var dt) && Math.Abs((dt - timestamp).TotalSeconds) < 90)));
        }
    }

    public static void MarkSent(NoteType noteType, string text, DateTime timestamp)
    {
        lock (Gate)
        {
            var sent = LoadSentInternal();
            string minute = timestamp.ToString("yyyy-MM-dd HH:mm");
            bool exists = sent.Any(s =>
                s.NoteType == noteType &&
                string.Equals(s.Text.Trim(), text.Trim(), StringComparison.Ordinal) &&
                s.TimestampMinute == minute);

            if (!exists)
            {
                sent.Add(new TelegramSentNoteRecord(noteType, text.Trim(), minute, DateTime.UtcNow));
                // Prune records older than 30 days
                var cutoffUtc = DateTime.UtcNow.AddDays(-30);
                sent.RemoveAll(s => s.SentAtUtc < cutoffUtc);
                SaveSentInternal(sent);
            }
        }
    }

    public static void ClearAll()
    {
        lock (Gate)
        {
            Clear();
            SaveSentInternal(new List<TelegramSentNoteRecord>());
        }
    }

    private static List<TelegramOutboxEntry> LoadInternal()
    {
        string path = FilePath;
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }
            return JsonSerializer.Deserialize<List<TelegramOutboxEntry>>(json) ?? [];
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "TelegramOutboxStore.LoadInternal");
            return [];
        }
    }

    private static void SaveInternal(List<TelegramOutboxEntry> entries)
    {
        string path = FilePath;
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(entries, JsonOptions);
            string tmpPath = path + $".tmp.{Guid.NewGuid():N}";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, path, overwrite: true);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "TelegramOutboxStore.SaveInternal");
        }
    }

    private static List<TelegramSentNoteRecord> LoadSentInternal()
    {
        string path = SentFilePath;
        if (!File.Exists(path))
        {
            return [];
        }

        try
        {
            string json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return [];
            }
            return JsonSerializer.Deserialize<List<TelegramSentNoteRecord>>(json) ?? [];
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "TelegramOutboxStore.LoadSentInternal");
            return [];
        }
    }

    private static void SaveSentInternal(List<TelegramSentNoteRecord> records)
    {
        string path = SentFilePath;
        try
        {
            string? dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string json = JsonSerializer.Serialize(records, JsonOptions);
            string tmpPath = path + $".tmp.{Guid.NewGuid():N}";
            File.WriteAllText(tmpPath, json);
            File.Move(tmpPath, path, overwrite: true);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "TelegramOutboxStore.SaveSentInternal");
        }
    }
}
