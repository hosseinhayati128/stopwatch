using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using StopwatchOverlay;
using StopwatchOverlay.Habitica;

internal static class RecoveryChecks
{
    public static async Task Run()
    {
        await ImportProgressSurvivesRestart();
        await ImportTracksBlockedAndInterruptedItems();
        ImportDoesNotOverwriteNewerLocalIntent();
        await UnknownCreateAcrossRestart();
        await UnknownCompletion();
        await UnknownDelete();
        await ChildChangedDuringUnknownAdd(delete: false);
        await ChildChangedDuringUnknownAdd(delete: true);
        await ChildChangedDuringUnknownParentCreate(delete: false);
        await ChildChangedDuringUnknownParentCreate(delete: true);
        await SameTextExternalChildIsPreserved();
        await RateLimitDeadlineSurvivesRestart();
        await PendingIntentRecoveredBeforeNextRename();
        await InvalidInputEditUnblocks();
        await MissingRemoteRequiresExplicitRecreation();
        await MissingOrCorruptVaultDoesNotDelete();
    }

    private static async Task ImportProgressSurvivesRestart()
    {
        using var f = new Fixture();
        f.Add();
        f.Sync.ImportExisting(f.Settings);
        object Progress() => typeof(HabiticaSyncStatus).GetProperty("ImportProgress")?.GetValue(f.Sync.GetStatus(f.Settings))
            ?? throw new InvalidOperationException("Existing-item import must expose persisted batch progress");
        int Count(string name) => (int)Progress().GetType().GetProperty(name)!.GetValue(Progress())!;
        Require(Count("Total") == 1 && Count("Queued") == 1 && Count("Completed") == 0,
            "Import should report queued items before remote acknowledgement");
        f.Restart();
        Require(Count("Total") == 1 && Count("Completed") == 0, "Import progress was lost across restart");
        await f.Drain();
        Require(Count("Completed") == 1 && Count("Queued") == 0, "Acknowledgement did not complete imported item");
        var unrelated = f.Entry with { Timestamp = f.Entry.Timestamp.AddHours(1), Text = "unrelated later item" };
        f.Sync.Apply(f.Settings, null, unrelated, null, null, false,
            () => ObsidianNotesSync.AppendEntry(f.Root, unrelated.Type, unrelated.Text, "Notes", unrelated.Timestamp));
        Require(Count("Total") == 1 && Count("Completed") == 1, "Unrelated work changed the import denominator");
        string originalAccount = f.Settings.HabiticaUserId;
        f.Settings.HabiticaUserId = "33333333-3333-4333-8333-333333333333";
        Require(typeof(HabiticaSyncStatus).GetProperty("ImportProgress")!.GetValue(f.Sync.GetStatus(f.Settings)) == null,
            "Another account inherited import progress");
        f.Settings.HabiticaUserId = originalAccount;
        f.Sync.ImportExisting(f.Settings);
        Require(f.Server.Creates == 1 && Count("Total") == 2, "Repeat import should preserve remote identities");
        Console.WriteLine("PASS import batch progress persists and excludes unrelated work/accounts");
    }

    private static async Task ImportTracksBlockedAndInterruptedItems()
    {
        using var f = new Fixture();
        f.Add();
        f.Sync.ImportExisting(f.Settings);
        await f.Drain();
        f.ChangeText("superseding revision awaiting acknowledgement");
        f.Sync.ImportExisting(f.Settings);
        Require(f.Sync.GetStatus(f.Settings).ImportProgress is { Total: 1, Completed: 0, Queued: 1 },
            "Earlier acknowledgements must not complete the newly captured import revision");
        f.Server.RejectNextUpdate = true;
        await f.Until(() => f.Server.RejectedUpdates == 1);
        Require(f.Sync.GetStatus(f.Settings).ImportProgress is { Total: 1, Completed: 0, Blocked: 1, Queued: 0 },
            "Rejected imported revisions must remain visibly blocked");
        var v = JsonSerializer.Deserialize<HabiticaIntegration.Vault>(File.ReadAllText(f.Metadata))!;
        v.Imports[f.Settings.HabiticaUserId].IsQueuing = true;
        v.Imports[f.Settings.HabiticaUserId].Items.Add(new() { ItemId = Guid.NewGuid() });
        File.WriteAllText(f.Metadata, JsonSerializer.Serialize(v));
        f.Restart();
        Require(f.Sync.GetStatus(f.Settings).ImportProgress is { Total: 2, Blocked: 1, Completed: 0,
            IsQueuing: false, Error: not null }, "An interrupted partial enrollment must preserve its batch and show recovery guidance");
        Console.WriteLine("PASS import tracks captured revisions, blocked tasks and interrupted preparation");
    }

