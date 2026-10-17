using System.Net;
using System.Text;
using System.Text.Json;
using StopwatchOverlay;

internal static class TodoDeletionChecks
{
    public static void Run(string directory)
    {
        var remove = typeof(ObsidianNotesSync).GetMethod("DeleteTodo") ?? throw new Exception("Safe todo deletion is missing.");
        var everywhere = typeof(TelegramNotesSync).GetMethod("DeleteTodoEverywhereAsync")!;
        string vault = Path.Combine(directory,"delete-vault");Directory.CreateDirectory(Path.Combine(vault,"Notes"));
        string file = Path.Combine(vault,"Notes","Todos.md");
        string document="## 2026-10-15\r\n- [ ] 09:10 Keep this\r\n- [ ] 09:11 Delete this\r\n";
        File.WriteAllText(file,document);
        var target=ObsidianNotesSync.LoadAllNotes(vault).First();
        var local=(NoteSyncResult)remove.Invoke(null,new object?[]{vault,target,"Notes"})!;
        if (!local.Success || File.ReadAllText(file)!=document.Replace("- [ ] 09:11 Delete this\r\n","")) throw new Exception("Todo deletion changed unrelated Markdown.");
        File.WriteAllText(file,document+"- [ ] 09:11 Delete this\r\n");
        string snapshot=File.ReadAllText(file);
        local=(NoteSyncResult)remove.Invoke(null,new object?[]{vault,target,"Notes"})!;
        if(local.Success || File.ReadAllText(file)!=snapshot)throw new Exception("Ambiguous todo deletion was allowed.");
        File.WriteAllText(file,document);
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(directory,"delete-outbox.json"));
        var settings=new AppSettings { TelegramEnabled=true,TelegramBotToken="123:test-token",TelegramChatId="-100456",ObsidianVaultFolder=vault };
        var handler=new DeleteHandler();using var client=new HttpClient(handler);
        TelegramOutboxStore.MarkSent(NoteType.Todo,target.Text,target.Timestamp,42,"-100456","123",TelegramNotesSync.GetTodoVaultKey(settings));
        handler.Fail=true;
        var result=Delete(target);
        if(result.Success || File.ReadAllText(file)!=document)throw new Exception("Failed Telegram deletion removed the local todo.");
        handler.Fail=false;result=Delete(target);
        if(!result.Success || ObsidianNotesSync.LoadAllNotes(vault).Any(e=>e.Text==target.Text))throw new Exception("Confirmed deletion did not remove the linked task.");
        if(!handler.Requests.Last().Path.EndsWith("deleteMessage") || !handler.Requests.Last().Body.Contains("42"))throw new Exception("Deletion targeted the wrong Telegram method/message.");
        int count=handler.Requests.Count;
        TelegramNotesSync.SyncTodoCompletionsAsync(settings,client).GetAwaiter().GetResult();
        if(handler.Requests.Count!=count)throw new Exception("Deleted Telegram message was edited again.");
        // Deleting an unsent task must cancel its scoped queue entry without publishing it.
        File.WriteAllText(file,document.Replace("Delete this","Unsent todo"));
        var unsent=ObsidianNotesSync.LoadAllNotes(vault).First();
        TelegramOutboxStore.Enqueue(NoteType.Todo,unsent.Text,unsent.Timestamp,"123",TelegramNotesSync.GetTodoVaultKey(settings),"-100456");
        TelegramOutboxStore.Enqueue(NoteType.Todo,unsent.Text,unsent.Timestamp,"789",TelegramNotesSync.GetTodoVaultKey(settings),"-100789");
        result=Delete(unsent);
        if(!result.Success || handler.Requests.Count!=count || TelegramOutboxStore.GetAll().Any(e=>e.BotIdentity=="123"))throw new Exception("Unsent todo deletion published or retained its pending message.");
        if(!TelegramOutboxStore.GetAll().Any(e=>e.BotIdentity=="789"))throw new Exception("Deletion removed another account's queue entry.");
        // If Obsidian changes during HTTP, preserve that file and retain the successful remote deletion for a safe retry.
        File.WriteAllText(file,document.Replace("Delete this","Changing task"));
        var changing=ObsidianNotesSync.LoadAllNotes(vault).First();
        TelegramOutboxStore.MarkSent(NoteType.Todo,changing.Text,changing.Timestamp,99,"-100456","123",TelegramNotesSync.GetTodoVaultKey(settings));
        handler.DuringDelete=()=>ObsidianNotesSync.SetTodoCompleted(vault,changing,true);
        result=Delete(changing);handler.DuringDelete=null;
        if(result.Success || !ObsidianNotesSync.LoadAllNotes(vault).Any(e=>e.Text==changing.Text && e.IsCompleted))throw new Exception("Deletion overwrote an external Obsidian change.");
        count=handler.Requests.Count;
        result=Delete(ObsidianNotesSync.LoadAllNotes(vault).First());
        if(!result.Success || handler.Requests.Count!=count)throw new Exception("Retry repeated an already successful Telegram deletion.");
        // A recovered queue receipt must never replace a different message explicitly linked by the user.
        File.WriteAllText(file,document.Replace("Delete this","Conflicting receipt"));
        var conflict=ObsidianNotesSync.LoadAllNotes(vault).First();
        TelegramOutboxStore.MarkSent(NoteType.Todo,conflict.Text,conflict.Timestamp,55,"-100456","123",TelegramNotesSync.GetTodoVaultKey(settings));
        var deliveredQueue=TelegramOutboxStore.Enqueue(NoteType.Todo,conflict.Text,conflict.Timestamp,"123",TelegramNotesSync.GetTodoVaultKey(settings),"-100456");
        TelegramOutboxStore.Update(deliveredQueue with { DeliveredMessageId=42,DeliveredChatId="-100456" });
        snapshot=File.ReadAllText(file);count=handler.Requests.Count;
        result=Delete(conflict);
        if(result.Success || handler.Requests.Count!=count || File.ReadAllText(file)!=snapshot ||
            !TelegramOutboxStore.GetTrackedTodos("123",TelegramNotesSync.GetTodoVaultKey(settings)).Any(r=>r.Text==conflict.Text && r.MessageId==55))
            throw new Exception("A queued receipt replaced the confirmed Telegram message before deletion.");
        TelegramOutboxStore.SetCustomFilePath(null);
        TelegramSyncResult Delete(NoteEntry entry)=>((Task<TelegramSyncResult>)everywhere.Invoke(null,new object?[]{settings,entry,client})!).GetAwaiter().GetResult();
    }

    private sealed class DeleteHandler:HttpMessageHandler
    {
        public bool Fail;public Action? DuringDelete;
        public readonly List<(string Path,string Body)> Requests=new();
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            string body=await request.Content!.ReadAsStringAsync(token);Requests.Add((request.RequestUri!.AbsolutePath,body));
            if(request.RequestUri.AbsolutePath.EndsWith("deleteMessage"))DuringDelete?.Invoke();
            return new HttpResponseMessage(Fail?HttpStatusCode.BadRequest:HttpStatusCode.OK) {
                Content=new StringContent(Fail?"{\"ok\":false,\"description\":\"message can't be deleted\"}":"{\"ok\":true,\"result\":true}",Encoding.UTF8,"application/json")
            };
        }
    }
}
