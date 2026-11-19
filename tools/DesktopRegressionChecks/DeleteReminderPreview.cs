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
using StopwatchOverlay.Desktop.Controls;

// Design-only native controls and sample reminders; no settings, services or deletion callbacks.
internal static class DeleteReminderPreview
{
    const string Output = "design/one-piece-v2/delete-reminder/native-preview";
    const double Width = 540;
    static readonly IBrush Ink = Brush.Parse("#102B3B"), Muted = Brush.Parse("#655539"), Gold = Brush.Parse("#B78329"),
        Rule = Brush.Parse("#CDB784"), Cream = Brush.Parse("#F8EACA"), Red = Brush.Parse("#8D3F35"), RedRule = Brush.Parse("#B67A60");
    static readonly FontFamily Body = StopwatchTypography.Resolve("modern-deck").NameFont;
    static readonly int[] Scales = [70, 90, 125];
    static readonly string[] States = ["scheduled", "unscheduled", "long-scheduled", "long-unscheduled"];
    static TextBlock Text(string value, double size = 13, IBrush? brush = null) => new()
    {
        Text = value, FontSize = size, FontFamily = Body, Foreground = brush ?? Ink,
        VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap
    };
    static Button Action(string label, bool safe)
    {
        var text = Text(label, 13, safe ? Cream : Red); text.FontWeight = FontWeight.SemiBold;
        var button = new Button
        {
            MinHeight = 36, Padding = new(12, 7), CornerRadius = new(4),
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center, Background = safe ? Ink : Cream,
            Foreground = safe ? Cream : Red, BorderBrush = safe ? Gold : RedRule, BorderThickness = new(1), Content = text
        };
        button.Resources["ButtonBackgroundPointerOver"] = Brush.Parse(safe ? "#214655" : "#F0DCC0");
        button.Resources["ButtonBackgroundPressed"] = Brush.Parse(safe ? "#081E2A" : "#E7C7AB");
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
            Comparison(state);
        }
    }
    static void Render(int percent, string state)
    {
        bool longText = state.StartsWith("long-"), scheduled = !state.EndsWith("unscheduled");
        double height = longText ? 520 : 450;
        string reminder = longText
            ? "Review the dashboard sketches and the full accessibility checklist before tomorrow’s planning session. Bring the notes about keyboard navigation, smaller screens, timer switching, and the remaining reminder improvements."
            : "Review the dashboard sketches.";
        var title = Text("Delete Reminder", 28);
        title.FontFamily = StopwatchTypography.Resolve("navigator-bold").NameFont; title.FontWeight = FontWeight.Bold;
        var close = new Button
        {
            Width = 30, Height = 30, MinHeight = 0, Padding = new(0), Background = Brushes.Transparent, BorderThickness = new(0),
            VerticalAlignment = VerticalAlignment.Top, HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center,
            Content = new Avalonia.Controls.Shapes.Path { Width = 15, Height = 15, Stretch = Stretch.Uniform, Data = Geometry.Parse("M1,1 L15,15 M15,1 L1,15"), Stroke = Ink, StrokeThickness = 1.7 }
        };
        AutomationProperties.SetName(close, "Cancel deleting this reminder"); ToolTip.SetTip(close, "Cancel (Esc)");
        var titleRow = new Grid { ColumnDefinitions = new("*,30"), Children = { title, close } }; Grid.SetColumn(close, 1);
        var header = new StackPanel { Spacing = 3, Children = { titleRow, Text("Review the reminder before deleting.", 12, Muted) } };
        var name = Text(reminder, 17); name.FontWeight = FontWeight.SemiBold;
        var reminderScroll = new ScrollViewer
        {
            Content = name, MaxHeight = 116, Margin = new(0, 5, 0, 0),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };
        AutomationProperties.SetName(reminderScroll, "Reminder text");
        var schedule = Text(scheduled ? "20 Nov 2026 · 18:30" : "No date or time set", 13);
        schedule.FontWeight = FontWeight.SemiBold;
        var scheduleInfo = new StackPanel { Spacing = 2, Children = { schedule } };
        if (scheduled) scheduleInfo.Children.Add(Text("Local time", 11, Muted));
        var scheduleRow = new Grid { ColumnDefinitions = new("100,*"), Children = { Text("Schedule", 12, Muted), scheduleInfo } };
        Grid.SetColumn(scheduleInfo, 1);
        var summary = new Border
        {
            Padding = new(14), CornerRadius = new(5), Background = Cream, BorderBrush = Rule, BorderThickness = new(1),
            Child = new StackPanel { Children =
            {
                Text("REMINDER", 10, Muted), reminderScroll,
                new Border { Margin = new(0, 12, 0, 0), Padding = new(0, 10, 0, 0), BorderBrush = Rule, BorderThickness = new(0, 1, 0, 0), Child = scheduleRow }
            } }
        };
        var warning = Text("This cannot be undone.", 12, Red); warning.FontWeight = FontWeight.SemiBold;
        var explanation = new StackPanel { Spacing = 5, Margin = new(0, 14, 0, 0), Children =
        {
            Text("Deletes this reminder from your local notes.", 12),
            Text("If linked, its Habitica task is deleted when sync runs.", 12, Muted),
            Text("Existing Telegram reminders are kept.", 12, Muted),
            new Border { Margin = new(0, 4, 0, 0), Child = warning }
        } };
        var body = new StackPanel { Margin = new(0, 16, 0, 0), Children = { summary, explanation } };
        var cancel = Action("Cancel", true); var delete = Action("Delete reminder", false);
        var footerRow = new Grid { ColumnDefinitions = new("*,10,94,10,142"), Children = { Text("Esc · cancel", 11, Muted), cancel, delete } };
        Grid.SetColumn(cancel, 2); Grid.SetColumn(delete, 4);
        var footer = new Border { Margin = new(0, 14, 0, 0), Padding = new(0, 12, 0, 0), BorderBrush = Rule, BorderThickness = new(0, 1, 0, 0), Child = footerRow };
        var layout = new Grid { RowDefinitions = new("Auto,*,Auto"), Children = { header, body, footer } }; Grid.SetRow(body, 1); Grid.SetRow(footer, 2);
        double scale = percent / 100d;
        var frame = new StopwatchFrameSurface { Width = Width, Height = height, ArtworkScale = .16, Padding = new(10), Background = Brush.Parse("#EBD3A5"), Child = layout };
        var window = new Window
        {
            Title = "Delete Reminder design preview", Width = Width * scale, Height = height * scale,
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
            Require(Math.Abs(window.ClientSize.Width - Width * scale) < 1.1 && Math.Abs(window.ClientSize.Height - height * scale) < 1.1, "Incorrect native dimensions.");
            foreach (var label in layout.GetVisualDescendants().OfType<TextBlock>())
            {
                Require(label.TextLayout.Height <= label.Bounds.Height + 1, "Text clipped: " + label.Text);
                var end = label.TranslatePoint(new Point(label.Bounds.Width, label.Bounds.Height), layout)!.Value;
                Require(end.X <= layout.Bounds.Width + 1 && end.Y <= layout.Bounds.Height + 1, "Text exceeds panel: " + label.Text);
            }
            Require(reminderScroll.Extent.Height <= reminderScroll.Viewport.Height + 1, "Fixture reminder needs scrolling; full sample should fit.");
            Require(explanation.TranslatePoint(new Point(0, explanation.Bounds.Height), frame)!.Value.Y <= footer.TranslatePoint(default, frame)!.Value.Y, "Explanation overlaps actions.");
            Require(footer.TranslatePoint(new Point(0, footer.Bounds.Height), frame)!.Value.Y < frame.Bounds.Height - 20, "Actions overlap ornaments.");
            foreach (var button in new[] { cancel, delete })
            {
                var label = (TextBlock)button.Content!; var center = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), button)!.Value;
                Require(Math.Abs(center.X - button.Bounds.Width / 2) < 1 && Math.Abs(center.Y - button.Bounds.Height / 2) < 1, "Button label is off center.");
            }
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
            if (scale >= 1) bitmap.Render(window);
            else { using var dc = bitmap.CreateDrawingContext(); dc.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
            bitmap.Save(Path.Combine(Output, $"delete-reminder-{state}-{percent}.png"));
            Console.WriteLine($"PASS Delete Reminder {state} {percent}%: dimensions, complete text, schedule, footer separation, centered actions and Cancel focus");
        }
        finally { window.Close(); }
    }
    static void Comparison(string state)
    {
        const int gap = 22, top = 56;
        double panelHeight = state.StartsWith("long-") ? 520 : 450;
        int width = (int)Scales.Sum(s => Width * s / 100) + gap * 4, height = (int)(panelHeight * 1.25) + top + gap;
        using var sheet = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var dc = sheet.CreateDrawingContext())
        {
            dc.DrawRectangle(Brush.Parse("#15232C"), null, new Rect(0, 0, width, height));
            int x = gap;
            foreach (int scale in Scales)
            {
                using var bitmap = new Bitmap(Path.Combine(Output, $"delete-reminder-{state}-{scale}.png"));
                var text = new FormattedText($"{scale}% · {bitmap.PixelSize.Width} × {bitmap.PixelSize.Height}", CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface(Body), 16, Cream);
                dc.DrawText(text, new Point(x, 20));
                dc.DrawImage(bitmap, new Rect(0, 0, bitmap.PixelSize.Width, bitmap.PixelSize.Height), new Rect(x, top, bitmap.PixelSize.Width, bitmap.PixelSize.Height));
                x += bitmap.PixelSize.Width + gap;
            }
        }
        sheet.Save(Path.Combine(Output, $"delete-reminder-{state}-comparison.png"));
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
