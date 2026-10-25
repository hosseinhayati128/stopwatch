using System.Net;
using System.Text.Json;
using StopwatchOverlay;
using StopwatchOverlay.Habitica;

static class BatchChecks
{
    public static async Task Run()
    {
        await UncertainChecks();
        await ChildAndRevisionChecks();
        string root = Path.Combine(Path.GetTempPath(), "HabiticaBatch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var s = new AppSettings { ObsidianVaultFolder = root, NotesSubfolder = "Notes", HabiticaUserId = "11111111-1111-4111-8111-111111111111", HabiticaClientId = "22222222-2222-4222-8222-222222222222-Stopwatch" };
            var h = new BatchHandler();
            using var api = new HabiticaApiClient(new HttpClient(h));
            var sync = new HabiticaIntegration(api, new Secrets(), Path.Combine(root, "local.json"), () => now);
            sync.SetToken(s.HabiticaUserId, "test"); sync.EnableHere(s);
            for (int n = 0; n < 31; n++)
            {
                var entry = new NoteEntry(NoteType.Todo, new DateTime(2026, 10, 24).AddMinutes(n), "Batch " + n);
                sync.Apply(s, null, entry, null, null, false, () => ObsidianNotesSync.AppendEntry(root, entry.Type, entry.Text, "Notes", entry.Timestamp));
            }
            await sync.TickAsync(s);
            Check(h.Posts == 1 && h.Counts.SequenceEqual(new[] { 20 }), "first batch creates twenty without preliminary lookups");
            await sync.TickAsync(s); Check(h.Posts == 1, "batch honors thirty second spacing");
            now = now.AddSeconds(30); await sync.TickAsync(s);
            Check(h.Posts == 2 && h.Counts.SequenceEqual(new[] { 20, 11 }), "thirty one tasks use two bulk requests");
            for (int n = 0; n < 40; n++) { now = now.AddSeconds(30); await sync.TickAsync(s); }
            Check(sync.GetStatus(s).PendingCount == 0 && h.Posts == 2 && h.Gets == 0, "reordered authoritative responses acknowledge without redundant reads");
        }
        finally { Directory.Delete(root, true); }
    }
    public static async Task ChildAndRevisionChecks()
    {
        foreach (bool stale in new[] { false, true })
        {
            string root = Path.Combine(Path.GetTempPath(), "HabiticaChildren-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var now = DateTimeOffset.UtcNow;
                var s = new AppSettings { ObsidianVaultFolder = root, NotesSubfolder = "Notes", HabiticaUserId = "11111111-1111-4111-8111-111111111111", HabiticaClientId = "22222222-2222-4222-8222-222222222222-Stopwatch" };
                var h = new BatchHandler(); using var api = new HabiticaApiClient(new HttpClient(h));
                var sync = new HabiticaIntegration(api, new Secrets(), Path.Combine(root, "local.json"), () => now);
                sync.SetToken(s.HabiticaUserId, "test"); sync.EnableHere(s);
                var first = new NoteEntry(NoteType.Todo, new DateTime(2026, 10, 24), "Parent", !stale);
                var child = new HabiticaSubtask(Guid.NewGuid(), "Child", true);
                sync.Apply(s, null, first, null, new[] { child }, false, () =>
                {
                    var saved = ObsidianNotesSync.AppendEntry(root, first.Type, first.Text, "Notes", first.Timestamp);
                    return first.IsCompleted && saved.Success ? ObsidianNotesSync.SetTodoCompleted(root, first with { IsCompleted = false }, true, "Notes") : saved;
                });
                var second = first with { Text = "Other", Timestamp = first.Timestamp.AddMinutes(1), IsCompleted = false };
                sync.Apply(s, null, second, null, null, false, () => ObsidianNotesSync.AppendEntry(root, second.Type, second.Text, "Notes", second.Timestamp));
                if (stale) h.AfterCreate = () =>
                {
                    var edited = first with { Text = "Edited while HTTP was in flight" };
                    sync.Apply(s, first, edited, null, null, false, () => ObsidianNotesSync.UpdateTodoText(root, first, edited.Text, "Notes"));
                };
                await sync.TickAsync(s);
                Check(h.Scores == 0, "batch creation never scores completed parents implicitly");
                await sync.TickAsync(s);
                Check(sync.GetStatus(s).PendingCount == 2, "captured response cannot acknowledge a pending completion or newer revision");
                for (int n = 0; n < 12; n++) { now = now.AddSeconds(30); await sync.TickAsync(s); }
                Check(sync.GetStatus(s).PendingCount == 0 && h.Posts == 1 && h.Gets == (stale ? 0 : 1), "batch child IDs reconcile without duplicate child creates");
                Check(stale ? h.Updates == 1 && h.Scores == 0 : h.Scores == 1 && h.Updates == 0,
                    stale ? "concurrent edit is delivered after stale create response" : "completed parent uses exactly one separate score request");
            }
            finally { Directory.Delete(root, true); }
        }
    }
    public static async Task UncertainChecks()
    {
        foreach (string mode in new[] { "network", "partial", "duplicate" })
        {
            string root = Path.Combine(Path.GetTempPath(), "HabiticaUncertain-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var now = DateTimeOffset.UtcNow;
                var s = new AppSettings { ObsidianVaultFolder = root, NotesSubfolder = "Notes", HabiticaUserId = "11111111-1111-4111-8111-111111111111", HabiticaClientId = "22222222-2222-4222-8222-222222222222-Stopwatch" };
                var handler = new BatchHandler { Mode = mode };
                using var api = new HabiticaApiClient(new HttpClient(handler));
                var secrets = new Secrets();
                string local = Path.Combine(root, "local.json");
                HabiticaIntegration New() => new(api, secrets, local, () => now);
                var sync = New(); sync.SetToken(s.HabiticaUserId, "test"); sync.EnableHere(s);
                for (int n = 0; n < 3; n++)
                {
                    var e = new NoteEntry(NoteType.Todo, new DateTime(2026, 10, 24).AddMinutes(n), "Uncertain " + n);
                    sync.Apply(s, null, e, null, null, false, () => ObsidianNotesSync.AppendEntry(root, e.Type, e.Text, "Notes", e.Timestamp));
                }
                await sync.TickAsync(s);
                Check(handler.Posts == 1 && sync.GetStatus(s).PendingCount == 3, mode + " response retains all pending revisions");
                var metadata = Path.Combine(root, "Notes", ".stopwatch-habitica.json");
                var vault = JsonSerializer.Deserialize<HabiticaIntegration.Vault>(File.ReadAllText(metadata))!;
                Check(vault.Items.All(i => i.Targets[s.HabiticaUserId].Attempted && i.Targets[s.HabiticaUserId].RemoteId == null), mode + " retains durable aliases without trusting partial bindings");
                sync = New(); await sync.TickAsync(s);
                Check(handler.Gets == 0, mode + " restart respects persisted retry deadline");
                for (int n = 0; n < 20; n++) { now = now.AddSeconds(61); await sync.TickAsync(s); }
                Check(handler.Posts == 1 && handler.Gets == 3 && sync.GetStatus(s).PendingCount == 0, mode + " restart reconciles aliases without duplicate POST");
            }
            finally { Directory.Delete(root, true); }
        }
    }
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); Console.WriteLine("PASS " + label); }
    sealed class BatchHandler : HttpMessageHandler
    {
        public int Posts, Gets, Scores, Updates;
        public Action? AfterCreate;
        public string Mode = "";
        readonly Dictionary<string, JsonElement> stored = [];
        public List<int> Counts = [];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (request.RequestUri!.AbsolutePath.Contains("/score/") || request.Method == HttpMethod.Put)
            {
                var segments = request.RequestUri.AbsolutePath.Split('/');
                string id = request.Method == HttpMethod.Put ? segments[^1] : segments[^3];
                string key = stored.Single(k => k.Value.GetProperty("_id").GetString() == id).Key;
                var task = System.Text.Json.Nodes.JsonNode.Parse(stored[key].GetRawText())!;
                if (request.Method == HttpMethod.Put)
                {
                    Updates++; using var update = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
                    task["text"] = update.RootElement.GetProperty("text").GetString(); task["notes"] = update.RootElement.GetProperty("notes").GetString();
                }
                else { Scores++; task["completed"] = true; }
                stored[key] = JsonSerializer.SerializeToElement(task);
                return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { success = true, data = stored[key] })) };
            }
            if (request.Method != HttpMethod.Post)
            {
                Gets++; string alias = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath.Split('/')[^1]);
                if (!stored.ContainsKey(alias)) alias = stored.FirstOrDefault(k => k.Value.GetProperty("_id").GetString() == alias).Key ?? alias;
                return stored.TryGetValue(alias, out var task)
                    ? new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { success = true, data = task })) }
                    : new(HttpStatusCode.NotFound) { Content = new StringContent("{}") };
            }
            Posts++;
            using var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(ct));
            var tasks = body.RootElement.EnumerateArray().ToArray(); Counts.Add(tasks.Length);
            var data = tasks.Reverse().Select(t => new { _id = "remote-" + t.GetProperty("alias").GetString(), alias = t.GetProperty("alias").GetString(), text = t.GetProperty("text").GetString(), notes = t.GetProperty("notes").GetString(), completed = false, checklist = t.GetProperty("checklist").EnumerateArray().Select((c, n) => new { id = "child-" + n, text = c.GetProperty("text").GetString(), completed = c.GetProperty("completed").GetBoolean() }).ToArray() }).ToArray();
            foreach (var task in data) stored[task.alias!] = JsonSerializer.SerializeToElement(task);
            AfterCreate?.Invoke();
            if (Mode == "network") throw new HttpRequestException("Synthetic response loss after create");
            if (Mode == "partial") data = data.Take(1).ToArray();
            if (Mode == "duplicate") data = Enumerable.Repeat(data[0], data.Length).ToArray();
            return new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(new { success = true, data })) };
        }
    }
}
