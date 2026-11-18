using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using StopwatchOverlay.Desktop.Controls;

// Throwaway design controls: no controller, live timer, settings read or action handlers.
internal static class CloseTimerPreview
{
    const string Output = "design/one-piece-v2/close-timer/native-preview";
    static readonly IBrush Ink = Brush.Parse("#102B3B"), Muted = Brush.Parse("#655539"), Gold = Brush.Parse("#B78329"),
        Rule = Brush.Parse("#CDB784"), Cream = Brush.Parse("#F8EACA"), Red = Brush.Parse("#8D3F35"), RedRule = Brush.Parse("#B67A60"), Teal = Brush.Parse("#147A83");
    static readonly FontFamily Body = StopwatchTypography.Resolve("modern-deck").NameFont;
    static TextBlock Text(string value, double size = 13, IBrush? brush = null) => new()
    {
        Text = value, FontSize = size, FontFamily = Body, Foreground = brush ?? Ink,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
    };
    static Button Action(string label, bool cancel)
    {
        var text = Text(label, 13, cancel ? Cream : Red); text.FontWeight = FontWeight.SemiBold;
        var button = new Button
        {
            Name = cancel ? "CancelSample" : "CloseTimerSample", MinHeight = 36, Padding = new(12, 7), CornerRadius = new(4),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Background = cancel ? Ink : Cream, Foreground = cancel ? Cream : Red, BorderBrush = cancel ? Gold : RedRule, BorderThickness = new(1), Content = text
        };
        if (cancel)
        {
            button.Resources["ButtonBackgroundPointerOver"] = Brush.Parse("#214655");
            button.Resources["ButtonBackgroundPressed"] = Brush.Parse("#081E2A");
        }
        AutomationProperties.SetName(button, cancel ? "Cancel closing this timer" : "Close this timer");
        return button;
    }
    internal static void Run()
    {
        if (Application.Current is not FrameSizePreviewApp) throw new Exception("Isolated preview app required.");
        Application.Current.Styles.Add(new FluentTheme()); Directory.CreateDirectory(Output);
        foreach (int scale in new[] { 70, 90, 125 }) Render(scale, false);
        Render(90, true);
    }
    static void Render(int percent, bool longName)
    {
        var title = Text("Close Timer", 28); title.FontFamily = StopwatchTypography.Resolve("navigator-bold").NameFont; title.FontWeight = FontWeight.Bold;
        var close = new Button
        {
            Width = 30, Height = 30, MinHeight = 0, Padding = new(0), Background = Brushes.Transparent, BorderThickness = new(0),
            VerticalAlignment = VerticalAlignment.Top, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Avalonia.Controls.Shapes.Path { Width = 15, Height = 15, Stretch = Stretch.Uniform, Data = Geometry.Parse("M1,1 L15,15 M15,1 L1,15"), Stroke = Ink, StrokeThickness = 1.7 }
        };
        AutomationProperties.SetName(close, "Cancel closing this timer"); ToolTip.SetTip(close, "Cancel (Esc)");
        var titleRow = new Grid { ColumnDefinitions = new("*,30"), Children = { title, close } }; Grid.SetColumn(close, 1);
        var header = new StackPanel { Spacing = 3, Children = { titleRow, Text("Close the selected timer?", 12, Muted) } };
        var icon = new Avalonia.Controls.Shapes.Path
        {
            Width = 22, Height = 26, Stretch = Stretch.Uniform, Stroke = Teal, StrokeThickness = 1.5, VerticalAlignment = VerticalAlignment.Center,
            Data = Geometry.Parse("M8,1 L16,1 M12,1 L12,5 M19,5 L22,2 M12,6 A10,10 0 1 0 12,26 A10,10 0 1 0 12,6 M12,10 L12,16 L16,18")
        };
        var name = Text(longName ? "Programming · Desktop interface improvements" : "Programming", 18); name.FontWeight = FontWeight.SemiBold;
        var details = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { name, Text(longName ? "Paused stopwatch" : "Running stopwatch", 11, longName ? Muted : Teal) } };
        var time = Text("00:44:27", 24); time.FontWeight = FontWeight.SemiBold; time.HorizontalAlignment = HorizontalAlignment.Right;
        var elapsed = Text("Elapsed", 10, Muted); elapsed.HorizontalAlignment = HorizontalAlignment.Right;
        var reading = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Children = { time, elapsed } };
        var summaryRow = new Grid { ColumnDefinitions = new("22,12,*,14,Auto"), Children = { icon, details, reading } }; Grid.SetColumn(details, 2); Grid.SetColumn(reading, 4);
        var summary = new Border { MinHeight = 94, Margin = new(0, 14, 0, 0), Padding = new(12), CornerRadius = new(5), Background = Cream, BorderBrush = Rule, BorderThickness = new(1), Child = summaryRow };
        var explanation = new StackPanel { Spacing = 4, Margin = new(0, 14, 0, 0), Children =
        {
            Text("Stops this timer and removes it from your list.", 12),
            Text("Saved project records stay in your history.", 11, Muted)
        } };
        var cancel = Action("Cancel", true); var confirm = Action("Close Timer", false);
        var footerRow = new Grid { ColumnDefinitions = new("*,10,104,10,126"), Children = { Text("Esc · cancel", 11, Muted), cancel, confirm } };
        Grid.SetColumn(cancel, 2); Grid.SetColumn(confirm, 4);
        var footer = new Border { Margin = new(0, 14, 0, 0), Padding = new(0, 12, 0, 0), BorderBrush = Rule, BorderThickness = new(0, 1, 0, 0), Child = footerRow };
        var layout = new Grid { RowDefinitions = new("Auto,Auto,*,Auto"), Children = { header, summary, explanation, footer } };
        Grid.SetRow(summary, 1); Grid.SetRow(explanation, 2); Grid.SetRow(footer, 3);
        const double width = 500, height = 350; double scale = percent / 100d;
        var frame = new StopwatchFrameSurface { Width = width, Height = height, ArtworkScale = .16, Padding = new(10), Background = Brush.Parse("#EBD3A5"), Child = layout };
        var window = new Window
        {
            Title = "Close Timer design preview", Width = width * scale, Height = height * scale,
            ShowActivated = false, ShowInTaskbar = false, SystemDecorations = SystemDecorations.None, Background = Brushes.Transparent,
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light,
            Content = new LayoutTransformControl { LayoutTransform = new ScaleTransform(scale, scale), Child = frame }
        };
        try
        {
            window.Show(); ClockOverlayChecks.Pump(100);
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
            if (scale >= 1) bitmap.Render(window);
            else { using var dc = bitmap.CreateDrawingContext(); dc.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
            bitmap.Save(Path.Combine(Output, $"close-timer-{(longName ? "long-name" : "standard")}-{percent}.png"));
            Require(Math.Abs(window.ClientSize.Width - width * scale) < 1.1 && Math.Abs(window.ClientSize.Height - height * scale) < 1.1, "Incorrect native dimensions.");
            foreach (var label in layout.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible))
            {
                Require(label.TextLayout.Height <= label.Bounds.Height + 1, "Text clipped: " + label.Text);
                var end = label.TranslatePoint(new Point(label.Bounds.Width, label.Bounds.Height), layout)!.Value;
                Require(end.X <= layout.Bounds.Width + 1 && end.Y <= layout.Bounds.Height + 1, "Text exceeds panel: " + label.Text);
            }
            Require(summary.TranslatePoint(new Point(0, summary.Bounds.Height), frame)!.Value.Y <= explanation.TranslatePoint(default, frame)!.Value.Y, "Summary overlaps explanation.");
            Require(explanation.TranslatePoint(new Point(0, explanation.Bounds.Height), frame)!.Value.Y <= footer.TranslatePoint(default, frame)!.Value.Y, "Explanation overlaps footer.");
            Require(footer.TranslatePoint(new Point(0, footer.Bounds.Height), frame)!.Value.Y < frame.Bounds.Height - 20, "Footer overlaps ornament.");
            foreach (var button in new[] { cancel, confirm })
            {
                var label = (TextBlock)button.Content!; var center = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), button)!.Value;
                Require(Math.Abs(center.X - button.Bounds.Width / 2) < 1 && Math.Abs(center.Y - button.Bounds.Height / 2) < 1, "Button label is off center.");
            }
            Console.WriteLine($"PASS Close Timer {(longName ? "long name" : "standard")} {percent}%: native size, text bounds, summary/footer separation and centered buttons");
        }
        finally { window.Close(); }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
