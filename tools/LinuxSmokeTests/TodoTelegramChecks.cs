using System.Net;
using System.Net.Http;
using System.Text;
using System.Reflection;
using System.Text.Json;
using StopwatchOverlay;

internal static class TodoTelegramChecks
{
    public static void Run(string directory)
    {
        var sync = typeof(TelegramNotesSync).GetMethod("SyncTodoCompletionsAsync")
            ?? throw new Exception("Telegram completion sync is missing.");
        var link = typeof(TelegramNotesSync).GetMethod("LinkTodoMessage")!;
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(directory, "test-outbox.json"));
        var vault = Path.Combine(directory, "telegram-vault"); Directory.CreateDirectory(vault);
        var settings = new AppSettings { TelegramEnabled = true, TelegramBotToken = "123:test-token", TelegramChatId = "-100456", ObsidianVaultFolder = vault };
        var time = new DateTime(2026, 10, 15, 10, 20, 30);
        ObsidianNotesSync.AppendEntry(vault, NoteType.Todo, "Read <chapter> & practice", timestamp: time);
        var handler = new CaptureHandler();
        using var client = new HttpClient(handler);
        TelegramOutboxStore.Enqueue(NoteType.Todo, "Read <chapter> & practice", time);
        var flush = TelegramNotesSync.FlushOutboxAsync(settings, client).GetAwaiter().GetResult();
        if (flush.SentCount != 1) throw new Exception("Test todo was not delivered.");
        var entry = ObsidianNotesSync.LoadAllNotes(vault).Single();
        ObsidianNotesSync.SetTodoCompleted(vault, entry, true);
        Sync();
        if (handler.Requests.Count != 2 || !handler.Requests.Last().Path.EndsWith("editMessageText") ||
            !handler.Requests.Last().Body.Contains("<s>Read &lt;chapter&gt; &amp; practice</s>"))
            throw new Exception("Obsidian completion did not edit the original Telegram message with escaped strikethrough.");
        using (var payload = JsonDocument.Parse(handler.Requests.Last().Body))
            if (payload.RootElement.GetProperty("message_id").GetInt32() != 42 || payload.RootElement.GetProperty("chat_id").GetString() != "-100456")
                throw new Exception("Completion targeted the wrong Telegram message.");
        Sync();
        if (handler.Requests.Count != 2) throw new Exception("Unchanged completion was sent repeatedly.");
        handler.Fail = true;
        ObsidianNotesSync.SetTodoCompleted(vault, ObsidianNotesSync.LoadAllNotes(vault).Single(), false);
        Sync();
        if (!File.ReadAllText(TelegramOutboxStore.SentFilePath).Contains("PendingCompleted")) throw new Exception("Offline edit was not retained.");
        handler.Fail = false;
        Sync();
        if (handler.Requests.Last().Body.Contains("<s>")) throw new Exception("Unchecking did not restore Telegram text.");
        int count = handler.Requests.Count; Sync();
        if (handler.Requests.Count != count) throw new Exception("Successful retry did not settle.");
        // An older todo has no retained message id: link it explicitly, then update it.
        ObsidianNotesSync.AppendEntry(vault, NoteType.Todo, "Older todo", timestamp:time.AddMinutes(1));
        var old = ObsidianNotesSync.LoadAllNotes(vault).First();
        var linked = (TelegramSyncResult)link.Invoke(null,new object[]{settings,old,"https://t.me/c/456/55"})!;
        if (!linked.Success) throw new Exception("Old Telegram message could not be linked.");
        ObsidianNotesSync.SetTodoCompleted(vault,old,true); Sync();
        if (!handler.Requests.Last().Body.Contains("Older todo") || !handler.Requests.Last().Body.Contains("55")) throw new Exception("Linked old message was not edited.");
        linked = (TelegramSyncResult)link.Invoke(null,new object[]{settings,entry,"https://t.me/c/456/55"})!;
        if (linked.Success) throw new Exception("The same Telegram message was linked to two tasks.");
        var invalid = (TelegramSyncResult)link.Invoke(null,new object[]{settings,old,"https://evil.example/c/456/55"})!;
        if (invalid.Success) throw new Exception("Invalid message link was accepted.");
        // Changing bots must not allow a new bot to edit old message bindings.
        settings.TelegramBotToken="789:different-test-token"; count=handler.Requests.Count; Sync();
        if (handler.Requests.Count != count) throw new Exception("Message binding crossed bot accounts.");
        // An unscoped legacy outbox must not be sent automatically through a newly selected account.
        TelegramOutboxStore.Enqueue(NoteType.Todo,"Offline from another account",time.AddMinutes(2));
        count=handler.Requests.Count; Sync();
        if (handler.Requests.Count != count) throw new Exception("Automatic retry sent an unscoped task through a different account.");
        TelegramOutboxStore.Clear();
        settings.TelegramBotToken="123:test-token";
        ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,"Line one\nLine two",timestamp:time.AddMinutes(3));
        TelegramOutboxStore.Enqueue(NoteType.Todo,"Line one\nLine two",time.AddMinutes(3));
        TelegramNotesSync.FlushOutboxAsync(settings,client).GetAwaiter().GetResult();
        var multiline=ObsidianNotesSync.LoadAllNotes(vault).First();
        if (!TelegramNotesSync.HasTodoMessageLink(settings,multiline)) throw new Exception("Multiline todo did not retain its Telegram binding.");
        var rich="Paragraph one.\n\n- **Edit** works\n- [Reference](https://example.com/docs)";
        ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,rich,timestamp:time.AddMinutes(3).AddSeconds(10));
        var richEntry=ObsidianNotesSync.LoadAllNotes(vault).Single(e=>e.Text==rich);
        var richLink=TelegramNotesSync.LinkTodoMessage(settings,richEntry,"88");
        if(!richLink.Success)throw new Exception("Structured todo could not link.");
        ObsidianNotesSync.SetTodoCompleted(vault,richEntry,true);Sync();
        var richPayload=JsonDocument.Parse(handler.Requests.Last().Body);
        var richMessage=richPayload.RootElement.GetProperty("text").GetString()!;
        if(!richMessage.Contains("<s>Paragraph one.\n\n• <b>Edit</b> works") || !richMessage.Contains("<a href=\"https://example.com/docs\">Reference</a>"))throw new Exception("Structured completion lost Telegram formatting.");
        richEntry=ObsidianNotesSync.LoadAllNotes(vault).Single(e=>e.Text==rich);
        ObsidianNotesSync.SetTodoCompleted(vault,richEntry,false);Sync();
        richPayload=JsonDocument.Parse(handler.Requests.Last().Body);
        richMessage=richPayload.RootElement.GetProperty("text").GetString()!;
        if(richMessage.Contains("<s>") || !richMessage.Contains("<b>Edit</b>"))throw new Exception("Unchecking stripped structured Telegram formatting.");
        // Preserve the originating account even if settings are edited during HTTP.
        ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,"Account snapshot",timestamp:time.AddMinutes(4));
        TelegramOutboxStore.Enqueue(NoteType.Todo,"Account snapshot",time.AddMinutes(4));
        handler.DuringRequest=()=>settings.TelegramBotToken="789:different-test-token";
        TelegramNotesSync.FlushOutboxAsync(settings,client).GetAwaiter().GetResult();
        handler.DuringRequest=null; settings.TelegramBotToken="123:test-token";
        if (!TelegramNotesSync.HasTodoMessageLink(settings,ObsidianNotesSync.LoadAllNotes(vault).First()))
            throw new Exception("A settings change attributed the sent message to the wrong bot.");
        // A deleted old message must not block later independent edits.
        ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,"Healthy later task",timestamp:time.AddMinutes(5));
        var healthy=ObsidianNotesSync.LoadAllNotes(vault).First();
        var healthyLink=TelegramNotesSync.LinkTodoMessage(settings,healthy,"https://t.me/c/456/77");
        if (!healthyLink.Success) throw new Exception("Healthy task link failed.");
        var older=ObsidianNotesSync.LoadAllNotes(vault).Single(e=>e.Text=="Older todo");
        ObsidianNotesSync.SetTodoCompleted(vault,older,false);
        handler.FailMessageId=55;count=handler.Requests.Count;Sync();
        if (!handler.Requests.Skip(count).Any(r=>r.Body.Contains("\"message_id\":77"))) throw new Exception("One unavailable message blocked other todo updates.");
        handler.FailMessageId=null;
        // Original destination is preserved for a scoped offline task.
        TelegramOutboxStore.Enqueue(NoteType.Todo,"Scoped offline",time.AddMinutes(6),"123",TelegramNotesSync.GetTodoVaultKey(settings),"-100111",7);
        count=handler.Requests.Count;Sync();
        if (!handler.Requests.Skip(count).Any(r=>r.Path.EndsWith("sendMessage") && r.Body.Contains("-100111") && r.Body.Contains("\"message_thread_id\":7")))
            throw new Exception("Scoped retry changed the task's original destination.");
        // A blocked queued note must not prevent editing healthy existing todos.
        TelegramOutboxStore.Enqueue(NoteType.Note,"Blocked note",time.AddMinutes(8),"123",TelegramNotesSync.GetTodoVaultKey(settings),"-100111",999);
        handler.FailSendText="Blocked note";
        ObsidianNotesSync.SetTodoCompleted(vault,ObsidianNotesSync.LoadAllNotes(vault).Single(e=>e.Text=="Healthy later task"),true);
        count=handler.Requests.Count;Sync();
        if (!handler.Requests.Skip(count).Any(r=>r.Path.EndsWith("editMessageText") && r.Body.Contains("\"message_id\":77")))
            throw new Exception("A blocked outbox message starved healthy Telegram completion edits.");
        handler.FailSendText=null; TelegramOutboxStore.Clear();
        // A receipt-file write failure retains the delivered message id, so recovery does not resend.
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(directory,"receipt-failure","queue.json"));
        Directory.CreateDirectory(TelegramOutboxStore.SentFilePath);
        TelegramOutboxStore.Enqueue(NoteType.Todo,"Receipt retry",time.AddMinutes(7));
        count=handler.Requests.Count;
        var failedReceipt=TelegramNotesSync.FlushOutboxAsync(settings,client).GetAwaiter().GetResult();
        if (failedReceipt.LastError==null || TelegramOutboxStore.PendingCount!=1 || TelegramOutboxStore.GetAll().Single().DeliveredMessageId==null)
            throw new Exception("Delivered task was removed before its receipt was saved.");
        Directory.Delete(TelegramOutboxStore.SentFilePath);
        TelegramNotesSync.FlushOutboxAsync(settings,client).GetAwaiter().GetResult();
        if (handler.Requests.Count!=count+1 || TelegramOutboxStore.PendingCount!=0) throw new Exception("Receipt recovery sent a duplicate Telegram message.");
        TelegramOutboxStore.SetCustomFilePath(null);
        void Sync() => ((Task<TelegramSyncResult>)sync.Invoke(null,new object?[]{settings,client})!).GetAwaiter().GetResult();
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public readonly List<(string Path,string Body)> Requests = new();
        public bool Fail;
        public int? FailMessageId;
        public string? FailSendText;
        private int _nextId = 42;
        public Action? DuringRequest;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string raw = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var json=JsonDocument.Parse(raw);
            // Decode JSON escapes so the assertion checks the actual HTML sent to Telegram.
            string normalized=JsonSerializer.Serialize(json.RootElement,new JsonSerializerOptions { Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            Requests.Add((request.RequestUri!.AbsolutePath,normalized));
            DuringRequest?.Invoke();
            bool sending = request.RequestUri!.AbsolutePath.EndsWith("sendMessage");
            int messageId = sending ? _nextId++ : json.RootElement.GetProperty("message_id").GetInt32();
            bool failure = Fail || (!sending && FailMessageId == messageId) ||
                (sending && FailSendText != null && normalized.Contains(FailSendText));
            string chat = json.RootElement.GetProperty("chat_id").GetString()!;
            long actualChat = long.TryParse(chat, out var numeric) ? numeric : -100456;
            string body = failure ? "{\"ok\":false,\"description\":\"offline test\"}" :
                JsonSerializer.Serialize(new { ok=true, result=new { message_id=messageId, chat=new { id=actualChat } } });
            return new HttpResponseMessage(failure?HttpStatusCode.ServiceUnavailable:HttpStatusCode.OK) {
                Content=new StringContent(body,Encoding.UTF8,"application/json")
            };
        }
    }
}
