using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StopwatchOverlay;

public static partial class TelegramNotesSync
{
    private static readonly SemaphoreSlim CompletionGate = new(1, 1);

    public static string NormalizeTodoText(string text) => text.Replace("\r\n", " ").Replace("\n", " ").Trim();
    private static AppSettings SnapshotTelegramSettings(AppSettings settings) => new() {
        TelegramEnabled = settings.TelegramEnabled, TelegramBotToken = settings.TelegramBotToken,
        TelegramChatId = settings.TelegramChatId, TelegramTodosTopicId = settings.TelegramTodosTopicId,
        TelegramNotesTopicId = settings.TelegramNotesTopicId, TelegramRemindersTopicId = settings.TelegramRemindersTopicId,
        ObsidianVaultFolder = settings.ObsidianVaultFolder, NotesSubfolder = settings.NotesSubfolder
    };

    public static string GetBotIdentity(AppSettings settings) => (settings.TelegramBotToken ?? "").Split(':')[0].Trim();
    public static string GetTodoVaultKey(AppSettings settings) => string.IsNullOrWhiteSpace(settings.ObsidianVaultFolder) ? "" :
        Path.GetFullPath(Path.Combine(settings.ObsidianVaultFolder, string.IsNullOrWhiteSpace(settings.NotesSubfolder) ? "Notes" : settings.NotesSubfolder.Trim()))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    public static bool MatchesTodo(TelegramSentNoteRecord record, NoteEntry entry) =>
        record.NoteType == NoteType.Todo && entry.Type == NoteType.Todo && NormalizeTodoText(record.Text) == NormalizeTodoText(entry.Text) &&
        record.TimestampMinute == entry.Timestamp.ToString("yyyy-MM-dd HH:mm");

    public static bool HasTodoMessageLink(AppSettings settings, NoteEntry entry) =>
        TelegramOutboxStore.GetTrackedTodos(GetBotIdentity(settings), GetTodoVaultKey(settings)).Count(record => MatchesTodo(record, entry)) == 1;

    public static TelegramSyncResult LinkTodoMessage(AppSettings settings, NoteEntry entry, string reference)
    {
        if (!settings.TelegramEnabled || string.IsNullOrWhiteSpace(settings.TelegramBotToken))
            return new(false, "Enable Telegram and configure your bot in Settings first.");
        var actual = ObsidianNotesSync.LoadAllNotes(settings.ObsidianVaultFolder, settings.NotesSubfolder).Where(item =>
            item.Type == NoteType.Todo && item.Text.Trim() == entry.Text.Trim() && item.Timestamp == entry.Timestamp).ToArray();
        if (actual.Length != 1) return new(false, "This todo changed or is duplicated. Refresh and try again.");
        string chatId = ResolveDestination(settings, NoteType.Todo).targetChatId;
        string input = reference.Trim();
        int messageId;
        if (!int.TryParse(input, out messageId))
        {
            if (!Uri.TryCreate(input, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
                (uri.Host != "t.me" && uri.Host != "telegram.me") || uri.UserInfo.Length != 0 || uri.Port != 443)
                return new(false, "Paste a Telegram message link or a positive message ID.");
            string[] parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3 && parts[0] == "c" && long.TryParse(parts[1], out long privateId) && privateId > 0)
                chatId = "-100" + privateId;
            else if (parts.Length == 2 && parts[0].All(c => char.IsAsciiLetterOrDigit(c) || c == '_') && !string.IsNullOrWhiteSpace(parts[0]))
                chatId = "@" + parts[0];
            else return new(false, "That is not a Telegram message link.");
            if (!int.TryParse(parts[^1], out messageId)) return new(false, "The link must end with its message ID.");
        }
        if (messageId <= 0 || string.IsNullOrWhiteSpace(chatId)) return new(false, "Choose a Telegram destination in Settings, then enter a positive message ID.");
        bool saved = TelegramOutboxStore.BindTodoMessage(actual[0], GetBotIdentity(settings), GetTodoVaultKey(settings), chatId, messageId);
        return saved ? new(true) : new(false, "This message is already linked to another task, or its link could not be saved.");
    }

