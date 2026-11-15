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
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class SettingsActivityWatchNativeChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "design/one-piece-v2/settings/activitywatch-connection-implemented";
    internal static void Controls()
    {
        using var f = new Fixture(); var w = f.Window;
        Require(Visual<ScrollViewer>(w, "ActivityWatchScroll").IsVisible, "Missing production ActivityWatch page.");
        Require(f.Http.Requests == 0 && f.Launches == 0, "Opening Settings contacted or launched ActivityWatch.");
        var changes = new List<SettingsChangeKind>();
        typeof(SettingsWindow).GetEvent("SettingsChanged", Hidden)!.GetAddMethod(true)!.Invoke(w, [new Action<SettingsChangeKind>(changes.Add)]);
        Click(Find<CheckBox>(w, "ActivityWatchEnabledCheck")); Pump();
        Require(f.Model.ActivityWatchEnabled && changes.Count == 1 && changes[0] == SettingsChangeKind.ActivityWatch, "Enable lost its settings callback.");
        Require(Visual<TextBlock>(w, "ActivityWatchConnectionHeading").Text == "Not checked yet", "Enable falsely reported a successful connection.");
        Click(Visual<Button>(w, "ActivityWatchSyncTab")); Pump();
        Require(Find<TextBox>(w, "ActivityWatchPeriodicSyncIntervalTextBox").IsEffectivelyVisible && Find<Button>(w, "SyncActivityWatchNowButton").IsEffectivelyVisible,
            "Existing sync/export settings became inaccessible.");
        Find<TextBox>(w, "ActivityWatchPeriodicSyncIntervalTextBox").Text = "75"; Pump();
        Require(f.Model.ActivityWatchPeriodicSyncIntervalMinutes == 75, "Retained sync settings lost persistence.");
        Click(Visual<Button>(w, "ActivityWatchReviewTab")); Pump();
        int reviews = 0; w.PeriodicReviewRequested += () => reviews++;
        Click(Find<Button>(w, "ReviewNowButton")); Require(reviews == 1, "Review Now lost its existing action.");
        Click(Visual<Button>(w, "ActivityWatchConnectionTab")); Pump();
        int before = changes.Count;
        foreach (string theme in new[] { AppThemeCatalog.Midnight, AppThemeCatalog.Nika, AppThemeCatalog.Pirate, AppThemeCatalog.Nika })
        {
            f.Model.ThemeMode = theme; Desktop("ThemeResources", "ApplyApplication", theme); Call(w, "ReloadFromSettings"); Call(w, "UpdatePreviewSafely"); Pump(60);
            Require(Find<TextBox>(w, "ActivityWatchServerUrlTextBox").IsEffectivelyVisible, "Theme switch lost the server address.");
            if (theme == AppThemeCatalog.Midnight)
                Require(Find<CheckBox>(w, "ActivityWatchEnabledCheck").Content is string && Find<Button>(w, "TestActivityWatchButton").Classes.Contains("modern"), "Legacy styles were not restored.");
        }
        Require(changes.Count == before, "Theme restoration saved settings.");
        Click(Find<CheckBox>(w, "ActivityWatchEnabledCheck")); Pump();
        Require(changes.Count == before + 1, "Reparenting duplicated callbacks.");
    }

    internal static void Actions()
    {
        using var f = new Fixture(); f.RequireTransport(); var w = f.Window;
        var test = Find<Button>(w, "TestActivityWatchButton"); var start = Find<Button>(w, "StartActivityWatchAppButton");
        var address = Find<TextBox>(w, "ActivityWatchServerUrlTextBox");
        Click(test); Wait(() => test.IsEnabled);
        Require(f.Http.Requests == 1 && !f.Model.ActivityWatchEnabled && Heading(w) == "Server reachable", "Real client result was not shown while integration is off.");
        Require(Visual<TextBlock>(w, "ActivityWatchServerVersion").Text == "sample-0.13.2", "Server version is missing.");
        Save(w, "connected");
        Click(Find<CheckBox>(w, "ActivityWatchEnabledCheck")); Require(Heading(w) == "Server reachable", "Enablement erased a valid result.");
        foreach (string invalid in new[] { "not a valid address", "ftp://localhost:5600" })
        {
            address.Text = invalid; Pump(); Require(!test.IsEnabled && !start.IsEnabled, "Invalid URL enables an action.");
        }
        address.Text = "http://activitywatch.example:5600"; Pump(); int launches = f.Launches;
        Click(start); Require(test.IsEnabled && !start.IsEnabled && f.Launches == launches, "Remote address started a local process.");
        address.Text = ""; Pump(); Require(test.IsEnabled && start.IsEnabled, "Blank address lost its local default.");
        address.Text = "localhost:5600"; Pump(); Click(test); Wait(() => test.IsEnabled);
        Require(f.Http.LastUri!.ToString() == "http://localhost:5600/api/0/info", "Bare host was not normalized.");
        f.Http.Code = HttpStatusCode.BadGateway; Click(test); Wait(() => test.IsEnabled);
        Require(Heading(w) == "Couldn’t reach the server" && Visual<TextBlock>(w, "ActivityWatchConnectionDetail").Text!.Contains("502"), "Server failure was not explained."); Save(w, "unreachable");
        f.Found = false; int requests = f.Http.Requests; Click(start); Wait(() => start.IsEnabled);
        Require(Heading(w) == "Open ActivityWatch manually" && f.Http.Requests == requests, "Missing executable was reported as a running server."); Save(w, "app-not-found");
        f.Found = true; f.Http.Code = HttpStatusCode.OK; Click(start); Wait(() => start.IsEnabled);
        Require(Heading(w) == "Server reachable" && f.Launches == launches + 2, "Start did not launch once and verify the server.");
        f.Http.Body = "invalid json"; Click(test); Wait(() => test.IsEnabled);
        Require(Heading(w) == "Couldn’t reach the server", "Invalid server response reported success.");
        f.Http.Body = null; f.Http.Code = HttpStatusCode.ServiceUnavailable; requests = f.Http.Requests;
        Click(start); Wait(() => start.IsEnabled);
        Require(Heading(w) == "Server not ready yet" && f.Http.Requests == requests + 12,
            "Launched but unavailable server lost its bounded retries or distinct feedback.");
        Require(Visual<TextBlock>(w, "ActivityWatchConnectionDetail").Text!.Contains("503"),
            "Startup retries discarded the server's actual failure response."); Save(w, "server-not-ready");
    }

    internal static void Lifecycle()
    {
        using var f = new Fixture(); f.RequireTransport(); var w = f.Window;
        var test = Find<Button>(w, "TestActivityWatchButton"); var start = Find<Button>(w, "StartActivityWatchAppButton");
        var address = Find<TextBox>(w, "ActivityWatchServerUrlTextBox");
        var old = f.Http.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously); Click(test);
        Require(!test.IsEnabled && !start.IsEnabled && !address.IsEnabled, "Pending check did not disable conflicting controls.");
        int requests = f.Http.Requests; Click(test); Click(start);
        Require(f.Http.Requests == requests && f.Launches == 0, "Duplicate check or launch started while busy."); Save(w, "checking");
        var oldToken = f.Http.LastToken;
        address.Text = "http://127.0.0.1:5600"; Pump();
        Require(oldToken.IsCancellationRequested && Heading(w) == "Address changed", "Address edit did not invalidate/cancel the old check.");
        var newer = f.Http.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously); Click(test);
        old.SetResult(true); Pump(70); Require(!test.IsEnabled, "Old completion unlocked a newer pending check.");
        newer.SetResult(true); Wait(() => test.IsEnabled); Require(Heading(w) == "Server reachable", "New result did not arrive.");
        var hidden = f.Http.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously); Click(test); var hiddenToken = f.Http.LastToken;
        Click(Visual<Button>(w, "SettingsNav_Application")); Pump();
        Require(hiddenToken.IsCancellationRequested, "Hidden connection check was not canceled."); hidden.SetResult(true); Pump(70);
        Click(Visual<Button>(w, "SettingsNav_ActivityWatch")); Pump();
        Require(Heading(w) == "Check canceled" && test.IsEnabled && address.IsEnabled, "Hidden result overwrote cancellation or left controls disabled.");
        var tab = f.Http.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously); Click(start); Pump(550);
        var tabToken = f.Http.LastToken; Click(Visual<Button>(w, "ActivityWatchSyncTab")); Pump();
        Require(tabToken.IsCancellationRequested, "Leaving the connection tab did not cancel startup checks.");
        tab.SetResult(true); Pump(70); Click(Visual<Button>(w, "ActivityWatchConnectionTab")); Pump();
        Require(Heading(w) == "Check canceled" && start.IsEnabled, "Canceled startup overwrote tab feedback.");
        var themed = f.Http.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously); Click(test); var themeToken = f.Http.LastToken;
        f.Model.ThemeMode = AppThemeCatalog.Midnight; Desktop("ThemeResources", "ApplyApplication", f.Model.ThemeMode);
        Call(w, "ReloadFromSettings"); Call(w, "UpdatePreviewSafely"); Pump(60);
        Require(themeToken.IsCancellationRequested, "Theme replacement left a connection check running.");
        themed.SetResult(true); Pump(70);
        f.Model.ThemeMode = AppThemeCatalog.Nika; Desktop("ThemeResources", "ApplyApplication", f.Model.ThemeMode);
        Call(w, "ReloadFromSettings"); Call(w, "UpdatePreviewSafely"); Pump(60);
        Require(Heading(w) == "Check canceled" && test.IsEnabled, "Late result crossed theme restoration.");
        var closed = f.Http.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously); Click(test); var closeToken = f.Http.LastToken;
        var label = Visual<TextBlock>(w, "ActivityWatchConnectionHeading"); string? before = label.Text;
        w.Close(); closed.SetResult(true); Pump(70);
        Require(closeToken.IsCancellationRequested && label.Text == before, "Closing did not cancel safely or received late feedback.");
    }

    internal static void Layout()
    {
        foreach (int scale in new[] { 70, 90, 125 })
        foreach (bool compact in new[] { false, true })
        {
            using var f = new Fixture(scale, compact); f.RequireTransport(); var w = f.Window;
            var scroll = Visual<ScrollViewer>(w, "ActivityWatchScroll"); var done = Visual<Button>(w, "SettingsDone");
            CheckFit(); if (scale == 90 && !compact) Save(w, "setup");
            var test = Find<Button>(w, "TestActivityWatchButton"); Click(test); Wait(() => test.IsEnabled);
            if (scale == 90) Save(w, compact ? "compact-connection" : "connected");
            if (!compact && scale != 90) Save(w, "connected-" + scale);
            var footer = done.TranslatePoint(default, w); scroll.ScrollToEnd(); Pump(); CheckFit();
            Require(footer == done.TranslatePoint(default, w), "Done moved with the content.");
            if (scale == 90 && compact) Save(w, "compact-status");
            if (scale == 125 && compact)
            {
                f.Model.Typography.Global.SizePercent = 175; Desktop("DesktopTypography", "Apply", f.Model.Typography); Pump(70); CheckFit();
                var field = Find<TextBox>(w, "ActivityWatchServerUrlTextBox"); field.BringIntoView(); Pump();
                var text = field.GetVisualDescendants().OfType<TextPresenter>().Single(); var viewport = field.GetVisualDescendants().OfType<ScrollViewer>().Single();
                Require(text.TextLayout.Height <= viewport.Viewport.Height + 1, "Enlarged address clips text."); Save(w, "large-text");
            }
            void CheckFit()
            {
                Require(scroll.Viewport.Height > 65 && scroll.Extent.Width <= scroll.Viewport.Width + 1, "ActivityWatch content overflows horizontally.");
                var end = done.TranslatePoint(new Point(done.Bounds.Width, done.Bounds.Height), w)!.Value;
                Require(end.X <= w.ClientSize.Width + 1 && end.Y <= w.ClientSize.Height + 1, "Done is outside the window.");
            }
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly AppSettings Model = new() { ThemeMode = AppThemeCatalog.Nika };
        internal readonly FakeHttp Http = new(); internal readonly SettingsWindow Window;
        internal int Launches; internal bool Found = true; private readonly HttpClient _client; private readonly bool _injected;
        internal Fixture(int scale = 90, bool compact = false)
        {
            TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"StopwatchActivityWatch-{Guid.NewGuid():N}.json"));
            Model.UiScalePercent = scale; Desktop("ThemeResources", "ApplyApplication", Model.ThemeMode);
            Desktop("DesktopPageAppearance", "Initialize", Model); Desktop("DesktopTypography", "Initialize", Model.Typography);
            _client = new(Http);
            var ctor = typeof(SettingsWindow).GetConstructor(Hidden, null, [typeof(AppSettings), typeof(Func<ProjectHistoryView>), typeof(HttpClient), typeof(HttpClient), typeof(Func<bool>)], null);
            _injected = ctor != null;
            Window = ctor != null ? (SettingsWindow)ctor.Invoke([Model, null, null, _client, (Func<bool>)(() => { Launches++; return Found; })]) : new SettingsWindow(Model);
            Window.ShowActivated = false; Window.ShowInTaskbar = false; Window.Width = compact ? 840 : 1120; Window.Height = compact ? 600 : 720;
            Window.Show(); Pump(60); Click(Visual<Button>(Window, "SettingsNav_ActivityWatch")); Pump(60);
        }
        internal void RequireTransport() => Require(_injected, "ActivityWatch isolated transport/launcher constructor is missing.");
        public void Dispose() { Window.Close(); _client.Dispose(); TelegramOutboxStore.SetCustomFilePath(null); Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }
    private sealed class FakeHttp : HttpMessageHandler
    {
        internal int Requests; internal HttpStatusCode Code = HttpStatusCode.OK; internal string? Body;
        internal TaskCompletionSource<bool>? Delay; internal Uri? LastUri; internal CancellationToken LastToken;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Require(request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/api/0/info", "Unexpected request in isolated ActivityWatch transport.");
            Requests++; LastUri = request.RequestUri; LastToken = cancellationToken;
            var delay = Delay; var code = Code; string body = Body ?? "{\"version\":\"sample-0.13.2\"}";
            if (delay != null) await delay.Task; // Ignore cancellation deliberately to exercise late responses.
            return new(code) { Content = new StringContent(body) };
        }
    }
    private static string? Heading(SettingsWindow w) => Visual<TextBlock>(w, "ActivityWatchConnectionHeading").Text;
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
