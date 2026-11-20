using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;

// Design-only native controls and synthetic tasks; no settings, services or deletion callbacks.
internal static partial class DeleteTodoPreview
{
    const string Output = "design/one-piece-v2/delete-todo/native-preview";
    const double Width = 540;
    static readonly TodoAppearanceProfile Profile = new();
    static readonly IBrush Ink = TodoVisuals.Ink(Profile), Muted = TodoVisuals.Muted(Profile),
        Rule = Brush.Parse("#C4AB77"), Red = Brush.Parse("#8D3F35"), Teal = Brush.Parse("#21586A");
    static readonly int[] Scales = [70, 90, 125];
    static readonly string[] States = ["linked", "unlinked", "long-linked", "long-unlinked"];
    static double Height(string state) => state.StartsWith("long-") ? 500 : 430;
    static TextBlock Text(string value, double size = 13, IBrush? brush = null) => new()
    {
        Text = value, FontSize = size, FontFamily = TodoVisuals.BodyFont, Foreground = brush ?? Ink,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
    };
    static Button Action(string label, bool safe)
    {
        var text = Text(label, 13, safe ? TodoVisuals.Ivory : Red); text.FontWeight = FontWeight.SemiBold;
        var button = new Button
        {
            MinHeight = 36, Padding = new(12, 8), HorizontalAlignment = HorizontalAlignment.Stretch,
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = text
        };
        TodoVisuals.StyleButton(button, Profile);
        button.Background = safe ? TodoVisuals.Navy : Brush.Parse("#F8EACA");
        button.Foreground = text.Foreground;
        button.BorderBrush = safe ? TodoVisuals.Gold : Brush.Parse("#B67A60");
        AutomationProperties.SetName(button, label);
        return button;
    }
    internal static void Run()
    {
        if (Application.Current is not FrameSizePreviewApp) throw new Exception("Isolated preview app required.");
        Application.Current.Styles.Add(new FluentTheme()); Directory.CreateDirectory(Output);
        foreach (string state in States)
        {
            foreach (int scale in Scales) Render(scale, state);
            Comparison(Path.Combine(Output, $"delete-todo-{state}"));
        }
    }
    static void Render(int percent, string state)
    {
        bool longText = state.StartsWith("long-"), linked = !state.EndsWith("unlinked");
        string task = longText
            ? "Review the dashboard sketches and the full accessibility checklist before tomorrow’s planning session. Bring the notes about keyboard navigation, smaller screens, timer switching, and the remaining reminder improvements."
            : "Review the dashboard sketches.";
        var name = Text(task, 17); name.FontWeight = FontWeight.SemiBold;
        var taskScroll = new ScrollViewer
        {
            Content = name, MaxHeight = 116, Margin = new(0, 5, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        AutomationProperties.SetName(taskScroll, "Task text");
        var linkLabel = Text(linked ? "Linked message" : "No linked message", 11, linked ? TodoVisuals.Ivory : Muted);
        linkLabel.FontWeight = FontWeight.SemiBold;
        var linkBadge = new Border
        {
            Padding = new(9, 4), CornerRadius = new(4), Background = linked ? Teal : Brushes.Transparent,
            BorderBrush = linked ? Teal : Rule, BorderThickness = new(1), Child = linkLabel, HorizontalAlignment = HorizontalAlignment.Right
        };
        var telegramRow = new Grid { ColumnDefinitions = new("*,Auto"), Children = { Text("Telegram", 12, Muted), linkBadge } };
        Grid.SetColumn(linkBadge, 1);
        var summary = new Border
        {
            Padding = new(14), CornerRadius = new(5), Background = Brush.Parse("#EDFDF0D1"), BorderBrush = Rule, BorderThickness = new(1),
            Child = new StackPanel { Children =
            {
                Text("TASK", 10, Muted), taskScroll,
                new Border { Margin = new(0, 12, 0, 0), Padding = new(0, 10, 0, 0), BorderBrush = Rule, BorderThickness = new(0, 1, 0, 0), Child = telegramRow }
            } }
        };
        var warning = Text("This cannot be undone.", 12, Red); warning.FontWeight = FontWeight.SemiBold;
        var explanation = new StackPanel { Spacing = 6, Margin = new(0, 14, 0, 0), Children =
        {
            Text("Removes this task from your Obsidian todo file.", 12),
            Text(linked ? "Deletes its Telegram message and cancels unsent copies." : "Cancels any unsent Telegram copy.", 12),
            Text("If linked, its Habitica task is deleted when sync runs.", 12, Muted),
            new Border { Margin = new(0, 4, 0, 0), Child = warning }
        } };
        var body = new StackPanel { Margin = new(22, 18, 22, 0), Children = { summary, explanation } };
        RenderPanel(percent, Path.Combine(Output, $"delete-todo-{state}"), Width, Height(state), "Delete Todo",
            "Review this task before deleting.", "Delete todo", body, taskScroll);
    }

    static void RenderPanel(int percent, string fileStem, double width, double? height, string heading,
        string? subtitle, string action, StackPanel body, ScrollViewer textScroll)
    {
        var title = Text(heading, 26, TodoVisuals.Ivory); title.FontFamily = TodoVisuals.HeadingFont;
        var titles = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Children = { title } };
        if (subtitle != null) titles.Children.Add(Text(subtitle, 12, TodoVisuals.Ivory));
        var close = new Button
        {
            Width = 30, Height = 30, MinHeight = 0, Padding = new(0), Background = Brushes.Transparent, BorderThickness = new(0),
            HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Avalonia.Controls.Shapes.Path { Width = 15, Height = 15, Stretch = Stretch.Uniform, Data = Geometry.Parse("M1,1 L15,15 M15,1 L1,15"), Stroke = TodoVisuals.Ivory, StrokeThickness = 1.7 }
        };
        AutomationProperties.SetName(close, "Cancel"); ToolTip.SetTip(close, "Cancel (Esc)");
        var header = new Grid { ColumnDefinitions = new("68,*,46"), Background = TodoVisuals.Navy };
        var engraving = new TodoArtwork { Kind = "Header", Opacity = .65 }; Grid.SetColumnSpan(engraving, 3);
        header.Children.Add(engraving); header.Children.Add(new TodoArtwork { Choice = Profile.HeaderEmblem, Margin = new(10) });
        Grid.SetColumn(titles, 1); header.Children.Add(titles); Grid.SetColumn(close, 2); header.Children.Add(close);
        var cancel = Action("Cancel", true); var delete = Action(action, false);
        var footerRow = new Grid { ColumnDefinitions = new("*,94,10,124"), Children = { Text("Esc · cancel", 11, Muted), cancel, delete } };
        Grid.SetColumn(cancel, 1); Grid.SetColumn(delete, 3);
        var footer = new Border { Margin = new(22, 12, 22, 20), Padding = new(0, 12, 0, 0), BorderBrush = Rule, BorderThickness = new(0, 1, 0, 0), Child = footerRow };
        var layout = new Grid { RowDefinitions = new("74,*,Auto") };
        var map = new TodoArtwork { Kind = "Background", Choice = Profile.Background, Opacity = .65 };
        Grid.SetRow(map, 1); Grid.SetRowSpan(map, 2); layout.Children.Add(map);
        layout.Children.Add(header); Grid.SetRow(body, 1); layout.Children.Add(body); Grid.SetRow(footer, 2); layout.Children.Add(footer);
        TodoVisuals.AddShellTrim(layout);
        double scale = percent / 100d;
        var shell = new TodoFrame
        {
            Margin = new(8), Child = layout, Background = TodoVisuals.Paper(Profile), BorderBrush = TodoVisuals.Gold,
            BorderThickness = new(1), CornerRadius = new(9), ClipToBounds = true
        };
        var frame = new Grid { Width = width, Height = height ?? double.NaN, Children = { shell } };
        var window = new Window
        {
            Title = heading, Width = width * scale, Height = height * scale ?? double.NaN,
            SizeToContent = height.HasValue ? SizeToContent.Manual : SizeToContent.Height,
            ShowActivated = false, ShowInTaskbar = false, SystemDecorations = SystemDecorations.None, Background = Brushes.Transparent,
            TransparencyLevelHint = [WindowTransparencyLevel.Transparent], RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light,
            Content = new LayoutTransformControl { LayoutTransform = new ScaleTransform(scale, scale), Child = frame }
        };
        close.Click += (_, _) => window.Close(); cancel.Click += (_, _) => window.Close();
        window.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Escape) { e.Handled = true; window.Close(); } };
        try
        {
            window.Show(); ClockOverlayChecks.Pump(80); cancel.Focus(); ClockOverlayChecks.Pump(20);
            Require(cancel.IsFocused, "Cancel must receive initial focus.");
            Require(Math.Abs(window.ClientSize.Width - width * scale) < 1.1 && Math.Abs(window.ClientSize.Height - frame.Bounds.Height * scale) < 1.1, "Incorrect native dimensions.");
            foreach (var label in layout.GetVisualDescendants().OfType<TextBlock>())
            {
                Require(label.TextLayout.Height <= label.Bounds.Height + 1, "Text clipped: " + label.Text);
                var start = label.TranslatePoint(default, layout)!.Value;
                var end = label.TranslatePoint(new Point(label.Bounds.Width, label.Bounds.Height), layout)!.Value;
                Require(start.X >= 0 && start.Y >= 0 && end.X <= layout.Bounds.Width + 1 && end.Y <= layout.Bounds.Height + 1, "Text exceeds panel: " + label.Text);
            }
            Require(textScroll.Extent.Height <= textScroll.Viewport.Height + 1, "Sample text needs scrolling.");
            var last = body.Children.Last();
            double footerGap = footer.TranslatePoint(default, frame)!.Value.Y - last.TranslatePoint(new Point(0, last.Bounds.Height), frame)!.Value.Y;
            Require(footerGap >= 10, $"Explanation needs more separation from the actions ({footerGap:F1} DIP).");
            foreach (var button in new[] { cancel, delete })
            {
                var label = (TextBlock)button.Content!; var center = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), button)!.Value;
                Require(Math.Abs(center.X - button.Bounds.Width / 2) < 1 && Math.Abs(center.Y - button.Bounds.Height / 2) < 1, "Button label is off center.");
            }
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
            if (scale >= 1) bitmap.Render(window);
            else { using var dc = bitmap.CreateDrawingContext(); dc.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
            bitmap.Save($"{fileStem}-{percent}.png");
            Console.WriteLine($"PASS {Path.GetFileName(fileStem)} {percent}%: dimensions, complete text, footer separation, centered actions and Cancel focus");
        }
        finally { window.Close(); }
    }
    internal static void Comparison(string fileStem)
    {
        const int gap = 22, top = 56;
        var sizes = Scales.Select(scale =>
        {
            using var bitmap = new Bitmap($"{fileStem}-{scale}.png");
            return bitmap.PixelSize;
        }).ToArray();
        int width = sizes.Sum(s => s.Width) + gap * (sizes.Length + 1), height = sizes.Max(s => s.Height) + top + gap;
        using var sheet = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var dc = sheet.CreateDrawingContext())
        {
            dc.DrawRectangle(Brush.Parse("#15232C"), null, new Rect(0, 0, width, height));
            int x = gap;
            foreach (int scale in Scales)
            {
                using var bitmap = new Bitmap($"{fileStem}-{scale}.png");
                var text = new FormattedText($"{scale}% · {bitmap.PixelSize.Width} × {bitmap.PixelSize.Height}", CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface(TodoVisuals.BodyFont), 16, TodoVisuals.Ivory);
                dc.DrawText(text, new Point(x, 20));
                dc.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), new Rect(x, top, bitmap.PixelSize.Width, bitmap.PixelSize.Height));
                x += bitmap.PixelSize.Width + gap;
            }
        }
        sheet.Save($"{fileStem}-comparison.png");
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
