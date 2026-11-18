using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using StopwatchOverlay.Desktop.Controls;

// Design spike only: independent controls; never loads settings, hides the controller or exits the app.
internal static class CloseStopwatchPreview
{
    const string Output = "design/one-piece-v2/close-stopwatch/native-preview";
    static readonly IBrush Ink = Brush.Parse("#102B3B"), Muted = Brush.Parse("#655539"), Gold = Brush.Parse("#B78329"),
        Rule = Brush.Parse("#CDB784"), Cream = Brush.Parse("#F8EACA"), ExitInk = Brush.Parse("#8D3F35"), ExitRule = Brush.Parse("#B67A60");
    static readonly FontFamily Body = StopwatchTypography.Resolve("modern-deck").NameFont;
    static TextBlock Text(string value, double size = 13, IBrush? brush = null) => new()
    {
        Text = value, FontSize = size, FontFamily = Body, Foreground = brush ?? Ink,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
    };
    static Button Choice(bool minimize)
    {
        var foreground = minimize ? Cream : ExitInk;
        var icon = new Avalonia.Controls.Shapes.Path
        {
            Width = 22, Height = 22, Stretch = Stretch.Uniform, Stroke = foreground, StrokeThickness = 1.5,
            VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center,
            Data = Geometry.Parse(minimize ? "M2,15 L2,22 L22,22 L22,15 M7,12 L12,17 L17,12 M12,1 L12,17"
                : "M12,1 L12,12 M6,5 A10,10 0 1 0 18,5")
        };
        var title = Text(minimize ? "Minimize to tray" : "Exit Stopwatch", 15, foreground); title.FontWeight = FontWeight.SemiBold;
        var description = Text(minimize ? "Timers, overlays and shortcuts stay active." : "Close the app, overlays and shortcuts.", 11,
            minimize ? Brush.Parse("#DACDAF") : Muted);
        var words = new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { title, description } };
        var row = new Grid { ColumnDefinitions = new(minimize ? "22,12,*,10,48" : "22,12,*"), Children = { icon, words } }; Grid.SetColumn(words, 2);
        if (minimize)
        {
            var key = Text("Enter", 10.5, Cream); key.FontWeight = FontWeight.SemiBold; key.HorizontalAlignment = HorizontalAlignment.Center;
            var badge = new Border { Name = "CloseEnterBadge", Width = 48, Height = 26, Background = Brush.Parse("#284654"), BorderBrush = Gold,
                BorderThickness = new(1), CornerRadius = new(4), VerticalAlignment = VerticalAlignment.Center, Child = key };
            Grid.SetColumn(badge, 4); row.Children.Add(badge);
        }
        var button = new Button
        {
            Name = minimize ? "MinimizeSample" : "ExitSample", MinHeight = 72, Padding = new(12, 10),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Center, Background = minimize ? Ink : Cream,
            Foreground = foreground, BorderBrush = minimize ? Gold : ExitRule, BorderThickness = new(1), CornerRadius = new(5), Content = row
        };
        if (minimize)
        {
            button.Resources["ButtonBackgroundPointerOver"] = Brush.Parse("#214655");
            button.Resources["ButtonBackgroundPressed"] = Brush.Parse("#081E2A");
        }
        AutomationProperties.SetName(button, minimize ? "Minimize to tray. Timers, overlays and shortcuts stay active." : "Exit Stopwatch. Close the app, overlays and shortcuts.");
        return button;
    }
    internal static void Run()
    {
        if (Application.Current is not FrameSizePreviewApp) throw new Exception("Isolated preview app required.");
        Application.Current.Styles.Add(new FluentTheme()); Directory.CreateDirectory(Output);
        foreach (int percent in new[] { 70, 90, 125 }) Render(percent, false);
        Render(90, true);
    }
    static void Render(int percent, bool remember)
    {
        var title = Text("Close Stopwatch", 28); title.FontFamily = StopwatchTypography.Resolve("navigator-bold").NameFont; title.FontWeight = FontWeight.Bold;
        var close = new Button
        {
            Width = 30, Height = 30, MinHeight = 0, Padding = new(0), Background = Brushes.Transparent, BorderThickness = new(0),
            VerticalAlignment = VerticalAlignment.Top, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Avalonia.Controls.Shapes.Path { Width = 15, Height = 15, Stretch = Stretch.Uniform, Data = Geometry.Parse("M1,1 L15,15 M15,1 L1,15"), Stroke = Ink, StrokeThickness = 1.7 }
        };
        AutomationProperties.SetName(close, "Cancel closing Stopwatch"); ToolTip.SetTip(close, "Cancel (Esc)");
        var titleRow = new Grid { ColumnDefinitions = new("*,30"), Children = { title, close } }; Grid.SetColumn(close, 1);
        var header = new StackPanel { Spacing = 3, Children = { titleRow, Text("What would you like to do?", 12, Muted) } };
        var choices = new StackPanel { Spacing = 8, Margin = new(0, 14, 0, 0), Children = { Choice(true), Choice(false) } };
        var rememberLabel = Text("Remember my choice", 13); rememberLabel.Margin = new(8, 0, 0, 0);
        var checkbox = new CheckBox { Content = rememberLabel, IsChecked = remember, MinHeight = 28, Padding = new(0), Foreground = Ink };
        checkbox.Resources["CheckBoxCheckBackgroundFillChecked"] = Ink;
        checkbox.Resources["CheckBoxCheckBorderBrushChecked"] = Gold;
        checkbox.Resources["CheckBoxCheckGlyphForegroundChecked"] = Cream;
        AutomationProperties.SetName(checkbox, "Remember my choice for closing the controller");
        var rememberHint = Text(remember ? "Next time, use the action I choose here." : "Ask each time I close the controller.", 11, Muted); rememberHint.Margin = new(28, 0, 0, 0);
        var settingsHint = Text("Change later in Settings → Application.", 11, Muted); settingsHint.Margin = new(28, 0, 0, 0);
        var preference = new StackPanel { Spacing = 3, Margin = new(0, 14, 0, 0), Children = { checkbox, rememberHint, settingsHint } };
        var cancelLabel = Text("Cancel", 13); cancelLabel.FontWeight = FontWeight.SemiBold; cancelLabel.HorizontalAlignment = HorizontalAlignment.Center;
        var cancel = new Button { Width = 92, Height = 34, Padding = new(12, 5), Background = Cream, Foreground = Ink, BorderBrush = Gold, BorderThickness = new(1),
            CornerRadius = new(4), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Content = cancelLabel };
        AutomationProperties.SetName(cancel, "Cancel closing Stopwatch");
        var footerRow = new Grid { ColumnDefinitions = new("*,12,Auto"), Children = { Text("Esc · keep Stopwatch open", 11, Muted), cancel } }; Grid.SetColumn(cancel, 2);
        var footer = new Border { Margin = new(0, 14, 0, 0), Padding = new(0, 12, 0, 0), BorderBrush = Rule, BorderThickness = new(0, 1, 0, 0), Child = footerRow };
        var layout = new Grid { RowDefinitions = new("Auto,Auto,Auto,Auto"), Children = { header, choices, preference, footer } };
        Grid.SetRow(choices, 1); Grid.SetRow(preference, 2); Grid.SetRow(footer, 3);
        const double width = 500, height = 428; double scale = percent / 100d;
        var frame = new StopwatchFrameSurface { Width = width, Height = height, ArtworkScale = .16, Padding = new(10), Background = Brush.Parse("#EBD3A5"), Child = layout };
        var window = new Window
        {
            Title = "Close Stopwatch design preview", Width = width * scale, Height = height * scale,
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
            bitmap.Save(Path.Combine(Output, $"close-stopwatch-{(remember ? "remember" : "standard")}-{percent}.png"));
            Require(Math.Abs(window.ClientSize.Width - width * scale) < 1.1 && Math.Abs(window.ClientSize.Height - height * scale) < 1.1, "Native size mismatch.");
            foreach (var label in layout.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible))
            {
                Require(label.TextLayout.Height <= label.Bounds.Height + 1, "Text clipped: " + label.Text);
                var end = label.TranslatePoint(new Point(label.Bounds.Width, label.Bounds.Height), layout)!.Value;
                Require(end.X <= layout.Bounds.Width + 1 && end.Y <= layout.Bounds.Height + 1, "Text exceeds panel: " + label.Text);
            }
            Require(footer.TranslatePoint(new Point(0, footer.Bounds.Height), frame)!.Value.Y < frame.Bounds.Height - 20, "Footer overlaps ornament.");
            Require(preference.TranslatePoint(new Point(0, preference.Bounds.Height), frame)!.Value.Y < footer.TranslatePoint(default, frame)!.Value.Y, "Preference overlaps footer.");
            Require(choices.Children.Count == 2 && checkbox.IsChecked == remember, "Choice count or preference state is incorrect.");
            var badge = frame.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "CloseEnterBadge"); var key = (TextBlock)badge.Child!;
            var center = key.TranslatePoint(new Point(key.Bounds.Width / 2, key.Bounds.Height / 2), badge)!.Value;
            Require(Math.Abs(center.X - badge.Bounds.Width / 2) < 1 && Math.Abs(center.Y - badge.Bounds.Height / 2) < 1, "Enter badge is not centered.");
            Console.WriteLine($"PASS Close Stopwatch {(remember ? "remember" : "standard")} {percent}%: native size, text bounds, action count, preference state, centered Enter badge and footer separation");
        }
        finally { window.Close(); }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
