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

    public static string FormatEntry(NoteType type, string text, DateTime time)
    {
        string timeStr = time.ToString("HH:mm");
        return type switch
        {
            NoteType.Todo => $"- [ ] {timeStr} {text.Replace("\r\n", " ").Replace("\n", " ")}",
            NoteType.Reminder => $"- [ ] {timeStr} ⏰ {text.Replace("\r\n", " ").Replace("\n", " ")}",
            NoteType.Note => text.Contains('\n')
                ? $"### {timeStr}{Environment.NewLine}{text}{Environment.NewLine}"
                : $"- **{timeStr}** {text}",
            _ => $"- {timeStr} {text}"
        };
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
                var taskMatch = Regex.Match(line, @"^-\s+\[([ xX])\]\s+(\d{1,2}:\d{2})\s*(?:⏰)?\s*(.*)$");
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
