using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StopwatchOverlay;

public enum NoteType
{
    Todo = 1,
    Note = 2,
    Reminder = 3
}

public sealed record NoteEntry(
    NoteType Type,
    DateTime Timestamp,
    string Text,
    bool IsCompleted = false);

public sealed record NoteSyncResult(
    bool Success,
    string TargetFilePath,
    string? Message = null);

public static class ObsidianNotesSync
{
    private static readonly Regex TaskLine = new(@"^\s*-\s+\[([ xX])\]\s+(\d{1,2}:\d{2})\s*(?:⏰)?\s*(.*)$");

    public const string DefaultNotesFolder = "Notes";
    public const string TodosFileName = "Todos.md";
    public const string NotesFileName = "Notes.md";
    public const string RemindersFileName = "Reminders.md";

    public static string GetFileName(NoteType type) => type switch
    {
        NoteType.Todo => TodosFileName,
        NoteType.Note => NotesFileName,
        NoteType.Reminder => RemindersFileName,
        _ => NotesFileName
    };

    public static string GetDefaultHeading(NoteType type) => type switch
    {
        NoteType.Todo => "Todos",
        NoteType.Note => "Quick Notes",
        NoteType.Reminder => "Reminders",
        _ => "Notes"
    };

    public static NoteSyncResult AppendEntry(
        string vaultFolder,
        NoteType type,
        string text,
        string? notesSubfolder = null,
        DateTime? timestamp = null)
    {
        if (string.IsNullOrWhiteSpace(vaultFolder))
        {
            return new NoteSyncResult(false, "", "No Obsidian vault selected.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            return new NoteSyncResult(false, "", "Note text cannot be empty.");
        }

        try
        {
            string subfolder = string.IsNullOrWhiteSpace(notesSubfolder) ? DefaultNotesFolder : notesSubfolder.Trim();
            string folderPath = Path.Combine(vaultFolder.Trim(), subfolder);
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }

            if (type == NoteType.Todo)
            {
                var candidate = new NoteEntry(type, timestamp ?? DateTime.Now, text.Trim());
                var organization = TodoOrganizationStore.Load(vaultFolder, notesSubfolder);
                string key = TodoOrganizationStore.GetKey(candidate);
                string normalizedKey = TodoOrganizationStore.GetKey(candidate with { Text = TelegramNotesSync.NormalizeTodoText(candidate.Text) });
                if (organization.ResolveKey(key) != key || organization.ResolveKey(normalizedKey) != normalizedKey)
                    return new(false, "", "This title and time belong to an edited task. Choose a different title or add it a minute later.");
            }
            string fileName = GetFileName(type);
            string filePath = Path.Combine(folderPath, fileName);
            DateTime now = timestamp ?? DateTime.Now;
            string dateHeader = $"## {now:yyyy-MM-dd}";
            string formattedEntry = FormatEntry(type, text.Trim(), now);

            string updatedContent;
            if (File.Exists(filePath))
            {
                string existingContent = File.ReadAllText(filePath, Encoding.UTF8);
                updatedContent = InsertEntryIntoDocument(existingContent, dateHeader, formattedEntry);
            }
            else
            {
                updatedContent = CreateNewDocument(type, now, dateHeader, formattedEntry);
            }

            string tempFile = filePath + $".tmp.{Guid.NewGuid():N}";
            File.WriteAllText(tempFile, updatedContent, new UTF8Encoding(false));
            File.Move(tempFile, filePath, overwrite: true);

            return new NoteSyncResult(true, filePath, $"Saved to {fileName}");
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "ObsidianNotesSync.AppendEntry");
            return new NoteSyncResult(false, "", $"Failed to save note: {ex.Message}");
        }
    }

    /// <summary>Update one unchanged, unambiguous todo while preserving the surrounding Markdown.</summary>
    public static NoteSyncResult SetTodoCompleted(string vaultFolder, NoteEntry entry, bool completed, string? notesSubfolder = null)
        => ChangeTodo(vaultFolder, entry, completed, notesSubfolder);

    public static NoteSyncResult DeleteTodo(string vaultFolder, NoteEntry entry, string? notesSubfolder = null)
        => ChangeTodo(vaultFolder, entry, null, notesSubfolder);

    public static NoteSyncResult ValidateTodoForDeletion(string vaultFolder, NoteEntry entry, string? notesSubfolder = null)
        => ChangeTodo(vaultFolder, entry, null, notesSubfolder, validateOnly: true);

    public static NoteSyncResult UpdateTodoText(string vaultFolder, NoteEntry entry, string text, string? notesSubfolder = null, bool validateOnly = false)
        => ChangeTodo(vaultFolder, entry, entry.IsCompleted, notesSubfolder, validateOnly, text);

    public static NoteSyncResult SetEntryCompleted(string vaultFolder, NoteEntry entry, bool completed, string? notesSubfolder = null)
        => ChangeTask(vaultFolder, entry, completed, notesSubfolder);

    public static NoteSyncResult DeleteEntry(string vaultFolder, NoteEntry entry, string? notesSubfolder = null)
        => ChangeTask(vaultFolder, entry, null, notesSubfolder);

    public static NoteSyncResult UpdateEntryText(string vaultFolder, NoteEntry entry, string text, string? notesSubfolder = null, bool validateOnly = false)
        => ChangeTask(vaultFolder, entry, entry.IsCompleted, notesSubfolder, validateOnly, text);

    private static NoteSyncResult ChangeTodo(string vaultFolder, NoteEntry entry, bool? completed, string? notesSubfolder, bool validateOnly = false, string? replacementText = null)
        => entry.Type == NoteType.Todo
            ? ChangeTask(vaultFolder, entry, completed, notesSubfolder, validateOnly, replacementText)
            : new(false, "", "Only todos can be changed with this operation.");

    private static NoteSyncResult ChangeTask(string vaultFolder, NoteEntry entry, bool? completed, string? notesSubfolder, bool validateOnly = false, string? replacementText = null)
    {
        string folder = string.IsNullOrWhiteSpace(notesSubfolder) ? DefaultNotesFolder : notesSubfolder.Trim();
        string path = Path.Combine(vaultFolder, folder, GetFileName(entry.Type));
        string? temporary = null;
        try
        {
            if (entry.Type is not (NoteType.Todo or NoteType.Reminder) || string.IsNullOrWhiteSpace(vaultFolder) || !File.Exists(path))
                return new(false, path, "Task file is unavailable. Check your notes folder in Settings.");
            string original = File.ReadAllText(path, Encoding.UTF8);
            var candidates = ReadTaskBlocks(original, entry.Type)
                .Where(block => block.Entry.Timestamp == entry.Timestamp && block.Entry.Text == entry.Text)
                .Select(block => (Index: block.CheckboxIndex, Completed: block.Entry.IsCompleted,
                    LineStart: block.Start, LineLength: block.Length)).ToList();
            if (candidates.Count != 1)
                return new(false, path, candidates.Count == 0 ? "This task changed. Refresh and try again." : "Identical tasks share this time. Rename one in your notes file, then refresh.");
            var target = candidates[0];
            if (target.Completed != entry.IsCompleted)
                return new(false, path, "This task changed. Refresh and try again.");
            if (replacementText != null)
            {
                if (string.IsNullOrWhiteSpace(replacementText)) return new(false, path, "Task text cannot be empty.");
                if (ReadTaskBlocks(original, entry.Type).Any(block => block.Start != target.LineStart &&
                    block.Entry.Timestamp == entry.Timestamp && TelegramNotesSync.NormalizeTodoText(block.Entry.Text) == TelegramNotesSync.NormalizeTodoText(replacementText)))
                    return new(false, path, "Another task already has this text and time. Choose a different title.");
            }
            if (validateOnly || replacementText == entry.Text) return new(true, path);
            string updated;
            if (replacementText != null)
            {
                string oldBlock = original.Substring(target.LineStart, target.LineLength);
                string newline = original.Contains("\r\n") ? "\r\n" : "\n";
                string replacement = FormatEntry(entry.Type, replacementText, entry.Timestamp).Replace("\n", newline);
                if (entry.IsCompleted) replacement = replacement.Remove(3, 1).Insert(3, "x");
                if (oldBlock.EndsWith("\n") || oldBlock.EndsWith("\r")) replacement += newline;
                updated = original.Remove(target.LineStart, target.LineLength).Insert(target.LineStart, replacement);
            }
            else if (completed.HasValue)
                updated = original.Remove(target.Index, 1).Insert(target.Index, completed.Value ? "x" : " ");
            else
            {
                int end = target.LineStart + target.LineLength;
                updated = original.Remove(target.LineStart, end - target.LineStart);
            }
            temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            byte[] bytes = File.ReadAllBytes(path);
            bool bom = bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191;
            File.WriteAllText(temporary, updated, new UTF8Encoding(bom));
            if (!OperatingSystem.IsWindows()) File.SetUnixFileMode(temporary, File.GetUnixFileMode(path));
            if (File.ReadAllText(path, Encoding.UTF8) != original)
                return new(false, path, "Your notes file changed. Refresh and try again.");
            File.Move(temporary, path, overwrite: true);
            return new(true, path);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "ObsidianNotesSync.ChangeTodo");
            return new(false, path, "Could not change this todo. Check the notes folder permissions and try again.");
        }
        finally
        {
            if (temporary != null && File.Exists(temporary))
                try { File.Delete(temporary); } catch (IOException) { }
        }
    }

    public static string FormatEntry(NoteType type, string text, DateTime time)
    {
        string timeStr = time.ToString("HH:mm");
        return type switch
        {
            NoteType.Todo => $"- [ ] {timeStr} {IndentTaskText(text)}",
            NoteType.Reminder => $"- [ ] {timeStr} ⏰ {text.Replace("\r\n", " ").Replace("\n", " ")}",
            NoteType.Note => text.Contains('\n')
                ? $"### {timeStr}{Environment.NewLine}{text}{Environment.NewLine}"
                : $"- **{timeStr}** {text}",
            _ => $"- {timeStr} {text}"
        };
    }

    // Continuation lines belong to the parent Markdown checkbox, including nested lists.
    private static string IndentTaskText(string text) => text.Replace("\r\n", "\n").Replace('\r', '\n')
        .Replace("\n", "\n    ");

    private sealed record TaskBlock(NoteEntry Entry, int CheckboxIndex, int Start, int Length);

    private static IReadOnlyList<TaskBlock> ReadTaskBlocks(string content, NoteType type)
    {
        var result = new List<TaskBlock>();
        var lines = Regex.Matches(content, @"[^\r\n]*(?:\r\n|\r|\n|$)")
            .Cast<Match>().Where(m => m.Length > 0).ToArray();
        DateTime date = DateTime.Today;
        for (int i = 0; i < lines.Length; i++)
        {
            string raw = lines[i].Value.TrimEnd('\r', '\n');
            var heading = Regex.Match(raw, @"^##\s+(\d{4}-\d{2}-\d{2})");
            if (heading.Success && DateTime.TryParse(heading.Groups[1].Value, out var parsed)) date = parsed;
            // Indented checkboxes are content of the parent task, not independent todos.
            if (!raw.StartsWith("-")) continue;
            var task = TaskLine.Match(raw);
            if (!task.Success || !TimeSpan.TryParse(task.Groups[2].Value, out var time)) continue;
            int start = lines[i].Index, check = start + task.Groups[1].Index;
            int end = start + lines[i].Length;
            var text = new StringBuilder(task.Groups[3].Value.TrimStart());
            while (i + 1 < lines.Length)
            {
                string next = lines[i + 1].Value.TrimEnd('\r', '\n');
                if (next.StartsWith("    ") || next.StartsWith("\t") || next.StartsWith("  "))
                {
                    int indent = next.StartsWith("    ") ? 4 : next.StartsWith("\t") ? 1 : 2;
                    text.Append('\n').Append(next[indent..]);
                    i++; end = lines[i].Index + lines[i].Length;
                }
                else if (next.Length == 0 && i + 2 < lines.Length &&
                    (lines[i + 2].Value.StartsWith("  ") || lines[i + 2].Value.StartsWith("\t")))
                {
                    text.Append('\n'); i++; end = lines[i].Index + lines[i].Length;
                }
                else break;
            }
            result.Add(new TaskBlock(new NoteEntry(type, date.Date + time, text.ToString().TrimEnd(),
                task.Groups[1].Value != " "), check, start, end - start));
        }
        return result;
    }

    private static string CreateNewDocument(NoteType type, DateTime now, string dateHeader, string formattedEntry)
    {
        string title = GetDefaultHeading(type);
        string typeTag = type switch
        {
            NoteType.Todo => "todos",
            NoteType.Note => "notes",
            NoteType.Reminder => "reminders",
            _ => "notes"
        };
        var sb = new StringBuilder();
        sb.AppendLine("---");
        sb.AppendLine($"type: {typeTag}");
        sb.AppendLine($"created: {now:yyyy-MM-ddTHH:mm:ss}");
        sb.AppendLine("tags:");
        sb.AppendLine($"  - {typeTag}");
        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"# {title}");
        sb.AppendLine();
        sb.AppendLine(dateHeader);
        sb.AppendLine(formattedEntry);
        return sb.ToString();
    }

    public static string InsertEntryIntoDocument(string existingText, string dateHeader, string formattedEntry)
    {
        if (string.IsNullOrWhiteSpace(existingText))
        {
            return $"{dateHeader}{Environment.NewLine}{formattedEntry}{Environment.NewLine}";
        }

        var lines = existingText.Split(["\r\n", "\r", "\n"], StringSplitOptions.None).ToList();
        int dateIndex = -1;

        for (int i = 0; i < lines.Count; i++)
        {
            if (lines[i].Trim().Equals(dateHeader, StringComparison.OrdinalIgnoreCase))
            {
                dateIndex = i;
                break;
            }
        }

        if (dateIndex >= 0)
        {
            // Insert directly below the date header
            lines.Insert(dateIndex + 1, formattedEntry);
            return string.Join(Environment.NewLine, lines);
        }
        else
        {
            // Find where to append or insert the new date section.
            // Look for the first date header (## YYYY-MM-DD) to insert in reverse chronological order at top,
            // or if none found, append at bottom.
            int firstHeaderIndex = -1;
            for (int i = 0; i < lines.Count; i++)
            {
                if (Regex.IsMatch(lines[i].Trim(), @"^##\s+\d{4}-\d{2}-\d{2}"))
                {
                    firstHeaderIndex = i;
                    break;
                }
            }

            if (firstHeaderIndex >= 0)
            {
                lines.Insert(firstHeaderIndex, "");
                lines.Insert(firstHeaderIndex, formattedEntry);
                lines.Insert(firstHeaderIndex, dateHeader);
            }
            else
            {
                lines.Add("");
                lines.Add(dateHeader);
                lines.Add(formattedEntry);
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    public static IReadOnlyList<NoteEntry> LoadAllNotes(string vaultFolder, string? notesSubfolder = null)
    {
        var result = new List<NoteEntry>();
        if (string.IsNullOrWhiteSpace(vaultFolder))
            return result;

        string subfolder = string.IsNullOrWhiteSpace(notesSubfolder) ? DefaultNotesFolder : notesSubfolder.Trim();
        string folderPath = Path.Combine(vaultFolder.Trim(), subfolder);
        if (!Directory.Exists(folderPath))
            return result;

        LoadFileNotes(Path.Combine(folderPath, TodosFileName), NoteType.Todo, result);
        LoadFileNotes(Path.Combine(folderPath, RemindersFileName), NoteType.Reminder, result);
        LoadFileNotes(Path.Combine(folderPath, NotesFileName), NoteType.Note, result);

        return result
            .OrderByDescending(n => n.Timestamp)
            .ToList();
    }

    private static void LoadFileNotes(string filePath, NoteType type, List<NoteEntry> result)
    {
        if (!File.Exists(filePath))
            return;

        try
        {
            string content = File.ReadAllText(filePath, Encoding.UTF8);
            if (type == NoteType.Todo || type == NoteType.Reminder)
            {
                result.AddRange(ReadTaskBlocks(content, type).Select(block => block.Entry));
                return;
            }
            var lines = content.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

            DateTime currentDate = DateTime.Today;
            bool inFrontmatter = false;
            string? pendingSubheadingTime = null;
            var multilineSb = new StringBuilder();

            for (int i = 0; i < lines.Length; i++)
            {
                string rawLine = lines[i];
                string line = rawLine.Trim();

                if (i == 0 && line == "---")
                {
                    inFrontmatter = true;
                    continue;
                }
                if (inFrontmatter)
                {
                    if (line == "---") inFrontmatter = false;
                    continue;
                }

                // Check for Date Heading: ## YYYY-MM-DD
                var dateMatch = Regex.Match(line, @"^##\s+(\d{4}-\d{2}-\d{2})");
                if (dateMatch.Success && DateTime.TryParse(dateMatch.Groups[1].Value, out DateTime parsedDate))
                {
                    if (pendingSubheadingTime != null && multilineSb.Length > 0)
                    {
                        FlushMultilineNote(type, currentDate, pendingSubheadingTime, multilineSb, result);
                        pendingSubheadingTime = null;
                    }
                    currentDate = parsedDate;
                    continue;
                }

                // Check for Subheading note: ### HH:mm
                var subheadMatch = Regex.Match(line, @"^###\s+(\d{1,2}:\d{2})");
                if (subheadMatch.Success)
                {
                    if (pendingSubheadingTime != null && multilineSb.Length > 0)
                    {
                        FlushMultilineNote(type, currentDate, pendingSubheadingTime, multilineSb, result);
                    }
                    pendingSubheadingTime = subheadMatch.Groups[1].Value;
                    multilineSb.Clear();
                    continue;
                }

                if (pendingSubheadingTime != null)
                {
                    if (line.StartsWith('#'))
                    {
                        FlushMultilineNote(type, currentDate, pendingSubheadingTime, multilineSb, result);
                        pendingSubheadingTime = null;
                        // Re-process heading
                        i--;
                        continue;
                    }
                    multilineSb.AppendLine(rawLine);
                    continue;
                }

                // Check for Todo or Reminder: - [ ] HH:mm [⏰] text or - [x] HH:mm [⏰] text
                var taskMatch = TaskLine.Match(line);
                if (taskMatch.Success)
                {
                    bool isCompleted = taskMatch.Groups[1].Value.Equals("x", StringComparison.OrdinalIgnoreCase);
                    string timeStr = taskMatch.Groups[2].Value;
                    string text = taskMatch.Groups[3].Value.Trim();

                    DateTime entryTime = ParseDateTime(currentDate, timeStr);
                    result.Add(new NoteEntry(type, entryTime, text, isCompleted));
                    continue;
                }

                // Check for single-line note: - **HH:mm** text or - HH:mm text
                var noteMatch = Regex.Match(line, @"^-\s+(?:\*\*)?(\d{1,2}:\d{2})(?:\*\*)?\s+(.*)$");
                if (noteMatch.Success)
                {
                    string timeStr = noteMatch.Groups[1].Value;
                    string text = noteMatch.Groups[2].Value.Trim();
                    DateTime entryTime = ParseDateTime(currentDate, timeStr);
                    result.Add(new NoteEntry(type, entryTime, text, false));
                    continue;
                }
            }

            if (pendingSubheadingTime != null && multilineSb.Length > 0)
            {
                FlushMultilineNote(type, currentDate, pendingSubheadingTime, multilineSb, result);
            }
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "ObsidianNotesSync.LoadFileNotes");
        }
    }

    private static void FlushMultilineNote(
        NoteType type,
        DateTime date,
        string timeStr,
        StringBuilder sb,
        List<NoteEntry> result)
    {
        DateTime entryTime = ParseDateTime(date, timeStr);
        string text = sb.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(text))
        {
            result.Add(new NoteEntry(type, entryTime, text, false));
        }
        sb.Clear();
    }

    private static DateTime ParseDateTime(DateTime date, string timeStr)
    {
        if (TimeSpan.TryParse(timeStr, out TimeSpan ts))
        {
            return date.Date + ts;
        }
        return date;
    }
}
