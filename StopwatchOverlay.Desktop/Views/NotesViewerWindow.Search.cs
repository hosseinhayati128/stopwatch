using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class NotesViewerWindow
{
    private readonly Dictionary<string, string> _todoDisplayText = new(StringComparer.Ordinal);

    private string DisplayText(NoteEntry entry)
    {
        if (entry.Type != NoteType.Todo) return entry.Text;
        if (!_todoDisplayText.TryGetValue(entry.Text, out var display))
            _todoDisplayText[entry.Text] = display = string.Concat(FormattedTodoText.Parse(entry.Text).Select(p => p.Text));
        return display;
    }

    private void ConfigureSearch()
    {
        AutoTextDirection.Attach(SearchField);
        SearchField.TemplateApplied += (_, e) =>
        {
            if (e.NameScope.Find<Border>("PART_BorderElement") is { } inner)
            {
                inner.Background = Brushes.Transparent;
                inner.BorderThickness = new Thickness(0);
            }
        };
        SearchField.GotFocus += (_, _) => PauseAutoClose();
        ClearSearch.Click += (_, _) => ClearQuery();
        SearchField.TextChanged += (_, _) =>
        {
            bool hasText = !string.IsNullOrEmpty(SearchField.Text);
            ClearSearch.IsVisible = hasText;
            SearchShortcut.IsVisible = !hasText;
            SearchHint.Text = hasText ? "Esc clears search" : "Ctrl+F search  ·  Esc close";
            RenderEntries();
            NotesScrollViewer.Offset = default;
        };
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.F && e.KeyModifiers == KeyModifiers.Control)
            {
                SearchField.Focus();
                SearchField.SelectAll();
            }
            else if (e.Key == Key.Escape && !string.IsNullOrEmpty(SearchField.Text))
                ClearQuery();
            else if (e.Key != Key.Enter || !SearchField.IsFocused)
                return;
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
    }

    private void ClearQuery()
    {
        SearchField.Text = "";
        SearchField.Focus();
    }

    private void HighlightMatches(TextBlock text, string display, string query)
    {
        int match = display.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        if (match < 0) return;
        var runs = text.Inlines!.OfType<Run>().ToArray();
        if (runs.Length == 0)
        {
            text.Text = null;
            runs = [new Run(display)];
        }
        text.Inlines!.Clear();
        int start = 0;
        foreach (var source in runs)
        {
            string value = source.Text ?? "";
            int end = start + value.Length;
            for (int pos = start; pos < end;)
            {
                if (match >= 0 && pos == match + query.Length)
                    match = display.IndexOf(query, pos, StringComparison.OrdinalIgnoreCase);
                bool matched = match >= 0 && pos >= match;
                int next = match < 0 ? end : Math.Min(end, matched ? match + query.Length : match);
                // Reuse whole runs when possible. Splitting keeps the same character
                // offsets used for link hit testing, including matches across markup.
                var run = pos == start && next == end ? source : SliceRun(source, value[(pos-start)..(next-start)]);
                if (matched)
                {
                    run.FontWeight = FontWeight.Bold;
                    if (_viewerSummary != null) run.Foreground = ViewerTeal;
                }
                text.Inlines.Add(run);
                pos = next;
            }
            start = end;
        }
    }

    private static Run SliceRun(Run source, string value)
    {
        var run = new Run(value);
        if (source.IsSet(Run.FontFamilyProperty)) run.FontFamily = source.FontFamily;
        if (source.IsSet(Run.FontStyleProperty)) run.FontStyle = source.FontStyle;
        if (source.IsSet(Run.FontWeightProperty)) run.FontWeight = source.FontWeight;
        if (source.IsSet(Run.TextDecorationsProperty)) run.TextDecorations = source.TextDecorations;
        return run;
    }
}
