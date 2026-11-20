using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Automation;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ConfirmationDialogWindow
{
    internal void ConfigureDeleteTodoDetail(string parent, TodoDetail detail, AppSettings settings)
    {
        bool note = detail.Kind == TodoDetailKind.Note;
        string action = note ? "Delete note" : "Delete subtask";
        string heading = action + "?";
        string label = note ? "Attached note" : "Subtask";
        const string effect = "The task and its other details will stay.";
        if (!PrepareTodoDeletion(settings, action, heading, action))
        {
            MessageText.Text = $"{label}\n{detail.Text}\n\nParent task\n{parent}\n\n{effect}";
            return;
        }

        var profile = settings.TodoAppearance.Current;
        var ink = TodoVisuals.Ink(profile);
        var muted = TodoVisuals.Muted(profile);
        var rule = TodoDeletionRule(profile);
        ScrollViewer Value(string text, string field, double size, double maxHeight)
        {
            var value = TodoDeletionText(text, size, ink);
            value.FontWeight = FontWeight.SemiBold;
            AutoTextDirection.Apply(value, text);
            var scroll = new ScrollViewer
            {
                MaxHeight = maxHeight, Content = value,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
            AutomationProperties.SetName(scroll, field);
            return scroll;
        }
        var card = new Border
        {
            Padding = new(15, 12), CornerRadius = new(5), BorderBrush = rule, BorderThickness = new(1),
            Background = profile.Background == "midnight-chart" ? TodoVisuals.Card(profile) : Brush.Parse("#F5FDF0D1"),
            Child = new StackPanel { Spacing = 5, Children =
            {
                TodoDeletionText(label, 12, muted), Value(detail.Text, label, 17, 140),
                new Border { Height = 1, Margin = new(0, 6), Background = rule },
                TodoDeletionText("Parent task", 12, muted), Value(parent, "Parent task", 14, 88)
            } }
        };
        var body = new ScrollViewer
        {
            Margin = new(22, 16, 22, 0), HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel { Spacing = 12, Children = { card, TodoDeletionText(effect, 13, ink) } }
        };
        SetTodoDeletionFrame(settings, heading, null, action, body, 480, null);
    }
}
