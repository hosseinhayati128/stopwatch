using System.Net;
using System.Text;
using System.Text.Json;
using StopwatchOverlay;
using StopwatchOverlay.Habitica;

internal static class MultiComputerChecks
{
    public static async Task Run()
    {
        using var world = new World();
        var a = world.Windows; var b = world.Ubuntu;
        Check(a.Sync.GetStatus(a.Settings).IsOwner && b.Sync.GetStatus(b.Settings).IsOwner,
            "two installations with separate vaults enable together on one account");
        a.Add(); b.Add();
        Check(a.Id != b.Id, "identical titles and timestamps in separate vaults receive distinct task identities");
        await world.Drain(a, b);
        Check(world.Server.Tasks.Count == 2 && world.Server.Creates == 2, "concurrent clients create exactly their own remote tasks");
        Check(a.Secret.Read(a.Settings.HabiticaUserId) != b.Secret.Read(b.Settings.HabiticaUserId), "each computer retains its own credential store");

        a.Rename("Windows edited its task"); b.Complete();
        await world.Drain(a, b);
        Check(world.Server.Get(a.Alias).Text == a.Entry.Text && !world.Server.Get(a.Alias).Completed &&
            world.Server.Get(b.Alias).Text == b.Entry.Text && world.Server.Get(b.Alias).Completed && world.Server.Scores == 1,
            "concurrent edit and completion affect only the originating computer's task");
        var child = new HabiticaSubtask(Guid.NewGuid(), "Windows-only checklist", true);
        var saved = a.Sync.SetChildren(a.Settings, a.Entry, [child]);
        Check(saved.LocalResult.Success && saved.SyncWarning == null, "local checklist mutation is queued");
        await world.Drain(a, b);
        Check(world.Server.Get(a.Alias).Children.Single().Completed && world.Server.Get(b.Alias).Children.Count == 0,
            "checklist sync stays isolated between computers");

        a.Restart(); b.Restart();
        a.Rename("Windows after restart"); b.Rename("Ubuntu after restart");
        await world.Drain(a, b);
        Check(world.Server.Creates == 2 && a.Sync.GetStatus(a.Settings).IsOwner && b.Sync.GetStatus(b.Settings).IsOwner,
            "restarting both installations preserves their own ownership and remote links");

        a.Sync.Disable(a.Settings);
        a.Rename("Windows changed while paused"); b.Rename("Ubuntu continues while Windows pauses");
        await world.Drain(b);
        Check(a.Sync.GetStatus(a.Settings).PendingCount == 1 && world.Server.Get(b.Alias).Text == b.Entry.Text && world.Server.Get(a.Alias).Text != a.Entry.Text,
            "pausing one computer does not stop the other computer");
        Check(a.Sync.EnableHere(a.Settings), "paused computer re-enables without transfer");
        await world.Drain(a, b);

        a.Handler.RateLimitNext = true;
        a.Rename("Windows waits for quota"); b.Rename("Ubuntu independent retry queue");
        await Task.WhenAll(a.Sync.TickAsync(a.Settings), b.Sync.TickAsync(b.Settings));
        int aCalls = a.Handler.Times.Count;
        Check(a.Handler.Limited == 1, "rate-limited client receives its own retry deadline");
        world.Now = world.Now.AddSeconds(31);
        await Task.WhenAll(a.Sync.TickAsync(a.Settings), b.Sync.TickAsync(b.Settings));
        world.Now = world.Now.AddSeconds(31);
        await b.Sync.TickAsync(b.Settings); // confirm update
        await b.Sync.TickAsync(b.Settings); // acknowledge without another HTTP call
        Check(a.Handler.Times.Count == aCalls && b.Sync.GetStatus(b.Settings).PendingCount == 0,
            "one client's rate delay does not block another eligible client");
        await world.Drain(a, b);

        a.Handler.Offline = true;
        a.Rename("Windows offline edit"); b.Rename("Ubuntu remains online");
        await Task.WhenAll(a.Sync.TickAsync(a.Settings), b.Sync.TickAsync(b.Settings));
        await world.Drain(b);
        Check(a.Sync.GetStatus(a.Settings).PendingCount == 1 && b.Sync.GetStatus(b.Settings).PendingCount == 0,
            "offline changes stay queued locally while the other computer syncs");
        a.Handler.Offline = false;
        await world.Drain(a, b);

        a.Delete(); b.Rename("Ubuntu survives Windows deletion");
        await world.Drain(a, b);
        Check(world.Server.Tasks.Count == 1 && world.Server.Get(b.Alias).Text == b.Entry.Text && world.Server.Creates == 2,
            "deleting on one computer leaves the other computer's linked task intact");
        foreach (var device in new[] { a, b })
            Check(device.Handler.Times.Zip(device.Handler.Times.Skip(1)).All(pair => pair.Second - pair.First >= TimeSpan.FromSeconds(30)),
                device.Name + " preserves per-installation HTTP spacing");
    }