    private static void ImportDoesNotOverwriteNewerLocalIntent()
    {
        using var f = new Fixture();
        f.Add();
        // Model the state an importer sees when its old source snapshot races a local edit:
        // the journal has advanced but the import candidate still contains the earlier entry.
        var newer = f.Entry with { Text = "newer local desired text" };
        var v = JsonSerializer.Deserialize<HabiticaIntegration.Vault>(File.ReadAllText(f.Metadata))!;
        var item = v.Items.Single();
        item.Entry = newer;
        item.Targets[f.Settings.HabiticaUserId].Entry = newer;
        item.Targets[f.Settings.HabiticaUserId].Revision++;
        File.WriteAllText(f.Metadata, JsonSerializer.Serialize(v));
        bool rejected = false;
        try { f.Sync.ImportExisting(f.Settings); } catch (InvalidOperationException) { rejected = true; }
        var saved = JsonSerializer.Deserialize<HabiticaIntegration.Vault>(File.ReadAllText(f.Metadata))!;
        Require(rejected && saved.Items.Single().Targets[f.Settings.HabiticaUserId].Entry!.Text == newer.Text
            && f.Sync.GetStatus(f.Settings).ImportProgress is { Total: 1, Queued: 0, Error: not null },
            "A stale import candidate must preserve newer local intent and report incomplete preparation");
        Console.WriteLine("PASS stale import candidate preserves newer local intent");
    }

    private static async Task UnknownCreateAcrossRestart()
    {
        using var f = new Fixture();
        f.Server.LoseCreate = true;
        f.Add();
        await f.Until(() => f.Server.Creates == 1);
        f.Restart();
        await f.Drain();
        Require(f.Server.Creates == 1 && f.Server.Remote != null,
            "Unknown create must resolve its alias after restart without posting twice");
        Console.WriteLine("PASS uncertain create reconciles across restart");

        // A copied vault and new local delivery store must recover the same link.
        f.ChangeText("changed after intentional handoff");
        f.Restart(otherInstallation: true);
        Require(f.Sync.EnableHere(f.Settings, true), "Explicit handoff failed");
        await f.Drain();
        Require(f.Server.Creates == 1 && f.Server.Remote!.Text == f.Entry.Text,
            "Intentional handoff must resolve the existing alias/link");
        Console.WriteLine("PASS handoff reuses remote task identity");
    }

    private static async Task MissingRemoteRequiresExplicitRecreation()
    {
        using var f = new Fixture();
        f.Add([new HabiticaSubtask(Guid.NewGuid(), "owned child", false)]);
        await f.Drain();
        f.Server.Remote = null;
        f.ChangeText("local task after remote deletion");
        await f.Until(() => f.Sync.CountMissing(f.Settings) == 1);
        Require(f.Server.Creates == 1 && f.Sync.GetStatus(f.Settings).PendingCount == 1, "Missing linked task was silently recreated");
        f.Sync.RecreateMissing(f.Settings);
        await f.Drain();
        Require(f.Server.Creates == 2 && f.Server.Remote!.Children.Count == 1 && f.Sync.CountMissing(f.Settings) == 0, "Explicit recreation did not restore task and checklist");
        Console.WriteLine("PASS missing linked task recreates only after explicit authorization");
    }

    private static async Task UnknownCompletion()
    {
        using var f = new Fixture();
        f.Add();
        await f.Drain();
        f.Server.LoseScore = true;
        var completed = f.Entry with { IsCompleted = true };
        var changed = f.Sync.Apply(f.Settings, f.Entry, completed, null, null, false,
            () => ObsidianNotesSync.SetTodoCompleted(f.Root, f.Entry, true, "Notes"));
        Require(changed.LocalResult.Success, "Local completion failed");
        f.Entry = completed;
        await f.Until(() => f.Server.ScoresUp == 1);
        f.Restart();
        f.Server.OmitCompletedNext = true;
        await f.Drain();
        Require(f.Server.ScoresUp == 1 && f.Server.Remote!.Completed,
            "Unknown completion must read completed state before awarding another score");
        Console.WriteLine("PASS uncertain completion awards once after restart");
    }

