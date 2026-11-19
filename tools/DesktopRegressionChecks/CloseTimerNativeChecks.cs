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
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class CloseTimerNativeChecks
{
    const string Output = "design/one-piece-v2/close-timer/implemented";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static T Named<T>(Window w, string name) where T : Control => w.FindControl<T>(name) ?? w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    static void Desktop(string type, string method, params object[] args) => typeof(ControllerWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
    static void Click(Window w, string name) => Named<Button>(w, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static void Key(Control control, Avalonia.Input.Key key)
    {
        control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = key });
        ClockOverlayChecks.Pump(25);
    }
    sealed class Fixture : IDisposable
    {
        internal readonly AppSettings Settings;
        internal readonly TimerSession Timer = new(4) { Name = "Programming", Mode = 0, IsRunning = true };
        internal Fixture(int scale = 90, string theme = AppThemeCatalog.Nika)
        {
            Settings = new() { ThemeMode = theme, UiScalePercent = scale, TimeFormat = 1, BlinkColon = false, TelegramEnabled = false, HabiticaEnabled = false };
            Timer.RestoreElapsed(TimeSpan.FromSeconds(2667), false);
            Desktop("ThemeResources", "ApplyApplication", theme); Desktop("DesktopPageAppearance", "Initialize", Settings); Desktop("DesktopTypography", "Initialize", Settings.Typography);
        }
        internal ConfirmationDialogWindow Create(string time = "00:44:27")
        {
            var w = new ConfirmationDialogWindow("Close Timer", "Close Timer", $"Are you sure you want to close '{Timer.DisplayName}'?", "Close", true) { ShowActivated = false, ShowInTaskbar = false };
            typeof(ConfirmationDialogWindow).GetMethod("ConfigureCloseTimer", Private)!.Invoke(w, [Timer, time, Settings, false]);
            return w;
        }
        internal ConfirmationDialogWindow Open(string time = "00:44:27") { var w = Create(time); w.Show(); ClockOverlayChecks.Pump(70); return w; }
        public void Dispose() { Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }
    internal static void Layout()
    {
        Directory.CreateDirectory(Output);
        foreach (int percent in new[] { 70, 90, 125 })
        {
            using var f = new Fixture(percent); var w = f.Open();
            try
            {
                Require(w.SystemDecorations == SystemDecorations.None && w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(), "Missing approved Close Timer frame.");
                Require(Math.Abs(w.ClientSize.Width - 500 * percent / 100d) < 1.1 && Math.Abs(w.ClientSize.Height - 350 * percent / 100d) < 1.1, "Close Timer native size differs from the design.");
                Bounds(w); Capture(w, percent, "standard");
                var body = Named<ScrollViewer>(w, "CloseTimerBody"); Require(body.Extent.Height <= body.Viewport.Height, "Standard text should not need scrolling.");
                var cancel = Named<Button>(w, "CancelButton"); Require(cancel.IsFocused, "Cancel should have initial keyboard focus.");
                var presenter = cancel.GetVisualDescendants().OfType<ContentPresenter>().Single(p => p.Name == "PART_ContentPresenter");
                foreach (string state in new[] { ":pointerover", ":pressed" })
                {
                    ((IPseudoClasses)cancel.Classes).Set(state, true); ClockOverlayChecks.Pump(15);
                    Require(presenter.Background is ISolidColorBrush brush && brush.Color.R < 60 && brush.Color.G < 100, "Cancel loses its readable background on " + state);
                    ((IPseudoClasses)cancel.Classes).Set(state, false);
                }
            }
            finally { w.Close(); }
        }
        using (var f = new Fixture())
        {
            f.Timer.Name = "Programming · Desktop interface improvements"; f.Timer.IsRunning = false;
            var w = f.Open(); try { Bounds(w); Capture(w, 90, "long-name"); } finally { w.Close(); }
            f.Timer.Name = "برنامه نویسی و طراحی رابط کاربری برای پروژه شخصی";
            f.Settings.Typography.Global.SizePercent = 150; Desktop("DesktopTypography", "Apply", f.Settings.Typography);
            w = f.Open();
            try
            {
                Require(Named<TextBlock>(w, "CloseTimerName").FlowDirection == FlowDirection.RightToLeft, "RTL timer name lost its base direction.");
                Bounds(w); ScrollReachability(w); Capture(w, 90, "large-text");
                f.Settings.UiScalePercent = 125; Desktop("DesktopPageAppearance", "Apply", f.Settings, true); ClockOverlayChecks.Pump(40);
                var screen = new Screen(1.25, new PixelRect(-800, 0, 800, 600), new PixelRect(-800, 32, 800, 548), true);
                typeof(ConfirmationDialogWindow).GetMethod("FitCloseTimerToScreen", Private)!.Invoke(w, [screen]); ClockOverlayChecks.Pump(40);
                Require(w.Position.X >= -800 && w.Position.Y >= 32 && w.Position.X + w.ClientSize.Width * 1.25 <= 1 && w.Position.Y + w.ClientSize.Height * 1.25 <= 581, "Dialog exceeds the working area.");
                Bounds(w); ScrollReachability(w); Capture(w, 125, "small-screen");
            }
            finally { w.Close(); }
        }
    }
    static void Bounds(Window w)
    {
        var frame = w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        var end = frame.TranslatePoint(new Point(frame.Bounds.Width, frame.Bounds.Height), w)!.Value;
        Require(Math.Abs(end.X - w.ClientSize.Width) < 1.5 && Math.Abs(end.Y - w.ClientSize.Height) < 1.5, "Frame does not fit native window.");
        foreach (string name in new[] { "HeadingText", "CloseTimerFooterHint", "CancelButton", "ConfirmButton" })
        {
            var c = Named<Control>(w, name); var corner = c.TranslatePoint(new Point(c.Bounds.Width, c.Bounds.Height), frame)!.Value;
            Require(corner.X <= frame.Bounds.Width - 10 && corner.Y <= frame.Bounds.Height - 15, name + " exceeds frame.");
        }
        foreach (var text in frame.GetVisualDescendants().OfType<TextBlock>()) Require(text.TextLayout.Height <= text.Bounds.Height + 1, "Text clipped: " + text.Text);
        var body = Named<ScrollViewer>(w, "CloseTimerBody"); Require(body.Viewport.Height > 80, "Summary viewport is unusable.");
        Require(body.TranslatePoint(new Point(0, body.Bounds.Height), frame)!.Value.Y < Named<Button>(w, "CancelButton").TranslatePoint(default, frame)!.Value.Y, "Body overlaps action footer.");
    }
    static void ScrollReachability(Window w)
    {
        var body = Named<ScrollViewer>(w, "CloseTimerBody"); body.Offset = new(0, 10000); ClockOverlayChecks.Pump(30);
        var history = Named<TextBlock>(w, "CloseTimerHistoryHint"); var bottom = history.TranslatePoint(new Point(0, history.Bounds.Height), body)!.Value;
        Require(bottom.Y <= body.Bounds.Height + 1 && bottom.Y >= history.Bounds.Height, "Scrolling cannot reach the history explanation.");
        body.Offset = default; ClockOverlayChecks.Pump(30);
        var name = Named<TextBlock>(w, "CloseTimerName"); var top = name.TranslatePoint(default, body)!.Value;
        Require(top.Y >= 0 && top.Y < body.Bounds.Height, "Scrolling cannot reach the timer identity.");
    }
    static void Capture(Window w, int scale, string state)
    {
        var frame = w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width), (int)Math.Ceiling(w.ClientSize.Height)), new Vector(96, 96));
        if (scale >= 100) bitmap.Render(w);
        else { using var dc = bitmap.CreateDrawingContext(); dc.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
        bitmap.Save(Path.Combine(Output, $"close-timer-{state}-{scale}.png"));
    }
    internal static void Modes()
    {
        using var f = new Fixture();
        foreach (var (mode, running, time, state, caption) in new[]
        {
            (0, true, "00:44:27", "Running stopwatch", "Elapsed"), (0, false, "125:44:27", "Paused stopwatch", "Elapsed"),
            (1, false, "23:45:06", "Clock", "Current time"), (2, true, "-00:03:10", "Running countdown", "Remaining"),
            (2, false, "00:25:00", "Paused countdown", "Remaining"), (3, true, "00:12:34.5", "Running timecode", "Elapsed")
        })
        {
            f.Timer.Mode = mode; f.Timer.IsRunning = running; f.Timer.Name = mode == 1 ? "Navid" : "Programming";
            var w = f.Open(time);
            try
            {
                Require(Named<TextBlock>(w, "CloseTimerName").Text == f.Timer.DisplayName && Named<TextBlock>(w, "CloseTimerTime").Text == time, "Timer identity or displayed time changed.");
                Require(Named<TextBlock>(w, "CloseTimerState").Text == state && Named<TextBlock>(w, "CloseTimerTimeCaption").Text == caption, "Incorrect timer mode or time meaning.");
                f.Timer.Name = "Changed elsewhere"; Require(Named<TextBlock>(w, "CloseTimerName").Text != f.Timer.Name, "Dialog summary should be an opening snapshot.");
                Bounds(w); if (mode == 1 || mode == 2 && running) Capture(w, 90, mode == 1 ? "clock" : "countdown");
            }
            finally { w.Close(); }
        }
    }
    internal static void Actions()
    {
        using var f = new Fixture();
        var unfocused = f.Open();
        try { Key(unfocused, Avalonia.Input.Key.Enter); Require(unfocused.IsVisible && !unfocused.Result, "Unfocused Enter closed the timer."); } finally { unfocused.Close(); }
        void Check(Action<ConfirmationDialogWindow> action, bool expected)
        {
            var w = f.Open(); int closed = 0; w.Closed += (_, _) => closed++;
            try
            {
                action(w); ClockOverlayChecks.Pump(25); Require(!w.IsVisible && w.Result == expected && closed == 1, "Incorrect close action/result.");
                Click(w, "ConfirmButton"); Click(w, "CancelButton"); Require(w.Result == expected && closed == 1, "Repeated clicks changed a completed result.");
            }
            finally { w.Close(); }
        }
        foreach (var (button, result) in new[] { ("CancelButton", false), ("ConfirmButton", true) })
        {
            Check(w => Click(w, button), result);
            foreach (var key in new[] { Avalonia.Input.Key.Enter, Avalonia.Input.Key.Space })
                Check(w => { var target = Named<Button>(w, button); target.Focus(); Key(target, key); }, result);
        }
        Check(w => { var target = Named<Button>(w, "ConfirmButton"); target.Focus(); Key(target, Avalonia.Input.Key.Escape); }, false);
        Check(w => Click(w, "CloseTimerCancel"), false); Check(w => w.Close(), false);
        foreach (string theme in new[] { AppThemeCatalog.Midnight, AppThemeCatalog.Daylight, AppThemeCatalog.Pirate })
        {
            f.Settings.ThemeMode = theme; Desktop("ThemeResources", "ApplyApplication", theme); var w = f.Open();
            try { Require(!w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(), "Nika appearance leaked into legacy theme."); Click(w, "ConfirmButton"); Require(w.Result, "Legacy timer confirmation failed."); } finally { w.Close(); }
        }
        var generic = new ConfirmationDialogWindow("Other", "Other", "Unrelated confirmation", "Confirm", true) { ShowActivated = false };
        try { generic.Show(); ClockOverlayChecks.Pump(30); Key(generic, Avalonia.Input.Key.Enter); Require(generic.Result && !generic.IsVisible, "Generic confirmation behavior changed."); } finally { generic.Close(); }
        f.Settings.ThemeMode = AppThemeCatalog.Nika; Desktop("ThemeResources", "ApplyApplication", f.Settings.ThemeMode);
        var todo = new ConfirmationDialogWindow("Delete todo", "Delete this todo?", "Synthetic task", "Delete", true) { ShowActivated = false };
        try
        {
            todo.ApplyTodoAppearance(f.Settings); todo.Show(); ClockOverlayChecks.Pump(50);
            Require(!todo.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any() && todo.GetVisualDescendants().OfType<TodoFrame>().Any(), "Todo-specific shell changed.");
            Click(todo, "ConfirmButton"); Require(todo.Result, "Todo confirmation action changed.");
        }
        finally { todo.Close(); }
    }
    internal static void Controller()
    {
        foreach (bool visible in new[] { true, false })
        {
            using var f = new Fixture();
            var controller = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Private, null, [typeof(AppSettings)], null)!.Invoke([f.Settings]);
            var manager = (TimerSessionManager)typeof(ControllerWindow).GetField("_timerManager", Private)!.GetValue(controller)!;
            var target = manager.Active!; target.Name = "Programming"; target.IsRunning = true; target.Stopwatch.Start();
            var history = (ProjectTimeHistory)typeof(ControllerWindow).GetField("_projectHistory", Private)!.GetValue(controller)!;
            history.StartTracking(target.Id, target.Name, DateTime.UtcNow.AddHours(-2)); history.StopTracking(target.Id, DateTime.UtcNow.AddHours(-1));
            var record = history.GetIntervalsForTimer(target.Id).Single();
            var ids = manager.Sessions.Select(t => t.Id).ToArray(); var prompts = new List<ConfirmationDialogWindow>();
            using var listener = Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) => { if (w is ConfirmationDialogWindow dialog) prompts.Add(dialog); });
            void CloseTarget() => typeof(ControllerWindow).GetMethod("CloseSession", Private)!.Invoke(controller, [target]);
            try
            {
                controller.ShowActivated = false; controller.ShowInTaskbar = false; controller.Show(); if (!visible) controller.Hide();
                manager.Separate(target);
                var overlay = (OverlayWindow)typeof(ControllerWindow).GetMethod("GetOrCreateSeparatedOverlay", Private)!.Invoke(controller, [target])!;
                int overlayClosed = 0; overlay.Closed += (_, _) => overlayClosed++;
                overlay.ShowActivated = false; overlay.Show();
                CloseTarget(); ClockOverlayChecks.Pump(60); CloseTarget(); ClockOverlayChecks.Pump(30);
                Require(prompts.Count == 1 && prompts[0].GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(), "Controller did not configure the dialog or allowed duplicate prompts.");
                Require(Named<TextBlock>(prompts[0], "CloseTimerName").Text == target.DisplayName, "Controller targeted the wrong timer.");
                Click(prompts[0], "CancelButton"); ClockOverlayChecks.Pump(30);
                Require(manager.Count == ids.Length && target.Stopwatch.IsRunning && overlayClosed == 0, "Cancel stopped or removed a timer/overlay.");
                CloseTarget(); ClockOverlayChecks.Pump(40);
                var other = manager.Sessions.First(t => t != target); manager.Activate(other);
                Click(prompts.Last(), "ConfirmButton"); ClockOverlayChecks.Pump(50);
                Require(!manager.Sessions.Contains(target) && !target.Stopwatch.IsRunning && manager.Count == ids.Length - 1, "Confirm did not close only the target timer.");
                Require(manager.Active == other && overlayClosed == 1, "Closing after active selection changed removed the wrong timer/overlay.");
                Require(manager.Sessions.Select(t => t.Id).Order().SequenceEqual(ids.Where(id => id != target.Id).Order()), "Closing changed other timer identities.");
                Require(history.CreateView(DateTime.UtcNow).Intervals.Contains(record), "Closing deleted saved project history.");
                CloseTarget(); ClockOverlayChecks.Pump(20); Require(prompts.Count == 2, "Stale target reopened a dialog.");
                target = manager.Active!; CloseTarget(); ClockOverlayChecks.Pump(40); controller.Close(); ClockOverlayChecks.Pump(40);
                Require(prompts.All(p => !p.IsVisible) && manager.Sessions.Contains(target), "Owner shutdown removed a timer or left an orphan prompt.");
            }
            finally { foreach (var prompt in prompts.Where(p => p.IsVisible).ToArray()) prompt.Close(); controller.Close(); }
        }
    }
}
