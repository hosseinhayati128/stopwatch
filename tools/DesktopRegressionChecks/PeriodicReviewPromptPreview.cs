using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using StopwatchOverlay.Desktop.Controls;

// Design spike only: sample periods and static countdown; no scheduler, settings or history access.
internal static class PeriodicReviewPromptPreview
{
    const string Output = "design/one-piece-v2/periodic-review-prompt/native-preview";
    static readonly IBrush Ink = Brush.Parse("#102B3B"), Muted = Brush.Parse("#655539"), Gold = Brush.Parse("#B78329"),
        Rule = Brush.Parse("#CDB784"), Cream = Brush.Parse("#F8EACA"), Teal = Brush.Parse("#147A83");
    static readonly FontFamily Body = StopwatchTypography.Resolve("modern-deck").NameFont;
    static TextBlock Text(string value, double size = 13, IBrush? brush = null) => new()
    {
        Text = value, FontSize = size, FontFamily = Body, Foreground = brush ?? Ink,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
    };

    static Button Choice(bool review, int interval)
    {
        var foreground = review ? Cream : Ink;
        var key = Text(review ? "Y" : "Esc", review ? 14 : 11, foreground);
        key.FontWeight = FontWeight.SemiBold; key.HorizontalAlignment = HorizontalAlignment.Center;
        var badge = new Border
        {
            Width = 32, Height = 28, CornerRadius = new(4), BorderBrush = review ? Gold : Rule, BorderThickness = new(1),
            Background = review ? Brush.Parse("#284654") : Brush.Parse("#F1DFB7"), VerticalAlignment = VerticalAlignment.Center, Child = key
        };
        var icon = new Avalonia.Controls.Shapes.Path
        {
            Width = 20, Height = 22, Stretch = Stretch.Uniform, Stroke = review ? Cream : Teal, StrokeThickness = 1.5,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
            Data = Geometry.Parse(review ? "M3,2 L16,2 L21,7 L21,23 L3,23 Z M16,2 L16,7 L21,7 M7,14 L10,17 L17,10"
                : "M3,3 L13,12 L3,21 Z M19,3 L19,21")
        };
        var title = Text(review ? "Review now" : "Skip this review", 15, foreground); title.FontWeight = FontWeight.SemiBold;
        var description = Text(review ? "Open Activity & Time Review" : $"Next reminder in {interval} min", 11,
            review ? Brush.Parse("#DACDAF") : Muted);
        var words = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Children = { title, description } };
        var row = new Grid { ColumnDefinitions = new("32,12,20,12,*"), Children = { badge, icon, words } };
        Grid.SetColumn(icon, 2); Grid.SetColumn(words, 4);
        var button = new Button
        {
            Name = review ? "ReviewNowSample" : "SkipReviewSample", MinHeight = 62, Padding = new(12, 9),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center, Background = review ? Ink : Cream,
            Foreground = foreground, BorderBrush = review ? Gold : Rule, BorderThickness = new(1), CornerRadius = new(5), Content = row
        };
        if (review)
        {
            button.Resources["ButtonBackgroundPointerOver"] = Brush.Parse("#214655");
            button.Resources["ButtonBackgroundPressed"] = Brush.Parse("#081E2A");
        }
        AutomationProperties.SetName(button, review ? "Y. Review now" : $"Escape. Skip this review; next reminder in {interval} minutes");
        return button;
    }

    internal static void Run()
    {
        if (Application.Current is not FrameSizePreviewApp) throw new Exception("Isolated preview app required.");
        Application.Current.Styles.Add(new FluentTheme()); Directory.CreateDirectory(Output);
        foreach (int percent in new[] { 70, 90, 125 }) Render(percent);
        Render(90, customTiming: true);
        Render(90, overnight: true);
    }

    static void Render(int percent, bool customTiming = false, bool overnight = false)
    {
        int interval = customTiming ? 45 : 30, snooze = customTiming ? 10 : 30, timeout = customTiming ? 8 : 5;
        var title = Text("Activity Review", 28); title.FontFamily = StopwatchTypography.Resolve("navigator-bold").NameFont; title.FontWeight = FontWeight.Bold;
        var close = new Button
        {
            Width = 30, Height = 30, MinHeight = 0, Padding = new(0), Background = Brushes.Transparent,
            BorderThickness = new(0), VerticalAlignment = VerticalAlignment.Top,
            Content = new Avalonia.Controls.Shapes.Path
            {
                Width = 15, Height = 15, Stretch = Stretch.Uniform, Data = Geometry.Parse("M1,1 L15,15 M15,1 L1,15"), Stroke = Ink, StrokeThickness = 1.7
            }
        };
        AutomationProperties.SetName(close, $"Close; remind again in {snooze} minutes");
        ToolTip.SetTip(close, $"Close and remind again in {snooze} min");
        var titleRow = new Grid { ColumnDefinitions = new("*,30"), Children = { title, close } }; Grid.SetColumn(close, 1);
        var header = new StackPanel { Spacing = 3, Children = { titleRow, Text("A quick look at your recent time.", 12, Muted) } };
        var period = Text(overnight ? "23:45 – 00:15" : customTiming ? "14:00 – 14:45" : "14:00 – 14:30", 21);
        period.FontWeight = FontWeight.SemiBold;
        var periodInfo = new StackPanel { Spacing = 3, Children = { Text(overnight ? "YESTERDAY → TODAY" : "TODAY", 10, Muted), period } };
        var duration = Text(customTiming ? "45 min" : "30 min", 23, Teal); duration.FontWeight = FontWeight.SemiBold;
        duration.HorizontalAlignment = HorizontalAlignment.Right;
        var summaryRow = new Grid { ColumnDefinitions = new("*,12,Auto"), Children = { periodInfo, duration } }; Grid.SetColumn(duration, 2);
        var summary = new Border
        {
            Padding = new(12, 10), Margin = new(0, 12, 0, 12), CornerRadius = new(5), BorderBrush = Rule,
            BorderThickness = new(1), Background = Brush.Parse("#26FFF3D8"), Child = summaryRow
        };
        var choices = new StackPanel { Spacing = 6, Children = { Choice(true, interval), Choice(false, interval) } };
        var countdown = Text($"Closes in {timeout}s", 12); countdown.FontWeight = FontWeight.SemiBold;
        var explanation = Text($"If ignored or closed: remind again in {snooze} min", 11, Muted);
        var footer = new StackPanel { Spacing = 4, Margin = new(0, 12, 0, 0), Children = { countdown, explanation } };
        var layout = new Grid { RowDefinitions = new("Auto,Auto,Auto,Auto"), Children = { header, summary, choices, footer } };
        Grid.SetRow(summary, 1); Grid.SetRow(choices, 2); Grid.SetRow(footer, 3);
        const double width = 480, height = 396; double scale = percent / 100d;
        var frame = new StopwatchFrameSurface { Width = width, Height = height, ArtworkScale = .16, Padding = new(10), Background = Brush.Parse("#EBD3A5"), Child = layout };
        var window = new Window
        {
            Title = "Periodic Activity Review design preview", Width = width * scale, Height = height * scale,
            ShowActivated = false, ShowInTaskbar = false, SystemDecorations = SystemDecorations.None, Background = Brushes.Transparent,
            RequestedThemeVariant = Avalonia.Styling.ThemeVariant.Light,
            Content = new LayoutTransformControl { LayoutTransform = new ScaleTransform(scale, scale), Child = frame }
        };
        try
        {
            window.Show(); ClockOverlayChecks.Pump(100);
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
            if (scale >= 1) bitmap.Render(window);
            else
            {
                using var dc = bitmap.CreateDrawingContext();
                dc.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size));
            }
            string state = customTiming ? "custom-timing" : overnight ? "overnight" : "standard";
            bitmap.Save(Path.Combine(Output, $"periodic-review-{state}-{percent}.png"));
            Require(Math.Abs(window.ClientSize.Width - width * scale) < 1.1 && Math.Abs(window.ClientSize.Height - height * scale) < 1.1, "Native size mismatch.");
            foreach (var label in layout.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible))
            {
                Require(label.TextLayout.Height <= label.Bounds.Height + 1, "Text clipped: " + label.Text);
                var start = label.TranslatePoint(default, layout)!.Value;
                var end = label.TranslatePoint(new Point(label.Bounds.Width, label.Bounds.Height), layout)!.Value;
                Require(start.X >= -1 && start.Y >= -1 && end.X <= layout.Bounds.Width + 1 && end.Y <= layout.Bounds.Height + 1, "Text exceeds panel: " + label.Text);
            }
            Require(choices.Children.Count == 2, "Expected two choices.");
            Require(footer.TranslatePoint(new Point(0, footer.Bounds.Height), frame)!.Value.Y < frame.Bounds.Height - 20, "Footer overlaps ornament.");
            Require(choices.TranslatePoint(new Point(0, choices.Bounds.Height), frame)!.Value.Y < countdown.TranslatePoint(default, frame)!.Value.Y, "Footer overlaps choices.");
            foreach (var choice in choices.Children.Cast<Button>())
            {
                var badge = (Border)((Grid)choice.Content!).Children[0]; var key = (TextBlock)badge.Child!;
                var center = key.TranslatePoint(new Point(key.Bounds.Width / 2, key.Bounds.Height / 2), badge)!.Value;
                Require(Math.Abs(center.X - badge.Bounds.Width / 2) < 1 && Math.Abs(center.Y - badge.Bounds.Height / 2) < 1, "Key is not centered.");
            }
            Console.WriteLine($"PASS Periodic Review {state} {percent}%: native size, text bounds, two choices, centered keys and footer separation");
        }
        finally { window.Close(); }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
