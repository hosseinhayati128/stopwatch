using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StopwatchOverlay;

public static partial class TelegramNotesSync
{
    /// <summary>Call only after user confirmation. Stop on remote refusal; never silently delete just one side.</summary>
    public static async Task<TelegramSyncResult> DeleteTodoEverywhereAsync(AppSettings settings, NoteEntry entry, HttpClient? httpClient = null)
    {
        settings = SnapshotTelegramSettings(settings);
        await CompletionGate.WaitAsync().ConfigureAwait(false);
        try
        {
            await FlushGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var valid = ObsidianNotesSync.ValidateTodoForDeletion(settings.ObsidianVaultFolder, entry, settings.NotesSubfolder);
                if (!valid.Success) return new(false, valid.Message);
                string bot = GetBotIdentity(settings), vault = GetTodoVaultKey(settings);
                var pending = TelegramOutboxStore.GetAll().Where(e => e.NoteType == NoteType.Todo && e.BotIdentity == bot && e.VaultKey == vault &&
                    MatchesTodo(new TelegramSentNoteRecord(NoteType.Todo, e.Text, e.Timestamp.ToString("yyyy-MM-dd HH:mm"), DateTime.UtcNow, VaultKey: vault), entry)).ToArray();
                // Collect all possible targets before modifying a user-selected link.
                var links = TelegramOutboxStore.GetTrackedTodos(bot, vault, includeDeleted: true).Where(r => MatchesTodo(r, entry)).ToList();
                foreach (var queued in pending)
                {
                    TelegramSyncResult? delivered = queued.DeliveredMessageId.HasValue
                        ? new(true, null, queued.DeliveredMessageId, queued.DeliveredChatId)
                        : DeliveredThisSession.TryGetValue(queued.Id, out var remembered) ? remembered : null;
                    if (delivered?.MessageId is int messageId)
                    {
                        string? chatId = delivered.ChatId ?? queued.ChatId;
                        if (string.IsNullOrWhiteSpace(chatId)) return new(false, "A delivered message has no saved destination. Link it explicitly before deleting.");
                        links.Add(new TelegramSentNoteRecord(NoteType.Todo, NormalizeTodoText(entry.Text), entry.Timestamp.ToString("yyyy-MM-dd HH:mm"),
                            DateTime.UtcNow, messageId, chatId, bot, vault));
                    }
                }
                var liveTodos = ObsidianNotesSync.LoadAllNotes(settings.ObsidianVaultFolder, settings.NotesSubfolder).Where(item => item.Type == NoteType.Todo).ToArray();
                if (links.Any(record => liveTodos.Count(item => MatchesTodo(record, item)) != 1))
                    return new(false, "This Telegram link matches more than one todo. Resolve the duplicate tasks before deleting.");
                var targets = links.GroupBy(r => (r.ChatId, r.MessageId)).ToArray();
                if (targets.Length > 1) return new(false, "Conflicting Telegram message receipts were found. Resolve the linked messages before deleting.");
                var link = targets.SingleOrDefault()?.First();
                if (link != null && !TelegramOutboxStore.GetTrackedTodos(bot, vault, includeDeleted: true)
                        .Any(r => MatchesTodo(r, entry) && r.ChatId == link.ChatId && r.MessageId == link.MessageId))
                {
                    if (!TelegramOutboxStore.MarkSent(NoteType.Todo, entry.Text, entry.Timestamp, link.MessageId, link.ChatId, bot, vault))
                        return new(false, "Could not save the Telegram message receipt. The todo was kept; try again.");
                }
                if (link != null && !link.MessageDeleted && (!settings.TelegramEnabled || string.IsNullOrWhiteSpace(settings.TelegramBotToken)))
                    return new(false, "Enable Telegram in Settings to delete the linked message. The todo was kept.");
                if (!TelegramOutboxStore.CancelPendingTodo(entry, bot, vault))
                    return new(false, "Could not cancel the queued Telegram copy. The todo was kept; try again.");
                foreach (var queued in pending) { DeliveredThisSession.TryRemove(queued.Id, out _); DeliveredContentThisSession.TryRemove(queued.Id, out _); }
                if (link != null && !link.MessageDeleted)
                {
                    var result = await DeleteLinkedTodoMessageAsync(settings.TelegramBotToken, link, httpClient ?? SharedClient).ConfigureAwait(false);
                    if (!result.Success) return result;
                }
                if (!TelegramOutboxStore.RecordTodoDeleted(entry, bot, vault, link))
                    return new(false, "Telegram deletion finished, but its status could not be saved. The local todo was kept; try again.");
                // Revalidate after HTTP: Obsidian may have changed while Telegram was responding.
                var removed = ObsidianNotesSync.DeleteTodo(settings.ObsidianVaultFolder, entry, settings.NotesSubfolder);
                if (!removed.Success) return new(false, (link != null ? "The Telegram message is deleted. " : "") + removed.Message + " Review the todo and try Delete again.");
                if (!TelegramOutboxStore.CancelPendingTodo(entry, bot, vault))
                    return new(false, "The todo was deleted, but its cancelled queue entry could not be cleaned up. It will not be sent again.");
                return new(true);
            }
            finally { FlushGate.Release(); }
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(new IOException("Todo deletion failed: " + ex.GetType().Name), "TelegramNotesSync.DeleteTodoEverywhereAsync");
            return new(false, "Deletion could not finish. Refresh and try Delete again.");
        }
        finally { CompletionGate.Release(); }
    }

    private static async Task<TelegramSyncResult> DeleteLinkedTodoMessageAsync(string token, TelegramSentNoteRecord record, HttpClient client)
    {
        try
        {
            var payload = new Dictionary<string, object?> { ["chat_id"] = record.ChatId, ["message_id"] = record.MessageId };
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            using var response = await client.PostAsync($"https://api.telegram.org/bot{token.Trim()}/deleteMessage", content).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
            bool ok = doc.RootElement.TryGetProperty("ok", out var value) && value.GetBoolean();
            string description = doc.RootElement.TryGetProperty("description", out var desc) ? desc.GetString() ?? "" : "";
            if ((ok && doc.RootElement.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.True) ||
                description.Contains("message to delete not found", StringComparison.OrdinalIgnoreCase)) return new(true);
            return new(false, "Telegram could not delete the message, so the todo was kept. Messages older than 48 hours or restricted messages may need to be deleted in Telegram first; then try Delete again.");
        }
        catch (Exception)
        {
            return new(false, "Telegram is unavailable, so the todo was kept. Reconnect and try Delete again.");
        }
    }
}
