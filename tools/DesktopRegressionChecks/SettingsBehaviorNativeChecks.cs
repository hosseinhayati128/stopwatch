using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class SettingsBehaviorNativeChecks
{
    private const BindingFlags Instance = BindingFlags.NonPublic | BindingFlags.Instance;
    private const string Output = "design/one-piece-v2/settings/behavior-implemented";
    private static readonly string[] Tabs = ["Timer", "Idle", "Focus", "Categories"];
    private static readonly DateTime SampleUtc = new(2026, 11, 14, 11, 55, 12, DateTimeKind.Utc);

    internal static void Settings()
    {
        var model = Sample();
        WithSettings(model, 1120, 720, window =>
        {
            Require(Find<CheckBox>(window, "FocusTrackingEnabledCheck").IsChecked == true, "Saved focus preference did not load.");
            Require(Find<TextBox>(window, "FocusMinimumPauseTextBox").Text == "10", "Minimum pause did not load.");
            var project = Find<ComboBox>(window, "IdleProjectComboBox");
            project.SelectedItem = "Programming";
            ClockOverlayChecks.Pump(30);
            var timeout = Find<TextBox>(window, "IdleProjectTimeoutTextBox");
            Require(Find<CheckBox>(window, "IdleProjectActiveCheck").IsChecked == true && timeout.Text == "15", "Selected project did not load its own idle rule.");
            project.SelectedItem = "Illustration";
            ClockOverlayChecks.Pump(30);
            Require(!model.ProjectIdleRules.ContainsKey("Illustration"), "Selecting an unconfigured project created a rule.");
            var defaultTimeout = Find<TextBox>(window, "IdleStopDefaultTimeoutTextBox");
            defaultTimeout.Text = "0";
            ClockOverlayChecks.Pump(20);
            Require(model.DefaultIdleStopTimeoutMinutes == 5, "Invalid timeout replaced the saved value.");
            defaultTimeout.Text = "9";
            ClockOverlayChecks.Pump(20);
            Require(model.DefaultIdleStopTimeoutMinutes == 9, "Valid timeout did not apply.");
            Click(Visual<Button>(window, "BehaviorCategoriesTab"));
            var name = Find<TextBox>(window, "NewCategoryTextBox");
            name.Text = "work";
            Click(Find<Button>(window, "AddCategorySettingsButton"));
            Require(Find<TextBlock>(window, "CategoryValidationStatusText").IsVisible, "Duplicate category had no validation.");
            ClockOverlayChecks.Pump(40);
            Save(window, "category-validation");
            name.Text = "Field work";
            name.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
            ClockOverlayChecks.Pump(20);
            Require(model.GetNormalizedProjectCategories().Contains("Field work"), "Enter did not add the category.");
            var remove = window.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && Equals(b.Tag, "Field work"));
            Click(remove);
            Require(!model.GetNormalizedProjectCategories().Contains("Field work"), "Remove did not delete the custom category.");
        });
    }

    internal static void Layout()
    {
        foreach (int scale in new[] { 70, 90, 125 })
        foreach (int width in new[] { 1120, 840 })
        {
            var settings = Sample();
            settings.UiScalePercent = scale;
            WithSettings(settings, width, width == 840 ? 600 : 720, window =>
            {
                var scroll = Visual<ScrollViewer>(window, "BehaviorScroll");
                var footer = Visual<Button>(window, "SettingsDone");
                foreach (var tab in Tabs)
                {
                    Click(Visual<Button>(window, $"Behavior{tab}Tab"));
                    ClockOverlayChecks.Pump(50);
                    CheckLayout(window, scroll, footer, $"{width}/{scale}%/{tab}");
                    if (tab == "Idle") Find<ComboBox>(window, "IdleProjectComboBox").SelectedItem = "Programming";
                    ClockOverlayChecks.Pump(20);
                    if (width == 1120 && scale == 90) Save(window, tab.ToLowerInvariant());
                    if (width == 840 && scale == 90 && tab == "Idle")
                    {
                        scroll.ScrollToEnd();
                        ClockOverlayChecks.Pump(30);
                        Save(window, "compact-idle");
                    }
                }
                if (width == 1120 && scale == 90)
                {
                    Click(Visual<Button>(window, "BehaviorFocusTab"));
                    Find<CheckBox>(window, "FocusTrackingEnabledCheck").IsChecked = false;
                    ClockOverlayChecks.Pump(30);
                    Require(!Find<TextBox>(window, "FocusMinimumPauseTextBox").IsEnabled, "Focus inputs stayed enabled after tracking was turned off.");
                    Save(window, "focus-off");
                    settings.ThemeMode = AppThemeCatalog.Midnight;
                    CallStatic("ThemeResources", "ApplyApplication", settings.ThemeMode);
                    Call(window, "UpdatePreviewSafely");
                    ClockOverlayChecks.Pump(40);
                    Require(Find<CheckBox>(window, "FocusTrackingEnabledCheck").IsEffectivelyVisible, "Changing theme stranded a Behavior control.");
                    settings.ThemeMode = AppThemeCatalog.Nika;
                    CallStatic("ThemeResources", "ApplyApplication", settings.ThemeMode);
                    Call(window, "UpdatePreviewSafely");
                    ClockOverlayChecks.Pump(50);
                    Require(Visual<Button>(window, "BehaviorFocusTab").IsEffectivelyVisible, "Returning to Nika lost the Behavior tabs.");
                }
                if (width == 840 && scale == 125)
                {
                    settings.Typography.Global.SizePercent = 175;
                    CallStatic("DesktopTypography", "Apply", settings.Typography);
                    ClockOverlayChecks.Pump(60);
                    foreach (var tab in Tabs)
                    {
                        Click(Visual<Button>(window, $"Behavior{tab}Tab"));
                        ClockOverlayChecks.Pump(30);
                        CheckLayout(window, scroll, footer, $"{width}/{scale}%/{tab}/175% text");
                    }
                    Save(window, "compact-large-text");
                }
            });
        }
    }

    internal static void Idle()
    {
        using var fixture = new ControllerFixture();
        var sessions = fixture.Manager.Sessions;
        DateTime now = DateTime.UtcNow;
        fixture.Model.SetProjectIdleRule(sessions[0].Name, true, 5);
        fixture.Model.SetProjectIdleRule(sessions[2].Name, true, 5);
        fixture.Model.SetProjectIdleRule(sessions[3].Name, true, 15);
        foreach (var timer in sessions.Take(4))
        {
            timer.IsRunning = true;
            timer.RestoreElapsed(TimeSpan.FromMinutes(30), true);
        }
        var timerWithHistory = sessions[0];
        var history = (ProjectTimeHistory)typeof(ControllerWindow).GetField("_projectHistory", Instance)!.GetValue(fixture.Window)!;
        history.StartTracking(timerWithHistory.Id, timerWithHistory.Name, now.AddMinutes(-30));
        history.StopTracking(timerWithHistory.Id, now.AddMinutes(-24));
        history.StartTracking(timerWithHistory.Id, timerWithHistory.Name, now.AddMinutes(-10));
        Call(fixture.Window, "ResetBehaviorRunBoundary", timerWithHistory);
        timerWithHistory.RestoreElapsed(TimeSpan.FromMinutes(40), true);
        sessions[2].Mode = 2;
        Call(fixture.Window, "ApplyIdleStop", TimeSpan.FromMinutes(7), now);
        Require(!timerWithHistory.IsRunning && Math.Abs((timerWithHistory.Elapsed - TimeSpan.FromMinutes(33)).TotalSeconds) < 1,
            "Idle stopping did not preserve earlier elapsed time and subtract the idle part of this run.");
        var intervals = history.GetIntervalsForTimer(timerWithHistory.Id).OrderBy(interval => interval.StartUtc).ToArray();
        Require(intervals.Length == 2 && intervals[0].EndUtc == now.AddMinutes(-24) && intervals[1].EndUtc == now.AddMinutes(-7),
            "Idle stopping did not close only the current history interval.");
        Require(sessions[1].IsRunning && sessions[2].IsRunning && sessions[3].IsRunning, "Idle stopping ignored project limits or countdown mode.");
    }

    internal static void Timer()
    {
        using var fixture = new ControllerFixture();
        var window = fixture.Window;
        var active = fixture.Manager.Active!;
        fixture.Model.AutoStart = true;
        Call(window, "ToggleOverlayVisibility");
        Call(window, "ShowActiveTimerOverlay");
        Require(!active.IsRunning, "The dedicated show shortcut started a paused timer.");
        Call(window, "ToggleActiveTimerOverlay");
        Call(window, "ToggleActiveTimerOverlay");
        Require(active.IsRunning, "Showing the overlay did not honor auto-start.");
        fixture.Model.ShowRecIndicator = true;
        Call(window, "UpdateOverlayStates");
        var overlay = (OverlayWindow)typeof(ControllerWindow).GetField("_herdOverlay", Instance)!.GetValue(window)!;
        Require(Find<ThemedClockFace>(overlay, "ClockFace").ShowRec, "The running overlay did not show REC.");
        active.Mode = 1;
        fixture.Model.BlinkColon = true;
        string alternate = (string)Call(window, "FormatDisplayTime", active, SampleUtc.AddMilliseconds(750))!;
        Require(!alternate.Contains(':'), "Clock mode did not blink its colons.");
    }

    internal static void CountdownAndFocus()
    {
        using var fixture = new ControllerFixture();
        var window = fixture.Window;
        var timer = fixture.Manager.Active!;
        fixture.Model.UseSmartCountdownInput = true;
        timer.Mode = 2;
        timer.CountdownInitialized = false;
        timer.CountdownRemaining = TimeSpan.FromMinutes(2);
        Call(window, "UpdateActiveTimerDisplay");
        Click(Find<Button>(window, "StartStopButton"));
        Require(timer.CountdownRemaining == TimeSpan.FromMinutes(2), "A countdown saved before this upgrade lost its remaining time.");
        Click(Find<Button>(window, "StartStopButton"));
        Call(window, "UpdateActiveTimerDisplay");
        Find<TextBox>(window, "SmartCountdownInputBox").Text = "2m 30s";
        Click(Find<Button>(window, "StartStopButton"));
        Require(timer.IsRunning && timer.CountdownRemaining == TimeSpan.FromSeconds(150), $"The countdown did not start from the typed duration (running={timer.IsRunning}, remaining={timer.CountdownRemaining}, input={timer.SmartInputText}).");
        Click(Find<Button>(window, "StartStopButton"));
        timer.Mode = 0;
        timer.LastPauseUtc = DateTime.UtcNow.AddSeconds(-160);
        var second = fixture.Manager.Sessions.First(session => session != timer);
        second.LastPauseUtc = DateTime.UtcNow.AddSeconds(-130);
        var history = (ProjectTimeHistory)typeof(ControllerWindow).GetField("_projectHistory", Instance)!.GetValue(window)!;
        fixture.Model.FocusTrackingEnabled = true;
        FocusInterruptionDialogWindow? prompt = null;
        using var opened = Window.WindowOpenedEvent.AddClassHandler<FocusInterruptionDialogWindow>((dialog, _) => prompt = dialog);
        Click(Find<Button>(window, "StartStopButton"));
        ClockOverlayChecks.Pump(50);
        Require(prompt is { IsVisible: true }, "Resuming after an eligible pause did not open the focus prompt.");
        var firstPrompt = prompt!;
        Call(window, "ToggleSessionStartPause", second);
        Require(prompt == firstPrompt, "A second prompt displaced the open prompt.");
        firstPrompt.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Escape });
        ClockOverlayChecks.Pump(70);
        Require(prompt is { IsVisible: true } && prompt != firstPrompt && history.FocusPauses.Count == 0,
            "Esc did not skip the first pause and advance to the next prompt.");
        prompt!.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.D2 });
        ClockOverlayChecks.Pump(45);
        Require(history.FocusPauses.Single() is { Reason: FocusPauseReason.Distraction } pause && pause.TimerSessionId == second.Id, "Focus choice was not recorded for the queued timer.");
    }

    internal static void CountdownTarget()
    {
        using var fixture = new ControllerFixture();
        var window = fixture.Window;
        var timer = fixture.Manager.Active!;
        var start = new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Local);
        timer.Mode = 2;
        timer.UseClockTarget = true;
        timer.SmartInputText = "12:20";
        Require((bool)Call(window, "PrepareCountdown", timer, start.ToUniversalTime())!, "Clock target input was rejected.");
        Require(timer.ClockTarget == start.AddMinutes(20), "The countdown did not store its clock target for restore.");
        var store = new TimerWorkspaceStore(Path.Combine(Path.GetTempPath(), $"stopwatch-behavior-{Guid.NewGuid():N}.json"));
        try
        {
            timer.IsRunning = true;
            var restored = new TimerSessionManager();
            var reopen = start.AddMinutes(15);
            Require(store.Save(fixture.Manager, start.ToUniversalTime()) && store.TryLoad(restored, reopen.ToUniversalTime(), reopen),
                "The running countdown workspace could not be saved and restored.");
            var restoredTimer = restored.Sessions.Single(session => session.Id == timer.Id);
            Require(restoredTimer.IsRunning && restoredTimer.CountdownRemaining == TimeSpan.FromMinutes(5), "Reopening lost the running clock target.");
        }
        finally
        {
            File.Delete(store.FilePath);
            File.Delete(store.BackupPath);
        }
        timer.SmartInputText = "until 12:20";
        Call(window, "ResetSession", timer);
        Require((bool)Call(window, "PrepareCountdown", timer, start.AddMinutes(15).ToUniversalTime())!, "Reset clock target was rejected.");
        Require(timer.CountdownRemaining == TimeSpan.FromMinutes(5), "Reset clock target reused its original duration.");
        timer.SmartInputText = "";
        timer.ClockTargetHoursText = "12";
        timer.ClockTargetMinutesText = "20";
        timer.ClockTargetSecondsText = "30";
        Call(window, "ResetSession", timer);
        Require((bool)Call(window, "PrepareCountdown", timer, start.ToUniversalTime())!
            && timer.ClockTarget == start.AddMinutes(20).AddSeconds(30), "A saved classic clock target could not be used.");
        var tick = typeof(ControllerWindow).GetMethod("OnTimerTick", Instance)!;
        timer.IsRunning = true;
        timer.Mode = 1;
        timer.LastNonClockMode = 2;
        timer.ClockTarget = DateTime.Now.AddMinutes(5);
        tick.Invoke(window, [null, EventArgs.Empty]);
        Require(timer.IsRunning && Math.Abs((timer.CountdownRemaining - TimeSpan.FromMinutes(5)).TotalSeconds) < 1,
            "A countdown shown as a clock did not follow its wall-clock target.");
        timer.ClockTarget = DateTime.Now.AddMinutes(-1);
        tick.Invoke(window, [null, EventArgs.Empty]);
        Require(!timer.IsRunning && timer.CountdownRemaining == TimeSpan.Zero, "An expired countdown stayed running behind clock mode.");
    }

    private sealed class ControllerFixture : IDisposable
    {
        internal AppSettings Model { get; } = Sample();
        internal ControllerWindow Window { get; }
        internal TimerSessionManager Manager { get; }

        internal ControllerFixture()
        {
            Model.FocusTrackingEnabled = false;
            CallStatic("ThemeResources", "ApplyApplication", Model.ThemeMode);
            Window = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Instance, null, [typeof(AppSettings)], null)!.Invoke([Model]);
            Window.ShowActivated = false;
            Window.ShowInTaskbar = false;
            Window.Show();
            Manager = (TimerSessionManager)typeof(ControllerWindow).GetField("_timerManager", Instance)!.GetValue(Window)!;
            ClockOverlayChecks.Pump(30);
        }

        public void Dispose() => Window.Close();
    }

    private static AppSettings Sample()
    {
        var settings = new AppSettings
        {
            ThemeMode = AppThemeCatalog.Nika, UiScalePercent = 90, OverlayTheme = OverlayThemeCatalog.Nika,
            TextColor = "Theme default", ShowRecIndicator = true, UseSmartCountdownInput = true, FocusTrackingEnabled = true
        };
        settings.SetProjectIdleRule("Learning", true, 8);
        settings.SetProjectIdleRule("Programming", true, 15);
        settings.AddProjectCategory("Client work");
        settings.AddProjectCategory("Personal study");
        return settings;
    }

    private static void WithSettings(AppSettings model, int width, int height, Action<SettingsWindow> check)
    {
        var history = new ProjectTimeHistory();
        foreach (string name in new[] { "Illustration", "Learning", "Programming" }) history.RegisterProject(name);
        var snapshot = history.CreateView(SampleUtc);
        CallStatic("ThemeResources", "ApplyApplication", model.ThemeMode);
        CallStatic("DesktopPageAppearance", "Initialize", model);
        CallStatic("DesktopTypography", "Initialize", model.Typography);
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"stopwatch-behavior-{Guid.NewGuid():N}.json"));
        var window = new SettingsWindow(model, () => snapshot) { Width = width, Height = height, ShowActivated = false, ShowInTaskbar = false };
        try
        {
            window.Show();
            ClockOverlayChecks.Pump(45);
            Click(Visual<Button>(window, "SettingsNav_Behavior"));
            ClockOverlayChecks.Pump(40);
            check(window);
        }
        finally
        {
            window.Close();
            TelegramOutboxStore.SetCustomFilePath(null);
            CallStatic("DesktopTypography", "Shutdown");
            CallStatic("DesktopPageAppearance", "Shutdown");
        }
    }

    private static void CheckLayout(Window window, ScrollViewer scroll, Button footer, string state)
    {
        Require(scroll.IsEffectivelyVisible && scroll.Viewport.Height > 65 && scroll.Extent.Width <= scroll.Viewport.Width + 1,
            $"Behavior does not fit in {state}: page {((Control)scroll.Parent!).Bounds}, scroll {scroll.Bounds}, row {Grid.GetRow(scroll)}, viewport {scroll.Viewport}, extent {scroll.Extent}.");
        var point = footer.TranslatePoint(default, window)!.Value;
        var bottom = footer.TranslatePoint(new Point(footer.Bounds.Width, footer.Bounds.Height), window)!.Value;
        Require(point.X >= 0 && point.Y >= 0 && bottom.X <= window.ClientSize.Width + 1 && bottom.Y <= window.ClientSize.Height + 1, "Done moved outside the window.");
        var offset = scroll.Offset;
        scroll.ScrollToEnd();
        ClockOverlayChecks.Pump(20);
        Require(footer.TranslatePoint(default, window)!.Value == point, "Done moved with the scroll area.");
        scroll.Offset = offset;
        ClockOverlayChecks.Pump(20);
    }

    private static void Save(SettingsWindow window, string name)
    {
        Directory.CreateDirectory(Output);
        var frame = window.GetVisualDescendants().OfType<StopwatchFrameSurface>().First();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
        if (((LayoutTransformControl)window.Content!).LayoutTransform!.Value.M11 >= 1) bitmap.Render(window);
        else
        {
            using var context = bitmap.CreateDrawingContext();
            context.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size));
        }
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }

    private static object? Call(object target, string name, params object[] args)
    {
        var method = target.GetType().GetMethod(name, Instance, null, args.Select(value => value.GetType()).ToArray(), null)
            ?? throw new InvalidOperationException($"{name} is not wired.");
        return method.Invoke(target, args);
    }
    private static void CallStatic(string type, string method, params object[] args) => typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!
        .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    private static T Find<T>(Window window, string name) where T : Control => window.FindControl<T>(name) ?? throw new InvalidOperationException($"Missing control {name}.");
    private static T Visual<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().SingleOrDefault(c => c.Name == name) ?? throw new InvalidOperationException($"Missing visible layout element {name}.");
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
