using System;
using System.Collections.Generic;
using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.TextFormatting;

namespace StopwatchOverlay.Desktop.Controls;

public static class TodoRichText
{
    public static TextBlock Create(string markdown, bool completed)
    {
        var text = new TodoTextBlock { TextWrapping = TextWrapping.Wrap, Opacity = completed ? .7 : 1 };
        var links = new List<(int Start, int End, string Url)>();
        var menu = new ContextMenu();
        int offset = 0;
        foreach (var part in FormattedTodoText.Parse(markdown))
        {
            var run = new Run(part.Text);
            if (part.Bold) run.FontWeight = FontWeight.Bold;
            if (part.Italic) run.FontStyle = FontStyle.Italic;
            if (part.Code) run.FontFamily = new FontFamily("monospace");
            if (completed) run.TextDecorations = TextDecorations.Strikethrough;
            if (part.Link != null)
            {
                if (!completed) run.TextDecorations = TextDecorations.Underline;
                run.FontWeight = FontWeight.SemiBold;
                links.Add((offset, offset + part.Text.Length, part.Link));
                string url = part.Link;
                var item = new MenuItem { Header = "Open " + part.Text };
                item.Click += (_, _) => Open(url);
                menu.Items.Add(item);
            }
            text.Inlines!.Add(run);
            offset += part.Text.Length;
        }
        if (links.Count > 0)
        {
            text.ContextMenu = menu;
            ToolTip.SetTip(text, "Click an underlined link to open it. Right-click for link actions.");
            text.PointerPressed += (_, e) =>
            {
                if (!e.GetCurrentPoint(text).Properties.IsLeftButtonPressed) return;
                int position = text.TextLayout.HitTestPoint(e.GetPosition(text)).TextPosition;
                foreach (var link in links)
                    if (position >= link.Start && position < link.End)
                    {
                        e.Handled = true;
                        Open(link.Url);
                        break;
                    }
            };
        }
        return text;
    }

    // This renderer creates only Runs. Rebuild them on demand if hover or scrolling
    // requests a layout after invalidation but before Avalonia's next measure pass.
    private sealed class TodoTextBlock : TextBlock
    {
        protected override TextLayout CreateTextLayout(string? text)
        {
            if (_textRuns == null && Inlines is { Count: > 0 })
            {
                var runs = new List<TextRun>();
                foreach (var inline in Inlines)
                {
                    if (inline is not Run run || string.IsNullOrEmpty(run.Text)) continue;
                    var properties = new GenericTextRunProperties(
                        new Typeface(run.FontFamily, run.FontStyle, run.FontWeight, run.FontStretch),
                        run.FontFeatures, run.FontSize, run.TextDecorations, run.Foreground);
                    runs.Add(new TextCharacters(run.Text, properties));
                }
                _textRuns = runs;
            }
            return base.CreateTextLayout(text);
        }
    }

    private static void Open(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch (Exception ex) { CrashLogger.LogRecoverable(ex, "TodoRichText.OpenLink"); }
    }
}
