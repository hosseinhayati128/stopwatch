using System.Net;
using System.Net.Http;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.ActivityWatch;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class SettingsActivityWatchExportNativeChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "design/one-piece-v2/settings/activitywatch-export-implemented";
    internal static void Controls()
    {
        using var f = new Fixture(); var w = f.Window;
        Require(Visual<Grid>(w, "ActivityWatchExportCards").IsEffectivelyVisible, "Missing production Sync & Export cards.");
        Require(f.Http.Requests == 0, "Opening export page performed a request.");
        var changes = new List<SettingsChangeKind>();
        typeof(SettingsWindow).GetEvent("SettingsChanged", Hidden)!.GetAddMethod(true)!.Invoke(w, [new Action<SettingsChangeKind>(changes.Add)]);
        var interval = Find<TextBox>(w, "ActivityWatchPeriodicSyncIntervalTextBox");
        var file = Find<TextBox>(w, "ActivityWatchFileNameTextBox"); var minimum = Find<TextBox>(w, "ActivityWatchMinDurationTextBox");
        var sync = Find<Button>(w, "SyncActivityWatchNowButton");
        Require(sync.IsEnabled && !interval.IsEnabled, "Integration off should pause automatic work, not manual export.");
        Click(Visual<Button>(w, "ActivityWatchConnectionTab")); Click(Find<CheckBox>(w, "ActivityWatchEnabledCheck"));
        Click(Visual<Button>(w, "ActivityWatchSyncTab")); Pump(); Require(interval.IsEnabled, "Integration on did not restore schedule fields.");
        interval.Text = "75"; Pump(); Require(f.Model.ActivityWatchPeriodicSyncIntervalMinutes == 75, "Interval lost persistence.");
        interval.Text = "1441"; Pump(); Require(f.Model.ActivityWatchPeriodicSyncIntervalMinutes == 75 && sync.IsEnabled, "Invalid interval was saved or blocked manual export."); Save(w, "invalid-interval");
        interval.Text = "60"; minimum.Text = "0"; Pump(); Require(!sync.IsEnabled && f.Model.ActivityWatchMinDurationSeconds == 15, "Invalid filter was saved or allowed.");
        minimum.Text = "15"; file.Text = @"..\outside.md"; Pump(); Require(!sync.IsEnabled && f.Model.ActivityWatchExportFileName == "ActivityWatch Log.md", "Unsafe filename allowed."); Save(w, "invalid-file");
        file.Text = ""; Pump(); Require(sync.IsEnabled, "Blank filename should use default."); file.Text = "ActivityWatch Log.md"; Pump();
        Click(Visual<Button>(w, "ActivityWatchChangeFolder")); Pump();
        Require(Find<TextBox>(w, "ObsidianFolderTextBox").IsEffectivelyVisible, "Change folder did not route to shared folder settings.");
        Find<TextBox>(w, "ObsidianFolderTextBox").Text = ""; Pump(); Click(Visual<Button>(w, "SettingsNav_ActivityWatch")); Pump();
        Click(Visual<Button>(w, "ActivityWatchSyncTab")); Pump(); Require(!sync.IsEnabled, "Missing folder enables export."); Save(w, "missing-folder");
        int before = changes.Count;
        foreach (string theme in new[] { AppThemeCatalog.Midnight, AppThemeCatalog.Nika, AppThemeCatalog.Pirate, AppThemeCatalog.Nika })
        {
            f.Model.ThemeMode = theme; Desktop("ThemeResources", "ApplyApplication", theme); Call(w, "ReloadFromSettings"); Call(w, "UpdatePreviewSafely"); Pump(60);
            if (theme != AppThemeCatalog.Nika)
            {
                Require(Find<CheckBox>(w, "ActivityWatchIncludeTitlesCheck").Content is string && sync.Classes.Contains("modern"), "Legacy export styles not restored.");
                foreach (var (field, label) in new[] { (interval, "Sync interval (minutes)"), (file, "Export file name (in Obsidian Vault)"), (minimum, "Minimum active duration (seconds)") })
                {
                    var siblings = ((Panel)field.Parent!).Children; int index = siblings.IndexOf(field);
                    Require(index > 0 && siblings[index - 1] is TextBlock text && text.Text == label,
                        "Legacy field restored under the wrong label: " + field.Name);
                }
                var original = ((Panel)interval.Parent!).Children;
                foreach (var (name, index) in new[] { ("ActivityWatchEnabledCheck", 0), ("ActivityWatchSyncOnStopwatchCheck", 1), ("ActivityWatchPeriodicSyncCheck", 2),
                    ("ActivityWatchPeriodicSyncIntervalTextBox", 4), ("ActivityWatchIncludeTitlesCheck", 6), ("ActivityWatchIncludeWebCheck", 7),
                    ("ActivityWatchServerUrlTextBox", 9), ("ActivityWatchFileNameTextBox", 12), ("ActivityWatchMinDurationTextBox", 15), ("ActivityWatchStatusText", 18) })
                    Require(original[index].Name == name, "Legacy sibling order changed for " + name);
                var actions = ((Panel)sync.Parent!).Children.OfType<Button>().Select(button => button.Name).ToArray();
                Require(actions.SequenceEqual(new[] { "StartActivityWatchAppButton", "TestActivityWatchButton", "SyncActivityWatchNowButton" }), "Legacy action order changed.");
            }
            else Click(Visual<Button>(w, "ActivityWatchSyncTab"));
        }
        Require(changes.Count == before, "Theme restoration saved settings.");
        Click(Find<CheckBox>(w, "ActivityWatchIncludeTitlesCheck")); Pump(); Require(changes.Count == before + 1, "Export callbacks were duplicated.");
    }

    internal static void Actions()
    {
        using var f = new Fixture(); f.RequireTransport(); var w = f.Window;
        var sync = Find<Button>(w, "SyncActivityWatchNowButton");
        Click(Visual<Button>(w, "ActivityWatchConnectionTab")); Click(Find<CheckBox>(w, "ActivityWatchEnabledCheck"));
        Click(Visual<Button>(w, "ActivityWatchSyncTab")); Pump();
        Save(w, "ready"); Click(sync); Wait(() => sync.IsEnabled);
        Require(Heading(w) == "Today’s report updated" && File.Exists(Path.Combine(f.Folder, "ActivityWatch Log.md")), "Manual export did not write or report success."); Visual<ScrollViewer>(w, "ActivityWatchScroll").ScrollToEnd(); Pump(); Save(w, "synced");
        string before = File.ReadAllText(Path.Combine(f.Folder, "ActivityWatch Log.md"));
        f.Http.Code = HttpStatusCode.ServiceUnavailable; Click(sync); Wait(() => sync.IsEnabled);
        Require(Heading(w) == "Couldn’t update the report" && File.ReadAllText(Path.Combine(f.Folder, "ActivityWatch Log.md")) == before, "Failed read replaced the report."); Save(w, "read-failed");
        f.Http.Code = HttpStatusCode.OK; f.Http.Body = "[]"; Click(sync); Wait(() => sync.IsEnabled);
        Require(Heading(w) == "Report contains no entries", "Empty data did not have distinct feedback."); Save(w, "empty-report");
        Directory.CreateDirectory(Path.Combine(f.Folder, "blocked.md")); Find<TextBox>(w, "ActivityWatchFileNameTextBox").Text = "blocked.md"; Pump();
        Click(sync); Wait(() => sync.IsEnabled); Require(Heading(w) == "Couldn’t update the report", "Write failure reported success."); Save(w, "write-failed");
    }

    internal static void Lifecycle()
    {
        using var f = new Fixture(); f.RequireTransport(); var w = f.Window;
        var sync = Find<Button>(w, "SyncActivityWatchNowButton"); var file = Find<TextBox>(w, "ActivityWatchFileNameTextBox");
        var old = f.Http.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously); Click(sync);
        Require(!sync.IsEnabled && !file.IsEnabled, "Busy export allows conflicting edits."); int requests = f.Http.Requests; Click(sync); Require(requests == f.Http.Requests, "Duplicate manual export."); Save(w, "syncing");
        var oldToken = f.Http.LastToken; file.Text = "new.md"; Pump(); Require(oldToken.IsCancellationRequested && sync.IsEnabled, "Configuration edit did not cancel old work.");
        var newer = f.Http.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously); Click(sync);
        old.SetResult(true); Pump(80); Require(!sync.IsEnabled, "Old completion unlocked newer export.");
        newer.SetResult(true); Wait(() => sync.IsEnabled); Require(Heading(w) == "Today’s report updated", "Current result lost.");
        foreach (string leave in new[] { "tab", "page", "theme", "close" })
        {
            var delay = f.Http.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously); Click(sync); var token = f.Http.LastToken;
            if (leave == "tab") Click(Visual<Button>(w, "ActivityWatchReviewTab"));
            if (leave == "page") Click(Visual<Button>(w, "SettingsNav_Application"));
            if (leave == "theme") { f.Model.ThemeMode = AppThemeCatalog.Midnight; Desktop("ThemeResources", "ApplyApplication", f.Model.ThemeMode); Call(w, "ReloadFromSettings"); Call(w, "UpdatePreviewSafely"); }
            if (leave == "close") w.Close();
            Pump(60); Require(token.IsCancellationRequested, "Export survived " + leave); delay.SetResult(true); Pump(80);
            if (leave == "close") break;
            if (leave == "theme") { f.Model.ThemeMode = AppThemeCatalog.Nika; Desktop("ThemeResources", "ApplyApplication", f.Model.ThemeMode); Call(w, "ReloadFromSettings"); Call(w, "UpdatePreviewSafely"); Pump(60); }
            Click(Visual<Button>(w, "SettingsNav_ActivityWatch")); Click(Visual<Button>(w, "ActivityWatchSyncTab")); Pump();
            Require(Heading(w) == "Sync canceled" && sync.IsEnabled, "Late completion overwrote cancellation after " + leave);
        }
    }

    internal static void Layout()
    {
        foreach (int scale in new[] { 70, 90, 125 }) foreach (bool compact in new[] { false, true })
        {
            using var f = new Fixture(scale, compact); var w = f.Window;
            var scroll = Visual<ScrollViewer>(w, "ActivityWatchScroll"); var done = Visual<Button>(w, "SettingsDone");
            CheckFit(); if (!compact) Save(w, "ready-" + scale); else if (scale == 90) Save(w, "compact-settings");
            var footer = done.TranslatePoint(default, w); scroll.ScrollToEnd(); Pump(); CheckFit();
            Require(footer == done.TranslatePoint(default, w), "Done moved with content."); if (compact && scale == 90) Save(w, "compact-export");
            if (compact && scale == 125)
            {
                f.Model.Typography.Global.SizePercent = 175; Desktop("DesktopTypography", "Apply", f.Model.Typography); Pump(80);
                var file = Find<TextBox>(w, "ActivityWatchFileNameTextBox"); file.BringIntoView(); Pump(); CheckFit();
                var text = file.GetVisualDescendants().OfType<TextPresenter>().Single(); var viewport = file.GetVisualDescendants().OfType<ScrollViewer>().Single();
                Require(text.TextLayout.Height <= viewport.Viewport.Height + 1, "Large filename text clips."); Save(w, "large-text");
            }
            void CheckFit()
            {
                Require(scroll.Viewport.Height > 65 && scroll.Extent.Width <= scroll.Viewport.Width + 1, "Export content overflows horizontally.");
                var end = done.TranslatePoint(new Point(done.Bounds.Width, done.Bounds.Height), w)!.Value;
                Require(end.X <= w.ClientSize.Width + 1 && end.Y <= w.ClientSize.Height + 1, "Done is outside window.");
            }
        }
    }

    internal static void ControllerLifecycle()
    {
        using var f = new Fixture(); var settings = f.Model;
        settings.ActivityWatchEnabled = true; settings.ActivityWatchPeriodicSyncEnabled = false;
        int calls = 0;
        var controller = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden, null, [typeof(AppSettings)], null)!.Invoke([settings]);
        controller.ShowActivated = false; controller.ShowInTaskbar = false;
        var service = new ActivityWatchSyncService(() => settings, (_, _) => { calls++; return Task.FromResult(new ActivityWatchSyncResult(true, 1, 0, "sample")); });
        try
        {
            controller.Show(); Pump(); Call(controller, "StartActivityWatchSync");
            Require(typeof(ControllerWindow).GetField("_activityWatchSyncService", Hidden)!.GetValue(controller) == null, "Preview started a live service.");
            typeof(ControllerWindow).GetMethod("AttachActivityWatchSync", Hidden)!.Invoke(controller, [service]);
            var history = new ProjectTimeHistory().CreateView(DateTime.UtcNow);
            Require(ObsidianLogSync.SyncHistory(history, settings).Success && calls == 1, "Successful stopwatch export did not trigger ActivityWatch.");
            var foreign = new AppSettings { ObsidianVaultFolder = f.Folder };
            Require(ObsidianLogSync.SyncHistory(history, foreign).Success && calls == 1, "Another settings instance triggered this controller.");
            Directory.CreateDirectory(Path.Combine(f.Folder, "blocked.md"));
            Require(!ObsidianLogSync.SyncHistory(history, settings, customFileName: "blocked.md").Success && calls == 1, "Failed export triggered ActivityWatch.");
            settings.ActivityWatchSyncOnStopwatchSync = false; service.RestartTimer();
            Require(ObsidianLogSync.SyncHistory(history, settings).Success && calls == 1, "After-export setting was ignored.");
            settings.ActivityWatchSyncOnStopwatchSync = true; service.RestartTimer(); controller.Close();
            Require(!service.TriggerSyncOnceAsync().GetAwaiter().GetResult().Success, "Closing controller did not dispose the service.");
            Require(ObsidianLogSync.SyncHistory(history, settings).Success && calls == 1, "Closed controller retained its export hook.");
        }
        finally { controller.Close(); service.Dispose(); }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly AppSettings Model = new() { ThemeMode = AppThemeCatalog.Nika };
        internal readonly FakeHttp Http = new(); internal readonly SettingsWindow Window;
        internal int Launches; internal bool Found = true;
        internal readonly string Folder = Path.Combine(Path.GetTempPath(), "AW sample-" + Guid.NewGuid().ToString("N")[..8]); private readonly HttpClient _client; private readonly bool _injected;
        internal Fixture(int scale = 90, bool compact = false)
        {
            TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"StopwatchActivityWatch-{Guid.NewGuid():N}.json"));
            Directory.CreateDirectory(Folder); Model.ObsidianVaultFolder = Folder;
            Model.UiScalePercent = scale; Desktop("ThemeResources", "ApplyApplication", Model.ThemeMode);
            Desktop("DesktopPageAppearance", "Initialize", Model); Desktop("DesktopTypography", "Initialize", Model.Typography);
            _client = new(Http);
            var ctor = typeof(SettingsWindow).GetConstructor(Hidden, null, [typeof(AppSettings), typeof(Func<ProjectHistoryView>), typeof(HttpClient), typeof(HttpClient), typeof(Func<bool>)], null);
            _injected = ctor != null;
            Window = ctor != null ? (SettingsWindow)ctor.Invoke([Model, null, null, _client, (Func<bool>)(() => { Launches++; return Found; })]) : new SettingsWindow(Model);
            Window.ShowActivated = false; Window.ShowInTaskbar = false; Window.Width = compact ? 840 : 1120; Window.Height = compact ? 600 : 720;
            Window.Show(); Pump(60); Click(Visual<Button>(Window, "SettingsNav_ActivityWatch")); Pump(60); Click(Visual<Button>(Window, "ActivityWatchSyncTab")); Pump(60);
        }
        internal void RequireTransport() => Require(_injected && typeof(SettingsWindow).GetMethod("RunActivityWatchExportAsync", Hidden) != null, "ActivityWatch isolated transport/launcher constructor is missing.");
        public void Dispose() { Window.Close(); _client.Dispose(); Directory.Delete(Folder, true); TelegramOutboxStore.SetCustomFilePath(null); Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }
    private sealed class FakeHttp : HttpMessageHandler
    {
        internal int Requests; internal HttpStatusCode Code = HttpStatusCode.OK; internal string? Body;
        internal TaskCompletionSource<bool>? Delay; internal Uri? LastUri; internal CancellationToken LastToken;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Require(request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.StartsWith("/api/0/buckets/"), "Unexpected request in isolated ActivityWatch transport.");
            Requests++; LastUri = request.RequestUri; LastToken = cancellationToken;
            var delay = Delay; var code = Code; string body = request.RequestUri!.AbsolutePath.EndsWith("/buckets/")
                ? "{\"window\":{\"id\":\"window\",\"type\":\"currentwindow\"}}"
                : Body ?? "[{\"timestamp\":\"" + DateTimeOffset.Now.ToString("o") + "\",\"duration\":60,\"data\":{\"app\":\"Editor.exe\",\"title\":\"Sample document\"}}]";
            if (delay != null) await delay.Task; // Ignore cancellation deliberately to exercise late responses.
            return new(code) { Content = new StringContent(body) };
        }
    }
    private static string? Heading(SettingsWindow w) => Visual<TextBlock>(w, "ActivityWatchExportHeading").Text;
    private static T Find<T>(SettingsWindow w, string name) => (T)typeof(SettingsWindow).GetField(name, Hidden)!.GetValue(w)!;
    private static T Visual<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().SingleOrDefault(c => c.Name == name) ?? throw new Exception("Missing " + name);
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Click(CheckBox check) => typeof(ToggleButton).GetMethod("OnClick", Hidden)!.Invoke(check, null);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Hidden)!.Invoke(target, null);
    private static void Pump(int ms = 30) => ClockOverlayChecks.Pump(ms);
    private static void Wait(Func<bool> ready) { for (int i = 0; i < 400 && !ready(); i++) Pump(25); Require(ready(), "Timed out waiting for ActivityWatch fixture."); Pump(); }
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
