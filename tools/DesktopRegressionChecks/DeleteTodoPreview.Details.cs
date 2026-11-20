using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Themes.Fluent;

internal static partial class DeleteTodoPreview
{
    internal const string IllustrationNote = "Use the second thumbnail for the final illustration. Keep the lighthouse silhouette clear against the sky, leave space beside the title, and check the lettering at print size before exporting the proof.";

    internal static void RunDetails()
    {
        if (Application.Current is not FrameSizePreviewApp) throw new Exception("Isolated preview app required.");
        Application.Current.Styles.Add(new FluentTheme());
        const string output = "design/one-piece-v2/delete-todo-detail/native-preview";
        Directory.CreateDirectory(output);
        var samples = new[]
        {
            (Kind: "subtask", Text: "Sketch layout"),
            (Kind: "note", Text: IllustrationNote)
        };

        foreach (var sample in samples)
        {
            string fileStem = Path.Combine(output, "delete-" + sample.Kind);
            string action = "Delete " + sample.Kind;
            foreach (int percent in Scales)
            {
                var detail = Text(sample.Text, 17);
                detail.FontWeight = FontWeight.SemiBold;
                var scroll = new ScrollViewer
                {
                    Content = detail, MaxHeight = 140,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto
                };
                string label = sample.Kind == "note" ? "Attached note" : "Subtask";
                AutomationProperties.SetName(scroll, label);
                var parent = Text("Finish illustration", 14);
                parent.FontWeight = FontWeight.SemiBold;
                var card = new Border
                {
                    Padding = new(15, 12), CornerRadius = new(5), Background = Brush.Parse("#F5FDF0D1"),
                    BorderBrush = Rule, BorderThickness = new(1),
                    Child = new StackPanel { Spacing = 5, Children =
                    {
                        Text(label, 12, Muted), scroll,
                        new Border { Height = 1, Margin = new(0, 6), Background = Rule },
                        Text("Parent task", 12, Muted), parent
                    } }
                };
                var body = new StackPanel { Spacing = 12, Margin = new(22, 16, 22, 0), Children =
                {
                    card, Text("The task and its other details will stay.", 13)
                } };
                RenderPanel(percent, fileStem, 480, null, action + "?", null, action, body, scroll);
            }
            Comparison(fileStem);
        }
    }
}
