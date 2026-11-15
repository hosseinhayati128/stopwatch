using System.Reflection;
using System.Threading;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;
using StopwatchOverlay.PeriodicReview;

internal static class SettingsActivityWatchReviewNativeChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "design/one-piece-v2/settings/activitywatch-review-implemented";

    internal static void ControllerLifecycle()
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, PeriodicReviewEnabled = true,
            PeriodicReviewIntervalMinutes = 45, PeriodicReviewSnoozeMinutes = 10 };
        var controller = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden, null, [typeof(AppSettings)], null)!.Invoke([settings]);
        var clock = new Clock(); using var service = new PeriodicReviewService(() => settings, clock);
        Require(typeof(ControllerWindow).GetMethod("AttachPeriodicReviewService", Hidden) != null, "Desktop scheduler integration is missing.");
        try
        {
            controller.ShowActivated = false; controller.ShowInTaskbar = false; controller.Show();
            Call(controller, "AttachPeriodicReviewService", service);
            var preview = typeof(ControllerWindow).GetField("_previewMode", Hidden)!;
            preview.SetValue(controller, false);
            try { Call(controller, "StartPeriodicReview"); }
            finally { preview.SetValue(controller, true); }
            Require(ReferenceEquals(service, Find<PeriodicReviewService>(controller, "_periodicReviewService")), "Reopening the controller restarted an existing schedule.");
            void Due() { service.SetNextPromptUtc(clock.GetUtcNow().UtcDateTime.AddSeconds(-1)); clock.Fire(); Pump(80); }
            PeriodicReviewPromptWindow? Prompt() => Find<PeriodicReviewPromptWindow?>(controller, "_periodicReviewPrompt");
            service.SetNextPromptUtc(clock.GetUtcNow().UtcDateTime.AddSeconds(-1));
            clock.FireWhenArmedDue = true;
            Call(controller, "RestartPeriodicReview"); Pump(80);
            clock.FireWhenArmedDue = false;
            Require(Prompt()?.IsVisible == true || (!service.IsPromptShowing && service.NextPromptUtc > clock.GetUtcNow().UtcDateTime),
                "A timer delivered during settings reconfiguration stranded automatic prompts.");
            if (Prompt() != null) Call(Prompt()!, "Finish", PeriodicReviewPromptResult.Skipped);
            typeof(ControllerWindow).GetField("_timerCommandDialog", Hidden)!.SetValue(controller, new Window());
            Due(); Require(Prompt() == null && service.NextPromptUtc == clock.GetUtcNow().UtcDateTime.AddMinutes(10), "An open dialog did not defer automatic prompts.");
            typeof(ControllerWindow).GetField("_timerCommandDialog", Hidden)!.SetValue(controller, null);
            Due(); var prompt = Prompt(); Require(prompt?.IsVisible == true, "Scheduled prompt did not open with ActivityWatch off.");
            clock.Fire(); Pump(); Require(ReferenceEquals(prompt, Prompt()), "Duplicate scheduled prompt.");
            Call(prompt!, "Finish", PeriodicReviewPromptResult.Skipped); Pump();
            Require(Prompt() == null && service.NextPromptUtc == clock.GetUtcNow().UtcDateTime.AddMinutes(45), "Skip did not use normal interval.");
            Due(); Call(Prompt()!, "Finish", PeriodicReviewPromptResult.Ignored); Pump();
            Require(service.NextPromptUtc == clock.GetUtcNow().UtcDateTime.AddMinutes(10), "Ignored prompt did not snooze.");
            Due(); Call(Prompt()!, "Finish", PeriodicReviewPromptResult.Accepted); Pump(100);
            var review = Find<Window?>(controller, "_timerCommandDialog");
            Require(review is PeriodicReviewWindow && review.IsVisible && Prompt() == null && service.IsPromptShowing, "Accept did not open one guarded review.");
            Call(controller, "OpenPeriodicReview"); Pump(); Require(ReferenceEquals(review, Find<Window?>(controller, "_timerCommandDialog")), "Manual review duplicated the editor.");
            review!.Close(); Pump(); Require(!service.IsPromptShowing, "Cancel left scheduler blocked.");
            Due(); Call(controller, "OpenPeriodicReview"); Pump(100);
            Require(Prompt() == null && Find<Window?>(controller, "_timerCommandDialog") is PeriodicReviewWindow, "Manual review did not replace the prompt.");
            Find<Window>(controller, "_timerCommandDialog").Close(); Pump();
            Due(); settings.PeriodicReviewEnabled = false; Call(controller, "RestartPeriodicReview"); Pump();
            Require(Prompt() == null && service.NextPromptUtc == null, "Disabling left a prompt or deadline active.");
            Call(controller, "OpenPeriodicReview"); Pump(80); Require(Find<Window?>(controller, "_timerCommandDialog") is PeriodicReviewWindow, "Manual review requires automatic prompting.");
            Find<Window>(controller, "_timerCommandDialog").Close(); Pump();
            settings.PeriodicReviewEnabled = true; Call(controller, "RestartPeriodicReview");
            service.SetNextPromptUtc(clock.GetUtcNow().UtcDateTime.AddSeconds(-1)); clock.Fire();
            settings.PeriodicReviewEnabled = false; Call(controller, "RestartPeriodicReview"); Pump(); Require(Prompt() == null, "Queued stale prompt survived disabling.");
            settings.PeriodicReviewEnabled = true; Call(controller, "RestartPeriodicReview");
            service.SetNextPromptUtc(clock.GetUtcNow().UtcDateTime.AddSeconds(-1)); clock.Fire();
            using var liveService = new PeriodicReviewService(() => settings);
            Call(controller, "AttachPeriodicReviewService", liveService); Pump();
            Require(Prompt() == null && service.NextPromptUtc == null, "Replacement retained a stale request or service.");
            liveService.SetNextPromptUtc(DateTime.UtcNow.AddMilliseconds(50));
            for (int i = 0; i < 100 && Prompt() == null; i++) Pump(50);
            Require(Prompt()?.IsVisible == true, "Real timer callback did not marshal to the UI thread.");
            controller.Close(); Pump(); clock.Fire(); Pump();
            Require(Prompt() == null && liveService.NextPromptUtc == null, "Shutdown retained scheduler or prompt.");
            Require(settings.LastPeriodicReviewCompletedUtc == null, "Canceling review changed completion history.");
        }
        finally { controller.Close(); }
    }

    internal static void PromptOutcomes()
    {
        foreach (var result in new[] { PeriodicReviewPromptResult.Accepted, PeriodicReviewPromptResult.Skipped, PeriodicReviewPromptResult.Ignored })
        {
            int calls = 0; bool closed = false; PeriodicReviewPromptWindow? prompt = null;
            prompt = new(DateTime.UtcNow.AddMinutes(-30), DateTime.UtcNow, 1, 10, 45, value =>
            { calls++; Require(value == result && closed && !prompt!.IsVisible, "Prompt callback ran before closing or returned wrong result."); });
            prompt.Closed += (_, _) => closed = true;
            try
            {
                prompt.Show();
                if (result == PeriodicReviewPromptResult.Ignored) Call(prompt, "Timer_Tick", null!, EventArgs.Empty);
                else Call(prompt, "OnKeyDown", new Avalonia.Input.KeyEventArgs { Key = result == PeriodicReviewPromptResult.Accepted ? Avalonia.Input.Key.Y : Avalonia.Input.Key.Escape });
                prompt.Close(); Pump(); Require(calls == 1, "Prompt result was delivered more than once.");
            }
            finally { prompt.Close(); }
        }
    }

    private sealed class Clock : TimeProvider
    {
        private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;
        private Action? _fire;
        internal bool FireWhenArmedDue;
        public override DateTimeOffset GetUtcNow() => _now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            _fire = () => callback(state);
            // Deliver before RestartTimer returns, modeling a due timer before the caller invalidates requests.
            if (FireWhenArmedDue && dueTime == TimeSpan.Zero) _fire();
            return new TestTimer();
        }
        internal void Fire() => _fire?.Invoke();
    }
    private sealed class TestTimer : ITimer
    {
        public bool Change(TimeSpan dueTime, TimeSpan period) => true;
        public void Dispose() { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    internal static void Controls()
    {
        using var f = new Fixture(); var w = f.Window;
        Require(Visual<Grid>(w, "ActivityWatchReviewCards").IsEffectivelyVisible, "Missing production Review Prompts cards.");
        int requests = 0, changes = 0;
        w.PeriodicReviewRequested += () => requests++;
        typeof(SettingsWindow).GetEvent("SettingsChanged", Hidden)!.GetAddMethod(true)!.Invoke(w, [new Action<SettingsChangeKind>(_ => changes++)]);
        var interval = Find<TextBox>(w, "PeriodicReviewIntervalTextBox");
        var snooze = Find<TextBox>(w, "PeriodicReviewSnoozeTextBox");
        var dismiss = Find<TextBox>(w, "PeriodicReviewAutoDismissTextBox");
        var minimum = Find<TextBox>(w, "PeriodicReviewMinDurationTextBox");
        var percent = Find<TextBox>(w, "PeriodicReviewMinPercentTextBox");
        var manual = Find<Button>(w, "ReviewNowButton");
        Require(!interval.IsEnabled && manual.IsEnabled, "Default-off must retain manual review."); Save(w, "default-off");
        Click(manual); Require(requests == 1, "Manual action is disconnected.");
        Click(Find<CheckBox>(w, "PeriodicReviewEnabledCheck")); Pump();
        Require(f.Model.PeriodicReviewEnabled && interval.IsEnabled, "Automatic prompting did not enable."); Save(w, "enabled");
        interval.Text = "45"; snooze.Text = "10"; dismiss.Text = "8"; Pump();
        Require(f.Model.PeriodicReviewIntervalMinutes == 45 && f.Model.PeriodicReviewSnoozeMinutes == 10 && f.Model.PeriodicReviewAutoDismissSeconds == 8, "Timing was not persisted.");
        Click(Visual<Button>(w, "ReviewExampleSkip")); Pump();
        Require(Visual<TextBlock>(w, "ReviewExampleOutcome").Text!.Contains("45 min") && requests == 1, "Skip example changed real review state."); Save(w, "skip-example");
        Click(Visual<Button>(w, "ReviewExampleAccept")); Pump();
        Require(requests == 1 && f.Model.LastPeriodicReviewCompletedUtc == null, "Example opened a review or changed history."); Save(w, "review-example");
        interval.Text = "1441"; snooze.Text = "0"; dismiss.Text = "121"; Pump();
        Require(f.Model.PeriodicReviewIntervalMinutes == 45 && f.Model.PeriodicReviewSnoozeMinutes == 10 && f.Model.PeriodicReviewAutoDismissSeconds == 8 && manual.IsEnabled, "Invalid timing saved or blocked manual review."); Save(w, "invalid-timing");
        minimum.Text = "3601"; percent.Text = "NaN"; Pump();
        Require(!manual.IsEnabled && f.Model.PeriodicReviewMinDurationSeconds == 15 && f.Model.PeriodicReviewMinActivityPercent == 3, "Invalid detail saved or enabled review.");
        Call(w, "ReviewNowButton_Click", null!, new RoutedEventArgs()); Require(requests == 1, "Manual handler bypassed validation.");
        minimum.Text = "0"; percent.Text = "0"; Pump();
        Require(manual.IsEnabled && f.Model.PeriodicReviewMinDurationSeconds == 0 && f.Model.PeriodicReviewMinActivityPercent == 0, "Zero filters lost semantics.");
        interval.Text = "30"; snooze.Text = "30"; dismiss.Text = "5"; Save(w, "minimum-filters");
        percent.Text = "3.25"; Pump(); Call(w, "ReloadFromSettings"); Pump();
        Require(percent.Text == "3.25", "Reload rounded a valid fractional grouping value.");
        percent.Text = "3.5"; Pump(); Require(f.Model.PeriodicReviewMinActivityPercent == 3.5, "Fractional grouping rejected.");
        foreach (string invalid in new[] { "Infinity", "-1", "50.1", "" }) { percent.Text = invalid; Pump(); Require(!manual.IsEnabled && f.Model.PeriodicReviewMinActivityPercent == 3.5, "Invalid percentage accepted: " + invalid); }
        percent.Text = "3"; minimum.Text = "15"; Pump();
        f.Model.CommandModeKeys[ShortcutAction.PeriodicReview] = 0x52; Call(w, "ReloadFromSettings"); Pump();
        Require(Visual<TextBlock>(w, "ReviewShortcutHint").Text!.EndsWith("then R"), "Shortcut label ignored binding.");
        f.Model.CommandModeKeys[ShortcutAction.PeriodicReview] = 0; Call(w, "ReloadFromSettings"); Pump();
        Require(Visual<TextBlock>(w, "ReviewShortcutHint").Text!.Contains("not assigned"), "Unassigned shortcut was advertised.");
        int before = changes;
        foreach (string theme in new[] { AppThemeCatalog.Midnight, AppThemeCatalog.Nika, AppThemeCatalog.Pirate, AppThemeCatalog.Nika })
        {
            f.Model.ThemeMode = theme; Desktop("ThemeResources", "ApplyApplication", theme); Call(w, "ReloadFromSettings"); Call(w, "UpdatePreviewSafely"); Pump(60);
            if (theme != AppThemeCatalog.Nika)
            {
                Require(Grid.GetColumn(interval) == 1 && interval.Parent is Grid && interval.Height == 32, "Legacy field geometry was not restored.");
                Require(Find<CheckBox>(w, "PeriodicReviewEnabledCheck").Content is string && manual.Classes.Contains("modern"), "Legacy controls not restored.");
            }
            else Click(Visual<Button>(w, "ActivityWatchReviewTab"));
        }
        Require(changes == before, "Theme change saved review settings.");
        Click(Find<CheckBox>(w, "PeriodicReviewEnabledCheck")); Pump(); Require(changes == before + 1, "Duplicate callbacks after restoration.");
        Require(manual.IsEnabled && !f.Model.ActivityWatchEnabled, "ActivityWatch off blocked manual review.");
    }

    internal static void Layout()
    {
        foreach (int scale in new[] { 70, 90, 125 }) foreach (bool compact in new[] { false, true })
        {
            using var f = new Fixture(scale, compact); var w = f.Window;
            Click(Find<CheckBox>(w, "PeriodicReviewEnabledCheck")); Pump(60);
            CheckFit(w); Save(w, $"enabled-{scale}-{(compact ? "compact" : "regular")}");
            if (scale == 90 && !compact)
            {
                f.Model.Typography.Global.SizePercent = 175; Desktop("DesktopTypography", "Apply", f.Model.Typography); Pump(120);
                CheckFit(w); Save(w, "large-text");
                Visual<ScrollViewer>(w, "ActivityWatchScroll").ScrollToEnd(); Pump(); Save(w, "large-text-example");
                Find<TextBox>(w, "PeriodicReviewIntervalTextBox").Text = "1440";
                Find<TextBox>(w, "PeriodicReviewMinDurationTextBox").Text = "3600"; Pump(60);
                foreach (string name in new[] { "PeriodicReviewIntervalTextBox", "PeriodicReviewMinDurationTextBox" })
                {
                    var field = Find<TextBox>(w, name); var text = field.GetVisualDescendants().OfType<TextPresenter>().Single();
                    Require(text.TextLayout.WidthIncludingTrailingWhitespace <= field.Bounds.Width - field.Padding.Left - field.Padding.Right - 2,
                        "Large text clips an allowed four-digit value: " + name);
                }
            }
        }
    }

    private static void CheckFit(SettingsWindow w)
    {
        var scroll = Visual<ScrollViewer>(w, "ActivityWatchScroll");
        Require(scroll.Viewport.Height > 50 && scroll.Extent.Width <= scroll.Viewport.Width + 1, "Review page horizontal overflow.");
        var done = Visual<Button>(w, "SettingsDone"); Require(done.IsEffectivelyVisible && done.Bounds.Height >= 30, "Footer disappeared.");
        foreach (var name in new[] { "PeriodicReviewIntervalTextBox", "PeriodicReviewSnoozeTextBox", "PeriodicReviewAutoDismissTextBox", "PeriodicReviewMinDurationTextBox", "PeriodicReviewMinPercentTextBox" })
        { var field = Find<TextBox>(w, name); Require(field.Bounds.Height >= 36 && field.Bounds.Width >= 70, "Review input clipped: " + name); }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly AppSettings Model = new() { ThemeMode = AppThemeCatalog.Nika };
        internal readonly SettingsWindow Window;
        internal Fixture(int scale = 90, bool compact = false)
        {
            TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"Review-{Guid.NewGuid():N}.json"));
            Model.UiScalePercent = scale; Desktop("ThemeResources", "ApplyApplication", Model.ThemeMode);
            Desktop("DesktopPageAppearance", "Initialize", Model); Desktop("DesktopTypography", "Initialize", Model.Typography);
            Window = new SettingsWindow(Model) { ShowActivated = false, ShowInTaskbar = false, Width = compact ? 840 : 1120, Height = compact ? 600 : 720 };
            Window.Show(); Pump(60); Click(Visual<Button>(Window, "SettingsNav_ActivityWatch")); Pump(); Click(Visual<Button>(Window, "ActivityWatchReviewTab")); Pump(60);
        }
        public void Dispose() { Window.Close(); TelegramOutboxStore.SetCustomFilePath(null); Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }
    private static T Find<T>(object w, string name) => (T)w.GetType().GetField(name, Hidden)!.GetValue(w)!;
    private static T Visual<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().SingleOrDefault(c => c.Name == name) ?? throw new Exception("Missing " + name);
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Click(CheckBox check) => typeof(ToggleButton).GetMethod("OnClick", Hidden)!.Invoke(check, null);
    private static void Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Hidden)!.Invoke(target, args);
    private static void Pump(int ms = 30) => ClockOverlayChecks.Pump(ms);
    private static void Desktop(string type, string method, params object[] args) => typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static void Save(SettingsWindow window, string name)
    {
        Directory.CreateDirectory(Output); var frame = window.GetVisualDescendants().OfType<StopwatchFrameSurface>().First();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
        if (((LayoutTransformControl)window.Content!).LayoutTransform!.Value.M11 >= 1) bitmap.Render(window);
        else { using var context = bitmap.CreateDrawingContext(); context.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }
}
