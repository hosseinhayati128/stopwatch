using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

// Rearrange the real editor in the preview process. Saves go only to an in-memory callback.
internal static class TodoDetailEditorPreview
{
    const string Output = "design/one-piece-v2/todo-detail-editor/native-preview";
    const string Parent = "Finish illustration";
    static readonly IBrush Ink = TodoVisuals.Navy, Muted = Brush.Parse("#495953"), Focus = Brush.Parse("#287386");
    static readonly IBrush Rule = Brush.Parse("#B99C67");

    internal static readonly (bool Note, bool Edit, string Parent, string Text)[] Samples =
        [
            (Note: false, Edit: false, Parent, Text: ""),
            (Note: false, Edit: true, Parent: "Finish the winter exhibition poster and prepare the first batch of prints for the studio", Text: "Check the title lettering at print size."),
            (Note: true, Edit: false, Parent, Text: "پیش از چاپ، خوانایی عنوان را در اندازهٔ نهایی بررسی کنم."),
            (Note: true, Edit: true, Parent, Text: "Use the second thumbnail for the final illustration. Keep the lighthouse silhouette clear against the sky.\n\nLeave room for the title, then print a small proof to check the lettering.")
        ];

    internal static void Run()
    {
        if (Application.Current is not FrameSizePreviewApp) throw new InvalidOperationException("Preview host required.");
        Directory.CreateDirectory(Output);
        foreach (var sample in Samples)
        {
            string title = (sample.Edit ? "Edit " : "Add ") + (sample.Note ? "note" : "subtask");
            string stem = Path.Combine(Output, title.ToLowerInvariant().Replace(' ', '-'));
            foreach (int percent in new[] { 70, 90, 125 })
                Render(sample.Note, sample.Edit, title, sample.Parent, sample.Text, percent, stem);
            DeleteTodoPreview.Comparison(stem);
        }
    }

