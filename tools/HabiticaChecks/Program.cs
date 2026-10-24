using System.Net;
using System.Text;
using System.Text.Json;
using StopwatchOverlay;
using StopwatchOverlay.Habitica;

var root = Path.Combine(Path.GetTempPath(), "StopwatchHabiticaWorker-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    var settings = new AppSettings { ObsidianVaultFolder = root, NotesSubfolder = "Notes", HabiticaUserId = "11111111-1111-4111-8111-111111111111", HabiticaClientId = "22222222-2222-4222-8222-222222222222-Stopwatch" };
    var now = DateTimeOffset.UtcNow;
    var handler = new Fake(); var api = new HabiticaApiClient(new HttpClient(handler)); var secrets = new Secrets();
    string state = Path.Combine(root, "local.json"), metadata = Path.Combine(root, "Notes", ".stopwatch-habitica.json");
    HabiticaIntegration New(string? file = null) => new(api, secrets, file ?? state, () => now);
    var sync = New(); sync.SetToken(settings.HabiticaUserId, "test-token"); sync.EnableHere(settings);
    secrets.RejectWrites = true;
    bool rejectedSaveReported = false;
    try { sync.SetToken(settings.HabiticaUserId, "synthetic-replacement"); } catch (InvalidOperationException) { rejectedSaveReported = true; }
    Check(rejectedSaveReported && secrets.Read(settings.HabiticaUserId) == "test-token", "rejected secret write is reported instead of claiming token saved");
    secrets.RejectWrites = false;
    string configuredClient = settings.HabiticaClientId;
    settings.HabiticaClientId = "StopwatchOverlay";
    Check((await sync.TestConnectionAsync(settings)).Error == HabiticaErrorKind.Configuration && handler.Calls == 0,
        "invalid client blocks connection before HTTP and before reserving a request delay");
    settings.HabiticaClientId = configuredClient;
    NoteSyncResult Save(NoteEntry e) => ObsidianNotesSync.AppendEntry(root, e.Type, e.Text, "Notes", e.Timestamp);
    var legacy = new NoteEntry(NoteType.Todo, new DateTime(2026, 10, 24, 10, 25, 0), "existing dated todo");
    var legacyDue = new DateTime(2026, 10, 25, 14, 30, 0);
    Check(Save(legacy).Success && TodoOrganizationStore.SaveTodo(root, legacy, legacy.Text, 3, legacyDue, "Notes").Success && sync.GetDetails(settings, legacy).DueAt == legacyDue,
        "existing todo due date survives Habitica enrollment");
    var note = new NoteEntry(NoteType.Note, DateTime.Now, "private note");
    sync.Apply(settings, null, note, null, null, false, () => Save(note));
    Check(sync.GetStatus(settings).PendingCount == 0, "standalone notes excluded");
    var todo = new NoteEntry(NoteType.Todo, new DateTime(2026, 10, 24, 10, 30, 0), "first title");
    sync.Apply(settings, null, todo, null, null, false, () => new(false, "", "failed"));
    Check(sync.GetStatus(settings).PendingCount == 0, "failed local mutation does not enqueue");
    var created = sync.Apply(settings, null, todo, null, null, false, () => Save(todo));
    Check(created.LocalResult.Success && created.SyncWarning == null, "local create and journal committed");
    var id = sync.GetDetails(settings, todo).Id;
    var edited = todo with { Text = "latest title" };
    var renamed = sync.Apply(settings, todo, edited, null, null, false, () => ObsidianNotesSync.UpdateTodoText(root, todo, edited.Text, "Notes"));
    Check(renamed.LocalResult.Success && renamed.SyncWarning == null, "local rename and journal committed: " + renamed.LocalResult.Message + renamed.SyncWarning);
    Check(sync.GetDetails(settings, edited).Id == id && id != Guid.Empty && sync.GetStatus(settings).PendingCount == 1, "rename preserves identity and coalesces revisions");
    await sync.TickAsync(settings); Check(handler.Calls == 1 && handler.Posts == 0, "lookup precedes create");
    await sync.TickAsync(settings); Check(handler.Calls == 1, "minimum 30 second spacing");
    sync = New(); now = now.AddSeconds(10); await sync.TickAsync(settings); Check(handler.Calls == 1, "restart preserves request deadline");
    now = now.AddSeconds(21); await sync.TickAsync(settings); // lookup after restart
    now = now.AddSeconds(31); await sync.TickAsync(settings); Check(handler.Posts == 1 && handler.Title == edited.Text, "create uses latest desired text");
    now = now.AddSeconds(31); await sync.TickAsync(settings); now = now.AddSeconds(31); await sync.TickAsync(settings);
    Check(sync.GetStatus(settings).PendingCount == 0, "remote match acknowledges desired revision");
    var second = todo with { Timestamp = todo.Timestamp.AddMinutes(2), Text = "cancel before send" };
    sync.Apply(settings, null, second, null, null, false, () => Save(second));
    sync.Apply(settings, second, second, null, null, true, () => ObsidianNotesSync.DeleteTodo(root, second, "Notes"));
    int calls = handler.Calls; await sync.TickAsync(settings); Check(handler.Calls == calls && sync.GetStatus(settings).PendingCount == 0, "unsent create-delete cancels without HTTP");
    var otherMachine = New(Path.Combine(root, "other-machine.json"));
    Check(!otherMachine.EnableHere(settings), "second installation cannot silently take ownership");
    Check(otherMachine.EnableHere(settings, true) && !sync.GetStatus(settings).IsOwner, "explicit handoff revokes previous owner");
    var otherAccount = settings.HabiticaUserId; settings.HabiticaUserId = "33333333-3333-4333-8333-333333333333";
    Check(otherMachine.GetStatus(settings).PendingCount == 0, "account changes do not retarget old work"); settings.HabiticaUserId = otherAccount;
    File.WriteAllText(metadata, "broken json");
    var local = otherMachine.Apply(settings, null, second, null, null, false, () => new(true, ""));
    Check(local.LocalResult.Success && local.SyncWarning != null && File.ReadAllText(metadata) == "broken json", "corrupt metadata preserved and local save warning visible");
    await RecoveryChecks.Run();
    await MultiComputerChecks.Run();
    Console.WriteLine("Worker checks passed; no live requests.");
}
finally { Directory.Delete(root, true); }
static void Check(bool value, string label) { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); }
sealed class Secrets : IHabiticaSecretStore { string? token; public bool RejectWrites; public string Status => "test memory"; public string? Read(string account) => token; public bool Write(string account, string value) { if (RejectWrites) return false; token = value; return true; } public void Delete(string account) => token = null; }
sealed class Fake : HttpMessageHandler
{
    public int Calls, Posts; public string? Title;
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Calls++;
        if (request.Method == HttpMethod.Post)
        {
            Posts++; using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct)); Title = body.RootElement.GetProperty("text").GetString();
        }
        bool found = Title != null;
        string json = found ? JsonSerializer.Serialize(new { success = true, data = new { _id = "remote-1", text = Title, notes = "", completed = false, checklist = Array.Empty<object>() } }) : "{}";
        return new(found ? HttpStatusCode.OK : HttpStatusCode.NotFound) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
