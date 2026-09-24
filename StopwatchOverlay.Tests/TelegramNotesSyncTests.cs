using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace StopwatchOverlay.Tests;

public class TelegramNotesSyncTests
{
    private sealed class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    [Fact]
    public void ResolveDestination_EmptyTopic_ReturnsBaseChatIdAndNullThread()
    {
        var (chatId, threadId) = TelegramNotesSync.ResolveDestination("-1001234567890", "");

        Assert.Equal("-1001234567890", chatId);
        Assert.Null(threadId);
    }

    [Fact]
    public void ResolveDestination_NumericTopic_ReturnsBaseChatIdAndParsedThread()
    {
        var (chatId, threadId) = TelegramNotesSync.ResolveDestination("-1001234567890", " 42 ");

        Assert.Equal("-1001234567890", chatId);
        Assert.Equal(42L, threadId);
    }

    [Fact]
    public void ResolveDestination_AlternateChatIdAsTopic_ReturnsAlternateChatAndNullThread()
    {
        var (chatId, threadId) = TelegramNotesSync.ResolveDestination("-1001234567890", "-1009876543210");

        Assert.Equal("-1009876543210", chatId);
        Assert.Null(threadId);

        var (channelChatId, channelThreadId) = TelegramNotesSync.ResolveDestination("-1001234567890", "@my_notes_channel");
        Assert.Equal("@my_notes_channel", channelChatId);
        Assert.Null(channelThreadId);
    }

    [Fact]
    public void ResolveDestination_CompositeChatAndTopic_ParsesBothCorrectly()
    {
        var (chatId, threadId) = TelegramNotesSync.ResolveDestination("-1001234567890", "-1005555555555:108");

        Assert.Equal("-1005555555555", chatId);
        Assert.Equal(108L, threadId);
    }

    [Fact]
    public void ResolveDestination_ByNoteType_RoutesToConfiguredTopics()
    {
        var settings = new AppSettings
        {
            TelegramChatId = "-1001000",
            TelegramNotesTopicId = "10",
            TelegramTodosTopicId = "20",
            TelegramRemindersTopicId = "30"
        };

        var (noteChat, noteThread) = TelegramNotesSync.ResolveDestination(settings, NoteType.Note);
        var (todoChat, todoThread) = TelegramNotesSync.ResolveDestination(settings, NoteType.Todo);
        var (remChat, remThread) = TelegramNotesSync.ResolveDestination(settings, NoteType.Reminder);

        Assert.Equal("-1001000", noteChat);
        Assert.Equal(10L, noteThread);

        Assert.Equal("-1001000", todoChat);
        Assert.Equal(20L, todoThread);

        Assert.Equal("-1001000", remChat);
        Assert.Equal(30L, remThread);
    }

    [Fact]
    public void FormatMessage_FormatsWithEmojiAndHtmlEscapes()
    {
        var time = new DateTime(2026, 9, 24, 15, 30, 0);
        string rawText = "Check <script>alert('xss')</script> & finish \"project\"";

        string todoFormatted = TelegramNotesSync.FormatMessage(NoteType.Todo, rawText, time);
        Assert.Contains("☑️ <b>Todo</b> (15:30)", todoFormatted);
        Assert.Contains("&lt;script&gt;", todoFormatted);
        Assert.Contains("&amp; finish", todoFormatted);

        string noteFormatted = TelegramNotesSync.FormatMessage(NoteType.Note, rawText, time);
        Assert.Contains("📝 <b>Quick Note</b> (15:30)", noteFormatted);

        string remFormatted = TelegramNotesSync.FormatMessage(NoteType.Reminder, rawText, time);
        Assert.Contains("⏰ <b>Reminder</b> (15:30)", remFormatted);
    }

    [Fact]
    public async Task SendNoteAsync_ValidationFailsOnMissingConfig()
    {
        var settings = new AppSettings();

        var res1 = await TelegramNotesSync.SendNoteAsync(settings, NoteType.Todo, "Hello");
        Assert.False(res1.Success);
        Assert.Contains("Bot Token", res1.ErrorMessage);

        settings.TelegramBotToken = "fake-token";
        var res2 = await TelegramNotesSync.SendNoteAsync(settings, NoteType.Todo, "Hello");
        Assert.False(res2.Success);
        Assert.Contains("Chat ID", res2.ErrorMessage);

        settings.TelegramChatId = "-100123";
        var res3 = await TelegramNotesSync.SendNoteAsync(settings, NoteType.Todo, "");
        Assert.False(res3.Success);
        Assert.Contains("empty", res3.ErrorMessage);
    }

