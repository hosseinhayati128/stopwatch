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

internal static class CloseStopwatchNativeChecks
{
    const string Output = "design/one-piece-v2/close-stopwatch/implemented";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static T Named<T>(Window w, string name) where T : Control => w.FindControl<T>(name) ?? w.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    static void Desktop(string type, string method, params object[] args) => typeof(ControllerWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
    static void Click(Window w, string name) => Named<Button>(w, name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    sealed class Fixture : IDisposable
    {
        internal readonly AppSettings Settings;
        internal Fixture(int scale = 90, string theme = AppThemeCatalog.Nika)
        {
            Settings = new() { ThemeMode = theme, UiScalePercent = scale, CloseAction = CloseActionChoice.Ask, TelegramEnabled = false, HabiticaEnabled = false };
            Desktop("ThemeResources", "ApplyApplication", theme); Desktop("DesktopPageAppearance", "Initialize", Settings); Desktop("DesktopTypography", "Initialize", Settings.Typography);
        }
        internal CloseActionDialogWindow Create()
        {
            return new CloseActionDialogWindow(Settings.ThemeMode, Settings) { ShowActivated = false, ShowInTaskbar = false };
        }
        internal CloseActionDialogWindow Open() { var w = Create(); w.Show(); ClockOverlayChecks.Pump(65); return w; }
        public void Dispose() { Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }
    internal static void Layout()
    {
        Directory.CreateDirectory(Output);
        foreach (int scale in new[] { 70, 90, 125 })
        {
            using var f = new Fixture(scale); var w = f.Open();
            try
            {
                Require(w.SystemDecorations == SystemDecorations.None && w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(), "Missing approved frameless Close Stopwatch panel.");
                Require(Math.Abs(w.ClientSize.Width - 500 * scale / 100d) < 1.1 && Math.Abs(w.ClientSize.Height - 428 * scale / 100d) < 1.1, "Native size differs from the compact design.");
                Bounds(w); Capture(w, scale, "standard");
                var body = Named<ScrollViewer>(w, "CloseDialogBody");
                Require(body.Extent.Height <= body.Viewport.Height, $"Standard {scale}% should not need scrolling: {body.Extent.Height} / {body.Viewport.Height}.");
                var remember = Named<CheckBox>(w, "RememberCheck"); Require(remember.IsChecked != true, "Remember should start off."); remember.IsChecked = true; ClockOverlayChecks.Pump(25);
                Require(Named<TextBlock>(w, "CloseRememberHint").Text!.StartsWith("Next time"), "Remember hint did not update.");
                if (scale == 90) Capture(w, scale, "remember");
                var primary = Named<Button>(w, "MinimizeButton"); var presenter = primary.GetVisualDescendants().OfType<ContentPresenter>().Single(p => p.Name == "PART_ContentPresenter");
                foreach (string state in new[] { ":pointerover", ":pressed" })
                {
                    ((IPseudoClasses)primary.Classes).Set(state, true); ClockOverlayChecks.Pump(15);
                    Require(presenter.Background is ISolidColorBrush brush && brush.Color.A == 255 && brush.Color.R < 60 && brush.Color.G < 100, "Minimize loses its dark background on " + state);
                    ((IPseudoClasses)primary.Classes).Set(state, false);
                }
            }
            finally { w.Close(); }
        }
        using (var f = new Fixture())
        {
            f.Settings.Typography.Global.SizePercent = 125; Desktop("DesktopTypography", "Apply", f.Settings.Typography);
            var w = f.Open();
            try
            {
                Bounds(w); var body = Named<ScrollViewer>(w, "CloseDialogBody"); body.Offset = new(0, 10000); ClockOverlayChecks.Pump(25); Bounds(w); Capture(w, 90, "large-text");
                f.Settings.UiScalePercent = 125; Desktop("DesktopPageAppearance", "Apply", f.Settings, true); ClockOverlayChecks.Pump(50); Bounds(w);
                var screen = new Screen(1.25, new PixelRect(0, 0, 800, 600), new PixelRect(0, 32, 800, 548), true);
                typeof(CloseActionDialogWindow).GetMethod("FitCloseDialogToScreen", Private)!.Invoke(w, [screen]); ClockOverlayChecks.Pump(40);
                Require(w.Position.X >= 0 && w.Position.Y >= 32 && w.Position.X + w.ClientSize.Width * 1.25 <= 801 && w.Position.Y + w.ClientSize.Height * 1.25 <= 581, "Dialog exceeds small working area.");
                Bounds(w); body.Offset = new(0, 10000); ClockOverlayChecks.Pump(25); Capture(w, 125, "small-screen");
            }
            finally { w.Close(); }
        }
    }
    static void Bounds(Window w)
    {
        var frame = w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        var edge = frame.TranslatePoint(new Point(frame.Bounds.Width, frame.Bounds.Height), w)!.Value;
        Require(Math.Abs(edge.X - w.ClientSize.Width) < 1.5 && Math.Abs(edge.Y - w.ClientSize.Height) < 1.5, "Frame does not fit native window.");
        foreach (string name in new[] { "HeadingText", "CloseFooterHint" })
        {
            var text = Named<TextBlock>(w, name); Require(text.TextLayout.Height <= text.Bounds.Height + 1, name + " clips.");
            var corner = text.TranslatePoint(new Point(text.Bounds.Width, text.Bounds.Height), frame)!.Value;
            Require(corner.X < frame.Bounds.Width - 10 && corner.Y < frame.Bounds.Height - 15, name + " exceeds frame.");
        }
        var body = Named<ScrollViewer>(w, "CloseDialogBody"); Require(body.Viewport.Height > 100, "Close actions have no usable viewport.");
        Require(body.TranslatePoint(new Point(0, body.Bounds.Height), frame)!.Value.Y < Named<Button>(w, "CancelButton").TranslatePoint(default, frame)!.Value.Y, "Body overlaps fixed footer.");
        foreach (string name in new[] { "MinimizeButton", "ExitButton", "RememberCheck", "CancelButton" }) Require(Named<Control>(w, name).IsEffectivelyVisible, name + " is unavailable.");
        var badge = Named<Border>(w, "CloseEnterBadge"); var key = (TextBlock)badge.Child!; var center = key.TranslatePoint(new Point(key.Bounds.Width / 2, key.Bounds.Height / 2), badge)!.Value;
        Require(Math.Abs(center.X - badge.Bounds.Width / 2) < 1 && Math.Abs(center.Y - badge.Bounds.Height / 2) < 1, "Enter badge is off center.");
    }
    static void Capture(Window w, int percent, string state)
    {
        var frame = w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Single();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width), (int)Math.Ceiling(w.ClientSize.Height)), new Vector(96, 96));
        if (percent >= 100) bitmap.Render(w);
        else { using var dc = bitmap.CreateDrawingContext(); dc.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
        bitmap.Save(Path.Combine(Output, $"close-stopwatch-{state}-{percent}.png"));
    }
    internal static void Actions()
    {
        using var f = new Fixture();
        void Check(Action<CloseActionDialogWindow> action, CloseDialogResult expected)
        {
            var w = f.Open(); int closes = 0; w.Closed += (_, _) => closes++;
            try
            {
                action(w); ClockOverlayChecks.Pump(20); Require(!w.IsVisible && w.SelectedAction == expected && closes == 1, "Incorrect close action or completion count.");
                w.ChooseExit(); w.ChooseMinimize(); w.ChooseCancel(); Require(w.SelectedAction == expected && closes == 1, "A repeated action changed the completed result.");
            }
            finally { w.Close(); }
        }
        foreach (string name in new[] { "ExitButton", "RememberCheck", "CancelButton" })
            Check(w => { var control = Named<Control>(w, name); control.Focus(); control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); }, CloseDialogResult.MinimizeToTray);
        foreach (string name in new[] { "ExitButton", "RememberCheck" })
            Check(w => { var control = Named<Control>(w, name); control.Focus(); control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Escape }); }, CloseDialogResult.Cancel);
        foreach (var (name, result) in new[] { ("MinimizeButton", CloseDialogResult.MinimizeToTray), ("ExitButton", CloseDialogResult.CloseCompletely), ("CancelButton", CloseDialogResult.Cancel), ("CloseDialogCancel", CloseDialogResult.Cancel) }) Check(w => Click(w, name), result);
        Check(w => { var button = Named<Button>(w, "ExitButton"); button.Focus(); button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Space }); button.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Avalonia.Input.Key.Space }); }, CloseDialogResult.CloseCompletely);
        Check(w => w.Close(), CloseDialogResult.Cancel);
        foreach (string theme in new[] { AppThemeCatalog.Midnight, AppThemeCatalog.Daylight, AppThemeCatalog.Pirate })
        {
            f.Settings.ThemeMode = theme; Desktop("ThemeResources", "ApplyApplication", theme); var w = f.Open();
            try { Require(!w.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(), "Nika frame leaked into a legacy theme."); Click(w, "MinimizeButton"); Require(w.SelectedAction == CloseDialogResult.MinimizeToTray, "Legacy action regressed."); }
            finally { w.Close(); }
        }
    }
    internal static void ModalResults()
    {
        using var f = new Fixture();
        foreach (bool owned in new[] { false, true }) foreach (var result in new[] { CloseDialogResult.Cancel, CloseDialogResult.MinimizeToTray, CloseDialogResult.CloseCompletely })
        {
            var owner = owned ? new Window { Width = 800, Height = 700, Position = new(100, 100), ShowActivated = false, ShowInTaskbar = false } : null;
            CloseActionDialogWindow? dialog = null;
            using var opened = Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) => { if (w is CloseActionDialogWindow close) dialog = close; });
            try
            {
                owner?.Show(); var task = CloseActionDialogWindow.ShowAsync(owner, f.Settings.ThemeMode, f.Settings); ClockOverlayChecks.Pump(50);
                Require(dialog != null && !task.IsCompleted, "Dialog did not wait for a choice.");
                if (owner != null)
                {
                    var area = dialog!.Screens.ScreenFromWindow(dialog)!.WorkingArea;
                    var size = owner.FrameSize ?? owner.ClientSize;
                    var expectedX = Math.Clamp(owner.Position.X + size.Width * owner.RenderScaling / 2 - dialog.ClientSize.Width * dialog.RenderScaling / 2, area.X, area.Right - dialog.ClientSize.Width * dialog.RenderScaling);
                    var expectedY = Math.Clamp(owner.Position.Y + size.Height * owner.RenderScaling / 2 - dialog.ClientSize.Height * dialog.RenderScaling / 2, area.Y, area.Bottom - dialog.ClientSize.Height * dialog.RenderScaling);
                    Require(Math.Abs(dialog.Position.X - expectedX) < 2 && Math.Abs(dialog.Position.Y - expectedY) < 2, "Dialog is not centered over its owner.");
                }
                Named<CheckBox>(dialog!, "RememberCheck").IsChecked = true;
                if (result == CloseDialogResult.Cancel) dialog!.Close(); else if (result == CloseDialogResult.MinimizeToTray) dialog!.ChooseMinimize(); else dialog!.ChooseExit();
                ClockOverlayChecks.Pump(30); Require(task.IsCompletedSuccessfully && task.Result.Action == result, "Modal/ownerless close result was lost.");
                Require(task.Result.RememberChoice == (result != CloseDialogResult.Cancel), "Cancel should not return an effective remembered action.");
            }
            finally { dialog?.Close(); owner?.Close(); }
        }
        var closingOwner = new Window { Width = 800, Height = 600, ShowActivated = false, ShowInTaskbar = false };
        CloseActionDialogWindow? ownedDialog = null;
        using var listener = Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) => { if (w is CloseActionDialogWindow close) ownedDialog = close; });
        try
        {
            closingOwner.Show(); var task = CloseActionDialogWindow.ShowAsync(closingOwner, f.Settings.ThemeMode, f.Settings); ClockOverlayChecks.Pump(40);
            Named<CheckBox>(ownedDialog!, "RememberCheck").IsChecked = true; closingOwner.Close(); ClockOverlayChecks.Pump(40);
            Require(task.IsCompletedSuccessfully && task.Result == (CloseDialogResult.Cancel, false) && !ownedDialog!.IsVisible, "Owner close left an orphan or remembered a choice.");
        }
        finally { ownedDialog?.Close(); closingOwner.Close(); }
        try
        {
            var task = CloseActionDialogWindow.ShowAsync(null, AppThemeCatalog.Nika); ClockOverlayChecks.Pump(40);
            Require(ownedDialog != null && !task.IsCompleted, "Explicit-theme compatibility call did not open a dialog.");
            ownedDialog!.ChooseMinimize(); ClockOverlayChecks.Pump(30);
            Require(task.IsCompletedSuccessfully && task.Result == (CloseDialogResult.MinimizeToTray, false), "Explicit-theme compatibility call lost its result.");
        }
        finally { ownedDialog?.Close(); }
    }
    internal static void Controller()
    {
        using var f = new Fixture(); var settings = f.Settings;
        var controller = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Private, null, [typeof(AppSettings)], null)!.Invoke([settings]);
        var closing = (EventHandler<WindowClosingEventArgs>)Delegate.CreateDelegate(typeof(EventHandler<WindowClosingEventArgs>), controller, typeof(ControllerWindow).GetMethod("OnClosing", Private, null, [typeof(object), typeof(WindowClosingEventArgs)], null)!);
        var manager = (TimerSessionManager)typeof(ControllerWindow).GetField("_timerManager", Private)!.GetValue(controller)!;
        var timer = manager.Active!; timer.IsRunning = true; timer.Stopwatch.Start(); var elapsed = timer.Elapsed;
        int closed = 0; controller.Closed += (_, _) => closed++;
        var prompts = new List<CloseActionDialogWindow>(); using var opened = Window.WindowOpenedEvent.AddClassHandler<Window>((w, _) => { if (w is CloseActionDialogWindow close) prompts.Add(close); });
        try
        {
            controller.Closing += closing; controller.ShowActivated = false; controller.ShowInTaskbar = false; controller.Show();
            controller.Close(); ClockOverlayChecks.Pump(50); controller.Close(); ClockOverlayChecks.Pump(50);
            Require(prompts.Count(p => p.IsVisible) == 1, "Repeated controller close opened duplicate dialogs.");
            var prompt = prompts.Last(); Require(prompt.GetVisualDescendants().OfType<StopwatchFrameSurface>().Any(), "Controller did not pass current settings.");
            Named<CheckBox>(prompt, "RememberCheck").IsChecked = true; prompt.ChooseCancel(); ClockOverlayChecks.Pump(35);
            Require(controller.IsVisible && settings.CloseAction == CloseActionChoice.Ask, "Cancel changed the close preference or hid the controller.");
            controller.Close(); ClockOverlayChecks.Pump(35); prompts.Last().ChooseMinimize(); ClockOverlayChecks.Pump(35);
            Require(!controller.IsVisible && settings.CloseAction == CloseActionChoice.Ask, "One-time minimize was remembered or failed to hide.");
            Require(timer.IsRunning && timer.Stopwatch.IsRunning && timer.Elapsed >= elapsed && closed == 0, "Minimize stopped timers or closed the controller.");
            controller.Show(); controller.Close(); ClockOverlayChecks.Pump(35);
            Named<CheckBox>(prompts.Last(), "RememberCheck").IsChecked = true; prompts.Last().ChooseMinimize(); ClockOverlayChecks.Pump(35);
            Require(!controller.IsVisible && settings.CloseAction == CloseActionChoice.Minimize && closed == 0, "Remembered minimize was not applied.");
            int count = prompts.Count; controller.Show(); controller.Close(); ClockOverlayChecks.Pump(35);
            Require(prompts.Count == count && !controller.IsVisible && closed == 0, "Remembered minimize prompted again or exited.");
            settings.CloseAction = CloseActionChoice.Ask; controller.Show(); controller.Close(); ClockOverlayChecks.Pump(35);
            Named<CheckBox>(prompts.Last(), "RememberCheck").IsChecked = true; prompts.Last().ChooseExit(); ClockOverlayChecks.Pump(35);
            Require(settings.CloseAction == CloseActionChoice.Exit && closed == 1 && !controller.IsVisible && prompts.All(p => !p.IsVisible), "Remembered exit did not complete once.");
        }
        finally { foreach (var prompt in prompts.Where(p => p.IsVisible).ToArray()) prompt.ChooseCancel(); controller.Closing -= closing; controller.Close(); }
    }
}