    /// <summary>Use Markdown as the completion source; edit linked bot messages without sending replacement messages.</summary>
    public static async Task<TelegramSyncResult> SyncTodoCompletionsAsync(AppSettings settings, HttpClient? httpClient = null)
    {
        if (!settings.TelegramEnabled || string.IsNullOrWhiteSpace(settings.TelegramBotToken) || string.IsNullOrWhiteSpace(settings.ObsidianVaultFolder)) return new(true);
        settings = SnapshotTelegramSettings(settings);
        if (!await CompletionGate.WaitAsync(0).ConfigureAwait(false)) return new(true);
        try
        {
            string bot = GetBotIdentity(settings), vault = GetTodoVaultKey(settings);
            if (!Directory.Exists(settings.ObsidianVaultFolder)) return new(false, "The notes folder is unavailable. Telegram updates will retry.");
            var entries = ObsidianNotesSync.LoadAllNotes(settings.ObsidianVaultFolder, settings.NotesSubfolder).Where(e => e.Type == NoteType.Todo).ToArray();
            bool QueueStates()
            {
                foreach (var record in TelegramOutboxStore.GetTrackedTodos(bot, vault))
                {
                    var matching = entries.Where(e => MatchesTodo(record, e)).ToArray();
                    if (matching.Length == 1 && !TelegramOutboxStore.QueueTodoState(record, matching[0].IsCompleted)) return false;
                }
                return true;
            }
            if (!QueueStates()) return new(false, "Could not save pending Telegram updates. Check disk permissions.");
            string? lastError = null;
            // Existing outbox entries were already requested by the user; retry their delivery as before.
            if (TelegramOutboxStore.PendingCount > 0)
            {
                var flush = await FlushOutboxAsync(settings, httpClient, scopedOnly: true).ConfigureAwait(false);
                if (flush.LastError != null && flush.LastError != "A flush is already in progress.")
                    lastError = "Some queued Telegram messages could not be delivered. Pending updates will retry.";
                if (!QueueStates()) return new(false, "Could not save pending Telegram updates. Check disk permissions.");
            }
            foreach (var record in TelegramOutboxStore.GetTrackedTodos(bot, vault).Where(r => r.PendingCompleted.HasValue))
            {
                // Re-read before each request so a quick check/uncheck uses the newest file state.
                var matching = ObsidianNotesSync.LoadAllNotes(settings.ObsidianVaultFolder, settings.NotesSubfolder)
                    .Where(e => MatchesTodo(record, e)).ToArray();
                if (matching.Length != 1) continue;
                bool desired = matching[0].IsCompleted;
                if (!TelegramOutboxStore.QueueTodoState(record, desired)) return new(false, "Could not save pending Telegram updates.");
                var result = await EditTodoMessageAsync(settings.TelegramBotToken, record, matching[0], httpClient ?? SharedClient).ConfigureAwait(false);
                if (!result.Success) { lastError = result.ErrorMessage; continue; }
                if (!TelegramOutboxStore.AcknowledgeTodoState(record, desired)) lastError = "Telegram updated, but its sync status could not be saved. It will retry safely.";
            }
            return new(lastError == null, lastError);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(new IOException("Todo Telegram sync failed: " + ex.GetType().Name), "TelegramNotesSync.SyncTodoCompletionsAsync");
            return new(false, "Telegram sync could not finish. It will retry automatically.");
        }
        finally { CompletionGate.Release(); }
    }

    private static async Task<TelegramSyncResult> EditTodoMessageAsync(string botToken, TelegramSentNoteRecord record, NoteEntry entry, HttpClient client)
    {
        try
        {
            string safe = FormattedTodoText.ToTelegramHtml(entry.Text.Trim());
            string text = $"☑️ <b>Todo</b> ({entry.Timestamp:HH:mm})\n" + (entry.IsCompleted ? $"<s>{safe}</s>" : safe);
            var payload = new Dictionary<string, object?> { ["chat_id"] = record.ChatId, ["message_id"] = record.MessageId, ["text"] = text, ["parse_mode"] = "HTML" };
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync($"https://api.telegram.org/bot{botToken.Trim()}/editMessageText", content).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            bool ok = doc.RootElement.TryGetProperty("ok", out var value) && value.GetBoolean();
            string description = doc.RootElement.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";
            if (ok || description.Contains("message is not modified", StringComparison.OrdinalIgnoreCase)) return new(true, null, record.MessageId, record.ChatId);
            return new(false, "Telegram could not update a todo. It will retry; if the message was deleted, link it again.");
        }
        catch (Exception)
        {
            return new(false, "Telegram is unavailable. The completion change is saved and will retry.");
        }
    }
}
