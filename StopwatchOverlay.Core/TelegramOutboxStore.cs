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
    string? LastError = null,
    string? BotIdentity = null,
    string? VaultKey = null,
    string? ChatId = null,
    long? MessageThreadId = null,
    int? DeliveredMessageId = null,
    string? DeliveredChatId = null,
    string? DeliveredContentHash = null);

public sealed record TelegramSentNoteRecord(
    NoteType NoteType,
    string Text,
    string TimestampMinute,
    DateTime SentAtUtc,
    int? MessageId = null,
    string? ChatId = null,
    string? BotIdentity = null,
    string? VaultKey = null,
    bool? IsCompleted = false,
    bool? PendingCompleted = null,
    bool MessageDeleted = false,
    string? ContentHash = null);

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

    public static TelegramOutboxEntry Enqueue(NoteType noteType, string text, DateTime timestamp,
        string? botIdentity = null, string? vaultKey = null, string? chatId = null, long? messageThreadId = null)
    {
        lock (Gate)
        {
            text = noteType == NoteType.Todo ? TelegramNotesSync.NormalizeTodoText(text) : text.Trim();
            var list = LoadInternal();
            string minute = timestamp.ToString("yyyy-MM-dd HH:mm");

            // Avoid duplicate identical note at identical timestamp
            var existing = list.FirstOrDefault(e =>
                e.NoteType == noteType && e.BotIdentity == botIdentity && e.VaultKey == vaultKey && e.ChatId == chatId && e.MessageThreadId == messageThreadId &&
                string.Equals(e.Text.Trim(), text.Trim(), StringComparison.Ordinal) &&
                (e.Timestamp.ToString("yyyy-MM-dd HH:mm") == minute ||
                 Math.Abs((e.Timestamp - timestamp).TotalSeconds) < 90));

            if (existing != null)
            {
                return existing;
            }

            var entry = new TelegramOutboxEntry(Guid.NewGuid(), noteType, text.Trim(), timestamp,
                BotIdentity: botIdentity, VaultKey: vaultKey, ChatId: chatId, MessageThreadId: messageThreadId);
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

    public static bool Update(TelegramOutboxEntry updatedEntry)
    {
        lock (Gate)
        {
            var list = LoadInternal();
            int index = list.FindIndex(e => e.Id == updatedEntry.Id);
            if (index >= 0)
            {
                list[index] = updatedEntry;
                bool saved = SaveInternal(list);
                OutboxChanged?.Invoke();
                return saved;
            }
            return false;
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

    public static bool IsTodoSent(string botIdentity, string vaultKey, NoteEntry entry)
    {
        lock (Gate) return LoadSentInternal().Any(record =>
            ((record.BotIdentity == botIdentity && record.VaultKey == vaultKey) || (record.BotIdentity == null && record.VaultKey == null)) &&
            TelegramNotesSync.MatchesTodo(record with { VaultKey = record.VaultKey ?? vaultKey }, entry));
    }

    public static bool MarkSent(NoteType noteType, string text, DateTime timestamp,
        int? messageId = null, string? chatId = null, string? botIdentity = null, string? vaultKey = null, bool isCompleted = false, string? contentHash = null)
    {
        lock (Gate)
        {
            var sent = LoadSentInternal();
            text = noteType == NoteType.Todo ? TelegramNotesSync.NormalizeTodoText(text) : text.Trim();
            string minute = timestamp.ToString("yyyy-MM-dd HH:mm");
            int index = sent.FindIndex(s => s.NoteType == noteType && s.Text == text.Trim() &&
                s.TimestampMinute == minute && s.BotIdentity == botIdentity && s.VaultKey == vaultKey);
            if (index >= 0 && !messageId.HasValue) return true;
            var record = new TelegramSentNoteRecord(noteType, text.Trim(), minute, DateTime.UtcNow,
                messageId, chatId, botIdentity, vaultKey, isCompleted, ContentHash: contentHash);
            if (index >= 0) sent[index] = record; else sent.Add(record);
            // Todo message links must survive until their task is no longer needed.
            sent.RemoveAll(s => s.SentAtUtc < DateTime.UtcNow.AddDays(-30) && !(s.NoteType == NoteType.Todo && s.MessageId.HasValue));
            return SaveSentInternal(sent);
        }
    }

    public static IReadOnlyList<TelegramSentNoteRecord> GetTrackedTodos(string botIdentity, string vaultKey, bool includeDeleted = false)
    {
        lock (Gate) return LoadSentInternal().Where(s => s.NoteType == NoteType.Todo &&
            s.BotIdentity == botIdentity && s.VaultKey == vaultKey && (includeDeleted || !s.MessageDeleted) && s.MessageId > 0 && !string.IsNullOrWhiteSpace(s.ChatId)).ToList();
    }

    public static bool IsDeletedTodo(string botIdentity, string vaultKey, string text, DateTime timestamp)
    {
        lock (Gate) return LoadSentInternal().Any(s => s.MessageDeleted && s.NoteType == NoteType.Todo &&
            s.BotIdentity == botIdentity && s.VaultKey == vaultKey && TelegramNotesSync.MatchesTodo(s, new(NoteType.Todo, timestamp, text)));
    }

    public static bool RecordTodoDeleted(NoteEntry entry, string botIdentity, string vaultKey, TelegramSentNoteRecord? record = null)
    {
        lock (Gate)
        {
            var sent = LoadSentInternal();
            if (record != null)
            {
                int index = sent.FindIndex(s => SameBinding(s, record));
                if (index < 0) return false;
                sent[index] = sent[index] with { MessageDeleted = true, PendingCompleted = null };
            }
            else if (!sent.Any(s => s.MessageDeleted && s.NoteType == NoteType.Todo && s.BotIdentity == botIdentity &&
                s.VaultKey == vaultKey && TelegramNotesSync.MatchesTodo(s, entry)))
                sent.Add(new TelegramSentNoteRecord(NoteType.Todo, TelegramNotesSync.NormalizeTodoText(entry.Text),
                    entry.Timestamp.ToString("yyyy-MM-dd HH:mm"), DateTime.UtcNow, BotIdentity: botIdentity, VaultKey: vaultKey, MessageDeleted: true));
            return SaveSentInternal(sent);
        }
    }

    public static bool CancelPendingTodo(NoteEntry entry, string botIdentity, string vaultKey)
    {
        lock (Gate)
        {
            var pending = LoadInternal();
            int removed = pending.RemoveAll(e => e.NoteType == NoteType.Todo && e.BotIdentity == botIdentity && e.VaultKey == vaultKey &&
                TelegramNotesSync.MatchesTodo(new TelegramSentNoteRecord(NoteType.Todo, e.Text, e.Timestamp.ToString("yyyy-MM-dd HH:mm"), DateTime.UtcNow, VaultKey: vaultKey), entry));
            return removed == 0 || SaveInternal(pending);
        }
    }

    public static bool BindTodoMessage(NoteEntry entry, string botIdentity, string vaultKey, string chatId, int messageId)
    {
        lock (Gate)
        {
            var sent = LoadSentInternal();
            string minute = entry.Timestamp.ToString("yyyy-MM-dd HH:mm");
            if (sent.Any(s => s.BotIdentity == botIdentity && s.ChatId == chatId && s.MessageId == messageId &&
                (s.VaultKey != vaultKey || !TelegramNotesSync.MatchesTodo(s, entry)))) return false;
            sent.RemoveAll(s => s.BotIdentity == botIdentity && s.VaultKey == vaultKey && TelegramNotesSync.MatchesTodo(s, entry));
            sent.Add(new TelegramSentNoteRecord(NoteType.Todo, entry.Text.Trim(), minute, DateTime.UtcNow,
                messageId, chatId, botIdentity, vaultKey, null, entry.IsCompleted));
            return SaveSentInternal(sent);
        }
    }

    public static bool QueueTodoState(TelegramSentNoteRecord record, bool completed, string? contentHash = null)
    {
        lock (Gate)
        {
            var sent = LoadSentInternal();
            int index = sent.FindIndex(s => SameBinding(s, record));
            if (index < 0) return false;
            var latest = sent[index];
            if ((contentHash == null || (latest.ContentHash ?? TelegramNotesSync.TodoContentHash(latest.Text, null)) == contentHash) &&
                (latest.PendingCompleted == completed || (latest.PendingCompleted == null && latest.IsCompleted == completed))) return true;
            sent[index] = latest with { PendingCompleted = completed };
            return SaveSentInternal(sent);
        }
    }

    public static bool AcknowledgeTodoState(TelegramSentNoteRecord record, bool completed, string? contentHash = null)
    {
        lock (Gate)
        {
            var sent = LoadSentInternal();
            int index = sent.FindIndex(s => SameBinding(s, record));
            if (index < 0) return false;
            var latest = sent[index];
            sent[index] = latest with { IsCompleted = completed, ContentHash = contentHash ?? latest.ContentHash, PendingCompleted = latest.PendingCompleted == completed ? null : latest.PendingCompleted };
            return SaveSentInternal(sent);
        }
    }

    private static bool SameBinding(TelegramSentNoteRecord a, TelegramSentNoteRecord b) =>
        a.NoteType == b.NoteType && a.Text == b.Text && a.TimestampMinute == b.TimestampMinute &&
        a.BotIdentity == b.BotIdentity && a.VaultKey == b.VaultKey && a.ChatId == b.ChatId && a.MessageId == b.MessageId;

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

    private static bool SaveInternal(List<TelegramOutboxEntry> entries)
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
            return true;
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "TelegramOutboxStore.SaveInternal");
            return false;
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

    private static bool SaveSentInternal(List<TelegramSentNoteRecord> records)
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
            return true;
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "TelegramOutboxStore.SaveSentInternal");
            return false;
        }
    }
}
