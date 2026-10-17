using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace StopwatchOverlay;

/// <summary>A snapshot of preferences; task completion remains owned by the Markdown.</summary>
public sealed class TodoOrganization
{
    private readonly Dictionary<string, int> priorities;
    private readonly Dictionary<string, int> positions;
    public string SortMode { get; }

    internal TodoOrganization(string sortMode, Dictionary<string, int> priorities, List<string> manualOrder)
    {
        SortMode = sortMode;
        this.priorities = priorities;
        positions = manualOrder.Select((key, index) => (key, index)).ToDictionary(p => p.key, p => p.index);
    }

    public int GetPriority(NoteEntry entry) => priorities.GetValueOrDefault(TodoOrganizationStore.GetKey(entry), 3);

    public NoteEntry[] Arrange(IEnumerable<NoteEntry> entries)
    {
        // Stable ordering retains the input order for ties and for newly discovered tasks.
        var ordered = entries.OrderBy(entry => entry.IsCompleted);
        return SortMode == "Manual"
            ? ordered.ThenBy(entry => positions.GetValueOrDefault(TodoOrganizationStore.GetKey(entry), int.MaxValue)).ToArray()
            : ordered.ThenBy(GetPriority).ToArray();
    }
}

public static class TodoOrganizationStore
{
    public const string FileName = ".stopwatch-todos.json";
    private static readonly object WriteLock = new();
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private sealed class Preferences
    {
        public int Version { get; set; } = 1;
        public string SortMode { get; set; } = "Priority";
        public Dictionary<string, int> Priorities { get; set; } = new();
        public List<string> ManualOrder { get; set; } = new();
    }

    public static TodoOrganization Load(string vaultFolder, string? notesSubfolder = null)
    {
        lock (WriteLock)
        {
            try
            {
                var preferences = Read(GetPath(vaultFolder, notesSubfolder));
                return new(preferences.SortMode, preferences.Priorities, preferences.ManualOrder);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
            {
                // Reading must remain usable; writes re-read and refuse damaged metadata.
                return new("Priority", new(), new());
            }
        }
    }

    /// <summary>Matches Markdown's minute precision, ignoring the checkbox and line endings.</summary>
    public static string GetKey(NoteEntry entry)
    {
        string text = entry.Text.Replace("\r\n", "\n").Replace('\r', '\n').Trim().Normalize(NormalizationForm.FormC);
        string identity = entry.Timestamp.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture) + "\n" + text;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    public static NoteSyncResult SetPriority(string vaultFolder, NoteEntry entry, int priority, string? notesSubfolder = null)
    {
        if (entry.Type != NoteType.Todo || priority is < 1 or > 4)
            return new(false, "", "Choose a todo priority from P1 to P4.");
        return Update(vaultFolder, notesSubfolder, preferences => preferences.Priorities[GetKey(entry)] = priority);
    }

    public static NoteSyncResult SetSortMode(string vaultFolder, string sortMode, string? notesSubfolder = null)
    {
        if (sortMode is not ("Priority" or "Manual"))
            return new(false, "", "Choose Priority or Manual sorting.");
        return Update(vaultFolder, notesSubfolder, preferences => preferences.SortMode = sortMode);
    }

    public static NoteSyncResult SetManualOrder(string vaultFolder, IReadOnlyList<NoteEntry> entries, string? notesSubfolder = null)
    {
        if (entries.Any(entry => entry.Type != NoteType.Todo))
            return new(false, "", "Only todos can be reordered.");
        string[] keys = entries.Select(GetKey).Distinct(StringComparer.Ordinal).ToArray();
        return Update(vaultFolder, notesSubfolder, preferences =>
        {
            var visible = keys.ToHashSet(StringComparer.Ordinal);
            var remaining = new Queue<string>(keys);
            // Reuse visible slots, preserving hidden/missing task positions and all priorities.
            for (int index = 0; index < preferences.ManualOrder.Count; index++)
                if (visible.Contains(preferences.ManualOrder[index]))
                    preferences.ManualOrder[index] = remaining.Dequeue();
            while (remaining.Count > 0) preferences.ManualOrder.Add(remaining.Dequeue());
        });
    }

    private static string GetPath(string vaultFolder, string? notesSubfolder)
    {
        if (string.IsNullOrWhiteSpace(vaultFolder)) throw new ArgumentException("No Obsidian vault selected.");
        string folder = string.IsNullOrWhiteSpace(notesSubfolder) ? ObsidianNotesSync.DefaultNotesFolder : notesSubfolder.Trim();
        return Path.Combine(vaultFolder.Trim(), folder, FileName);
    }

    private static Preferences Read(string path)
    {
        if (!File.Exists(path)) return new();
        string json = File.ReadAllText(path, Encoding.UTF8);
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        // Require the complete known schema, so damaged or newer files cannot be replaced silently.
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Version", out var version) ||
            version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out int number) || number != 1 ||
            !root.TryGetProperty("SortMode", out _) || !root.TryGetProperty("Priorities", out _) || !root.TryGetProperty("ManualOrder", out _))
            throw new JsonException("Unrecognized todo organization metadata.");
        var result = JsonSerializer.Deserialize<Preferences>(json) ?? throw new JsonException("Missing metadata.");
        if (result.SortMode is not ("Priority" or "Manual") || result.Priorities == null || result.ManualOrder == null ||
            result.Priorities.Any(pair => string.IsNullOrWhiteSpace(pair.Key) || pair.Value is < 1 or > 4) ||
            result.ManualOrder.Any(string.IsNullOrWhiteSpace) || result.ManualOrder.Distinct(StringComparer.Ordinal).Count() != result.ManualOrder.Count)
            throw new JsonException("Invalid todo organization metadata.");
        return result;
    }

    private static NoteSyncResult Update(string vaultFolder, string? notesSubfolder, Action<Preferences> update)
    {
        string path = "";
        string? temporary = null;
        lock (WriteLock)
        {
            try
            {
                path = GetPath(vaultFolder, notesSubfolder);
                string? original = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null;
                var preferences = Read(path);
                update(preferences);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(stream, preferences, JsonOptions);
                    stream.Flush(flushToDisk: true);
                }
                if (!OperatingSystem.IsWindows() && original != null)
                    File.SetUnixFileMode(temporary, File.GetUnixFileMode(path));
                if ((File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : null) != original)
                    return new(false, path, "Todo preferences changed. Refresh and try again.");
                File.Move(temporary, path, overwrite: true);
                return new(true, path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or ArgumentException or NotSupportedException)
            {
                return new(false, path, "Could not save todo preferences. Check folder permissions and existing metadata.");
            }
            finally
            {
                if (temporary != null)
                    try { File.Delete(temporary); } catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
    }
}
