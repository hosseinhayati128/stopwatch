using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Internet;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class SettingsInternetNativeChecks
{
    private const string Output = "design/one-piece-v2/settings/internet-connection-implemented";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static InternetCheckResult Result(InternetStatus status = InternetStatus.Online, double? speed = 38.6, string name = "Home Wi-Fi") =>
        new(DateTime.Today.AddHours(14).AddMinutes(32), status, status == InternetStatus.Offline ? null : status == InternetStatus.Slow ? 318 : 24,
            status == InternetStatus.Offline ? null : speed, new(true, "Wi-Fi", name, 88, "Sample adapter", name),
            status == InternetStatus.Offline ? "Ping timed out (offline or blocked)" : "Sample result");

    internal static void ResultsAndSettings()
    {
        using var f = new Fixture(); var w = f.Window; var test = Find<Button>(w, "TestInternetButton");
        Require(Text(w, "InternetConnectionHeading") == "Not checked yet" && f.Calls == 0, "Opening Settings launched a probe or invented status."); Save(w, "not-checked");
        Click(test); Wait(() => test.IsEnabled);
        Require(f.Calls == 1 && Text(w, "InternetConnectionHeading") == "Online" && Text(w, "InternetDownload") == "38.6 Mbps", "Manual result is not rendered while monitoring is off.");
        Find<CheckBox>(w, "InternetMonitorEnabledCheck").IsChecked = true; Pump();
        Require(f.Model.InternetMonitorEnabled && Text(w, "InternetConnectionHeading") == "Online", "Monitoring preference erased result.");
        var interval = Find<TextBox>(w, "InternetCheckIntervalTextBox"); interval.Focus();
        interval.Text = "2"; Pump(); Require(f.Model.InternetMonitorIntervalMinutes == 10, "An unfinished interval prefix was persisted.");
        interval.Text = "25"; Pump(); Require(f.Model.InternetMonitorIntervalMinutes == 10, "Interval was persisted before confirming the edit.");
        interval.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Pump();
        Require(f.Model.InternetMonitorIntervalMinutes == 25, "Valid interval was not persisted.");
        foreach (string invalid in new[] { "0", "121", "", "abc", "1.5", "999999999999" })
        {
            Find<TextBox>(w, "InternetCheckIntervalTextBox").Text = invalid; Pump();
            Require(f.Model.InternetMonitorIntervalMinutes == 25 && Text(w, "InternetIntervalHint")!.Contains("1 to 120") && test.IsEnabled, "Invalid interval replaced valid value or blocked manual testing.");
        }
        foreach (string draft in new[] { "1", "12", "121" }) { interval.Text = draft; Pump(); Require(f.Model.InternetMonitorIntervalMinutes == 25, "Invalid completed edit retained an intermediate prefix."); }
        interval.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Pump();
        Require(f.Model.InternetMonitorIntervalMinutes == 25, "Enter applied invalid interval.");
        interval.Text = "10"; Pump(); test.Focus(); Pump();
        Require(f.Model.InternetMonitorIntervalMinutes == 10, "Leaving the field did not apply a valid interval.");
        f.Running = false; Find<CheckBox>(w, "InternetMonitorOnlyDuringTimersCheck").IsChecked = true; Pump();
        Require(Text(w, "InternetScheduleHeading") == "Waiting for a stopwatch" && test.IsEnabled, "Timer-only waiting state is wrong."); Save(w, "waiting-for-timer");
        f.Next = Result(InternetStatus.Slow); Click(test); Wait(() => test.IsEnabled); Require(Text(w, "InternetConnectionHeading") == "Slow connection", "Slow result lost."); Save(w, "slow");
        f.Next = Result(InternetStatus.Offline); Click(test); Wait(() => test.IsEnabled); Require(Text(w, "InternetConnectionHeading") == "Internet not reached", "Unreachable result overclaims status."); Save(w, "unreachable");
        f.Next = Result(speed: null); Click(test); Wait(() => test.IsEnabled); Require(Text(w, "InternetLatency") == "24 ms" && Text(w, "InternetDownload") == "Unavailable", "Partial result invented a measurement."); Save(w, "partial-result");
        Click(Visual<Button>(w, "InternetHistoryTab"));
        Require(Find<TextBox>(w, "InternetFileNameTextBox").IsEffectivelyVisible && Find<Button>(w, "ClearInternetLogButton").IsEffectivelyVisible, "Existing export controls are inaccessible.");
        Click(Visual<Button>(w, "InternetConnectionTab")); Require(test.IsEffectivelyVisible, "Connection tab did not restore.");
    }

    internal static void Lifecycle()
    {
        using var f = new Fixture(); var w = f.Window; var test = Find<Button>(w, "TestInternetButton");
        var old = f.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously); Click(test); Wait(() => f.Calls == 1);
        var token = f.Token; Click(test); Pump(); Require(f.Calls == 1 && !test.IsEnabled, "Duplicate manual probe started."); Save(w, "checking");
        Click(Visual<Button>(w, "SettingsNav_Application")); Require(token.IsCancellationRequested, "Leaving the page did not cancel.");
        Click(Visual<Button>(w, "SettingsNav_Internet"));
        var newer = f.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously); Click(test); Wait(() => f.Calls == 2);
        old.SetResult(Result(InternetStatus.Offline)); Pump(80); Require(!test.IsEnabled, "Old completion unlocked a newer probe.");
        newer.SetResult(Result()); Wait(() => test.IsEnabled); Require(Text(w, "InternetConnectionHeading") == "Online", "New result did not win.");
        var themed = f.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously); Click(test); Wait(() => f.Calls == 3); token = f.Token;
        f.Theme(AppThemeCatalog.Midnight); Require(token.IsCancellationRequested, "Theme switch left pending probe alive.");
        themed.SetResult(Result(InternetStatus.Offline)); Pump(70); f.Theme(AppThemeCatalog.Nika);
        Require(Text(w, "InternetConnectionHeading") != "Internet not reached", "Late result crossed theme rebuild.");
        var closed = f.Pending = new(TaskCreationOptions.RunContinuationsAsynchronously); Click(test); Wait(() => f.Calls == 4); token = f.Token;
        var heading = Visual<TextBlock>(w, "InternetConnectionHeading"); string? before = heading.Text;
        w.Close(); closed.SetResult(Result()); Pump(80); Require(token.IsCancellationRequested && heading.Text == before, "Closed Settings received a late result.");
    }

    internal static void LayoutAndThemes()
    {
        foreach (int scale in new[] { 70, 90, 125 }) foreach (bool compact in new[] { false, true })
        {
            using var f = new Fixture(scale, compact); var w = f.Window; var test = Find<Button>(w, "TestInternetButton");
            Click(test); Wait(() => test.IsEnabled); Find<CheckBox>(w, "InternetMonitorEnabledCheck").IsChecked = true; Pump();
            var scroll = Visual<ScrollViewer>(w, "InternetScroll"); var done = Visual<Button>(w, "SettingsDone");
            Fit(); Save(w, (compact ? "compact-" : "wide-") + scale);
            var footer = done.TranslatePoint(default, w); scroll.ScrollToEnd(); Pump(); Fit();
            Require(footer == done.TranslatePoint(default, w), "Done moved when scrolling."); Save(w, (compact ? "compact-" : "wide-") + scale + "-monitoring");
            Require(test.Focus() && Find<TextBox>(w, "InternetCheckIntervalTextBox").Focus(), "Keyboard cannot reach controls.");
            if (scale == 90 && !compact)
            {
                f.Next = Result(name: "شبکهٔ خانه و دفتر — نام طولانی برای بررسی خوانایی و نمایش کامل"); Click(test); Wait(() => test.IsEnabled); scroll.ScrollToHome(); Pump(); Save(w, "long-network");
                for (int i = 0; i < 2; i++) { f.Theme(AppThemeCatalog.Daylight); Require(test.IsEffectivelyVisible, "Legacy test control was lost."); f.Theme(AppThemeCatalog.Nika); }
                Require(w.GetVisualDescendants().OfType<ScrollViewer>().Count(c => c.Name == "InternetScroll") == 1, "Theme rebuild duplicated page.");
            }
            if (scale == 125 && compact)
            {
                f.Model.Typography.Global.SizePercent = 175; Desktop("DesktopTypography", "Apply", f.Model.Typography); Pump(90); Fit();
                var field = Find<TextBox>(w, "InternetCheckIntervalTextBox"); field.BringIntoView(); Pump();
                var presenter = field.GetVisualDescendants().OfType<TextPresenter>().Single(); var fieldScroll = field.GetVisualDescendants().OfType<ScrollViewer>().Single();
                Require(presenter.TextLayout.Height <= fieldScroll.Viewport.Height + 1, "Large interval text is clipped."); Save(w, "large-text");
            }
            void Fit() { Require(scroll.Viewport.Height > 65 && scroll.Extent.Width <= scroll.Viewport.Width + 1, "Page has horizontal overflow.");
                var end = done.TranslatePoint(new Point(done.Bounds.Width, done.Bounds.Height), w)!.Value;
                Require(end.X <= w.ClientSize.Width + 1 && end.Y <= w.ClientSize.Height + 1, "Done is outside the window."); }
        }
    }

    internal static void ControllerLifecycle()
    {
        Require(typeof(ControllerWindow).GetMethod("AttachInternetMonitor", Hidden) != null, "Desktop monitor ownership is missing.");
        using var f = new Fixture(); f.Model.InternetMonitorEnabled = true;
        var controller = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden, null, [typeof(AppSettings)], null)!.Invoke([f.Model]);
        var pending = new TaskCompletionSource<InternetCheckResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        bool delay = false; int saves = 0; CancellationToken token = default;
        Func<int, CancellationToken, Task<InternetCheckResult>> probe = (_, ct) => { token = ct; return delay ? pending.Task : Task.FromResult(Result()); };
        var ctor = typeof(InternetMonitorService).GetConstructor(Hidden, null, [typeof(Func<AppSettings>), typeof(Func<bool>), typeof(Func<int, CancellationToken, Task<InternetCheckResult>>), typeof(Action<InternetCheckResult, AppSettings>)], null)!;
        using var service = (InternetMonitorService)ctor.Invoke([(Func<AppSettings>)(() => f.Model), (Func<bool>)(() => true), probe, (Action<InternetCheckResult, AppSettings>)((_, _) => saves++)]);
        try
        {
            controller.ShowActivated = false; controller.ShowInTaskbar = false; controller.Show();
            typeof(ControllerWindow).GetField("_settingsWindow", Hidden)!.SetValue(controller, f.Window);
            typeof(ControllerWindow).GetMethod("AttachInternetMonitor", Hidden)!.Invoke(controller, [service]);
            var check = typeof(InternetMonitorService).GetMethod("CheckAsync", Hidden)!;
            var first = Task.Run(async () => await (Task<InternetCheckResult?>)check.Invoke(service, [true, null])!);
            Wait(() => first.IsCompleted && Text(f.Window, "InternetConnectionHeading") == "Online");
            Require(saves == 1, "Background result was not logged once and marshaled to Settings.");
            var preview = typeof(ControllerWindow).GetField("_previewMode", Hidden)!; preview.SetValue(controller, false);
            try { Call(controller, "StartInternetMonitor"); } finally { preview.SetValue(controller, true); }
            Require(ReferenceEquals(typeof(ControllerWindow).GetField("_internetMonitorService", Hidden)!.GetValue(controller), service), "Controller reopening replaced the schedule.");
            delay = true; var late = (Task<InternetCheckResult?>)check.Invoke(service, [true, null])!;
            f.Model.InternetMonitorEnabled = false; Call(controller, "RestartInternetMonitor");
            Require(token.IsCancellationRequested, "Controller reconfiguration left the old check active.");
            controller.Close(); pending.SetResult(Result(InternetStatus.Offline)); Wait(() => late.IsCompleted); Pump();
            Require(saves == 1 && Text(f.Window, "InternetConnectionHeading") == "Online", "Shutdown allowed late log or UI updates.");
            Require(typeof(ControllerWindow).GetField("_internetMonitorService", Hidden)!.GetValue(controller) == null, "Controller retained monitor after close.");
        }
        finally { controller.Close(); }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly AppSettings Model = new() { ThemeMode = AppThemeCatalog.Nika, InternetMonitorEnabled = false };
        internal readonly SettingsWindow Window; internal int Calls; internal bool Running = true;
        internal InternetCheckResult Next = Result(); internal TaskCompletionSource<InternetCheckResult>? Pending; internal CancellationToken Token;
        internal Fixture(int scale = 90, bool compact = false)
        {
            Require(Application.Current is not StopwatchOverlay.Desktop.App, "Tests must not load live App.");
            TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"StopwatchInternet-{Guid.NewGuid():N}.json"));
            InternetHistoryStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"StopwatchInternetHistory-{Guid.NewGuid():N}.json"));
            Model.UiScalePercent = scale; Desktop("ThemeResources", "ApplyApplication", Model.ThemeMode);
            Desktop("DesktopPageAppearance", "Initialize", Model); Desktop("DesktopTypography", "Initialize", Model.Typography);
            var ctor = typeof(SettingsWindow).GetConstructor(Hidden, null, [typeof(AppSettings), typeof(Func<ProjectHistoryView>), typeof(System.Net.Http.HttpClient), typeof(System.Net.Http.HttpClient), typeof(Func<bool>), typeof(Func<int, CancellationToken, Task<InternetCheckResult>>), typeof(Func<bool>)], null);
            Require(ctor != null, "Internet isolated probe constructor is missing.");
            Func<int, CancellationToken, Task<InternetCheckResult>> probe = (bytes, token) => { Token = token; Interlocked.Increment(ref Calls); return Pending?.Task ?? Task.FromResult(Next); };
            Window = (SettingsWindow)ctor!.Invoke([Model, null, null, null, null, probe, (Func<bool>)(() => Running)]);
            Window.ShowActivated = false; Window.ShowInTaskbar = false; Window.Width = compact ? 840 : 1120; Window.Height = compact ? 600 : 720;
            Window.Show(); Pump(60); Click(Visual<Button>(Window, "SettingsNav_Internet")); Pump(60);
        }
        internal void Theme(string theme) { Model.ThemeMode = theme; Desktop("ThemeResources", "ApplyApplication", theme); Call(Window, "ReloadFromSettings"); Call(Window, "UpdatePreviewSafely"); Pump(80); }
        public void Dispose() { Window.Close(); InternetHistoryStore.SetCustomFilePath(null); TelegramOutboxStore.SetCustomFilePath(null); Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }
    private static string? Text(SettingsWindow w, string name) => Visual<TextBlock>(w, name).Text;
    private static T Find<T>(SettingsWindow w, string name) => (T)typeof(SettingsWindow).GetField(name, Hidden)!.GetValue(w)!;
    private static T Visual<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Hidden)!.Invoke(target, null);
    private static void Pump(int ms = 30) => ClockOverlayChecks.Pump(ms);
    private static void Wait(Func<bool> ready) { for (int i = 0; i < 200 && !ready(); i++) Pump(25); Require(ready(), "Timed out waiting for Internet fixture."); Pump(); }
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