    private static void Check(bool value, string label)
    { if (!value) throw new Exception(label); Console.WriteLine("PASS " + label); }

    private sealed class World : IDisposable
    {
        private readonly string root = Path.Combine(Path.GetTempPath(), "HabiticaMultiComputer-" + Guid.NewGuid().ToString("N"));
        public DateTimeOffset Now = new(2026, 10, 24, 0, 0, 0, TimeSpan.Zero);
        public readonly FakeServer Server = new();
        public Device Windows { get; }
        public Device Ubuntu { get; }
        public World()
        { Directory.CreateDirectory(root); Windows = new(this, Path.Combine(root, "windows"), "Windows"); Ubuntu = new(this, Path.Combine(root, "ubuntu"), "Ubuntu"); }
        public async Task Drain(params Device[] devices)
        {
            for (int step = 0; step < 80 && devices.Any(d => d.Sync.GetStatus(d.Settings).PendingCount != 0); step++)
            {
                await Task.WhenAll(devices.Select(d => d.Sync.TickAsync(d.Settings)));
                Now = Now.AddSeconds(31);
            }
            Check(devices.All(d => d.Sync.GetStatus(d.Settings).PendingCount == 0),
                "independent pending queues converge: " + string.Join(", ", devices.Select(d => d.Name)));
        }
        public void Dispose() { Windows.Dispose(); Ubuntu.Dispose(); Directory.Delete(root, true); }
    }

    private sealed class Device : IDisposable
    {
        private readonly World world;
        private readonly string localPath;
        private readonly HabiticaApiClient api;
        public string Name { get; }
        public AppSettings Settings { get; }
        public MemorySecrets Secret { get; } = new();
        public DeviceHandler Handler { get; }
        public HabiticaIntegration Sync { get; private set; }
        public NoteEntry Entry = new(NoteType.Todo, new DateTime(2026, 10, 24, 9, 30, 0), "same title on both computers");
        public Guid Id { get; private set; }
        public string Alias => $"stopwatch-{Id:N}";
        public Device(World world, string root, string name)
        {
            this.world = world; Name = name; Directory.CreateDirectory(root);
            Settings = new() { ObsidianVaultFolder = root, NotesSubfolder = "Notes",
                HabiticaUserId = "11111111-1111-4111-8111-111111111111", HabiticaClientId = "22222222-2222-4222-8222-222222222222-StopwatchOverlay" };
            localPath = Path.Combine(root, "local-delivery.json"); Handler = new(world);
            api = new(new HttpClient(Handler)); Sync = New();
            Sync.SetToken(Settings.HabiticaUserId, "synthetic-device-token-" + name);
            if (!Sync.EnableHere(Settings)) throw new Exception("Independent vault incorrectly requires handoff");
        }
        private HabiticaIntegration New() => new(api, Secret, localPath, () => world.Now);
        public void Restart() => Sync = New();
        public void Add()
        {
            var result = Sync.Apply(Settings, null, Entry, null, [], false,
                () => ObsidianNotesSync.AppendEntry(Settings.ObsidianVaultFolder, Entry.Type, Entry.Text, "Notes", Entry.Timestamp));
            Check(result.LocalResult.Success && result.SyncWarning == null, Name + " saves its local task");
            Id = Sync.GetDetails(Settings, Entry).Id;
        }
        public void Rename(string title)
        {
            var after = Entry with { Text = title };
            var result = Sync.Apply(Settings, Entry, after, null, null, false,
                () => ObsidianNotesSync.UpdateTodoText(Settings.ObsidianVaultFolder, Entry, title, "Notes"));
            if (!result.LocalResult.Success || result.SyncWarning != null) throw new Exception("Local edit failed");
            Entry = after;
        }
        public void Complete()
        {
            var result = Sync.Apply(Settings, Entry, Entry with { IsCompleted = true }, null, null, false,
                () => ObsidianNotesSync.SetTodoCompleted(Settings.ObsidianVaultFolder, Entry, true, "Notes"));
            if (!result.LocalResult.Success || result.SyncWarning != null) throw new Exception("Local completion failed");
            Entry = Entry with { IsCompleted = true };
        }
        public void Delete()
        {
            var result = Sync.Apply(Settings, Entry, Entry, null, null, true,
                () => ObsidianNotesSync.DeleteTodo(Settings.ObsidianVaultFolder, Entry, "Notes"));
            if (!result.LocalResult.Success || result.SyncWarning != null) throw new Exception("Local deletion failed");
        }
        public void Dispose() => api.Dispose();
    }

