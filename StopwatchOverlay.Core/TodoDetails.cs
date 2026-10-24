using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace StopwatchOverlay;

public enum TodoDetailKind { Note, Subtask }
public sealed record TodoDetail(TodoDetailKind Kind, string Text, bool IsCompleted, int Start, int Length, string Source);
public sealed record TodoDetailDocument(string Body, IReadOnlyList<TodoDetail> Items);

/// <summary>Attached details use ordinary Markdown inside the existing parent task block.</summary>
public static class TodoDetails
{
    private static readonly Regex Checkbox = new(@"^- \[([ xX])\] (.*)$", RegexOptions.None, TimeSpan.FromSeconds(1));
    public static TodoDetailDocument Parse(string text)
    {
        var items = new List<TodoDetail>();
        var body = new StringBuilder();
        int position = 0;
        bool fenced = false;
        while (position < text.Length)
        {
            int start = position;
            string line = ReadLine(text, ref position);
            if (line.TrimStart().StartsWith("```") || line.TrimStart().StartsWith("~~~")) fenced = !fenced;
            var match = Checkbox.Match(line);
            bool note = line.StartsWith("> ") || line == ">";
            if (start == 0 || fenced || (!match.Success && !note)) { body.Append(text.AsSpan(start, position - start)); continue; }
            var lines = new List<string> { note ? (line.Length > 1 ? line[2..] : "") : match.Groups[2].Value };
            while (position < text.Length)
            {
                int next = position;
                string continuation = ReadLine(text, ref next);
                if (note && (continuation.StartsWith("> ") || continuation == ">")) lines.Add(continuation.Length > 1 ? continuation[2..] : "");
                else if (!note && continuation.StartsWith("  ")) lines.Add(continuation[2..]);
                else break;
                position = next;
            }
            int length = position - start;
            // Keep the separator newline outside the detail for safe replacement/deletion.
            if (length > 0 && text[start + length - 1] == '\n') length--;
            items.Add(new(note ? TodoDetailKind.Note : TodoDetailKind.Subtask, string.Join("\n", lines),
                !note && match.Groups[1].Value != " ", start, length, text.Substring(start, length)));
        }
        return new(body.ToString().TrimEnd(), items);
    }

    public static string Append(string text, TodoDetailKind kind, string content) => text.TrimEnd() + "\n\n" + Format(kind, content, false);
    public static string Replace(string text, TodoDetail detail, string? content)
    {
        if (detail.Start < 0 || detail.Start + detail.Length > text.Length || text.Substring(detail.Start, detail.Length) != detail.Source)
            throw new InvalidOperationException("This detail changed. Reopen the task and try again.");
        string replacement = content == null ? "" : Format(detail.Kind, content, detail.IsCompleted);
        return (text[..detail.Start] + replacement + text[(detail.Start + detail.Length)..]).TrimEnd();
    }
    public static string SetCompleted(string text, TodoDetail detail, bool completed)
    {
        if (detail.Kind != TodoDetailKind.Subtask) throw new ArgumentException("Only subtasks have checkboxes.");
        return Replace(text, detail with { IsCompleted = completed }, detail.Text);
    }
    private static string Format(TodoDetailKind kind, string content, bool completed)
    {
        string[] lines = content.Replace("\r\n", "\n").Replace('\r', '\n').Trim().Split('\n');
        if (string.IsNullOrWhiteSpace(content)) throw new ArgumentException("Enter some text.");
        return kind == TodoDetailKind.Note ? string.Join("\n", lines.Select(line => "> " + line))
            : "- [" + (completed ? "x" : " ") + "] " + lines[0] + string.Concat(lines.Skip(1).Select(line => "\n  " + line));
    }
    private static string ReadLine(string text, ref int position)
    {
        int end = text.IndexOf('\n', position);
        if (end < 0) end = text.Length;
        string line = text[position..end].TrimEnd('\r');
        position = end < text.Length ? end + 1 : end;
        return line;
    }
}
