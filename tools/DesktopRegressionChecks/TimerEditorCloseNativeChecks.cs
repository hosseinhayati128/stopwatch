using System.Reflection;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Views;
using StopwatchOverlay.Desktop.Controls;

internal static class TimerEditorCloseNativeChecks
{
    const string Output = "design/one-piece-v2/timer-editor/close-timer-implemented";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static T Named<T>(Window w, string name) where T : Control => w.FindControl<T>(name) ?? w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    static void Click(Window w, string name) => Named<Button>(w, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    static void Key(Control target, Key key)
    {
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = key });
        ClockOverlayChecks.Pump(20);
    }
    static void Desktop(string type, string method, params object[] args) => typeof(ControllerWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!
        .GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
    sealed class Fixture : IDisposable
    {
        internal readonly AppSettings Settings;
        internal readonly ControllerWindow Controller;
        internal readonly TimerSessionManager Manager;
        internal readonly TimerSession Target;
        internal readonly ProjectTimeHistory History;
        internal readonly List<ConfirmationDialogWindow> Prompts = [];
        readonly IDisposable _listener;
        internal TimerEditorWindow Editor = null!;
        internal Fixture(int scale = 90, bool visible = true, string theme = AppThemeCatalog.Nika)
        {
            Settings = new() { ThemeMode = theme, UiScalePercent = scale, TelegramEnabled = false, HabiticaEnabled = false, ActivityWatchEnabled = false, TimeFormat = 1, BlinkColon = false };
            Desktop("ThemeResources", "ApplyApplication", theme); Desktop("DesktopPageAppearance", "Initialize", Settings); Desktop("DesktopTypography", "Initialize", Settings.Typography);
            Controller = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Private, null, [typeof(AppSettings)], null)!.Invoke([Settings]);
            Manager = (TimerSessionManager)typeof(ControllerWindow).GetField("_timerManager", Private)!.GetValue(Controller)!;
            Target = Manager.Active!; Target.Name = "Programming"; Target.IsRunning = true; Target.RestoreElapsed(TimeSpan.FromSeconds(2667), false);
            History = (ProjectTimeHistory)typeof(ControllerWindow).GetField("_projectHistory", Private)!.GetValue(Controller)!;
            History.StartTracking(Target.Id, Target.Name, DateTime.UtcNow.AddHours(-2)); History.StopTracking(Target.Id, DateTime.UtcNow.AddHours(-1));
            _listener = Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) => { if (w is ConfirmationDialogWindow dialog) Prompts.Add(dialog); });
            Controller.ShowActivated = false; Controller.ShowInTaskbar = false; Controller.Show(); if (!visible) Controller.Hide();
        }
        internal void Open()
        {
            Invoke("OpenTimerEditor"); ClockOverlayChecks.Pump(60);
            Editor = (TimerEditorWindow)typeof(ControllerWindow).GetField("_timerCommandDialog", Private)!.GetValue(Controller)!;
            if (Settings.ThemeMode == AppThemeCatalog.Nika)
            {
                Named<Expander>(Editor, "EditorRecordsExpander").IsExpanded = true; ClockOverlayChecks.Pump(20);
                var scroll = Named<ScrollViewer>(Editor, "EditorScroll"); scroll.Offset = new(0, scroll.Extent.Height); ClockOverlayChecks.Pump(20);
            }
        }
        internal void Invoke(string method) => typeof(ControllerWindow).GetMethod(method, Private)!.Invoke(Controller, [Target]);
        internal ConfirmationDialogWindow Prompt()
        {
            Click(Editor, "DeleteTimerButton"); ClockOverlayChecks.Pump(50); return Prompts.Last();
        }
        internal void Draft()
        {
            Click(Editor, "ToggleProjectInputButton"); Named<TextBox>(Editor, "CustomProjectBox").Text = "Unsaved project rename";
            Named<TextBox>(Editor, "TimeValueBox").Text = "15m"; Click(Editor, "DiscardRecordsButton"); ClockOverlayChecks.Pump(20);
        }
        public void Dispose()
        {
            foreach (var p in Prompts.Where(p => p.IsVisible).ToArray()) p.Close();
            Editor?.Close(); Controller.Close(); _listener.Dispose();
            Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown");
        }
    }
    internal static void SingleConfirmation()
    {
        foreach (bool visible in new[] { true, false })
        {
            using var f = new Fixture(visible: visible); f.Target.Stopwatch.Start();
            var ids = f.Manager.Sessions.Select(s => s.Id).ToArray();
            var record = f.History.GetIntervalsForTimer(f.Target.Id).Single();
            f.Manager.Separate(f.Target);
            var overlay = (OverlayWindow)typeof(ControllerWindow).GetMethod("GetOrCreateSeparatedOverlay", Private)!.Invoke(f.Controller, [f.Target])!;
            int closed = 0; overlay.Closed += (_, _) => closed++; overlay.ShowActivated = false; overlay.Show();
            f.Open(); f.Draft(); var dialog = f.Prompt();
            var other = f.Manager.Sessions.First(s => s != f.Target); f.Manager.Activate(other);
            Click(dialog, "ConfirmButton"); ClockOverlayChecks.Pump(70);
            Require(f.Prompts.Count == 1 && !f.Editor.IsVisible && !f.Manager.Sessions.Contains(f.Target), "Editor confirmation opened a second prompt instead of closing the timer.");
            Require(!f.Target.Stopwatch.IsRunning && closed == 1 && f.Manager.Active == other, "Wrong timer or overlay lifecycle after editor close.");
            Require(f.Manager.Sessions.Select(s => s.Id).Order().SequenceEqual(ids.Where(id => id != f.Target.Id).Order()), "Other timer identities changed.");
            Require(f.Target.Name == "Programming" && f.History.CreateView(DateTime.UtcNow).Intervals.Contains(record), "Closing applied draft edits or deleted saved history.");
            Click(dialog, "ConfirmButton"); ClockOverlayChecks.Pump(20);
            Require(f.Prompts.Count == 1 && closed == 1, "Repeated completion closed the timer twice.");
        }
    }
    internal static void CancellationAndDuplicates()
    {
        foreach (string action in new[] { "cancel", "escape", "x", "native", "enter", "space" })
        {
            using var f = new Fixture(); f.Open(); f.Draft();
            Named<TextBox>(f.Editor, "TimeValueBox").Text = "invalid duration"; Named<ComboBox>(f.Editor, "StateAfterSave").SelectedIndex = 1; ClockOverlayChecks.Pump(20);
            string? records = Named<TextBlock>(f.Editor, "RecordsStatusText").Text;
            var prompt = f.Prompt(); Click(f.Editor, "DeleteTimerButton"); ClockOverlayChecks.Pump(20);
            Require(f.Prompts.Count == 1, "Repeated editor close action opened duplicate confirmations.");
            Require(Named<TextBlock>(prompt, "CloseTimerName").Text == "Programming" && Named<TextBlock>(prompt, "CloseTimerTime").Text == "00:44:27"
                && Named<TextBlock>(prompt, "CloseTimerState").Text == "Running stopwatch", "Summary used unsaved name/time/running edits.");
            var cancel = Named<Button>(prompt, "CancelButton"); Require(cancel.IsFocused, "Cancel lost initial focus.");
            Key(prompt, Avalonia.Input.Key.Enter); Require(prompt.IsVisible && !prompt.Result, "Unfocused Enter confirmed closure.");
            switch (action)
            {
                case "cancel": Click(prompt, "CancelButton"); break;
                case "escape": Named<Button>(prompt, "ConfirmButton").Focus(); Key(prompt, Avalonia.Input.Key.Escape); break;
                case "x": Click(prompt, "CloseTimerCancel"); break;
                case "native": prompt.Close(); break;
                case "enter": cancel.Focus(); Key(cancel, Avalonia.Input.Key.Enter); break;
                case "space": cancel.Focus(); Key(cancel, Avalonia.Input.Key.Space); break;
            }
            ClockOverlayChecks.Pump(40);
            Require(f.Editor.IsVisible && f.Editor.IsEnabled && !f.Editor.WasSaved && !f.Editor.DeleteTimerRequested && f.Manager.Sessions.Contains(f.Target), "Cancel changed editor result or removed timer.");
            Require(Named<TextBox>(f.Editor, "CustomProjectBox").Text == "Unsaved project rename" && Named<TextBox>(f.Editor, "TimeValueBox").Text == "invalid duration"
                && !Named<Button>(f.Editor, "SaveButton").IsEnabled && Named<TextBlock>(f.Editor, "RecordsStatusText").Text == records, "Cancellation lost an invalid or pending-discard draft.");
            Require(f.Target.IsRunning && f.Target.Name == "Programming", "Cancellation applied draft edits.");
            Require(Named<Button>(f.Editor, "DeleteTimerButton").IsFocused, "Cancel did not restore editor action focus.");
            prompt = f.Prompt(); Named<Button>(prompt, "ConfirmButton").Focus(); Key(Named<Button>(prompt, "ConfirmButton"), Avalonia.Input.Key.Space); ClockOverlayChecks.Pump(40);
            Require(f.Prompts.Count == 2 && !f.Manager.Sessions.Contains(f.Target) && !f.Editor.IsVisible, "Reopened confirmation did not close once via focused keyboard activation.");
        }
    }
    internal static void ShutdownAndStaleTargets()
    {
        foreach (bool visible in new[] { true, false })
        foreach (bool closeController in new[] { true, false })
        {
            using var f = new Fixture(visible: visible); f.Open(); var prompt = f.Prompt();
            if (closeController) f.Controller.Close(); else f.Editor.Close();
            ClockOverlayChecks.Pump(40);
            Require(!prompt.IsVisible && !f.Editor.IsVisible && f.Manager.Sessions.Contains(f.Target), "Owner shutdown orphaned a prompt or removed a timer.");
            Click(prompt, "ConfirmButton"); ClockOverlayChecks.Pump(20);
            Require(f.Manager.Sessions.Contains(f.Target) && !f.Editor.DeleteTimerRequested, "Late confirmation after owner close removed timer.");
        }
        using (var f = new Fixture())
        {
            f.Open(); var prompt = f.Prompt(); f.Manager.Close(f.Target); var ids = f.Manager.Sessions.Select(t => t.Id).ToArray();
            Click(prompt, "ConfirmButton"); ClockOverlayChecks.Pump(40);
            Require(f.Prompts.Count == 1 && f.Manager.Sessions.Select(t => t.Id).SequenceEqual(ids), "Stale target confirmation closed another timer.");
        }
    }
    internal static void ModesAndThemes()
    {
        foreach (int mode in new[] { 0, 1, 2, 3 })
        {
            using var f = new Fixture(); f.Target.Mode = mode; f.Target.IsRunning = false; f.Target.CountdownRemaining = TimeSpan.FromSeconds(-190);
            f.Open(); var prompt = f.Prompt();
            Require(Named<TextBlock>(prompt, "CloseTimerTimeCaption").Text == (mode == 1 ? "Current time" : mode == 2 ? "Remaining" : "Elapsed"), "Wrong time meaning for timer mode.");
            if (mode == 2) Require(Named<TextBlock>(prompt, "CloseTimerTime").Text == "-00:03:10", "Overdue countdown lost its sign or remaining time.");
            if (mode == 1) Require(Named<TextBlock>(prompt, "CloseTimerState").Text == "Clock" && Named<TextBlock>(prompt, "MessageText").Text!.Contains("clock"), "Clock presented as a paused stopwatch.");
            Click(prompt, "ConfirmButton"); ClockOverlayChecks.Pump(30);
            Require(f.Prompts.Count == 1 && !f.Manager.Sessions.Contains(f.Target), "Timer mode still needs a second confirmation.");
        }
        foreach (string theme in new[] { AppThemeCatalog.Midnight, AppThemeCatalog.Daylight, AppThemeCatalog.Pirate })
        foreach (int mode in new[] { 0, 1, 2, 3 })
        {
            using var f = new Fixture(theme: theme); f.Target.Mode = mode; f.Target.IsRunning = false; f.Target.CountdownRemaining = TimeSpan.FromSeconds(-190);
            f.Open(); var prompt = f.Prompt();
            Require(!prompt.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any() && Named<TextBlock>(prompt, "MessageText").Text!.Contains("Unsaved editor"), "Legacy dialog lost contextual warning or acquired Nika artwork.");
            string state = mode switch { 1 => "Clock", 2 => "Paused countdown", 3 => "Paused timecode", _ => "Paused stopwatch" };
            string caption = mode switch { 1 => "Current time", 2 => "Remaining", _ => "Elapsed" };
            Require(Named<TextBlock>(prompt, "MessageText").Text!.Contains(state) && Named<TextBlock>(prompt, "MessageText").Text!.Contains(caption), "Legacy close confirmation omits timer state/mode or time meaning.");
            Key(prompt, Avalonia.Input.Key.Escape); ClockOverlayChecks.Pump(20); Require(f.Editor.IsVisible, "Legacy Escape closed editor.");
            prompt = f.Prompt(); Click(prompt, "ConfirmButton"); ClockOverlayChecks.Pump(30);
            Require(f.Prompts.Count == 2 && !f.Manager.Sessions.Contains(f.Target), "Legacy editor close still prompted twice.");
        }
    }
    internal static void Layout()
    {
        Directory.CreateDirectory(Output);
        foreach (string state in new[] { "running", "paused", "long-name" })
        {
            foreach (int percent in new[] { 70, 90, 125 })
            {
                using var f = new Fixture(percent); f.Target.IsRunning = state != "paused";
                if (state == "long-name") f.Target.Name = "Programming · Desktop interface improvements";
                f.Open(); if (state == "running" && percent == 90) Capture(f.Editor, percent, "editor-entry-90.png");
                var prompt = f.Prompt(); var frame = prompt.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
                Require(Math.Abs(prompt.ClientSize.Width - 500 * percent / 100d) < 1.1 && Math.Abs(prompt.ClientSize.Height - 380 * percent / 100d) < 1.1, "Editor dialog size differs from approved design.");
                foreach (var text in frame.GetVisualDescendants().OfType<TextBlock>())
                {
                    var end = text.TranslatePoint(new Point(text.Bounds.Width, text.Bounds.Height), frame)!.Value;
                    Require(text.TextLayout.Height <= text.Bounds.Height + 1 && end.X <= frame.Bounds.Width - 10 && end.Y <= frame.Bounds.Height - 15, "Clipped dialog text: " + text.Text);
                }
                var body = Named<ScrollViewer>(prompt, "CloseTimerBody"); var cancel = Named<Button>(prompt, "CancelButton");
                Require(body.Extent.Height <= body.Viewport.Height + 1 && body.TranslatePoint(new Point(0, body.Bounds.Height), frame)!.Value.Y < cancel.TranslatePoint(default, frame)!.Value.Y, "Dialog scrolls or overlaps footer.");
                foreach (var button in new[] { cancel, Named<Button>(prompt, "ConfirmButton") })
                {
                    var text = (TextBlock)button.Content!; var center = text.TranslatePoint(new Point(text.Bounds.Width / 2, text.Bounds.Height / 2), button)!.Value;
                    Require(Math.Abs(center.X - button.Bounds.Width / 2) < 1 && Math.Abs(center.Y - button.Bounds.Height / 2) < 1, "Off-center action text.");
                }
                Capture(prompt, percent, $"close-timer-{state}-{percent}.png");
                Click(prompt, "CancelButton"); ClockOverlayChecks.Pump(20);
            }
            Comparison(state);
        }
        using var large = new Fixture(70); large.Target.Name = "برنامه نویسی و طراحی رابط کاربری برای پروژه شخصی";
        large.Settings.Typography.Global.SizePercent = 150; Desktop("DesktopTypography", "Apply", large.Settings.Typography);
        large.Open(); var dialog = large.Prompt();
        Require(Named<TextBlock>(dialog, "CloseTimerName").FlowDirection == FlowDirection.RightToLeft, "RTL timer name lost direction.");
        large.Settings.UiScalePercent = 125; Desktop("DesktopPageAppearance", "Apply", large.Settings, true); ClockOverlayChecks.Pump(30);
        Require(Math.Abs(dialog.ClientSize.Height - 475) < 1.1, "Live page-scale update lost editor dialog height.");
        var bodyScroll = Named<ScrollViewer>(dialog, "CloseTimerBody"); bodyScroll.Offset = new(0, 10000); ClockOverlayChecks.Pump(30);
        var draft = Named<TextBlock>(dialog, "EditorDraftHint"); var bottom = draft.TranslatePoint(new Point(0, draft.Bounds.Height), bodyScroll)!.Value.Y;
        Require(bottom <= bodyScroll.Bounds.Height + 1 && bottom >= draft.Bounds.Height, "Large text cannot scroll to the draft warning.");
        Capture(dialog, 125, "close-timer-large-rtl-125.png");
    }
    static void Capture(Window window, int scale, string file)
    {
        var frame = window.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
        if (scale >= 100) bitmap.Render(window);
        else { using var dc = bitmap.CreateDrawingContext(); dc.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
        bitmap.Save(Path.Combine(Output, file));
    }
    static void Comparison(string state)
    {
        using var sheet = new RenderTargetBitmap(new PixelSize(1513, 553), new Vector(96, 96));
        using (var dc = sheet.CreateDrawingContext())
        {
            dc.DrawRectangle(Brush.Parse("#15232C"), null, new Rect(0, 0, 1513, 553)); int x = 22;
            foreach (int scale in new[] { 70, 90, 125 })
            {
                using var bitmap = new Bitmap(Path.Combine(Output, $"close-timer-{state}-{scale}.png"));
                dc.DrawText(new FormattedText($"{scale}% · {bitmap.PixelSize.Width} × {bitmap.PixelSize.Height}", CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight, new Typeface(StopwatchTypography.Resolve("modern-deck").NameFont), 16, Brush.Parse("#F8EACA")), new Point(x, 20));
                var size = new Size(bitmap.PixelSize.Width, bitmap.PixelSize.Height); dc.DrawImage(bitmap, new Rect(size), new Rect(new Point(x, 56), size)); x += bitmap.PixelSize.Width + 22;
            }
        }
        sheet.Save(Path.Combine(Output, $"close-timer-{state}-comparison.png"));
    }
}