    private sealed class MemorySecrets : IHabiticaSecretStore
    {
        private readonly Dictionary<string, string> values = [];
        public string Status => "synthetic memory-only store";
        public string? Read(string account) => values.GetValueOrDefault(account);
        public bool Write(string account, string token) { values[account] = token; return true; }
        public void Delete(string account) => values.Remove(account);
    }

    private sealed class DeviceHandler(World world) : HttpMessageHandler
    {
        public readonly List<DateTimeOffset> Times = [];
        public bool RateLimitNext, Offline;
        public int Limited;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Times.Add(world.Now);
            if (Offline) throw new HttpRequestException("synthetic offline computer");
            if (RateLimitNext)
            { RateLimitNext = false; Limited++; var limited = Reply(HttpStatusCode.TooManyRequests, "{}"); limited.Headers.RetryAfter = new(TimeSpan.FromSeconds(120)); return limited; }
            string? body = request.Content == null ? null : await request.Content.ReadAsStringAsync(ct);
            return world.Server.Send(request, body);
        }
    }
    private sealed class TaskState
    {
        public string Id = Guid.NewGuid().ToString("N"), Alias = "", Text = "", Notes = "";
        public bool Completed;
        public List<HabiticaChecklistItem> Children = [];
    }
    private sealed class FakeServer
    {
        private readonly object gate = new();
        public readonly Dictionary<string, TaskState> Tasks = [];
        public int Creates, Scores;
        public TaskState Get(string alias) => Tasks.Values.Single(t => t.Alias == alias);
        public HttpResponseMessage Send(HttpRequestMessage request, string? body)
        {
            lock (gate)
            {
                CheckHeader(request);
                var segments = request.RequestUri!.AbsolutePath.Split('/');
                using var document = body == null ? null : JsonDocument.Parse(body);
                var data = document?.RootElement ?? default;
                TaskState? task;
                if (request.Method == HttpMethod.Post && segments[^1] == "user")
                {
                    task = new() { Alias = data.GetProperty("alias").GetString()!, Text = data.GetProperty("text").GetString()!, Notes = data.GetProperty("notes").GetString()! };
                    if (data.TryGetProperty("checklist", out var children)) foreach (var child in children.EnumerateArray()) task.Children.Add(new(Guid.NewGuid().ToString("N"), child.GetProperty("text").GetString()!, child.GetProperty("completed").GetBoolean()));
                    Tasks.Add(task.Id, task); Creates++;
                }
                else
                {
                    string id = Uri.UnescapeDataString(segments[4]);
                    task = Tasks.Values.SingleOrDefault(t => t.Id == id || t.Alias == id);
                    if (task == null) return Reply(HttpStatusCode.NotFound, "{}");
                    if (segments.Contains("score")) { task.Completed = segments[^1] == "up"; if (task.Completed) Scores++; return Reply(HttpStatusCode.OK, "{\"success\":true,\"data\":{}}"); }
                    if (segments.Contains("checklist"))
                    {
                        if (request.Method == HttpMethod.Post) task.Children.Add(new(Guid.NewGuid().ToString("N"), data.GetProperty("text").GetString()!, data.GetProperty("completed").GetBoolean()));
                        else
                        {
                            int index = task.Children.FindIndex(c => c.Id == segments[^1]);
                            if (request.Method == HttpMethod.Delete) task.Children.RemoveAt(index);
                            else task.Children[index] = task.Children[index] with { Text = data.GetProperty("text").GetString()!, Completed = data.GetProperty("completed").GetBoolean() };
                        }
                    }
                    else if (request.Method == HttpMethod.Delete) { Tasks.Remove(task.Id); return Reply(HttpStatusCode.OK, "{\"success\":true,\"data\":{}}"); }
                    else if (request.Method == HttpMethod.Put) { task.Text = data.GetProperty("text").GetString()!; task.Notes = data.GetProperty("notes").GetString()!; }
                }
                return Reply(HttpStatusCode.OK, JsonSerializer.Serialize(new { success = true, data = new { _id = task.Id, alias = task.Alias, text = task.Text, notes = task.Notes, completed = task.Completed, checklist = task.Children.Select(c => new { id = c.Id, text = c.Text, completed = c.Completed }) } }));
            }
        }
        private static void CheckHeader(HttpRequestMessage request)
        {
            if (request.RequestUri!.Host != "habitica.com" || request.Headers.GetValues("x-api-user").Single() != "11111111-1111-4111-8111-111111111111")
                throw new Exception("Unexpected fake server destination/account");
        }
    }
    private static HttpResponseMessage Reply(HttpStatusCode code, string json) => new(code) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
}