    private static async Task UnknownDelete()
    {
        using var f = new Fixture();
        f.Add();
        await f.Drain();
        f.Server.LoseDelete = true;
        var removed = f.Sync.Apply(f.Settings, f.Entry, f.Entry, null, null, true,
            () => ObsidianNotesSync.DeleteTodo(f.Root, f.Entry, "Notes"));
        Require(removed.LocalResult.Success, "Local deletion failed");
        await f.Until(() => f.Server.Deletes == 1);
        f.Restart();
        await f.Drain();
        Require(f.Server.Deletes == 1 && f.Server.Remote == null,
            "Confirmed absence must satisfy an uncertain delete without replay");
        Console.WriteLine("PASS uncertain deletion reconciles confirmed absence");
    }

    private static async Task ChildChangedDuringUnknownAdd(bool delete)
    {
        using var f = new Fixture();
        f.Add();
        await f.Drain();
        var child = new HabiticaSubtask(Guid.NewGuid(), "original child", false);
        Require(f.Sync.SetChildren(f.Settings, f.Entry, [child]).SyncWarning == null,
            "Could not save first child");
        f.Server.LoseChildAdd = true;
        await f.Until(() => f.Server.ChildAdds == 1);
        var desired = delete ? Array.Empty<HabiticaSubtask>() : [child with { Text = "renamed child", Completed = true }];
        Require(f.Sync.SetChildren(f.Settings, f.Entry, desired).SyncWarning == null,
            "Could not change child while its create outcome was unknown");
        f.Restart();
        await f.Drain();
        Require(f.Server.ChildAdds == 1, "Unknown child add was posted twice after changing desired state");
        Require(delete ? f.Server.Remote!.Children.Count == 0
            : f.Server.Remote!.Children.Count == 1 && f.Server.Remote.Children[0].Text == "renamed child"
                && f.Server.Remote.Children[0].Completed,
            delete ? "Deleted child was orphaned after uncertain add" : "Renamed child did not converge after uncertain add");
        Console.WriteLine("PASS uncertain child add reconciles before " + (delete ? "delete" : "rename"));
    }

    private static async Task RateLimitDeadlineSurvivesRestart()
    {
        using var f = new Fixture();
        f.Add();
        f.Server.RateLimitNext = true;
        await f.Sync.TickAsync(f.Settings);
        Require(f.Server.Calls == 1, "Expected one rate limited lookup");
        f.Restart();
        var deadline = typeof(HabiticaSyncStatus).GetProperty("NextRequestUtc")?.GetValue(f.Sync.GetStatus(f.Settings));
        Require(deadline is DateTime next && next == f.Now.AddSeconds(120).UtcDateTime,
            "Status must expose the persisted server retry deadline");
        f.Now = f.Now.AddSeconds(119);
        await f.Sync.TickAsync(f.Settings);
        Require(f.Server.Calls == 1, "Restart ignored the longer Retry-After deadline");
        f.Now = f.Now.AddSeconds(1);
        await f.Sync.TickAsync(f.Settings);
        Require(f.Server.Calls == 2, "Lookup was not resumed at the persisted Retry-After deadline");
        await f.Drain();
        Require(f.Server.RequestTimes.Zip(f.Server.RequestTimes.Skip(1)).All(x => x.Second - x.First >= TimeSpan.FromSeconds(30)),
            "Automated calls occurred less than 30 seconds apart");
        Console.WriteLine("PASS 429 deadline persists across restart and preserves request spacing");
    }

    private static async Task ChildChangedDuringUnknownParentCreate(bool delete)
    {
        using var f = new Fixture();
        var child = new HabiticaSubtask(Guid.NewGuid(), "child included in uncertain parent create", false);
        f.Server.LoseCreate = true;
        f.Add([child]);
        await f.Until(() => f.Server.Creates == 1);
        var desired = delete ? Array.Empty<HabiticaSubtask>() : [child with { Text = "renamed after parent create" }];
        Require(f.Sync.SetChildren(f.Settings, f.Entry, desired).SyncWarning == null,
            "Could not change child during uncertain parent create");
        f.Restart();
        await f.Drain();
        Require(f.Server.Creates == 1 && f.Server.ChildAdds == 0,
            "An uncertain parent create caused duplicate task or checklist creates");
        Require(delete ? f.Server.Remote!.Children.Count == 0
            : f.Server.Remote!.Children.Count == 1 && f.Server.Remote.Children[0].Text == "renamed after parent create",
            "Children included in uncertain parent create were not recovered before later edits");
        Console.WriteLine("PASS uncertain parent create recovers child IDs before " + (delete ? "delete" : "rename"));
    }

