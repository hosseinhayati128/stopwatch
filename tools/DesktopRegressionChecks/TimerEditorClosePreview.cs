using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

// Design spike: existing native controls with synthetic data; no editor or controller actions are invoked.
internal static class TimerEditorClosePreview
{
    const string Output = "design/one-piece-v2/timer-editor/close-timer-native-preview";
    const double Width = 500, Height = 380;
    static readonly int[] Scales = [70, 90, 125];
    static readonly FontFamily Body = StopwatchTypography.Resolve("modern-deck").NameFont;
    static readonly IBrush Cream = Brush.Parse("#F8EACA"), Muted = Brush.Parse("#655539");
    static T Named<T>(Window w, string name) where T : Control => w.FindControl<T>(name) ?? w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    static void Desktop(string type, string method, params object[] args) => typeof(ControllerWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
    static AppSettings Settings(int scale)
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = scale, TelegramEnabled = false, HabiticaEnabled = false, ActivityWatchEnabled = false };
        Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
        Desktop("DesktopPageAppearance", "Initialize", settings);
        Desktop("DesktopTypography", "Initialize", settings.Typography);
        return settings;
    }
    static void Shutdown() { Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    static TimerSession Sample(string state)
    {
        var timer = new TimerSession(4)
        {
            Name = state == "long-name" ? "Programming · Desktop interface improvements" : "Programming",
            Mode = 0, IsRunning = state != "paused", Category = "Work"
        };
        timer.RestoreElapsed(TimeSpan.FromSeconds(2667), false);
        return timer;
    }
    internal static void Run()
    {
        Require(Application.Current is FrameSizePreviewApp, "Isolated preview app required.");
        Directory.CreateDirectory(Output);
        Application.Current!.Styles.Add(new FluentTheme());
        Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")) { Source = new Uri("avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml") });
        foreach (string state in new[] { "running", "paused", "long-name" })
        {
            foreach (int scale in Scales) RenderConfirmation(state, scale);
            Comparison(state);
        }
        RenderEditor();
        Flow();
    }
    static void RenderConfirmation(string state, int percent)
    {
        var settings = Settings(percent); var timer = Sample(state);
        var window = new ConfirmationDialogWindow("Close Timer", "Close Timer", "", "Close Timer", true) { ShowActivated = false, ShowInTaskbar = false };
        try
        {
            typeof(ConfirmationDialogWindow).GetMethod("ConfigureCloseTimer", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, [timer, "00:44:27", settings, false]);
            window.Height = Height * percent / 100;
            window.Show(); ClockOverlayChecks.Pump(70);
            var heading = Named<TextBlock>(window, "HeadingText");
            var header = (StackPanel)heading.Parent!.Parent!;
            ((TextBlock)header.Children[1]).Text = "Close this timer from the editor?";
            var message = Named<TextBlock>(window, "MessageText");
            message.Text = timer.IsRunning ? "Stops this timer and closes the editor." : "Closes this timer and the editor.";
            ((StackPanel)message.Parent!).Children.Add(new TextBlock
            {
                Name = "EditorDraftHint", Text = "Unsaved editor changes won’t be applied.", FontFamily = Body, FontSize = 11,
                Foreground = Muted, TextWrapping = TextWrapping.Wrap
            });
            Named<TextBlock>(window, "CloseTimerFooterHint").Text = "Esc · back to editor";
            ClockOverlayChecks.Pump(30);
            Require(Math.Abs(window.ClientSize.Width - Width * percent / 100) < 1.1 && Math.Abs(window.ClientSize.Height - Height * percent / 100) < 1.1, "Unexpected native dialog size.");
            var frame = window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
            Require(Math.Abs(frame.Bounds.Width - Width) < 1.1 && Math.Abs(frame.Bounds.Height - Height) < 1.1, "Frame scaling differs from the native window.");
            foreach (var label in frame.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible))
            {
                Require(label.TextLayout.Height <= label.Bounds.Height + 1, "Clipped text: " + label.Text);
                var end = label.TranslatePoint(new Point(label.Bounds.Width, label.Bounds.Height), frame)!.Value;
                Require(end.X <= frame.Bounds.Width - 10 && end.Y <= frame.Bounds.Height - 15, "Text outside frame: " + label.Text);
            }
            var body = Named<ScrollViewer>(window, "CloseTimerBody");
            Require(body.Extent.Height <= body.Viewport.Height + 1, "Sample requires scrolling.");
            var cancel = Named<Button>(window, "CancelButton");
            Require(cancel.IsFocused, "Cancel should have initial focus.");
            Require(body.TranslatePoint(new Point(0, body.Bounds.Height), frame)!.Value.Y < cancel.TranslatePoint(default, frame)!.Value.Y, "Body overlaps footer.");
            Require(Named<TextBlock>(window, "CloseTimerName").Text == timer.Name, "Timer identity changed.");
            Require(Named<TextBlock>(window, "CloseTimerState").Text == (timer.IsRunning ? "Running stopwatch" : "Paused stopwatch"), "Incorrect timer state.");
            foreach (var button in new[] { cancel, Named<Button>(window, "ConfirmButton") })
            {
                var label = (TextBlock)button.Content!;
                var center = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), button)!.Value;
                Require(Math.Abs(center.X - button.Bounds.Width / 2) < 1 && Math.Abs(center.Y - button.Bounds.Height / 2) < 1, "Button label is off center.");
            }
            Capture(window, percent, $"close-timer-{state}-{percent}.png");
            Console.WriteLine($"PASS {state} {percent}%: dimensions, complete text, no scrolling, identity/state, Cancel focus, centered buttons and separated footer.");
        }
        finally { window.Close(); Shutdown(); }
    }
    static void RenderEditor()
    {
        var settings = Settings(90); var timer = Sample("running");
        var end = DateTime.Today.AddHours(9).AddSeconds(2667).ToUniversalTime();
        ProjectWorkIntervalView[] records = [new(Guid.NewGuid(), timer.Id, timer.Name, timer.Name, end.AddSeconds(-2667), end)];
        var window = new TimerEditorWindow(timer, [timer.Name, "Reading"], records, settings: settings) { ShowActivated = false, ShowInTaskbar = false };
        try
        {
            var action = Named<Button>(window, "DeleteTimerButton"); action.Content = "Close Timer…";
            Avalonia.Automation.AutomationProperties.SetName(action, "Close Timer");
            var hint = ((StackPanel)action.Parent!.Parent!).Children.OfType<TextBlock>()
                .Single(t => t.Text == "Closing the timer keeps saved project records.");
            window.Show(); ClockOverlayChecks.Pump(70);
            var frame = window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
            window.MinWidth = window.MinHeight = 0; window.Width = 620 * .9; window.Height = 780 * .9;
            frame.Width = 620; frame.Height = 780;
            Named<Expander>(window, "EditorRecordsExpander").IsExpanded = true;
            ClockOverlayChecks.Pump(50);
            var scroll = Named<ScrollViewer>(window, "EditorScroll"); scroll.Offset = new(0, scroll.Extent.Height); action.Focus(); ClockOverlayChecks.Pump(35);
            foreach (var c in new Control[] { action, hint })
            {
                var top = c.TranslatePoint(default, scroll)!.Value; var bottom = c.TranslatePoint(new Point(c.Bounds.Width, c.Bounds.Height), scroll)!.Value;
                Require(top.X >= 0 && top.Y >= 0 && bottom.X <= scroll.Bounds.Width + 1 && bottom.Y <= scroll.Bounds.Height + 1, "Editor close action or explanation is clipped.");
            }
            Require(!window.WasSaved && !window.DeleteTimerRequested && timer.Elapsed == TimeSpan.FromSeconds(2667), "Preview mutated editor or timer state.");
            Capture(window, 90, "editor-entry-90.png");
            Console.WriteLine("PASS editor entry 90%: native Close Timer action and history hint visible; no save/close action invoked.");
        }
        finally { window.Close(); Shutdown(); }
    }
    static void Capture(Window window, int percent, string name)
    {
        var frame = window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
        if (percent >= 100) bitmap.Render(window);
        else { using var dc = bitmap.CreateDrawingContext(); dc.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
        bitmap.Save(Path.Combine(Output, name));
    }
    static void Label(DrawingContext dc, string text, double x, double y, double size = 16) => dc.DrawText(new FormattedText(text,
        CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(Body), size, Cream), new Point(x, y));
    static void Place(DrawingContext dc, string file, int x, int y)
    {
        using var bitmap = new Bitmap(Path.Combine(Output, file));
        var size = new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height);
        dc.DrawImage(bitmap, new Rect(size), new Rect(new Point(x, y), size));
    }
    static void Comparison(string state)
    {
        const int gap = 22, top = 56;
        int width = (int)Scales.Sum(s => Width * s / 100) + gap * 4, height = (int)(Height * 1.25) + top + gap;
        using var sheet = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
        using (var dc = sheet.CreateDrawingContext())
        {
            dc.DrawRectangle(Brush.Parse("#15232C"), null, new Rect(0, 0, width, height));
            int x = gap;
            foreach (int scale in Scales)
            {
                Label(dc, $"{scale}% · {Width * scale / 100:0} × {Height * scale / 100:0}", x, 20);
                Place(dc, $"close-timer-{state}-{scale}.png", x, top);
                x += (int)(Width * scale / 100) + gap;
            }
        }
        sheet.Save(Path.Combine(Output, $"close-timer-{state}-comparison.png"));
    }
    static void Flow()
    {
        using var sheet = new RenderTargetBitmap(new PixelSize(1140, 820), new Vector(96, 96));
        using (var dc = sheet.CreateDrawingContext())
        {
            dc.DrawRectangle(Brush.Parse("#15232C"), null, new Rect(0, 0, 1140, 820));
            Label(dc, "Timer Editor → one confirmation", 22, 18, 24);
            Label(dc, "1 · Close Timer…  (90%)", 22, 61); Place(dc, "editor-entry-90.png", 22, 90);
            Label(dc, "2 · Confirm once  (90%)", 660, 160); Place(dc, "close-timer-running-90.png", 660, 190);
            Label(dc, "→", 604, 345, 28);
            Label(dc, "Cancel / Esc / X → return to editor", 670, 560);
            Label(dc, "Draft edits stay available.", 670, 587, 14);
            Label(dc, "Close Timer → close timer and editor", 670, 630);
            Label(dc, "Saved history stays. Draft edits are not applied.", 670, 657, 14);
            Label(dc, "Native design proposal · synthetic data", 670, 755, 12);
        }
        sheet.Save(Path.Combine(Output, "close-timer-editor-flow-90.png"));
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
