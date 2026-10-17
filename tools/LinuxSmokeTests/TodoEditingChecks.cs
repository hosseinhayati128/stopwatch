using System.Net;
using System.Text;
using System.Text.Json;
using StopwatchOverlay;

internal static class TodoEditingChecks
{
    public static void Run(string directory)
    {
        string vault=Path.Combine(directory,"todo-editing"); Directory.CreateDirectory(vault);
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(directory,"editing-outbox.json"));
        var settings=new AppSettings {ObsidianVaultFolder=vault,TelegramEnabled=true,TelegramBotToken="123:test-token",TelegramChatId="-100456"};
        var time=new DateTime(2026,10,17,12,0,0);
        ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,"Original\n\n- **one**",timestamp:time);
        ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,"Neighbor",timestamp:time.AddMinutes(1));
        var old=ObsidianNotesSync.LoadAllNotes(vault).Single(e=>e.Timestamp==time);
        var neighbor=ObsidianNotesSync.LoadAllNotes(vault).Single(e=>e.Timestamp!=time);
        Check(TodoOrganizationStore.Load(vault).GetDueAt(old)==null,"Existing todos gained a deadline.");
        TodoOrganizationStore.SetSortMode(vault,"Manual"); TodoOrganizationStore.SetManualOrder(vault,new[]{old,neighbor});
        TelegramOutboxStore.MarkSent(NoteType.Todo,old.Text,old.Timestamp,42,"-100456","123",TelegramNotesSync.GetTodoVaultKey(settings));
        var due=time.AddHours(23);
        Check(TodoOrganizationStore.SaveTodo(vault,old,"Updated\n\n- **two**",2,due).Success,"Edit failed.");
        var edited=ObsidianNotesSync.LoadAllNotes(vault).Single(e=>e.Timestamp==time);
        Check(edited.Text=="Updated\n\n- **two**" && !edited.IsCompleted,"Edit lost formatting or completion.");
        var org=TodoOrganizationStore.Load(vault);
        Check(org.GetDueAt(edited)==due && org.GetPriority(edited)==2 && org.Arrange(new[]{neighbor,edited})[0]==edited,"Edit lost metadata or manual position.");
        Check(TelegramNotesSync.HasTodoMessageLink(settings,edited),"Edit orphaned the Telegram link.");
        Check(TodoDeadlines.GetState(edited,due,time)==TodoDueState.Soon && TodoDeadlines.GetState(edited,due,time.AddDays(2))==TodoDueState.Overdue &&
            TodoDeadlines.GetState(edited,due,time.AddDays(-1))==TodoDueState.Scheduled && TodoDeadlines.GetState(edited with {IsCompleted=true},due,time.AddDays(2))==TodoDueState.None,"Deadline boundary states are wrong.");
        var handler=new Handler(); using var client=new HttpClient(handler);
        handler.Fail=true; Sync(); handler.Fail=false; Sync();
        Check(LastText().Contains("Updated") && LastText().Contains("<b>two</b>") && LastText().Contains("Due:") && LastText().Contains("2026-10-18 11:00"),"Telegram edit lost body, formatting, or due time.");
        using(var doc=JsonDocument.Parse(handler.Bodies.Last()))Check(doc.RootElement.GetProperty("message_id").GetInt32()==42,"Edit replaced or retargeted Telegram message.");
        int count=handler.Bodies.Count;Sync();Check(count==handler.Bodies.Count,"Successful edit did not settle.");
        Check(!TelegramNotesSync.GetUnsentVaultNotes(settings,36500).Any(e=>e==edited),"Edited delivered task was rediscovered as unsent.");
        Check(!TodoOrganizationStore.SaveTodo(vault,old,"Stale save",1,null).Success,"Stale edit overwrote current task.");
        Check(TodoOrganizationStore.SaveTodo(vault,edited,old.Text,4,null).Success,"Renaming back to an earlier title failed.");
        var restored=ObsidianNotesSync.LoadAllNotes(vault).Single(e=>e.Timestamp==time);
        Check(TodoOrganizationStore.Load(vault).GetDueAt(restored)==null && TelegramNotesSync.HasTodoMessageLink(settings,restored),"Clearing deadline or rename-back broke identity.");
        Sync();Check(!LastText().Contains("Due:"),"Cleared deadline remained in Telegram.");
        ObsidianNotesSync.SetTodoCompleted(vault,restored,true);
        restored=ObsidianNotesSync.LoadAllNotes(vault).Single(e=>e.Timestamp==time);
        Check(TodoOrganizationStore.SaveTodo(vault,restored,"Completed edited",1,due).Success,"Completed task could not be edited.");
        edited=ObsidianNotesSync.LoadAllNotes(vault).Single(e=>e.Timestamp==time);
        Check(edited.IsCompleted,"Editing unchecked a completed todo.");Sync();Check(LastText().Contains("<s>Completed edited</s>"),"Editing lost Telegram strikethrough.");
        Check(TelegramNotesSync.DeleteTodoEverywhereAsync(settings,edited,client).GetAwaiter().GetResult().Success,"Edited todo could not be deleted with its original Telegram link.");
        Check(ObsidianNotesSync.LoadAllNotes(vault).Single()==neighbor,"Editing/deleting changed the neighboring todo.");
        // A queued, unsent task must send the latest text/deadline, never its stale body.
        ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,"Queued original",timestamp:time.AddMinutes(2));
        var queued=ObsidianNotesSync.LoadAllNotes(vault).First();
        TelegramOutboxStore.Enqueue(NoteType.Todo,queued.Text,queued.Timestamp,"123",TelegramNotesSync.GetTodoVaultKey(settings),"-100456");
        Check(TodoOrganizationStore.SaveTodo(vault,queued,"Queued latest",3,due).Success,"Queued edit failed.");
        Check(!TelegramNotesSync.GetUnsentVaultNotes(settings,36500).Any(e=>e.Text=="Queued latest"),"Edited queued task was rediscovered as unsent.");
        var flush=TelegramNotesSync.FlushOutboxAsync(settings,client,scopedOnly:true).GetAwaiter().GetResult();
        Check(flush.SentCount==1 && LastText().Contains("Queued latest") && LastText().Contains("Due:"),"Outbox sent stale edited content.");
        // If receipt persistence fails after HTTP, later edits must not be acknowledged as already delivered.
        ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,"Receipt original",timestamp:time.AddMinutes(4));
        var receiptEntry=ObsidianNotesSync.LoadAllNotes(vault).First();
        TelegramOutboxStore.Enqueue(NoteType.Todo,receiptEntry.Text,receiptEntry.Timestamp,"123",TelegramNotesSync.GetTodoVaultKey(settings),"-100456");
        string queueFile=TelegramOutboxStore.FilePath, originalQueue=File.ReadAllText(TelegramOutboxStore.FilePath);
        handler.DuringRequest=()=>{File.Delete(queueFile);Directory.CreateDirectory(queueFile);handler.DuringRequest=null;};
        TelegramNotesSync.FlushOutboxAsync(settings,client,scopedOnly:true).GetAwaiter().GetResult();
        Directory.Delete(queueFile);File.WriteAllText(queueFile,originalQueue);
        Check(TodoOrganizationStore.SaveTodo(vault,receiptEntry,"Receipt latest",3,due).Success,"Edit after receipt failure failed.");
        int sends=handler.SendCount;
        TelegramNotesSync.FlushOutboxAsync(settings,client,scopedOnly:true).GetAwaiter().GetResult();Sync();
        Check(handler.SendCount==sends && LastText().Contains("Receipt latest") && LastText().Contains("Due:"),"Receipt recovery acknowledged content that was never delivered, or sent a duplicate message.");
        ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,"Identity A",timestamp:time.AddMinutes(3));
        ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,"Identity B",timestamp:time.AddMinutes(3));
        var a=ObsidianNotesSync.LoadAllNotes(vault).Single(e=>e.Text=="Identity A");
        var b=ObsidianNotesSync.LoadAllNotes(vault).Single(e=>e.Text=="Identity B");
        TelegramOutboxStore.MarkSent(NoteType.Todo,a.Text,a.Timestamp,61,"-100456","123",TelegramNotesSync.GetTodoVaultKey(settings));
        TelegramOutboxStore.MarkSent(NoteType.Todo,b.Text,b.Timestamp,62,"-100456","123",TelegramNotesSync.GetTodoVaultKey(settings));
        Check(TodoOrganizationStore.SaveTodo(vault,a,"Identity C",3,null).Success,"Initial identity edit failed.");
        var before=File.ReadAllText(Path.Combine(vault,"Notes","Todos.md"));
        var metadataBefore=File.ReadAllText(Path.Combine(vault,"Notes",TodoOrganizationStore.FileName));
        Check(!TodoOrganizationStore.SaveTodo(vault,b,a.Text,3,null).Success,"A historical identity redirected another task's receipt.");
        Check(File.ReadAllText(Path.Combine(vault,"Notes","Todos.md"))==before && File.ReadAllText(Path.Combine(vault,"Notes",TodoOrganizationStore.FileName))==metadataBefore,"Rejected identity collision changed task data.");
        Check(!ObsidianNotesSync.AppendEntry(vault,NoteType.Todo,a.Text,timestamp:a.Timestamp).Success,"A new task reused another task's historical identity.");
        TelegramOutboxStore.SetCustomFilePath(null);
        string LastText() {using var doc=JsonDocument.Parse(handler.Bodies.Last());return doc.RootElement.GetProperty("text").GetString()!;}
        void Sync()=>TelegramNotesSync.SyncTodoCompletionsAsync(settings,client).GetAwaiter().GetResult();
    }
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    sealed class Handler:HttpMessageHandler
    {
        public bool Fail; public List<string> Bodies=new(); public Action? DuringRequest; public int SendCount;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(token));
            bool sending=request.RequestUri!.AbsolutePath.EndsWith("sendMessage");if(sending)SendCount++;
            DuringRequest?.Invoke();
            string body=Fail?"{\"ok\":false}":request.RequestUri!.AbsolutePath.EndsWith("deleteMessage")?"{\"ok\":true,\"result\":true}":"{\"ok\":true,\"result\":{\"message_id\":42,\"chat\":{\"id\":-100456}}}";
            return new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent(body,Encoding.UTF8,"application/json")};
        }
    }
}
