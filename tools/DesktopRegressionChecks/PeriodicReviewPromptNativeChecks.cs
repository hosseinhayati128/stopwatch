using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;
using StopwatchOverlay.PeriodicReview;

internal static class PeriodicReviewPromptNativeChecks
{
    const string Output = "design/one-piece-v2/periodic-review-prompt/implemented";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static T Named<T>(Window w, string name) where T : Control => w.FindControl<T>(name) ?? w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    static void Desktop(string type, string method, params object[] args) => typeof(ControllerWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    static DispatcherTimer Timer(PeriodicReviewPromptWindow w) => (DispatcherTimer)typeof(PeriodicReviewPromptWindow).GetField("_timer", Private)!.GetValue(w)!;
    static void Tick(PeriodicReviewPromptWindow w) => typeof(PeriodicReviewPromptWindow).GetMethod("Timer_Tick", Private)!.Invoke(w, [null, EventArgs.Empty]);
    static void Click(PeriodicReviewPromptWindow w, string name) => Named<Button>(w, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    sealed class Fixture : IDisposable
    {
        public readonly AppSettings Settings;
        public Fixture(int scale = 90, string theme = AppThemeCatalog.Nika)
        {
            Settings = new() { ThemeMode = theme, UiScalePercent = scale, TelegramEnabled = false, HabiticaEnabled = false };
            Desktop("ThemeResources", "ApplyApplication", theme); Desktop("DesktopPageAppearance", "Initialize", Settings); Desktop("DesktopTypography", "Initialize", Settings.Typography);
        }
        public PeriodicReviewPromptWindow Open(int timeout = 5, int snooze = 30, int interval = 30, Action<PeriodicReviewPromptResult>? callback = null, DateTime? start = null, DateTime? end = null)
        {
            object?[] args = [start ?? DateTime.Today.AddHours(14).ToUniversalTime(), end ?? DateTime.Today.AddHours(14.5).ToUniversalTime(), timeout, snooze, interval, callback];
            var ctor = typeof(PeriodicReviewPromptWindow).GetConstructors().Single(c => c.GetParameters().Length >= 6);
            if (ctor.GetParameters().Length == 7) args = [.. args, Settings];
            var w = (PeriodicReviewPromptWindow)ctor.Invoke(args); w.ShowActivated = false; w.Topmost = false; w.Show(); ClockOverlayChecks.Pump(65); return w;
        }
        public void Dispose() { Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }
    internal static void Layout()
    {
        Directory.CreateDirectory(Output);
        foreach (int scale in new[] { 70, 90, 125 })
        {
            using var f = new Fixture(scale); var w = f.Open(); Timer(w).Stop();
            try
            {
                Require(w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(), "Missing approved Activity Review frame.");
                Require(Math.Abs(w.ClientSize.Width - 480 * scale / 100d) < 1.1 && Math.Abs(w.ClientSize.Height - 396 * scale / 100d) < 1.1, "Incorrect compact native size.");
                Bounds(w); Capture(w, scale, "standard");
                var primary = Named<Button>(w, "ReviewNowButton");
                var presenter = primary.GetVisualDescendants().OfType<ContentPresenter>().Single(c => c.Name == "PART_ContentPresenter");
                foreach (string state in new[] { ":pointerover", ":pressed" })
                {
                    ((IPseudoClasses)primary.Classes).Set(state, true); ClockOverlayChecks.Pump(20);
                    Require(presenter.Background is ISolidColorBrush brush && brush.Color.A == 255 && brush.Color.R < 60 && brush.Color.G < 100, "Primary action loses dark contrast on " + state);
                    ((IPseudoClasses)primary.Classes).Set(state, false);
                }
            }
            finally { w.Close(); }
        }
        using (var f = new Fixture())
        {
            var w = f.Open(8, 10, 45, end: DateTime.Today.AddHours(14.75).ToUniversalTime()); Timer(w).Stop();
            try { Bounds(w); Capture(w, 90, "custom-timing"); } finally { w.Close(); }
            w = f.Open(start: DateTime.Today.AddMinutes(-15).ToUniversalTime(), end: DateTime.Today.AddMinutes(15).ToUniversalTime()); Timer(w).Stop();
            try { Bounds(w); Capture(w, 90, "overnight"); } finally { w.Close(); }
            f.Settings.Typography.Global.SizePercent = 125; Desktop("DesktopTypography", "Apply", f.Settings.Typography);
            w = f.Open(120, 1440, 1440); Timer(w).Stop();
            try
            {
                Bounds(w); var scroll = Named<ScrollViewer>(w, "ReviewPromptBody"); scroll.Offset = new(0, 10000); ClockOverlayChecks.Pump(25);
                Bounds(w); Capture(w, 90, "large-text");
                f.Settings.UiScalePercent = 125; Desktop("DesktopPageAppearance", "Apply", f.Settings, true); ClockOverlayChecks.Pump(40); Bounds(w);
                var roomyScreen = new Screen(1.25, new PixelRect(0, 0, 1366, 900), new PixelRect(0, 32, 1366, 848), true);
                typeof(PeriodicReviewPromptWindow).GetMethod("FitReviewPromptToScreen", Private)!.Invoke(w, [roomyScreen]); ClockOverlayChecks.Pump(40);
                Require(Math.Abs(w.Position.X - 308) <= 1 && Math.Abs(w.Position.Y - 146) <= 1, $"Popup is not centered on both axes: {w.Position}.");
                var screen = new Screen(1.25, new PixelRect(0, 0, 800, 600), new PixelRect(0, 32, 800, 548), true);
                typeof(PeriodicReviewPromptWindow).GetMethod("FitReviewPromptToScreen", Private)?.Invoke(w, [screen]); ClockOverlayChecks.Pump(50);
                Require(w.ClientSize.Width * 1.25 <= 801 && w.ClientSize.Height * 1.25 <= 549, "Popup exceeds small work area.");
                Require(w.Position.X >= 0 && w.Position.Y >= 32 && w.Position.X + w.ClientSize.Width * 1.25 <= 801 && w.Position.Y + w.ClientSize.Height * 1.25 <= 581, "Popup placement exceeds work area.");
                Bounds(w); scroll.Offset = new(0, 10000); ClockOverlayChecks.Pump(25); Capture(w, 125, "small-screen");
            }
            finally { w.Close(); }
        }
    }
    static void Bounds(Window w)
    {
        var frame = w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        var edge = frame.TranslatePoint(new Point(frame.Bounds.Width, frame.Bounds.Height), w)!.Value;
        Require(Math.Abs(edge.X - w.ClientSize.Width) < 1.5 && Math.Abs(edge.Y - w.ClientSize.Height) < 1.5, "Frame does not fit window.");
        foreach (var name in new[] { "ReviewPromptHeading", "CountdownText", "FooterHintText" })
        {
            var text = Named<TextBlock>(w, name); Require(text.TextLayout.Height <= text.Bounds.Height + 1, name + " clips vertically.");
            var bottom = text.TranslatePoint(new Point(text.Bounds.Width, text.Bounds.Height), frame)!.Value;
            Require(bottom.X < frame.Bounds.Width - 10 && bottom.Y < frame.Bounds.Height - 15, name + " exceeds frame.");
        }
        var scroll = Named<ScrollViewer>(w, "ReviewPromptBody"); Require(scroll.Viewport.Height >= 100, "Action viewport collapsed.");
        Require(scroll.TranslatePoint(new Point(0, scroll.Bounds.Height), frame)!.Value.Y < Named<TextBlock>(w, "CountdownText").TranslatePoint(default, frame)!.Value.Y, "Actions overlap footer.");
        foreach (string name in new[] { "ReviewNowButton", "SkipButton" })
        {
            var button = Named<Button>(w, name); Require(button.IsEffectivelyVisible && button.IsEnabled, "Action unavailable.");
            foreach (var label in button.GetVisualDescendants().OfType<TextBlock>()) Require(label.TextLayout.Height <= label.Bounds.Height + 1, "Action text clips.");
            var badge = (Border)((Grid)button.Content!).Children[0]; var key = (TextBlock)badge.Child!;
            var center = key.TranslatePoint(new Point(key.Bounds.Width / 2, key.Bounds.Height / 2), badge)!.Value;
            Require(Math.Abs(center.X - badge.Bounds.Width / 2) < 1 && Math.Abs(center.Y - badge.Bounds.Height / 2) < 1, "Shortcut badge is off center.");
        }
    }
    static void Capture(Window w, int percent, string state)
    {
        var frame = w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width), (int)Math.Ceiling(w.ClientSize.Height)), new Vector(96, 96));
        if (percent >= 100) bitmap.Render(w);
        else { using var dc = bitmap.CreateDrawingContext(); dc.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
        bitmap.Save(Path.Combine(Output, $"periodic-review-{state}-{percent}.png"));
    }
    internal static void Periods()
    {
        using var f = new Fixture();
        foreach (var (start, end, day, duration) in new[] {
            (DateTime.Today.AddHours(14).ToUniversalTime(), DateTime.Today.AddHours(14.5).ToUniversalTime(), "TODAY", "30 min"),
            (DateTime.Today.AddMinutes(-15).ToUniversalTime(), DateTime.Today.AddMinutes(15).ToUniversalTime(), "YESTERDAY → TODAY", "30 min"),
            (new DateTime(2021,3,21,20,15,0,DateTimeKind.Utc), new DateTime(2021,3,21,21,15,0,DateTimeKind.Utc), "", "60 min") })
        {
            var w = f.Open(start: start, end: end); Timer(w).Stop();
            try
            {
                var label = Named<TextBlock>(w, "ReviewPromptDate").Text!;
                Require(day.Length == 0 ? label.Contains("2021") : label == day, "Period date is misleading.");
                Require(Named<TextBlock>(w, "ReviewPromptDuration").Text == duration, "UTC period duration is wrong.");
            }
            finally { w.Close(); }
        }
    }
    internal static void Behavior()
    {
        using var f = new Fixture();
        void Check(Action<PeriodicReviewPromptWindow> action, PeriodicReviewPromptResult expected, int timeout = 5)
        {
            int calls = 0; bool closed = false; PeriodicReviewPromptResult? received = null;
            var w = f.Open(timeout, 10, 45, result => { Require(closed, "Callback ran before the window closed."); calls++; received = result; });
            w.Closed += (_, _) => closed = true;
            try
            {
                Require(Timer(w).IsEnabled, "Countdown did not start."); action(w); ClockOverlayChecks.Pump(15);
                Require(!w.IsVisible && calls == 1 && received == expected && w.Result == expected, "Incorrect or repeated result.");
                Require(!Timer(w).IsEnabled, "Countdown survived dismissal."); w.Close(); Tick(w); Require(calls == 1, "Closed popup repeated callback.");
            }
            finally { w.Close(); }
        }
        Check(w => Click(w, "ReviewNowButton"), PeriodicReviewPromptResult.Accepted);
        Check(w => Click(w, "SkipButton"), PeriodicReviewPromptResult.Skipped);
        foreach (var (key, expected) in new[] { (Avalonia.Input.Key.Y, PeriodicReviewPromptResult.Accepted), (Avalonia.Input.Key.Escape, PeriodicReviewPromptResult.Skipped) })
            Check(w => { var target = Named<Button>(w, key == Avalonia.Input.Key.Y ? "SkipButton" : "ReviewNowButton"); target.Focus(); target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key }); }, expected);
        Check(w => Click(w, "ReviewPromptClose"), PeriodicReviewPromptResult.Ignored);
        Check(w => w.Close(), PeriodicReviewPromptResult.Ignored);
        Check(Tick, PeriodicReviewPromptResult.Ignored, 1);
        var countdown = f.Open(8, 10, 45);
        try
        {
            Tick(countdown); Require(Named<TextBlock>(countdown, "CountdownText").Text == "Closes in 7s", "Live countdown did not update.");
            Require(Named<TextBlock>(countdown, "SkipSubText").Text!.Contains("45 min") && Named<TextBlock>(countdown, "FooterHintText").Text!.Contains("10 min"), "Skip and snooze intervals were confused.");
        }
        finally { countdown.Close(); }
        foreach (var theme in new[] { AppThemeCatalog.Midnight, AppThemeCatalog.Daylight })
        {
            f.Settings.ThemeMode = theme; Desktop("ThemeResources", "ApplyApplication", theme);
            var w = f.Open(8, start: new DateTime(2021, 3, 21, 20, 15, 0, DateTimeKind.Utc), end: new DateTime(2021, 3, 21, 21, 15, 0, DateTimeKind.Utc));
            try
            {
                Require(!w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(), "Nika frame leaked to classic themes.");
                Require(Named<TextBlock>(w, "PeriodSummaryText").Text!.Contains(", 60m)"), "Classic duration used local clock arithmetic: " + Named<TextBlock>(w, "PeriodSummaryText").Text);
                Tick(w); Require(Named<TextBlock>(w, "CountdownText").Text!.StartsWith("Closing in 7s"), "Classic countdown regressed."); Click(w, "SkipButton"); Require(w.Result == PeriodicReviewPromptResult.Skipped, "Classic action regressed.");
            }
            finally { w.Close(); }
        }
    }
}