    static void Render(bool note, bool edit, string title, string parent, string content, int percent, string stem)
    {
        string? submitted = null;
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika };
        var window = NoteEntryWindow.ForTodoDetail(settings, title, edit ? content : "", value =>
        {
            submitted = value;
            return new NoteSyncResult(true, "");
        });
        try
        {
            var frame = (TodoFrame)window.Content!;
            var input = window.FindControl<TextBox>("NoteInputBox")!;
            var inputFrame = (Border)input.Parent!;
            var body = (StackPanel)inputFrame.Parent!;
            var close = window.FindControl<Button>("CloseButton")!;
            var cancel = window.FindControl<Button>("CancelButton")!;
            var save = window.FindControl<Button>("SaveButton")!;
            var footer = (StackPanel)save.Parent!.Parent!;
            var hint = window.FindControl<TextBlock>("EditorShortcutHint")!;
            var actions = (Grid)save.Parent!;

            close.Content = new Avalonia.Controls.Shapes.Path { Data = Geometry.Parse("M1,1 L15,15 M15,1 L1,15"),
                Width = 15, Height = 15, Stroke = TodoVisuals.Ivory, StrokeThickness = 1.7, Stretch = Stretch.Uniform };
            close.Width = close.Height = 32; close.CornerRadius = new(4);
            close.VerticalAlignment = VerticalAlignment.Center; close.Margin = new(0, 0, 12, 0);
            AutomationProperties.SetName(close, "Close editor");

            TextBlock Label(string value, double size, IBrush brush) => new()
            {
                Text = value, FontFamily = TodoVisuals.BodyFont, Foreground = brush,
                FontSize = size, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
            };
            var parentName = Label(parent, 15, Ink); parentName.FontWeight = FontWeight.SemiBold;
            AutoTextDirection.Apply(parentName, parent);
            var context = new Grid { ColumnDefinitions = new("Auto,12,*"), Children = { Label("Parent task", 12, Muted), parentName } };
            Grid.SetColumn(parentName, 2);
            body.Children.Insert(0, new Border { Child = context, Background = Brush.Parse("#F0F8E8C7"), BorderBrush = Rule,
                BorderThickness = new(2, 0, 0, 0), Padding = new(12, 9), Margin = new(0, 0, 0, 14) });
            string field = note ? "Attached note" : "Subtask";
            var fieldLabel = Label(field, 14, Ink); fieldLabel.FontWeight = FontWeight.SemiBold;
            fieldLabel.Margin = new(0, 0, 0, 5); body.Children.Insert(1, fieldLabel);
            AutomationProperties.SetName(input, field);
            inputFrame.Background = Brush.Parse("#FFF5DF"); inputFrame.Padding = new(5);
            inputFrame.CornerRadius = new(5); inputFrame.BorderThickness = new(1.5);
            inputFrame.Height = note ? 138 : 83;
            input.Foreground = input.CaretBrush = Ink;
            input.Padding = new(7, 5); input.FontSize = 16;
            input.VerticalContentAlignment = VerticalAlignment.Top;
            string prompt = note ? "Write a note for this task…" : "What needs to be done?";
            input.Watermark = null;
            AutomationProperties.SetHelpText(input, prompt);
            var placeholder = Label(prompt, 16, Muted);
            placeholder.Margin = new(8, 5, 8, 0); placeholder.VerticalAlignment = VerticalAlignment.Top;
            placeholder.IsHitTestVisible = false;
            inputFrame.Child = null;
            inputFrame.Child = new Grid { Children = { input, placeholder } };
            ScrollViewer.SetHorizontalScrollBarVisibility(input, ScrollBarVisibility.Disabled);
            ScrollViewer.SetVerticalScrollBarVisibility(input, ScrollBarVisibility.Auto);
            void UpdateFocus() => inputFrame.BorderBrush = input.IsFocused ? Focus : Rule;
            input.GotFocus += (_, _) => UpdateFocus(); input.LostFocus += (_, _) => UpdateFocus(); UpdateFocus();
            ((ScrollViewer)body.Parent!).Margin = new(22, 16, 22, 0);

            footer.Children.Remove(hint); actions.Children.Insert(0, hint);
            actions.ColumnDefinitions = new("*,Auto,8,Auto");
            Grid.SetColumn(cancel, 1); Grid.SetColumn(save, 3);
            footer.Margin = new(22, 13, 22, 17);
            footer.Children.Insert(0, new Border { Height = 1, Background = Rule });
            hint.HorizontalAlignment = HorizontalAlignment.Left; hint.VerticalAlignment = VerticalAlignment.Center;
            hint.Text = "Ctrl+Enter save  ·  Esc cancel"; hint.Foreground = Muted; hint.FontSize = 11;
            string actionName = edit ? "Save changes" : title;
            var actionLabel = Label(actionName, 14, TodoVisuals.Ivory); actionLabel.FontWeight = FontWeight.SemiBold;
            save.Content = actionLabel; AutomationProperties.SetName(save, actionName);
            save.MinWidth = 110; cancel.MinWidth = 78;
            save.MinHeight = cancel.MinHeight = 36; save.Padding = cancel.Padding = new(12, 6);
            save.FontWeight = FontWeight.SemiBold; save.FontSize = cancel.FontSize = 14;
            save.Opacity = 1;
            save.TemplateApplied += (_, e) =>
            {
                if (e.NameScope.Find<ContentPresenter>("PART_ContentPresenter") is { } presenter)
                    presenter.Background = Brushes.Transparent;
            };
            void UpdateSave()
            {
                placeholder.IsVisible = string.IsNullOrEmpty(input.Text);
                save.IsEnabled = !string.IsNullOrWhiteSpace(input.Text);
                save.Background = save.IsEnabled ? Ink : Brush.Parse("#E3D3B3");
                save.BorderBrush = save.IsEnabled ? TodoVisuals.Gold : Rule;
                actionLabel.Foreground = save.IsEnabled ? TodoVisuals.Ivory : Muted;
            }
            input.TextChanged += (_, _) => UpdateSave(); UpdateSave();
            if (!edit && content.Length != 0) input.Text = content;

            double scale = percent / 100d;
            window.Content = null;
            var root = new Grid { Width = 520, Children = { frame } };
            window.MinWidth = window.MinHeight = 0; window.Width = root.Width * scale; window.Height = double.NaN;
            window.RequestedThemeVariant = ThemeVariant.Light;
            window.SizeToContent = SizeToContent.Height; window.Topmost = window.ShowActivated = false;
            window.Content = new LayoutTransformControl { Child = root, LayoutTransform = new ScaleTransform(scale, scale) };
            window.Show(); ClockOverlayChecks.Pump(100);
            Require(input.IsFocused && input.Text == content && save.IsEnabled == !string.IsNullOrWhiteSpace(content), "Draft, save state or focus differs.");
            Require(input.FlowDirection == AutoTextDirection.Detect(content), "Input direction differs.");
            Require(Math.Abs(window.ClientSize.Width - 520 * scale) < 1.1 && Math.Abs(window.ClientSize.Height - root.Bounds.Height * scale) < 1.1, "Incorrect scale.");
            Require(parentName.TextLayout.Height <= parentName.Bounds.Height + 1 && hint.DesiredSize.Width <= hint.Bounds.Width + 1, "Parent or shortcut hint clipped.");
            Require(inputFrame.TranslatePoint(new Point(0, inputFrame.Bounds.Height), root)!.Value.Y + 6 <= footer.TranslatePoint(default, root)!.Value.Y, "Footer overlaps editor.");
            foreach (var element in new Control[] { close, input, hint, cancel, save })
            {
                var bounds = element.TranslatePoint(new Point(element.Bounds.Width, element.Bounds.Height), root)!.Value;
                Require(bounds.X <= root.Bounds.Width && bounds.Y <= root.Bounds.Height, "Control extends beyond frame.");
            }
            DeleteTodoNativeChecks.Capture(window, percent, $"{stem}-{percent}.png");

            void Press(Avalonia.Input.Key key, KeyModifiers modifiers = KeyModifiers.None) =>
                input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });
            if (content.Length == 0)
            {
                input.Text = "   "; ClockOverlayChecks.Pump(15); Press(Avalonia.Input.Key.Enter, KeyModifiers.Control);
                Require(window.IsVisible && submitted == null && !save.IsEnabled, "Blank input submitted.");
                input.Text = "Sketch layout"; ClockOverlayChecks.Pump(15); Require(save.IsEnabled, "Valid text cannot be added.");
            }
            if (percent == 70) Press(Avalonia.Input.Key.Escape);
            else if (percent == 90) Press(Avalonia.Input.Key.Enter, KeyModifiers.Control);
            else save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ClockOverlayChecks.Pump(30);
            Require(!window.IsVisible && submitted == (percent == 70 ? null : input.Text), "In-memory save or cancel failed.");
            Console.WriteLine($"PASS {title} {percent}%: text direction, layout, focus, save state and keyboard or button action.");
        }
        finally { window.Close(); }
    }

    static void Require(bool ok, string detail) { if (!ok) throw new Exception(detail); }
}