    private static async Task SameTextExternalChildIsPreserved()
    {
        using var f = new Fixture();
        f.Add();
        await f.Drain();
        var external = new Child { Text = "same text", Completed = true };
        f.Server.Remote!.Children.Add(external);
        var child = new HabiticaSubtask(Guid.NewGuid(), "same text", false);
        Require(f.Sync.SetChildren(f.Settings, f.Entry, [child]).SyncWarning == null, "Could not save own child");
        await f.Drain();
        Require(f.Server.ChildAdds == 1 && f.Server.Remote.Children.Count == 2 && external.Completed,
            "A fresh local child must not adopt or overwrite an external same-text checklist item");
        Console.WriteLine("PASS independent same-text remote child remains untouched");
    }

    private static async Task PendingIntentRecoveredBeforeNextRename()
    {
        using var f = new Fixture();
        f.Add();
        Guid originalId = f.Sync.GetDetails(f.Settings, f.Entry).Id;
        var after = f.Entry with { Text = "rename saved before metadata finalize" };
        var vault = JsonSerializer.Deserialize<HabiticaIntegration.Vault>(File.ReadAllText(f.Metadata))!;
        vault.Intents.Add(new HabiticaIntegration.Intent { ItemId = originalId, Before = f.Entry, After = after,
            Account = f.Settings.HabiticaUserId, Enroll = true });
        File.WriteAllText(f.Metadata, JsonSerializer.Serialize(vault));
        Require(ObsidianNotesSync.UpdateTodoText(f.Root, f.Entry, after.Text, "Notes").Success,
            "Could not simulate locally committed rename");
        f.Entry = after;
        f.Restart();
        f.ChangeText("next rename before worker tick");
        Require(f.Sync.GetDetails(f.Settings, f.Entry).Id == originalId,
            "An unfinalized previous rename created a second task identity");
        vault = JsonSerializer.Deserialize<HabiticaIntegration.Vault>(File.ReadAllText(f.Metadata))!;
        Require(vault.Items.Count == 1 && vault.Intents.Count == 0,
            "Previous mutation intent was not recovered before beginning the next mutation");
        await f.Drain();
        Require(f.Server.Creates == 1 && f.Server.Remote!.Text == f.Entry.Text,
            "Recovered rapid rename did not converge to one latest remote task");
        Console.WriteLine("PASS pending journal intent recovers before next rename");
    }

    private static async Task InvalidInputEditUnblocks()
    {
        using var f = new Fixture();
        f.Add();
        await f.Drain();
        f.ChangeText("input rejected by fake server");
        f.Server.RejectNextUpdate = true;
        await f.Until(() => f.Server.RejectedUpdates == 1);
        int rejectedCalls = f.Server.Calls;
        await f.Sync.TickAsync(f.Settings);
        Require(f.Server.Calls == rejectedCalls && f.Sync.GetStatus(f.Settings).PendingCount == 1,
            "Invalid input should stay visibly pending without automatic retry");
        f.ChangeText("corrected task input");
        await f.Drain();
        Require(f.Server.Remote!.Text == "corrected task input",
            "Editing rejected task input did not unblock its pending work");
        Console.WriteLine("PASS editing rejected input unblocks pending task");
    }