    [Fact]
    public async Task SendNoteAsync_SuccessfulPost_ReturnsSuccessAndMessageId()
    {
        var settings = new AppSettings
        {
            TelegramBotToken = "123456:ABC-DEF",
            TelegramChatId = "-1001234567890",
            TelegramNotesTopicId = "77"
        };

        HttpRequestMessage? capturedRequest = null;
        string? capturedBody = null;

        var handler = new MockHttpMessageHandler(req =>
        {
            capturedRequest = req;
            capturedBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"ok":true,"result":{"message_id":9988,"chat":{"id":-1001234567890}}}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var result = await TelegramNotesSync.SendNoteAsync(
            settings,
            NoteType.Note,
            "Meeting notes summary",
            new DateTime(2026, 9, 24, 10, 0, 0),
            client);

        Assert.True(result.Success);
        Assert.Equal(9988, result.MessageId);
        Assert.NotNull(capturedRequest);
        Assert.Equal("https://api.telegram.org/bot123456:ABC-DEF/sendMessage", capturedRequest.RequestUri?.ToString());

        using var doc = JsonDocument.Parse(capturedBody!);
        var root = doc.RootElement;
        Assert.Equal("-1001234567890", root.GetProperty("chat_id").GetString());
        Assert.Equal(77L, root.GetProperty("message_thread_id").GetInt64());
        Assert.Equal("HTML", root.GetProperty("parse_mode").GetString());
        Assert.Contains("Meeting notes summary", root.GetProperty("text").GetString());
    }

    [Fact]
    public async Task SendNoteAsync_TelegramApiError_ReturnsFailureWithDescription()
    {
        var settings = new AppSettings
        {
            TelegramBotToken = "123456:ABC-DEF",
            TelegramChatId = "-1001234567890"
        };

        var handler = new MockHttpMessageHandler(_ =>
        {
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent(
                    """{"ok":false,"error_code":400,"description":"Bad Request: chat not found"}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var result = await TelegramNotesSync.SendNoteAsync(
            settings,
            NoteType.Todo,
            "Buy groceries",
            null,
            client);

        Assert.False(result.Success);
        Assert.Equal("Bad Request: chat not found", result.ErrorMessage);
    }

    [Fact]
    public async Task TestConnectionAsync_ValidTokenAndChat_ReturnsSuccess()
    {
        var settings = new AppSettings
        {
            TelegramBotToken = "123456:ABC-DEF",
            TelegramChatId = "-1001234567890",
            TelegramNotesTopicId = "15"
        };

        var handler = new MockHttpMessageHandler(req =>
        {
            if (req.RequestUri!.AbsolutePath.Contains("getMe"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"ok":true,"result":{"id":123456,"is_bot":true,"first_name":"TestBot","username":"my_awesome_bot"}}""",
                        System.Text.Encoding.UTF8,
                        "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"ok":true,"result":{"message_id":42}}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var result = await TelegramNotesSync.TestConnectionAsync(settings, client);

        Assert.True(result.Success);
        Assert.Equal("my_awesome_bot", result.BotUsername);
        Assert.Contains("my_awesome_bot", result.Message);
        Assert.Contains("topic 15", result.Message);
    }

    [Fact]
    public async Task TestConnectionAsync_InvalidToken_ReturnsFailure()
    {
        var settings = new AppSettings
        {
            TelegramBotToken = "invalid-token"
        };

        var handler = new MockHttpMessageHandler(_ =>
        {
            return new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(
                    """{"ok":false,"error_code":401,"description":"Unauthorized"}""",
                    System.Text.Encoding.UTF8,
                    "application/json")
            };
        });

        using var client = new HttpClient(handler);
        var result = await TelegramNotesSync.TestConnectionAsync(settings, client);

        Assert.False(result.Success);
        Assert.Contains("Unauthorized", result.Message);
    }

    [Fact]
    public void AppSettings_SerializationAndNormalization_PreservesTelegramFields()
    {
        var settings = new AppSettings
        {
            TelegramEnabled = true,
            TelegramBotToken = "  12345:TOKEN  ",
            TelegramChatId = "  -1001234567  ",
            TelegramNotesTopicId = "  11  ",
            TelegramTodosTopicId = "  22  ",
            TelegramRemindersTopicId = "  33  "
        };

        settings.NormalizeForRuntime();

        Assert.True(settings.TelegramEnabled);
        Assert.Equal("12345:TOKEN", settings.TelegramBotToken);
        Assert.Equal("-1001234567", settings.TelegramChatId);
        Assert.Equal("11", settings.TelegramNotesTopicId);
        Assert.Equal("22", settings.TelegramTodosTopicId);
        Assert.Equal("33", settings.TelegramRemindersTopicId);

        string json = JsonSerializer.Serialize(settings);
        var deserialized = JsonSerializer.Deserialize<AppSettings>(json);

        Assert.NotNull(deserialized);
        Assert.True(deserialized.TelegramEnabled);
        Assert.Equal("12345:TOKEN", deserialized.TelegramBotToken);
        Assert.Equal("-1001234567", deserialized.TelegramChatId);
        Assert.Equal("11", deserialized.TelegramNotesTopicId);
        Assert.Equal("22", deserialized.TelegramTodosTopicId);
        Assert.Equal("33", deserialized.TelegramRemindersTopicId);
    }
}
