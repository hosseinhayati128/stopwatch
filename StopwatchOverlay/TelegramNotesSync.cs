using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace StopwatchOverlay;

public sealed record TelegramSyncResult(
    bool Success,
    string? ErrorMessage = null,
    int? MessageId = null);

public sealed record TelegramTestResult(
    bool Success,
    string Message,
    string? BotUsername = null);

public static class TelegramNotesSync
{
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public static (string targetChatId, long? messageThreadId) ResolveDestination(
        AppSettings settings,
        NoteType type)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        string? topicInput = type switch
        {
            NoteType.Todo => settings.TelegramTodosTopicId,
            NoteType.Reminder => settings.TelegramRemindersTopicId,
            NoteType.Note => settings.TelegramNotesTopicId,
            _ => null
        };

        return ResolveDestination(settings.TelegramChatId, topicInput);
    }

    public static (string targetChatId, long? messageThreadId) ResolveDestination(
        string defaultChatId,
        string? topicInput)
    {
        string baseChatId = (defaultChatId ?? "").Trim();
        string input = (topicInput ?? "").Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            return (baseChatId, null);
        }

        // Support explicit "chatId:topicId" format
        int colonIdx = input.IndexOf(':');
        if (colonIdx > 0 && colonIdx < input.Length - 1)
        {
            string explicitChat = input.Substring(0, colonIdx).Trim();
            string explicitTopic = input.Substring(colonIdx + 1).Trim();
            if (long.TryParse(explicitTopic, out long parsedThread))
            {
                return (explicitChat, parsedThread);
            }
            return (explicitChat, null);
        }

        // If input starts with '-' or '@', treat as an alternate Chat ID instead of a thread ID
        if (input.StartsWith("-", StringComparison.Ordinal) || input.StartsWith("@", StringComparison.Ordinal))
        {
            return (input, null);
        }

        // If numeric thread ID, use with default chat
        if (long.TryParse(input, out long threadId))
        {
            return (baseChatId, threadId);
        }

        return (baseChatId, null);
    }

    public static string FormatMessage(NoteType type, string text, DateTime time)
    {
        string safeText = WebUtility.HtmlEncode(text.Trim());
        string timeStr = time.ToString("HH:mm");

        return type switch
        {
            NoteType.Todo => $"☑️ <b>Todo</b> ({timeStr})\n{safeText}",
            NoteType.Reminder => $"⏰ <b>Reminder</b> ({timeStr})\n{safeText}",
            NoteType.Note => $"📝 <b>Quick Note</b> ({timeStr})\n{safeText}",
            _ => $"📌 <b>Note</b> ({timeStr})\n{safeText}"
        };
    }

    public static async Task<TelegramSyncResult> SendNoteAsync(
        AppSettings settings,
        NoteType type,
        string text,
        DateTime? timestamp = null,
        HttpClient? httpClient = null)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        if (string.IsNullOrWhiteSpace(settings.TelegramBotToken))
        {
            return new TelegramSyncResult(false, "Telegram Bot Token is not configured.");
        }

        if (string.IsNullOrWhiteSpace(settings.TelegramChatId))
        {
            return new TelegramSyncResult(false, "Telegram Chat ID is not configured.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new TelegramSyncResult(false, "Note text cannot be empty.");
        }

        var (targetChatId, threadId) = ResolveDestination(settings, type);
        if (string.IsNullOrWhiteSpace(targetChatId))
        {
            return new TelegramSyncResult(false, "Destination Chat ID is empty.");
        }

        DateTime time = timestamp ?? DateTime.Now;
        string messageText = FormatMessage(type, text, time);

        return await SendTelegramMessageAsync(
            settings.TelegramBotToken,
            targetChatId,
            messageText,
            threadId,
            httpClient ?? SharedClient);
    }

    public static async Task<TelegramSyncResult> SendTelegramMessageAsync(
        string botToken,
        string chatId,
        string text,
        long? messageThreadId = null,
        HttpClient? httpClient = null)
    {
        try
        {
            var client = httpClient ?? SharedClient;
            string url = $"https://api.telegram.org/bot{botToken.Trim()}/sendMessage";

            var payload = new Dictionary<string, object>
            {
                ["chat_id"] = chatId.Trim(),
                ["text"] = text,
                ["parse_mode"] = "HTML"
            };

            if (messageThreadId.HasValue && messageThreadId.Value > 0)
            {
                payload["message_thread_id"] = messageThreadId.Value;
            }

            string json = JsonSerializer.Serialize(payload);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await client.PostAsync(url, content);
            string responseBody = await response.Content.ReadAsStringAsync();

            using var doc = JsonDocument.Parse(responseBody);
            var root = doc.RootElement;
            bool ok = root.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();

            if (ok)
            {
                int? messageId = null;
                if (root.TryGetProperty("result", out var resultProp) &&
                    resultProp.TryGetProperty("message_id", out var idProp) &&
                    idProp.TryGetInt32(out int id))
                {
                    messageId = id;
                }
                return new TelegramSyncResult(true, null, messageId);
            }
            else
            {
                string desc = root.TryGetProperty("description", out var descProp)
                    ? descProp.GetString() ?? "Unknown error from Telegram API."
                    : "Unknown error from Telegram API.";
                return new TelegramSyncResult(false, desc);
            }
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "TelegramNotesSync.SendTelegramMessageAsync");
            return new TelegramSyncResult(false, ex.Message);
        }
    }

    public static async Task<TelegramTestResult> TestConnectionAsync(
        AppSettings settings,
        HttpClient? httpClient = null)
    {
        if (settings == null) throw new ArgumentNullException(nameof(settings));

        if (string.IsNullOrWhiteSpace(settings.TelegramBotToken))
        {
            return new TelegramTestResult(false, "Bot Token cannot be empty.");
        }

        var client = httpClient ?? SharedClient;
        string token = settings.TelegramBotToken.Trim();

        // Step 1: Call getMe
        string getMeUrl = $"https://api.telegram.org/bot{token}/getMe";
        string botUsername = "Bot";
        try
        {
            using var getMeResponse = await client.GetAsync(getMeUrl);
            string getMeBody = await getMeResponse.Content.ReadAsStringAsync();
            using var getMeDoc = JsonDocument.Parse(getMeBody);
            var getMeRoot = getMeDoc.RootElement;

            bool getMeOk = getMeRoot.TryGetProperty("ok", out var okProp) && okProp.GetBoolean();
            if (!getMeOk)
            {
                string desc = getMeRoot.TryGetProperty("description", out var descProp)
                    ? descProp.GetString() ?? "Unauthorized"
                    : "Unauthorized";
                return new TelegramTestResult(false, $"Token invalid: {desc}");
            }

            if (getMeRoot.TryGetProperty("result", out var resProp) &&
                resProp.TryGetProperty("username", out var userProp))
            {
                botUsername = userProp.GetString() ?? "Bot";
            }
        }
        catch (Exception ex)
        {
            return new TelegramTestResult(false, $"Failed to connect to Telegram API: {ex.Message}");
        }

        // Step 2: Check Chat ID and send test message
        if (string.IsNullOrWhiteSpace(settings.TelegramChatId))
        {
            return new TelegramTestResult(true, $"Bot @{botUsername} verified successfully! (Enter a Chat ID to test sending messages)", botUsername);
        }

        var (targetChatId, threadId) = ResolveDestination(settings, NoteType.Note);
        string testMessage = $"🔔 <b>Stopwatch Overlay</b>\nTelegram integration test successful!\n<i>{DateTime.Now:yyyy-MM-dd HH:mm:ss}</i>";

        var sendResult = await SendTelegramMessageAsync(
            token,
            targetChatId,
            testMessage,
            threadId,
            client);

        if (sendResult.Success)
        {
            string targetInfo = threadId.HasValue
                ? $"chat {targetChatId} (topic {threadId})"
                : $"chat {targetChatId}";
            return new TelegramTestResult(true, $"Connected as @{botUsername}! Test message sent to {targetInfo}.", botUsername);
        }
        else
        {
            return new TelegramTestResult(false, $"Bot @{botUsername} verified, but test message failed: {sendResult.ErrorMessage}", botUsername);
        }
    }

    public static void DispatchNoteInBackground(
        AppSettings settings,
        NoteType type,
        string text,
        DateTime? timestamp = null)
    {
        if (settings == null) return;
        if (!settings.TelegramEnabled ||
            string.IsNullOrWhiteSpace(settings.TelegramBotToken) ||
            string.IsNullOrWhiteSpace(settings.TelegramChatId))
        {
            return;
        }

        Task.Run(async () =>
        {
            try
            {
                var result = await SendNoteAsync(settings, type, text, timestamp);
                if (!result.Success)
                {
                    CrashLogger.RecordUiAction($"Telegram dispatch failed: {result.ErrorMessage}", "Telegram");
                }
            }
            catch (Exception ex)
            {
                CrashLogger.LogRecoverable(ex, "TelegramNotesSync.DispatchNoteInBackground");
            }
        });
    }
}