    private static async Task MissingOrCorruptVaultDoesNotDelete()
    {
        using var f = new Fixture();
        f.Add();
        await f.Drain();
        int calls = f.Server.Calls;
        string folder = Path.Combine(f.Root, "Notes"), moved = Path.Combine(f.Root, "UnavailableNotes");
        Directory.Move(folder, moved);
        await f.Sync.TickAsync(f.Settings);
        Require(f.Server.Calls == calls && f.Server.Deletes == 0 && f.Server.Remote != null,
            "An unavailable vault folder must not infer remote deletion");
        Directory.Move(moved, folder);
        File.WriteAllText(f.Metadata, "broken metadata retained verbatim");
        var after = f.Entry with { Text = "local edit despite corrupt metadata" };
        var result = f.Sync.Apply(f.Settings, f.Entry, after, null, null, false,
            () => ObsidianNotesSync.UpdateTodoText(f.Root, f.Entry, after.Text, "Notes"));
        Require(result.LocalResult.Success && result.SyncWarning != null,
            "Corrupt sync metadata must allow local save with an explicit sync warning");
        await f.Sync.TickAsync(f.Settings);
        Require(File.ReadAllText(f.Metadata) == "broken metadata retained verbatim" && f.Server.Calls == calls
            && f.Server.Deletes == 0 && ObsidianNotesSync.LoadAllNotes(f.Root, "Notes").Single().Text == after.Text,
            "Corrupt metadata was overwritten or caused remote work/local data loss");
        Console.WriteLine("PASS missing and corrupt metadata preserve remote task and local edits");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly string Root = Path.Combine(Path.GetTempPath(), "StopwatchHabiticaRecovery-" + Guid.NewGuid().ToString("N"));
        public readonly AppSettings Settings;
        public readonly FakeServer Server;
        private readonly TestSecrets secrets = new();
        private readonly HabiticaApiClient api;
        private string localPath;
        public DateTimeOffset Now = new(2026, 10, 24, 0, 0, 0, TimeSpan.Zero);
        public NoteEntry Entry = new(NoteType.Todo, new DateTime(2026, 10, 24, 9, 30, 0), "recovery test task");
        public HabiticaIntegration Sync;
        public string Metadata => Path.Combine(Root, "Notes", ".stopwatch-habitica.json");

        public Fixture()
        {
            Directory.CreateDirectory(Root);
            Settings = new() { ObsidianVaultFolder = Root, NotesSubfolder = "Notes",
                HabiticaUserId = "11111111-1111-4111-8111-111111111111",
                HabiticaClientId = "22222222-2222-4222-8222-222222222222-Stopwatch" };
            Server = new(() => Now);
            api = new(new HttpClient(Server));
            localPath = Path.Combine(Root, "delivery.json");
            Sync = New();
            Sync.SetToken(Settings.HabiticaUserId, "fake-token-only");
            Require(Sync.EnableHere(Settings), "Could not enable test installation");
        }

        private HabiticaIntegration New() => new(api, secrets, localPath, () => Now);
        public void Restart(bool otherInstallation = false)
        {
            if (otherInstallation) localPath = Path.Combine(Root, "other-delivery.json");
            Sync = New();
        }
        public void Add(IReadOnlyList<HabiticaSubtask>? children = null)
        {
            var r = Sync.Apply(Settings, null, Entry, null, children ?? [], false,
                () => ObsidianNotesSync.AppendEntry(Root, Entry.Type, Entry.Text, "Notes", Entry.Timestamp));
            Require(r.LocalResult.Success && r.SyncWarning == null, "Could not create test task");
        }
        public void ChangeText(string text)
        {
            var next = Entry with { Text = text };
            var r = Sync.Apply(Settings, Entry, next, null, null, false,
                () => ObsidianNotesSync.UpdateTodoText(Root, Entry, text, "Notes"));
            Require(r.LocalResult.Success && r.SyncWarning == null, "Could not rename task");
            Entry = next;
        }
        public async Task Until(Func<bool> done)
        {
            for (int n = 0; n < 30 && !done(); n++)
            {
                await Sync.TickAsync(Settings);
                Now = Now.AddSeconds(61);
            }
            Require(done(), "Expected request did not occur: " + Sync.GetStatus(Settings).Message);
        }
        public async Task Drain()
        {
            await Until(() => Sync.GetStatus(Settings).PendingCount == 0);
        }
        public void Dispose() => Directory.Delete(Root, recursive: true);
    }

    private sealed class TestSecrets : IHabiticaSecretStore
    {
        private readonly Dictionary<string, string> tokens = [];
        public string Status => "fake in-memory store";
        public string? Read(string account) => tokens.GetValueOrDefault(account);
        public bool Write(string account, string token) { tokens[account] = token; return true; }
        public void Delete(string account) => tokens.Remove(account);
    }

    private sealed class Remote
    {
        public string Id = "fake-remote-task", Alias = "", Text = "", Notes = "";
        public bool Completed;
        public readonly List<Child> Children = [];
    }
    private sealed class Child
    {
        public string Id = Guid.NewGuid().ToString("N"), Text = "";
        public bool Completed;
    }

