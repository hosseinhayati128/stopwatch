namespace StopwatchOverlay.Habitica;

public sealed partial class HabiticaIntegration
{
    private readonly Dictionary<string, HabiticaRemoteTask?> snapshots = [];
    public async Task<HabiticaApiResult<string>> TestConnectionAsync(AppSettings settings, CancellationToken ct = default)
    {
        if (!await requestGate.WaitAsync(0, ct)) return new(null, HabiticaErrorKind.Conflict);
        try
        {
            string account = Account(settings);
            string clientId = settings.HabiticaClientId.Trim();
            if (HabiticaApiClient.ConfigurationError(account, clientId) != null) return new(null, HabiticaErrorKind.Configuration);
            var token = await Task.Run(() => secrets.Read(account), ct);
            var credentials = string.IsNullOrWhiteSpace(token) ? null : new HabiticaCredentials(account, token, clientId);
            if (credentials == null) return new(null, HabiticaErrorKind.Configuration);
            string path = VaultPath(settings);
            lock (StoreGate) { var v = ReadVault(path); if (!Reserve(account, v)) return new(null, HabiticaErrorKind.RateLimited); Write(path, v); }
            var r = await api.TestConnectionAsync(credentials, ct);
            FinishRequest(account, r.Error, r.RetryAfter, path);
            message = r.Success ? "Connected to Habitica." : Describe(r.Error);
            return r;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        { message = "The notes folder or Habitica sync state could not be read or saved. Check folder permissions and sync metadata."; return new(null, HabiticaErrorKind.Storage); }
        finally { requestGate.Release(); }
    }
    private bool Reserve(string account, Vault vault)
    {
        var l = ReadLocal(); var now = clock();
        if (l.Next.GetValueOrDefault(account) > now || vault.Next.GetValueOrDefault(account) > now) return false;
        l.Next[account] = now.AddSeconds(30); vault.Next[account] = l.Next[account]; Write(localPath, l); return true;
    }
    private void FinishRequest(string account, HabiticaErrorKind error, TimeSpan? retry, string path)
    {
        lock (StoreGate)
        {
            var l = ReadLocal(); var delay = retry ?? TimeSpan.Zero;
            if (error is HabiticaErrorKind.RateLimited or HabiticaErrorKind.Network or HabiticaErrorKind.Server or HabiticaErrorKind.InvalidResponse)
            {
                int failures = Math.Min(l.Failures.GetValueOrDefault(account) + 1, 6); l.Failures[account] = failures;
                var backoff = error == HabiticaErrorKind.RateLimited
                    ? retry ?? TimeSpan.FromSeconds(60)
                    : TimeSpan.FromSeconds(Math.Min(900, 30 * (1 << (failures - 1))) + Random.Shared.Next(1, 6));
                delay = delay < backoff ? backoff : delay;
            }
            var next = clock() + delay;
            if (next > l.Next.GetValueOrDefault(account)) l.Next[account] = next;
            if (error == HabiticaErrorKind.None) { l.LastSuccess = clock(); l.Failures[account] = 0; }
            Write(localPath, l);
            var v = ReadVault(path);
            if (l.Next[account] > v.Next.GetValueOrDefault(account)) { v.Next[account] = l.Next[account]; Write(path, v); }
        }
    }
    private static string Describe(HabiticaErrorKind error) => error switch
    {
        HabiticaErrorKind.Authentication => "Habitica rejected the credentials. Update the user ID and API token.",
        HabiticaErrorKind.Configuration => "Enter your Habitica user ID, API token and author client ID.",
        HabiticaErrorKind.RateLimited => "Habitica rate limit reached. Changes are saved and will retry after the server delay.",
        HabiticaErrorKind.InvalidInput => "Habitica rejected this task. Review its text and due date.",
        _ => "Habitica is unavailable. Changes are saved; retry is scheduled."
    };
    private static HabiticaTaskPayload Payload(Item i, Target t, bool creating)
    {
        var text = (t.Entry?.Text ?? "").Replace("\r\n", "\n"); var lines = text.Split('\n');
        string notes = string.Join('\n', lines.Skip(1));
        if (t.Entry?.Type == NoteType.Reminder) notes = "Stopwatch reminder" + (notes.Length == 0 ? "" : "\n" + notes);
        return new(lines[0], notes, t.Due is DateTime date ? new DateTimeOffset(date) : null,
            creating ? $"stopwatch-{i.Id:N}" : null,
            creating ? t.Children.Select(c => new HabiticaChecklistItem("", c.Text, c.Completed)).ToArray() : null);
    }
    private static void Recover(Vault v, AppSettings s)
    {
        if (v.Intents.Count == 0) return;
        // Never interpret an absent vault/file as a successful deletion.
        foreach (var intent in v.Intents.ToArray())
        {
            string filename = intent.After.Type == NoteType.Todo ? "Todos.md" : "Reminders.md";
            if (!File.Exists(Path.Combine(Path.GetDirectoryName(VaultPath(s))!, filename))) throw new IOException("Recovery source unavailable.");
            var entries = ObsidianNotesSync.LoadAllNotes(s.ObsidianVaultFolder, s.NotesSubfolder);
            int after = entries.Count(e => Key(e) == Key(intent.After) && e.IsCompleted == intent.After.IsCompleted);
            int before = intent.Before == null ? 0 : entries.Count(e => Key(e) == Key(intent.Before) && e.IsCompleted == intent.Before.IsCompleted);
            if ((!intent.Deleted && after == 1) || (intent.Deleted && after == 0 && before == 0)) Commit(v, intent);
            else if (before != 1) throw new IOException("Ambiguous unfinished task change.");
            v.Intents.Remove(intent);
        }
    }
    public async Task TickAsync(AppSettings s, CancellationToken ct = default)
    {
        Configure(s);
        if (!s.HabiticaEnabled || !await requestGate.WaitAsync(0, ct)) return;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, cancellation.Token);
        try { await TickCore(s, linked.Token); }
        catch (OperationCanceledException) { snapshots.Clear(); }
        catch { snapshots.Clear(); message = "Habitica sync needs attention: the vault or delivery metadata could not be read or saved."; }
        finally { requestGate.Release(); }
    }
    private async Task TickCore(AppSettings s, CancellationToken ct)
    {
        if (await TryCreateBatch(s, ct)) return;
        string path = VaultPath(s), account = Account(s), cacheKey;
        Vault v; Item item; Target target; HabiticaRemoteTask? remote; string operation; Guid childId = Guid.Empty; string? remoteChild = null;
        string clientId = s.HabiticaClientId.Trim();
        lock (StoreGate)
        {
            v = ReadVault(path); var l = ReadLocal();
            if (v.Owners.GetValueOrDefault(account) != l.Installation || !l.Active.Contains($"{v.Id}:{account}")) { message = "This vault's Habitica links belong to another installation. Separate local vaults may sync together; use advanced move options only if relocating this linked vault."; return; }
            bool recovered = v.Intents.Count != 0;
            Recover(v, s);
            if (refreshedAccounts.Remove(account))
            {
                foreach (var t in v.Items.SelectMany(i => i.Targets.Where(k => k.Key == account).Select(k => k.Value)))
                    if (t.BlockKind is HabiticaErrorKind.Authentication or HabiticaErrorKind.Configuration) { t.Block = null; t.BlockKind = HabiticaErrorKind.None; }
                recovered = true;
            }
            if (recovered) Write(path, v);
            item = v.Items.FirstOrDefault(i => i.Targets.TryGetValue(account, out var t) && t.Revision > t.Ack && t.Block == null)!;
            if (item == null) { message = v.Items.Any(i => i.Targets.TryGetValue(account, out var t) && t.Block != null) ? "A linked task needs attention. Review the task or update credentials." : "All Habitica changes are synced."; return; }
            target = item.Targets[account]; cacheKey = $"{v.Id}:{account}:{item.Id}";
            if (!target.Deleted)
            {
                string source = Path.Combine(Path.GetDirectoryName(path)!, ObsidianNotesSync.GetFileName(target.Entry!.Type));
                if (!File.Exists(source)) { message = "The local task file is unavailable. Pending changes are kept."; return; }
                _ = File.ReadAllText(source); // establish readability; a parser's empty fallback must not imply absence
                var matches = ObsidianNotesSync.LoadAllNotes(s.ObsidianVaultFolder, s.NotesSubfolder)
                    .Count(e => Key(e) == Key(target.Entry) && e.IsCompleted == target.Entry.IsCompleted);
                if (matches != 1) { target.Block = "The local task changed outside Stopwatch or matches duplicate entries. Review it before syncing."; Write(path, v); return; }
            }
            if (target.Deleted && !target.Attempted && target.RemoteId == null) { target.Ack = target.Revision; Write(path, v); return; }
            if (!snapshots.TryGetValue(cacheKey, out remote)) operation = "lookup";
            else if (remote == null) operation = target.Deleted ? "ack" : "create";
            else if (target.Deleted) operation = "delete";
            else
            {
                var p = Payload(item, target, false);
                if (p.Text != remote.Text || p.Notes != remote.Notes || p.DueAt != remote.DueAt) operation = "update";
                else
                {
                    // Resolve the operation that was sent, even when its desired child was since renamed or removed.
                    if (target.CreatingChildren != null)
                    {
                        foreach (var child in target.CreatingChildren)
                        {
                            var matches = remote.Checklist.Where(c => c.Text == child.Text && !target.ChildIds.Values.Contains(c.Id)).ToArray();
                            if (matches.Length != 1) { target.Block = "The created checklist could not be reconciled. Review the linked task in Habitica."; Write(path, v); return; }
                            target.ChildIds[child.Id] = matches[0].Id;
                        }
                        target.CreatingChildren = null;
                    }
                    if (target.AddingChild is { } added)
                    {
                        var matches = remote.Checklist.Where(c => c.Text == added.Text && !target.BeforeChildIds.Contains(c.Id)).ToArray();
                        if (matches.Length > 1) { target.Block = "Ambiguous checklist add. Review the linked task in Habitica."; Write(path, v); return; }
                        if (matches.Length == 1) target.ChildIds[added.Id] = matches[0].Id;
                        target.AddingChild = null; target.BeforeChildIds.Clear();
                    }
                    var missing = target.Children.FirstOrDefault(c => !target.ChildIds.ContainsKey(c.Id));
                    var changed = target.Children.FirstOrDefault(c => target.ChildIds.TryGetValue(c.Id, out var id) && remote.Checklist.Any(r => r.Id == id && (r.Text != c.Text || r.Completed != c.Completed)));
                    var removed = target.ChildIds.FirstOrDefault(k => target.Children.All(c => c.Id != k.Key) && remote.Checklist.Any(r => r.Id == k.Value));
                    var vanished = target.Children.FirstOrDefault(c => target.ChildIds.TryGetValue(c.Id, out var id) && remote.Checklist.All(r => r.Id != id));
                    if (vanished != null) { target.Block = "A linked checklist item was removed in Habitica. Review it before syncing."; Write(path, v); return; }
                    if (missing != null) { operation = "addChild"; childId = missing.Id; }
                    else if (changed != null) { operation = "updateChild"; childId = changed.Id; remoteChild = target.ChildIds[childId]; }
                    else if (removed.Key != Guid.Empty) { operation = "deleteChild"; childId = removed.Key; remoteChild = removed.Value; }
                    else operation = target.Entry!.IsCompleted != remote.Completed ? "complete" : "ack";
                }
            }
            if (operation == "ack") { target.Ack = target.Revision; Write(path, v); snapshots.Remove(cacheKey); message = "Task synced to Habitica."; return; }
            if (ReadLocal().Next.GetValueOrDefault(account) > clock() || v.Next.GetValueOrDefault(account) > clock()) return;
            if (operation == "create") { target.ChildIds.Clear(); target.CreatingChildren = target.Children.ToList(); }
            if (operation == "addChild") { target.AddingChild = target.Children.Single(c => c.Id == childId); target.BeforeChildIds = remote!.Checklist.Select(c => c.Id).ToList(); }
            if (operation != "lookup") { target.Attempted = true; snapshots.Remove(cacheKey); }
            Write(path, v); // durable attempted marker precedes any mutation
        }
        // A locked Linux keyring may wait for its service; do not block Avalonia's UI thread.
        var token = await Task.Run(() => secrets.Read(account), ct);
        if (string.IsNullOrWhiteSpace(token)) { message = Describe(HabiticaErrorKind.Configuration); return; }
        ct.ThrowIfCancellationRequested();
        var credentials = new HabiticaCredentials(account, token, clientId);
        lock (StoreGate)
        {
            var latest = ReadVault(path);
            if (latest.Owners.GetValueOrDefault(account) != ReadLocal().Installation || !Reserve(account, latest)) return;
            Write(path, latest); // reserve immediately before HTTP, after any keyring wait
        }
        HabiticaRemoteTask? returned = null; HabiticaErrorKind error; TimeSpan? retry; bool unknown;
        if (operation is "lookup" or "create" or "update" or "addChild" or "updateChild" or "deleteChild")
        {
            var child = target.Children.FirstOrDefault(c => c.Id == childId);
            HabiticaApiResult<HabiticaRemoteTask> result = operation switch
            {
                "lookup" => await api.GetTaskAsync(credentials, target.RemoteId ?? $"stopwatch-{item.Id:N}", ct),
                "create" => await api.CreateTaskAsync(credentials, Payload(item, target, true), ct),
                "update" => await api.UpdateTaskAsync(credentials, remote!.Id, Payload(item, target, false), ct),
                "addChild" => await api.AddChecklistItemAsync(credentials, remote!.Id, child!.Text, child.Completed, ct),
                "updateChild" => await api.UpdateChecklistItemAsync(credentials, remote!.Id, remoteChild!, child!.Text, child.Completed, ct),
                _ => await api.DeleteChecklistItemAsync(credentials, remote!.Id, remoteChild!, ct)
            };
            error = result.Error; retry = result.RetryAfter; unknown = result.OutcomeUnknown; returned = result.Data;
        }
        else
        {
            var result = operation == "delete" ? await api.DeleteTaskAsync(credentials, remote!.Id, ct) : await api.SetCompletedAsync(credentials, remote!.Id, target.Entry!.IsCompleted, ct);
            error = result.Error; retry = result.RetryAfter; unknown = result.OutcomeUnknown;
        }
        FinishRequest(account, error, retry, path);
        lock (StoreGate)
        {
            var latest = ReadVault(path); var current = latest.Items.Single(i => i.Id == item.Id).Targets[account];
            if (operation == "lookup" && error == HabiticaErrorKind.NotFound)
            {
                if (current.RemoteId != null && !current.Deleted) { current.Block = "The linked Habitica task is missing. Review it, then use Settings → Habitica → Recreate missing linked tasks if desired."; current.BlockKind = HabiticaErrorKind.NotFound; }
                else snapshots[cacheKey] = null;
            }
            else if (error == HabiticaErrorKind.None)
            {
                if (returned != null)
                {
                    current.RemoteId = returned.Id;
                    snapshots[cacheKey] = returned;
                    if (operation == "create" && returned.Checklist.Count == target.Children.Count)
                    {
                        for (int n = 0; n < target.Children.Count; n++) current.ChildIds[target.Children[n].Id] = returned.Checklist[n].Id;
                        current.CreatingChildren = null;
                    }
                }
                if (operation == "delete") current.Ack = target.Revision;
                if (operation == "deleteChild") current.ChildIds.Remove(childId);
                message = "Habitica sync is progressing.";
            }
            else if (operation == "delete" && error == HabiticaErrorKind.NotFound) current.Ack = target.Revision;
            else
            {
                message = Describe(error);
                if (!unknown && operation == "addChild") { current.AddingChild = null; current.BeforeChildIds.Clear(); }
                if (!unknown && operation == "create") current.CreatingChildren = null;
                if (!unknown && error is HabiticaErrorKind.Authentication or HabiticaErrorKind.InvalidInput or HabiticaErrorKind.Configuration or HabiticaErrorKind.Conflict) { current.Block = message; current.BlockKind = error; }
            }
            Write(path, latest);
        }
    }
    // Only pristine, unlinked targets may bypass alias reconciliation. The durable
    // attempted marker sends interrupted or uncertain batches through normal lookup.
    private async Task<bool> TryCreateBatch(AppSettings s, CancellationToken ct)
    {
        string path = VaultPath(s), account = Account(s);
        List<(Guid Id, Target Target)> batch = [];
        Guid vaultId;
        lock (StoreGate)
        {
            var v = ReadVault(path); var local = ReadLocal();
            if (v.Owners.GetValueOrDefault(account) != local.Installation || !local.Active.Contains($"{v.Id}:{account}") || v.Intents.Count != 0) return false;
            vaultId = v.Id;
            var candidates = v.Items.Where(i => i.Targets.TryGetValue(account, out var t) && t.Revision > t.Ack && !t.Deleted && !t.Attempted && t.RemoteId == null && t.Block == null)
                .Take(HabiticaApiClient.MaxCreateBatchSize).ToArray();
            if (candidates.Length < 2) return false;
            if (local.Next.GetValueOrDefault(account) > clock() || v.Next.GetValueOrDefault(account) > clock()) return true;
            var entries = ObsidianNotesSync.LoadAllNotes(s.ObsidianVaultFolder, s.NotesSubfolder);
            foreach (var item in candidates)
            {
                var t = item.Targets[account];
                string source = Path.Combine(Path.GetDirectoryName(path)!, ObsidianNotesSync.GetFileName(t.Entry!.Type));
                if (!File.Exists(source)) return false;
                _ = File.ReadAllText(source);
                if (entries.Count(e => Key(e) == Key(t.Entry) && e.IsCompleted == t.Entry.IsCompleted) != 1) return false;
                batch.Add((item.Id, t));
            }
        }
        var token = await Task.Run(() => secrets.Read(account), ct);
        if (string.IsNullOrWhiteSpace(token)) { message = Describe(HabiticaErrorKind.Configuration); return true; }
        ct.ThrowIfCancellationRequested();
        lock (StoreGate)
        {
            var latest = ReadVault(path); var local = ReadLocal();
            if (latest.Id != vaultId || latest.Owners.GetValueOrDefault(account) != local.Installation || !local.Active.Contains($"{latest.Id}:{account}")) return true;
            // Revalidate after the keyring wait: edits must never be acknowledged as
            // though they were part of an earlier payload.
            foreach (var sent in batch)
            {
                var current = latest.Items.SingleOrDefault(i => i.Id == sent.Id)?.Targets.GetValueOrDefault(account);
                if (current == null || current.Revision != sent.Target.Revision || current.Attempted || current.RemoteId != null || current.Deleted || current.Block != null) return true;
            }
            var entries = ObsidianNotesSync.LoadAllNotes(s.ObsidianVaultFolder, s.NotesSubfolder);
            foreach (var sent in batch)
            {
                string source = Path.Combine(Path.GetDirectoryName(path)!, ObsidianNotesSync.GetFileName(sent.Target.Entry!.Type));
                if (!File.Exists(source)) return true;
                _ = File.ReadAllText(source);
                if (entries.Count(e => Key(e) == Key(sent.Target.Entry) && e.IsCompleted == sent.Target.Entry.IsCompleted) != 1) return true;
            }
            if (!Reserve(account, latest)) return true;
            foreach (var sent in batch)
            {
                var current = latest.Items.Single(i => i.Id == sent.Id).Targets[account];
                current.Attempted = true; current.ChildIds.Clear(); current.CreatingChildren = current.Children.ToList();
            }
            Write(path, latest);
        }
        var payloads = batch.Select(b => Payload(new Item { Id = b.Id }, b.Target, true)).ToArray();
        var result = await api.CreateTasksAsync(new HabiticaCredentials(account, token, s.HabiticaClientId.Trim()), payloads, ct);
        // Even a syntactically valid partial response cannot establish which writes
        // happened. Preserve every alias for reconciliation instead of retrying POST.
        var returned = result.Data;
        bool complete = result.Success && returned != null && returned.Count == batch.Count
            && returned.All(t => !string.IsNullOrWhiteSpace(t.Id) && !string.IsNullOrWhiteSpace(t.Alias))
            && returned.Select(t => t.Id).Distinct().Count() == batch.Count
            && returned.Select(t => t.Alias).Distinct().Count() == batch.Count
            && batch.All(b => returned.Any(t => t.Alias == $"stopwatch-{b.Id:N}"));
        var error = result.Success && !complete ? HabiticaErrorKind.InvalidResponse : result.Error;
        FinishRequest(account, error, result.RetryAfter, path);
        lock (StoreGate)
        {
            var latest = ReadVault(path); var local = ReadLocal();
            if (latest.Id != vaultId || latest.Owners.GetValueOrDefault(account) != local.Installation || !local.Active.Contains($"{latest.Id}:{account}")) return true;
            foreach (var sent in batch)
            {
                var current = latest.Items.Single(i => i.Id == sent.Id).Targets[account];
                if (complete)
                {
                    var task = returned!.Single(t => t.Alias == $"stopwatch-{sent.Id:N}");
                    current.RemoteId = task.Id;
                    snapshots[$"{vaultId}:{account}:{sent.Id}"] = task;
                    // The normal snapshot comparison reconciles children and latest
                    // revisions, and scores completed parents in a separate request.
                }
                else if (!result.OutcomeUnknown && error == HabiticaErrorKind.RateLimited)
                {
                    current.Attempted = false; current.CreatingChildren = null;
                }
                else if (!result.OutcomeUnknown && !result.Success && error is HabiticaErrorKind.Authentication or HabiticaErrorKind.InvalidInput or HabiticaErrorKind.Configuration or HabiticaErrorKind.Conflict)
                { current.Block = Describe(error); current.BlockKind = error; }
            }
            Write(path, latest);
        }
        message = complete ? "Habitica sync is progressing." : Describe(error);
        return true;
    }

}
