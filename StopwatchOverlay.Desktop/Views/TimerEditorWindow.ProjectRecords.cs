using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class TimerEditorWindow
{
    private static readonly IBrush PendingRecordPaper = Brush.Parse("#F6DFBF");
    private static readonly IBrush PendingRecordBorder = Brush.Parse("#BF8161");

    private Expander BuildProjectRecords(bool pixel)
    {
        foreach (var button in new[] { DiscardRecordsButton, DeleteTimerButton })
        {
            button.Height = 33;
            button.MinWidth = 113;
            button.Padding = new(11, 3);
            button.Margin = default;
            button.VerticalAlignment = VerticalAlignment.Center;
            if (pixel) button.Classes.Add("pixelAction");
        }
        DeleteTimerButton.Content = "Close timer…";

        var marker = new Border { Width = 7, Height = 7, CornerRadius = new(pixel ? 0 : 4), VerticalAlignment = VerticalAlignment.Center };
        var title = Text("", 15, strong: true);
        var statusLine = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 7, Children = { marker, title } };
        RecordsStatusText.Margin = default;
        RecordsStatusText.FontFamily = Body;
        RecordsStatusText.FontSize = 12;
        RecordsStatusText.Foreground = Muted;
        RecordsStatusText.TextWrapping = TextWrapping.Wrap;
        var copy = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { statusLine, RecordsStatusText } };
        var card = new TodoFrame
        {
            Pixel = pixel, CornerRadius = new(5), BorderThickness = new(1), Padding = new(12, 11),
            Child = RecordActionRow(copy, DiscardRecordsButton)
        };
        var closeRow = RecordActionRow(Text("Closing this timer keeps its saved records.", 12, Muted), DeleteTimerButton);
        var summary = Text("", 11, Muted);
        var section = new Expander
        {
            Name = "EditorRecordsExpander", Margin = new(0, 7, 0, 0),
            Header = new StackPanel { Spacing = 2, Children = { Text("Project records", 17, strong: true), summary } },
            Content = new StackPanel { Spacing = 9, Children = { card, closeRow } }
        };
        StyleEditorExpander(section);
        AutomationProperties.SetName(section, "Project records");
        section.PropertyChanged += (_, e) =>
        {
            if (e.Property == Expander.IsExpandedProperty) summary.IsVisible = !section.IsExpanded;
        };

        int count = _intervals.Count;
        string segments = count == 1 ? "1 segment" : $"{count} segments";
        void ShowDraft()
        {
            var status = _discardRecords ? "Removal pending" : count > 0 ? "Included in project history" : "No saved records";
            title.Text = summary.Text = status;
            title.Foreground = _discardRecords ? EditorDanger : Navy;
            summary.Foreground = _discardRecords ? EditorDanger : Muted;
            marker.Background = _discardRecords ? EditorDanger : count > 0 ? Teal : Muted;
            card.Background = _discardRecords ? PendingRecordPaper : FieldPaper;
            card.BorderBrush = _discardRecords ? PendingRecordBorder : Rule;
            RecordsStatusText.IsVisible = count > 0;
            RecordsStatusText.Text = _discardRecords
                ? $"Save & Apply will remove this timer’s {segments}."
                : $"{segments} saved for this timer.";
            DiscardRecordsButton.IsVisible = DiscardRecordsButton.IsEnabled = count > 0;
            DiscardRecordsButton.Content = _discardRecords ? "Keep records" : "Remove on save";
            DiscardRecordsButton.Classes.Set("editorDanger", !_discardRecords);
            ToolTip.SetTip(DiscardRecordsButton, _discardRecords
                ? "Cancel the pending removal and keep your other edits."
                : "Remove this timer’s segments from project history when you save.");
        }
        DiscardRecordsButton.Click += (_, _) =>
        {
            if (count == 0) return;
            _discardRecords = !_discardRecords;
            ShowDraft();
        };
        ShowDraft();
        return section;
    }

    private static Grid RecordActionRow(Control description, Button action)
    {
        var row = new Grid { ColumnDefinitions = new("*,16,Auto"), Children = { description, action } };
        Grid.SetColumn(action, 2);
        return row;
    }
}
