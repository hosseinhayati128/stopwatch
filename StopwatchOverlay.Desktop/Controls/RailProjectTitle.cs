using System.Globalization;
using Avalonia.Controls;
using Avalonia.Media.TextFormatting;

namespace StopwatchOverlay.Desktop.Controls;

public sealed class RailProjectTitle : TextBlock
{
    protected override TextLayout CreateTextLayout(string? text)
    {
        var layout = base.CreateTextLayout(text);
        if (string.IsNullOrEmpty(text) || layout.TextLines.Count != MaxLines) return layout;
        var last = layout.TextLines[^1];
        int end = last.FirstTextSourceIndex + last.Length;
        if (last.HasCollapsed || end >= text.Length) return layout;

        // Avalonia can end the second line at a word break without painting an ellipsis.
        // Shape only the visible prefix, retaining the original name for the tooltip and accessibility.
        string prefix = text[..end].TrimEnd();
        if (prefix.Length == 0) return layout;
        layout.Dispose();
        int[] starts = StringInfo.ParseCombiningCharacters(prefix);
        int low = 0, high = starts.Length;
        string Candidate(int count) => (count == starts.Length ? prefix : prefix[..starts[count]].TrimEnd()) + "…";
        while (low < high)
        {
            int middle = (low + high + 1) / 2;
            string candidate = Candidate(middle);
            using var probe = base.CreateTextLayout(candidate);
            var line = probe.TextLines[^1];
            if (line.FirstTextSourceIndex + line.Length >= candidate.Length) low = middle;
            else high = middle - 1;
        }
        return base.CreateTextLayout(Candidate(low));
    }
}
