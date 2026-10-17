using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;

namespace StopwatchOverlay;

/// <summary>A safe Markdown subset shared by native todo rendering and Telegram HTML.</summary>
public static class FormattedTodoText
{
    public sealed record Part(string Text, bool Bold = false, bool Italic = false, bool Code = false, string? Link = null);
    private static readonly Regex Inline = new(@"\*\*(.+?)\*\*|`([^`\n]+)`|\[([^\]\n]+)\]\((https?://(?:[^\s()]|\((?<Depth>)|\)(?<-Depth>))+(?(Depth)(?!)))\)|(?<!\*)\*([^*\n]+)\*(?!\*)",
        RegexOptions.Singleline, TimeSpan.FromSeconds(1));

    public static IReadOnlyList<Part> Parse(string markdown)
    {
        string text = markdown.Replace("\r\n", "\n").Replace('\r', '\n');
        text = Regex.Replace(text, @"(?m)^(\s*)[-*+] ", "$1• ");
        return ParseInline(text, 0);
    }

    private static List<Part> ParseInline(string text, int depth)
    {
        var parts = new List<Part>();
        if (depth > 8) { parts.Add(new(text)); return parts; }
        int position = 0;
        foreach (Match match in Inline.Matches(text))
        {
            if (match.Index > position) parts.Add(new(text[position..match.Index]));
            if (match.Groups[1].Success)
                parts.AddRange(ParseInline(match.Groups[1].Value, depth + 1).Select(p => p with { Bold = true }));
            else if (match.Groups[2].Success) parts.Add(new(match.Groups[2].Value, Code: true));
            else if (match.Groups[3].Success && Uri.TryCreate(match.Groups[4].Value, UriKind.Absolute, out var uri) &&
                (uri.Scheme == "http" || uri.Scheme == "https"))
                parts.AddRange(ParseInline(match.Groups[3].Value, depth + 1).Select(p => p with { Link = uri.AbsoluteUri }));
            else if (match.Groups[5].Success)
                parts.AddRange(ParseInline(match.Groups[5].Value, depth + 1).Select(p => p with { Italic = true }));
            else parts.Add(new(match.Value));
            position = match.Index + match.Length;
        }
        if (position < text.Length) parts.Add(new(text[position..]));
        return parts;
    }

    public static string ToTelegramHtml(string markdown) => string.Concat(Parse(markdown).Select(part =>
    {
        string value = WebUtility.HtmlEncode(part.Text);
        if (part.Code) value = "<code>" + value + "</code>";
        if (part.Bold) value = "<b>" + value + "</b>";
        if (part.Italic) value = "<i>" + value + "</i>";
        if (part.Link != null) value = "<a href=\"" + WebUtility.HtmlEncode(part.Link) + "\">" + value + "</a>";
        return value;
    }));
}