    private sealed class FakeServer(Func<DateTimeOffset> now) : HttpMessageHandler
    {
        public Remote? Remote;
        public bool LoseCreate, LoseScore, LoseDelete, LoseChildAdd, RateLimitNext, OmitCompletedNext, RejectNextUpdate;
        public int Calls, Creates, ScoresUp, Deletes, ChildAdds, RejectedUpdates;
        public readonly List<DateTimeOffset> RequestTimes = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Require(request.RequestUri!.Host == "habitica.com", "Unexpected fake request host");
            Calls++; RequestTimes.Add(now());
            if (RateLimitNext)
            {
                RateLimitNext = false;
                var limited = Json(HttpStatusCode.TooManyRequests, "{}");
                limited.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.FromSeconds(120));
                return limited;
            }
            string path = request.RequestUri.AbsolutePath;
            JsonElement body = default;
            if (request.Content != null)
                body = JsonSerializer.Deserialize<JsonElement>(await request.Content.ReadAsStringAsync(ct));
            if (path.EndsWith("/tasks/user") && request.Method == HttpMethod.Post)
            {
                Creates++;
                Remote = new() { Alias = body.GetProperty("alias").GetString()!, Text = body.GetProperty("text").GetString()!, Notes = body.GetProperty("notes").GetString()! };
                if (body.TryGetProperty("checklist", out var children))
                    foreach (var c in children.EnumerateArray()) Remote.Children.Add(new() { Text = c.GetProperty("text").GetString()!, Completed = c.GetProperty("completed").GetBoolean() });
                if (LoseCreate) { LoseCreate = false; throw new HttpRequestException("simulated lost create response"); }
            }
            else if (Remote == null) return Json(HttpStatusCode.NotFound, "{}");
            else if (request.Method == HttpMethod.Get && Uri.UnescapeDataString(path.Split('/')[^1]) != Remote.Id
                && Uri.UnescapeDataString(path.Split('/')[^1]) != Remote.Alias)
                return Json(HttpStatusCode.NotFound, "{}");
            else if (path.Contains("/score/"))
            {
                bool complete = path.EndsWith("/up");
                if (complete) ScoresUp++;
                Remote.Completed = complete;
                if (LoseScore) { LoseScore = false; throw new HttpRequestException("simulated lost score response"); }
                return Json(HttpStatusCode.OK, "{\"success\":true,\"data\":{}}");
            }
            else if (path.EndsWith("/checklist") && request.Method == HttpMethod.Post)
            {
                ChildAdds++;
                Remote.Children.Add(new() { Text = body.GetProperty("text").GetString()!, Completed = body.GetProperty("completed").GetBoolean() });
                if (LoseChildAdd) { LoseChildAdd = false; throw new HttpRequestException("simulated lost child response"); }
            }
            else if (path.Contains("/checklist/"))
            {
                var child = Remote.Children.Single(c => c.Id == path.Split('/')[^1]);
                if (request.Method == HttpMethod.Delete) Remote.Children.Remove(child);
                else { child.Text = body.GetProperty("text").GetString()!; child.Completed = body.GetProperty("completed").GetBoolean(); }
            }
            else if (request.Method == HttpMethod.Delete)
            {
                Deletes++; Remote = null;
                if (LoseDelete) { LoseDelete = false; throw new HttpRequestException("simulated lost delete response"); }
                return Json(HttpStatusCode.OK, "{\"success\":true,\"data\":{}}");
            }
            else if (request.Method == HttpMethod.Put)
            {
                if (RejectNextUpdate)
                {
                    RejectNextUpdate = false; RejectedUpdates++;
                    return Json(HttpStatusCode.BadRequest, "{}");
                }
                Remote.Text = body.GetProperty("text").GetString()!;
                Remote.Notes = body.GetProperty("notes").GetString()!;
            }
            return TaskResponse();
        }
        private HttpResponseMessage TaskResponse()
        {
            if (OmitCompletedNext)
            {
                OmitCompletedNext = false;
                return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new { success = true,
                    data = new { _id = Remote!.Id, alias = Remote.Alias, text = Remote.Text, notes = Remote.Notes,
                        checklist = Remote.Children.Select(c => new { id = c.Id, text = c.Text, completed = c.Completed }).ToArray() } }));
            }
            return Json(HttpStatusCode.OK, JsonSerializer.Serialize(new
            {
                success = true,
                data = new { _id = Remote!.Id, alias = Remote.Alias, text = Remote.Text, notes = Remote.Notes, completed = Remote.Completed,
                    checklist = Remote.Children.Select(c => new { id = c.Id, text = c.Text, completed = c.Completed }).ToArray() }
            }));
        }
        private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
        { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    }
}
