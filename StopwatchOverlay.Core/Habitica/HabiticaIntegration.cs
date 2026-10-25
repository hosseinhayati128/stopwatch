using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StopwatchOverlay.Habitica;

public sealed record HabiticaSubtask(Guid Id, string Text, bool Completed);
public sealed record HabiticaTaskDetails(Guid Id, DateTime? DueAt, IReadOnlyList<HabiticaSubtask> Children);
public sealed record HabiticaMutationResult(NoteSyncResult LocalResult, string? SyncWarning = null);
public sealed record HabiticaImportProgress(int Total, int Queued, int Completed, int Blocked, bool IsQueuing, string? Error = null);
public sealed record HabiticaSyncStatus(string Message, int PendingCount, DateTime? LastSuccessUtc, bool IsOwner,
    HabiticaImportProgress? ImportProgress = null, DateTime? NextRequestUtc = null, bool IsRequestRunning = false);

// Metadata belongs to the vault; credentials and request deadlines belong to this installation.
public sealed partial class HabiticaIntegration
{
    public static HabiticaIntegration Current { get; } = new();
    private static readonly object StoreGate = new();
    private readonly SemaphoreSlim requestGate = new(1, 1);
    private readonly HabiticaApiClient api;
    private readonly IHabiticaSecretStore secrets;
    private readonly Func<DateTimeOffset> clock;
    private readonly string localPath;
    private CancellationTokenSource cancellation = new();
    private string configuration = "", message = "Waiting for the next Habitica sync check.";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public HabiticaIntegration(HabiticaApiClient? api = null, IHabiticaSecretStore? secrets = null,
        string? statePath = null, Func<DateTimeOffset>? clock = null)
    {
        this.api = api ?? new(); this.secrets = secrets ?? HabiticaSecretStore.Default;
        this.clock = clock ?? (() => DateTimeOffset.UtcNow);
        localPath = statePath ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StopwatchOverlay", "habitica-sync-state.json");
    }
    public string CredentialStatus => secrets.Status;
    private readonly HashSet<string> refreshedAccounts = [];
    public void SetToken(string account, string token)
    {
        string normalized = account.Trim().ToLowerInvariant();
        if (!secrets.Write(normalized, token.Trim())) throw new InvalidOperationException("The API token could not be saved. Check the token and try again.");
        lock (StoreGate) refreshedAccounts.Add(normalized);
    }
    private static string Account(AppSettings s) => s.HabiticaUserId.Trim().ToLowerInvariant();
    private static string VaultPath(AppSettings s)
    {
        if (string.IsNullOrWhiteSpace(s.ObsidianVaultFolder) || !Directory.Exists(s.ObsidianVaultFolder)) throw new IOException("Vault unavailable.");
        string root = Path.GetFullPath(s.ObsidianVaultFolder);
        string folder = Path.GetFullPath(Path.Combine(root, s.NotesSubfolder));
        if (!folder.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Invalid notes folder.");
        return Path.Combine(folder, ".stopwatch-habitica.json");
    }
    private static string Key(NoteEntry e) => $"{e.Type}:{e.Timestamp:yyyy-MM-ddTHH:mm}:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(e.Text.Replace("\r\n", "\n").Trim())));
    private static T Read<T>(string path) where T : new()
    {
        if (!File.Exists(path)) return new();
        string content = File.ReadAllText(path);
        using var document = JsonDocument.Parse(content);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Version", out var version) || version.GetInt32() != 1)
            throw new IOException("Unsupported sync metadata.");
        string identity = typeof(T) == typeof(Vault) ? "Id" : "Installation";
        if (!root.TryGetProperty(identity, out var id) || !Guid.TryParse(id.GetString(), out var guid) || guid == Guid.Empty)
            throw new IOException("Invalid sync identity.");
        foreach (string field in typeof(T) == typeof(Vault) ? new[] { "Items", "Intents", "Owners", "Next" } : new[] { "Active", "Next", "Failures" })
            if (!root.TryGetProperty(field, out var value) || value.ValueKind == JsonValueKind.Null) throw new IOException("Incomplete sync metadata.");
        return JsonSerializer.Deserialize<T>(content) ?? throw new IOException("Invalid sync metadata.");
    }
    private static void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, JsonSerializer.Serialize(value, Json)); File.Move(temp, path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private static Vault ReadVault(string path)
    {
        var v = Read<Vault>(path);
        if (v.Version != 1 || v.Id == Guid.Empty || v.Items == null || v.Intents == null || v.Owners == null || v.Next == null || v.Imports == null ||
            v.Imports.Values.Any(b => b == null || b.Items == null || b.Items.Any(m => m.ItemId == Guid.Empty || (m.Queued && m.Revision < 1))) ||
            v.Items.GroupBy(i => i.Id).Any(g => g.Key == Guid.Empty || g.Count() != 1) ||
            v.Items.Any(i => i.Keys == null || i.Children == null || i.Targets == null || i.Targets.Values.Any(t => t.Children == null || t.ChildIds == null || t.Revision < t.Ack)))
            throw new IOException("Unsupported sync metadata.");
        return v;
    }
    private Local ReadLocal()
    {
        var l = Read<Local>(localPath);
        if (l.Version != 1 || l.Installation == Guid.Empty || l.Active == null || l.Next == null || l.Failures == null) throw new IOException("Unsupported delivery metadata.");
        return l;
    }
    public void Configure(AppSettings s)
    {
        string next = $"{s.HabiticaEnabled}|{Account(s)}|{s.ObsidianVaultFolder}|{s.NotesSubfolder}|{s.HabiticaClientId}";
        lock (StoreGate)
        {
            if (next == configuration) return;
            // Configuration changes are an explicit opportunity to retry rejected credentials.
            refreshedAccounts.Add(Account(s));
            CancelPendingRequests(); configuration = next; snapshots.Clear();
        }
    }
    public void CancelPendingRequests() { var old = cancellation; cancellation = new(); old.Cancel(); /* active calls own the old token */ }
    public bool EnableHere(AppSettings s, bool takeOver = false)
    {
        if (!Guid.TryParse(Account(s), out _)) throw new InvalidOperationException("Enter a valid Habitica user ID.");
        lock (StoreGate)
        {
            var path = VaultPath(s); var v = ReadVault(path); var l = ReadLocal(); string a = Account(s);
            if (v.Owners.TryGetValue(a, out var owner) && owner != l.Installation && !takeOver) return false;
            v.Owners[a] = l.Installation; l.Active.Add($"{v.Id}:{a}");
            Write(path, v); Write(localPath, l); s.HabiticaEnabled = true; message = "Sync enabled for this local vault. Other computers can sync their own vaults at the same time.";
        }
        Configure(s); return true;
    }
    public void Disable(AppSettings s) { s.HabiticaEnabled = false; Configure(s); message = "Habitica sync is paused; pending changes are saved."; }
    private static Item? Find(Vault v, NoteEntry e)
    {
        var found = v.Items.Where(x => x.Keys.Contains(Key(e))).ToArray();
        if (found.Length > 1) throw new IOException("Ambiguous task metadata.");
        return found.SingleOrDefault();
    }
    public HabiticaTaskDetails GetDetails(AppSettings s, NoteEntry entry)
    {
        try
        {
            lock (StoreGate)
            {
                var i = Find(ReadVault(VaultPath(s)), entry);
                var due = i != null ? i.Due?.ToLocalTime() : entry.Type == NoteType.Todo
                    ? TodoOrganizationStore.Load(s.ObsidianVaultFolder, s.NotesSubfolder).GetDueAt(entry) : null;
                return new(i?.Id ?? Guid.Empty, due, i?.Children.ToArray() ?? []);
            }
        }
        catch { return new(Guid.Empty, null, []); }
    }
    public HabiticaMutationResult SetChildren(AppSettings s, NoteEntry entry, IReadOnlyList<HabiticaSubtask> children) =>
        Apply(s, entry, entry, GetDetails(s, entry).DueAt, children, false, () => new(true, ""));
    public HabiticaMutationResult Apply(AppSettings s, NoteEntry? before, NoteEntry after, DateTime? dueAt,
        IReadOnlyList<HabiticaSubtask>? children, bool deleted, Func<NoteSyncResult> localWrite)
    {
        if (after.Type == NoteType.Note) return new(localWrite());
        lock (StoreGate)
        {
            Vault? v = null; string? path = null; Intent? intent = null;
            try
            {
                path = VaultPath(s); v = ReadVault(path); Recover(v, s);
                var i = before == null ? null : Find(v, before);
                if (i == null) { i = new(); v.Items.Add(i); }
                if (children?.GroupBy(x => x.Id).Any(g => g.Key == Guid.Empty || g.Count() > 1) == true) throw new IOException("Invalid child identities.");
                intent = new() { ItemId = i.Id, Before = before, After = after, Due = dueAt, Children = children?.ToList(), Deleted = deleted, Account = Account(s), Enroll = s.HabiticaEnabled };
                v.Intents.Add(intent); Write(path, v);
            }
            catch { var saved = localWrite(); return new(saved, "Habitica metadata could not be saved. Local changes are kept; sync needs attention."); }
            var result = localWrite();
            try
            {
                if (result.Success) Commit(v!, intent!);
                v!.Intents.Remove(intent!); Write(path!, v);
                return new(result);
            }
            catch { return new(result, "Local change saved; Habitica recovery is pending."); }
        }
    }
    private static void Commit(Vault v, Intent intent)
    {
        var i = v.Items.Single(x => x.Id == intent.ItemId);
        if (intent.Before != null) i.Keys.Add(Key(intent.Before));
        i.Keys.Add(Key(intent.After)); i.Entry = intent.After; i.Due = intent.Due?.ToUniversalTime();
        if (intent.Children != null) i.Children = intent.Children;
        if (!string.IsNullOrWhiteSpace(intent.Account) && (intent.Enroll || i.Targets.ContainsKey(intent.Account)))
        {
            if (!i.Targets.TryGetValue(intent.Account, out var target)) i.Targets[intent.Account] = target = new();
            target.Revision++; target.Entry = intent.After; target.Due = i.Due; target.Children = i.Children.ToList(); target.Deleted = intent.Deleted;
            if (target.BlockKind is HabiticaErrorKind.InvalidInput or HabiticaErrorKind.Configuration) { target.Block = null; target.BlockKind = HabiticaErrorKind.None; }
        }
    }
    public int CountExisting(AppSettings s) => ObsidianNotesSync.LoadAllNotes(s.ObsidianVaultFolder, s.NotesSubfolder).Count(x => x.Type != NoteType.Note);
    private readonly HashSet<string> activeImports = [];
    private readonly Dictionary<string, string> importErrors = [];
    public void ImportExisting(AppSettings s)
    {
        if (!s.HabiticaEnabled || !GetStatus(s).IsOwner) throw new InvalidOperationException("Enable Habitica sync on this computer before queuing existing items.");
        string path = VaultPath(s), account = Account(s), key = path + "|" + account;
        ImportBatch batch;
        lock (StoreGate)
        {
            if (!activeImports.Add(key)) throw new InvalidOperationException("Existing items are already being queued.");
            importErrors.Remove(key);
        }
        try
        {
            var entries = ObsidianNotesSync.LoadAllNotes(s.ObsidianVaultFolder, s.NotesSubfolder).Where(x => x.Type != NoteType.Note).ToArray();
            lock (StoreGate)
            {
                var v = ReadVault(path); Recover(v, s);
                batch = new() { IsQueuing = true };
                foreach (var entry in entries)
                {
                    var item = Find(v, entry);
                    if (item == null) { item = new() { Entry = entry }; item.Keys.Add(Key(entry)); v.Items.Add(item); }
                    batch.Items.Add(new() { ItemId = item.Id });
                }
                v.Imports[account] = batch; Write(path, v);
            }
            for (int n = 0; n < entries.Length; n++)
            {
                // Commit each enrollment separately so status reads and the worker can interleave.
                lock (StoreGate)
                {
                    var v = ReadVault(path); Recover(v, s);
                    var member = v.Imports[account].Items[n];
                    var item = v.Items.Single(i => i.Id == member.ItemId);
                    var entry = entries[n];
                    // The UI may edit or remove a task while this batch is being prepared.
                    // Never let the earlier source snapshot replace its newer desired state.
                    string source = Path.Combine(Path.GetDirectoryName(path)!, ObsidianNotesSync.GetFileName(entry.Type));
                    if (item.Entry == null || Key(item.Entry) != Key(entry) || item.Entry.IsCompleted != entry.IsCompleted
                        || item.Targets.GetValueOrDefault(account)?.Deleted == true || !File.Exists(source))
                        throw new IOException("Import source changed.");
                    _ = File.ReadAllText(source);
                    if (ObsidianNotesSync.LoadAllNotes(s.ObsidianVaultFolder, s.NotesSubfolder)
                        .Count(e => Key(e) == Key(entry) && e.IsCompleted == entry.IsCompleted) != 1)
                        throw new IOException("Import source changed or is ambiguous.");
                    var due = item.Due?.ToLocalTime() ?? (entry.Type == NoteType.Todo
                        ? TodoOrganizationStore.Load(s.ObsidianVaultFolder, s.NotesSubfolder).GetDueAt(entry) : null);
                    Commit(v, new() { ItemId = item.Id, Before = entry, After = entry, Due = due,
                        Account = account, Enroll = true });
                    member.Revision = item.Targets[account].Revision;
                    member.Queued = true;
                    Write(path, v);
                }
            }
            lock (StoreGate)
            {
                var v = ReadVault(path); v.Imports[account].IsQueuing = false; Write(path, v);
                message = "Existing items queued. Habitica sync continues in the background.";
            }
        }
        catch
        {
            const string error = "Some existing items could not be queued. Queued changes are saved; check the vault and try again.";
            lock (StoreGate)
            {
                importErrors[key] = error;
                try
                {
                    var v = ReadVault(path);
                    if (v.Imports.TryGetValue(account, out var saved)) { saved.IsQueuing = false; saved.Error = error; Write(path, v); }
                }
                catch { /* Keep the original metadata and expose a safe session error. */ }
            }
            throw new InvalidOperationException(error);
        }
        finally { lock (StoreGate) activeImports.Remove(key); }
    }
    private HabiticaImportProgress? ImportStatus(Vault v, string account, string path)
    {
        if (!v.Imports.TryGetValue(account, out var batch)) return null;
        int completed = 0, blocked = 0, queued = 0;
        foreach (var member in batch.Items.Where(i => i.Queued))
        {
            var target = v.Items.FirstOrDefault(i => i.Id == member.ItemId)?.Targets.GetValueOrDefault(account);
            if (target != null && target.Ack >= member.Revision) completed++;
            else if (target == null || target.Block != null) blocked++;
            else queued++;
        }
        string key = path + "|" + account;
        bool preparing = activeImports.Contains(key);
        string? error = importErrors.GetValueOrDefault(key) ?? batch.Error;
        if (batch.IsQueuing && !preparing) error ??= "Queuing was interrupted. Queued changes are saved; sync existing items again to finish.";
        return new(batch.Items.Count, queued, completed, blocked, preparing, error);
    }
    public int CountMissing(AppSettings s)
    {
        lock (StoreGate) return ReadVault(VaultPath(s)).Items.Count(i => i.Targets.TryGetValue(Account(s), out var t) && t.BlockKind == HabiticaErrorKind.NotFound);
    }
    // Only the explicit Settings confirmation can authorize recreation of a previously linked missing task.
    public void RecreateMissing(AppSettings s)
    {
        if (!s.HabiticaEnabled || !GetStatus(s).IsOwner) throw new InvalidOperationException("Enable sync on this computer first.");
        lock (StoreGate)
        {
            string path = VaultPath(s); var v = ReadVault(path);
            foreach (var t in v.Items.Where(i => i.Targets.ContainsKey(Account(s))).Select(i => i.Targets[Account(s)]))
                if (t.BlockKind == HabiticaErrorKind.NotFound)
                { t.RemoteId = null; t.Block = null; t.BlockKind = HabiticaErrorKind.None; t.Attempted = false; }
            Write(path, v); snapshots.Clear();
        }
    }
    public HabiticaSyncStatus GetStatus(AppSettings s)
    {
        try
        {
            lock (StoreGate)
            {
                string path = VaultPath(s); var v = ReadVault(path); var l = ReadLocal(); string a = Account(s);
                var targets = v.Items.Where(i => i.Targets.ContainsKey(a)).Select(i => i.Targets[a]).ToArray();
                string status = !s.HabiticaEnabled ? "Habitica sync is paused; pending changes are saved." : targets.FirstOrDefault(t => t.Block != null)?.Block ?? message;
                return new(status, targets.Count(t => t.Revision > t.Ack), l.LastSuccess?.UtcDateTime,
                    v.Owners.GetValueOrDefault(a) == l.Installation && l.Active.Contains($"{v.Id}:{a}"), ImportStatus(v, a, path),
                    new[] { l.Next.GetValueOrDefault(a), v.Next.GetValueOrDefault(a) }.Max() is var next && next != default ? next.UtcDateTime : null,
                    requestGate.CurrentCount == 0);
            }
        }
        catch { return new("Habitica metadata is unavailable or invalid. Check the vault before syncing.", 0, null, false); }
    }
    public sealed class Vault { public int Version { get; set; } = 1; public Guid Id { get; set; } = Guid.NewGuid(); public Dictionary<string, Guid> Owners { get; set; } = []; public Dictionary<string, DateTimeOffset> Next { get; set; } = []; public List<Item> Items { get; set; } = []; public List<Intent> Intents { get; set; } = []; public Dictionary<string, ImportBatch> Imports { get; set; } = []; }
    public sealed class ImportBatch { public List<ImportMember> Items { get; set; } = []; public bool IsQueuing { get; set; } public string? Error { get; set; } }
    public sealed class ImportMember { public Guid ItemId { get; set; } public long Revision { get; set; } public bool Queued { get; set; } }
    public sealed class Item { public Guid Id { get; set; } = Guid.NewGuid(); public HashSet<string> Keys { get; set; } = []; public NoteEntry? Entry { get; set; } public DateTime? Due { get; set; } public List<HabiticaSubtask> Children { get; set; } = []; public Dictionary<string, Target> Targets { get; set; } = []; }
    public sealed class Target
    {
        public long Revision { get; set; }
        public long Ack { get; set; }
        public NoteEntry? Entry { get; set; }
        public DateTime? Due { get; set; }
        public List<HabiticaSubtask> Children { get; set; } = [];
        public bool Deleted { get; set; }
        public string? RemoteId { get; set; }
        public Dictionary<Guid, string> ChildIds { get; set; } = [];
        public bool Attempted { get; set; }
        public string? Block { get; set; }
        public HabiticaErrorKind BlockKind { get; set; }
        public List<HabiticaSubtask>? CreatingChildren { get; set; }
        public HabiticaSubtask? AddingChild { get; set; }
        public List<string> BeforeChildIds { get; set; } = [];
    }
    public sealed class Intent { public Guid ItemId { get; set; } public NoteEntry? Before { get; set; } public NoteEntry After { get; set; } = null!; public DateTime? Due { get; set; } public List<HabiticaSubtask>? Children { get; set; } public bool Deleted { get; set; } public string Account { get; set; } = ""; public bool Enroll { get; set; } }
    public sealed class Local { public int Version { get; set; } = 1; public Guid Installation { get; set; } = Guid.NewGuid(); public HashSet<string> Active { get; set; } = []; public Dictionary<string, DateTimeOffset> Next { get; set; } = []; public Dictionary<string, int> Failures { get; set; } = []; public DateTimeOffset? LastSuccess { get; set; } }
}
