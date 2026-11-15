using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
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

internal static class SettingsTelegramNativeChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "design/one-piece-v2/settings/telegram-implemented";
    private const string Token = "sample-preview-token";

    internal static void Controls()
    {
        using var f = new Fixture(); var w = f.Window;
        var token = Find<TextBox>(w, "TelegramBotTokenTextBox");
        Require(token.PasswordChar != '\0', "Telegram token must be masked by default.");
        Click(Visual<Button>(w, "TelegramRevealToken")); Require(token.PasswordChar == '\0', "Show did not reveal the token.");
        var changes = new List<SettingsChangeKind>(); Action<SettingsChangeKind> changed = changes.Add;
        typeof(SettingsWindow).GetEvent("SettingsChanged", Hidden)!.GetAddMethod(true)!.Invoke(w, [changed]);
        Find<TextBox>(w, "TelegramNotesTopicTextBox").Text = "42"; Pump();
        Require(f.Model.TelegramNotesTopicId == "42" && changes.Count == 1 && changes[0] == SettingsChangeKind.Telegram, "Topic edit lost its existing settings callback.");
        Find<TextBox>(w, "TelegramRemindersTopicTextBox").Text = ""; Pump();
        Click(Find<CheckBox>(w, "TelegramEnabledCheck"));
        Require(!f.Model.TelegramEnabled && !Find<Button>(w, "SendPendingNotesButton").IsEnabled, "Sync toggle did not reach the model.");
        int count = changes.Count;
        foreach (string theme in new[] { AppThemeCatalog.Midnight, AppThemeCatalog.Nika, AppThemeCatalog.Pirate, AppThemeCatalog.Nika })
        {
            f.Model.ThemeMode = theme; Desktop("ThemeResources", "ApplyApplication", theme);
            Call(w, "ReloadFromSettings"); Call(w, "UpdatePreviewSafely"); Pump(60);
            Require(token.IsEffectivelyVisible && token.PasswordChar != '\0', "Theme restoration lost or exposed the token editor.");
            if (theme != AppThemeCatalog.Nika) Require(Find<CheckBox>(w, "TelegramEnabledCheck").Content is string && Find<Button>(w, "TestTelegramButton").Classes.Contains("modern"), "Legacy Telegram styles were not restored.");
        }
        Require(changes.Count == count, "Theme switching saved settings.");
        Click(Find<CheckBox>(w, "TelegramEnabledCheck")); Require(changes.Count == count + 1, "Theme restoration duplicated callbacks.");
        Click(Visual<Button>(w, "TelegramRevealToken"));
        Click(Visual<Button>(w, "SettingsNav_Application")); Pump();
        Require(!token.IsEffectivelyVisible && token.PasswordChar != '\0', "Leaving Telegram did not hide and mask its token.");
        Click(Visual<Button>(w, "SettingsNav_Telegram")); Pump();
        Require(token.Focus() && Find<Button>(w, "TestTelegramButton").Focus(), "Telegram fields/actions cannot receive keyboard focus.");
        token.Text = " "; Pump();
        Require(!Find<Button>(w, "TestTelegramButton").IsEnabled && !Find<Button>(w, "SendPendingNotesButton").IsEnabled, "Empty credentials enable network actions.");
    }

    internal static void Actions()
    {
        using var f = new Fixture(); var w = f.Window; f.RequireTransport();
        var test = Find<Button>(w, "TestTelegramButton"); var send = Find<Button>(w, "SendPendingNotesButton");
        Click(test); Wait(() => test.IsEnabled);
        Require(f.Transport.Sends == 1 && f.Transport.LastBody!.Contains("\"message_thread_id\":12"), "Connection test did not use the Quick notes destination.");
        Require(Find<TextBlock>(w, "TelegramStatusText").Text!.Contains("sample_bot"), "Real service test result did not reach the page.");
        Save(w, "connected");
        TelegramOutboxStore.Enqueue(NoteType.Note, "Isolated pending note", DateTime.Now); Pump();
        f.Transport.Fail = true; Click(send); Wait(() => send.IsEnabled);
        Require(TelegramOutboxStore.PendingCount == 1 && Find<TextBlock>(w, "TelegramOutboxDetailText").Text!.Contains("unavailable"), "Failed delivery lost its queue entry or feedback.");
        Require(!Find<TextBlock>(w, "TelegramOutboxDetailText").Text!.Contains(Token), "Delivery feedback exposed the token.");
        Call(w, "UpdateTelegramOutboxUi"); Pump();
        Require(Find<TextBlock>(w, "TelegramOutboxDetailText").Text!.Contains("unavailable"), "Queue refresh erased failure feedback.");
        TelegramOutboxStore.Enqueue(NoteType.Note, "Another isolated pending note", DateTime.Now.AddSeconds(1)); Pump();
        Require(TelegramOutboxStore.PendingCount == 2 && Find<TextBlock>(w, "TelegramOutboxDetailText").Text!.Contains("unavailable") &&
            Find<TextBlock>(w, "TelegramOutboxStatusText").Foreground?.ToString() == "#ff913f32", "Queue growth erased an unresolved delivery failure.");
        Save(w, "pending");
        f.Transport.Fail = false; Click(send); Wait(() => TelegramOutboxStore.PendingCount == 0 && test.IsEnabled);
        Require(!send.IsEnabled && f.Transport.Sends == 4, "Successful retry did not empty the queue.");
        f.Transport.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Click(test); Require(!test.IsEnabled, "An in-flight test remained enabled.");
        int requests = f.Transport.Requests; Click(test);
        Require(f.Transport.Requests == requests, "Repeated click duplicated a pending test.");
        Find<TextBox>(w, "TelegramChatIdTextBox").Text = "-1000000000001"; Pump();
        f.Transport.Delay.SetResult(true); Wait(() => test.IsEnabled);
        Require(!Find<TextBlock>(w, "TelegramStatusText").Text!.Contains("sample_bot"), "Old connection result overwrote the changed destination.");
        f.Transport.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously); Click(test);
        string beforeClose = Find<TextBlock>(w, "TelegramStatusText").Text!;
        w.Close(); f.Transport.Delay.SetResult(true); Pump(100);
        Require(Find<TextBlock>(w, "TelegramStatusText").Text == beforeClose, "A closed Settings window received late feedback.");
    }

    internal static void Scan()
    {
        using var f = new Fixture(); var w = f.Window; f.RequireTransport();
        ObsidianNotesSync.AppendEntry(f.Model.ObsidianVaultFolder, NoteType.Note, "Isolated vault note", f.Model.NotesSubfolder, DateTime.Now);
        ConfirmationDialogWindow? dialog = null;
        using var opened = Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) => { if (window is ConfirmationDialogWindow c) dialog = c; });
        var scan = Find<Button>(w, "ScanVaultNotesButton");
        Click(scan); Wait(() => dialog?.IsVisible == true);
        Require(f.Transport.Sends == 0 && TelegramOutboxStore.PendingCount == 0, "Vault scan sent or queued notes before confirmation.");
        Click(Find<Button>(dialog!, "CancelButton")); Wait(() => scan.IsEnabled);
        Require(TelegramOutboxStore.PendingCount == 0 && f.Transport.Sends == 0, "Canceling scan changed delivery state.");
        dialog = null; Click(scan); Wait(() => dialog?.IsVisible == true);
        Click(Find<Button>(dialog!, "ConfirmButton"));
        Wait(() => f.Transport.Sends == 1 && dialog?.IsVisible == true);
        Click(Find<Button>(dialog!, "ConfirmButton")); Wait(() => scan.IsEnabled);
        Require(TelegramOutboxStore.PendingCount == 0 && TelegramNotesSync.GetUnsentVaultNotes(f.Model).Count == 0, "Confirmed scan did not retain the sent receipt.");
        ObsidianNotesSync.AppendEntry(f.Model.ObsidianVaultFolder, NoteType.Note, "Changed destination note", f.Model.NotesSubfolder, DateTime.Now.AddSeconds(1));
        dialog = null; Click(scan); Wait(() => dialog?.IsVisible == true);
        Find<TextBox>(w, "TelegramChatIdTextBox").Text = "-1000000000002"; Pump();
        Click(Find<Button>(dialog!, "ConfirmButton")); Pump(100);
        Require(f.Transport.Sends == 1 && TelegramOutboxStore.PendingCount == 0, "A stale scan confirmation sent notes after the destination changed.");
        dialog?.Close();
    }

    internal static void Layout()
    {
        foreach (int percent in new[] { 70, 90, 125 })
        foreach (bool compact in new[] { false, true })
        {
            using var f = new Fixture(percent, compact); var w = f.Window;
            var scroll = Visual<ScrollViewer>(w, "TelegramScroll"); var done = Visual<Button>(w, "SettingsDone");
            CheckFit(); if (percent == 90) Save(w, compact ? "compact-top" : "configured");
            TelegramOutboxStore.Enqueue(NoteType.Note, "Layout sample", DateTime.Now); Pump();
            var location = done.TranslatePoint(default, w); scroll.ScrollToEnd(); Pump();
            Require(done.TranslatePoint(default, w) == location, "Done moved with the page scroll.");
            if (compact && percent == 90) Save(w, "compact-delivery");
            if (compact && percent == 125)
            {
                f.Model.Typography.Global.SizePercent = 175; Desktop("DesktopTypography", "Apply", f.Model.Typography); Pump(80); CheckFit();
                foreach (string name in new[] { "TelegramBotTokenTextBox", "TelegramChatIdTextBox", "TelegramNotesTopicTextBox", "TelegramTodosTopicTextBox", "TelegramRemindersTopicTextBox" })
                {
                    var field = Find<TextBox>(w, name);
                    var text = field.GetVisualDescendants().OfType<TextPresenter>().Single();
                    var viewport = field.GetVisualDescendants().OfType<ScrollViewer>().Single();
                    Require(text.TextLayout.Height <= viewport.Viewport.Height + 1, name + " clips large text.");
                }
                foreach (var button in new[] { Visual<Button>(w, "TelegramRevealToken"), Find<Button>(w, "TestTelegramButton"), Find<Button>(w, "SendPendingNotesButton"), Find<Button>(w, "ScanVaultNotesButton") })
                {
                    var text = button.GetVisualDescendants().OfType<TextBlock>().Single();
                    var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().Single();
                    Require(text.TextLayout.WidthIncludingTrailingWhitespace <= presenter.Bounds.Width + 1, "Telegram action clips its caption.");
                }
                Find<TextBox>(w, "TelegramChatIdTextBox").BringIntoView(); Pump(); Save(w, "large-fields");
            }
            void CheckFit()
            {
                Require(scroll.Viewport.Height > 65 && scroll.Extent.Width <= scroll.Viewport.Width + 1, "Telegram content overflows.");
                var end = done.TranslatePoint(new Point(done.Bounds.Width, done.Bounds.Height), w)!.Value;
                Require(end.X <= w.ClientSize.Width + 1 && end.Y <= w.ClientSize.Height + 1, "Done is outside the window.");
            }
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly AppSettings Model;
        internal readonly FakeTelegram Transport = new();
        internal readonly HttpClient Client;
        internal readonly SettingsWindow Window;
        private readonly bool _hasTransport;
        internal Fixture(int percent = 90, bool compact = false)
        {
            string root = Path.GetFullPath(Path.Combine(".superpowers/sdd/2026-11-14-settings-telegram", "fixture-" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(root); TelegramOutboxStore.SetCustomFilePath(Path.Combine(root, "outbox.json"));
            Model = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = percent, TelegramEnabled = true,
                TelegramBotToken = Token, TelegramChatId = "-1000000000000", TelegramNotesTopicId = "12", TelegramTodosTopicId = "24", ObsidianVaultFolder = Path.Combine(root, "vault") };
            Directory.CreateDirectory(Model.ObsidianVaultFolder);
            Desktop("ThemeResources", "ApplyApplication", Model.ThemeMode); Desktop("DesktopPageAppearance", "Initialize", Model); Desktop("DesktopTypography", "Initialize", Model.Typography);
            Client = new HttpClient(Transport);
            var constructor = typeof(SettingsWindow).GetConstructor([typeof(AppSettings), typeof(Func<ProjectHistoryView>), typeof(HttpClient)]);
            _hasTransport = constructor != null;
            Window = constructor == null ? new SettingsWindow(Model) : (SettingsWindow)constructor.Invoke([Model, null, Client]);
            Window.ShowActivated = false; Window.ShowInTaskbar = false; Window.Width = compact ? 840 : 1120; Window.Height = compact ? 600 : 720;
            Window.Show(); Pump(); Click(Visual<Button>(Window, "SettingsNav_Telegram")); Pump(50);
        }
        internal void RequireTransport() => Require(_hasTransport, "Telegram actions require isolated HTTP transport before they can be tested.");
        public void Dispose()
        {
            Window.Close(); Client.Dispose(); TelegramOutboxStore.SetCustomFilePath(null);
            Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown");
        }
    }

    private sealed class FakeTelegram : HttpMessageHandler
    {
        internal int Requests, Sends;
        internal bool Fail;
        internal string? LastBody;
        internal TaskCompletionSource<bool>? Delay;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            if (Delay is { } delay) await delay.Task;
            string method = request.RequestUri!.Segments.Last();
            Require(method is "getMe" or "sendMessage", "Unexpected Telegram API method in isolated test.");
            if (method == "sendMessage") { Sends++; LastBody = await request.Content!.ReadAsStringAsync(cancellationToken); }
            string json = Fail ? JsonSerializer.Serialize(new { ok = false, description = "Telegram unavailable for " + Token }) : method == "getMe"
                ? "{\"ok\":true,\"result\":{\"username\":\"sample_bot\"}}" : "{\"ok\":true,\"result\":{\"message_id\":10,\"chat\":{\"id\":-1000000000000}}}";
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) };
        }
    }

    private static void Save(SettingsWindow w, string name)
    {
        Directory.CreateDirectory(Output); var frame = w.GetVisualDescendants().OfType<StopwatchFrameSurface>().First();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width), (int)Math.Ceiling(w.ClientSize.Height)), new Vector(96, 96));
        if (((LayoutTransformControl)w.Content!).LayoutTransform!.Value.M11 >= 1) bitmap.Render(w);
        else { using var context = bitmap.CreateDrawingContext(); context.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }
    private static T Find<T>(Window w, string name) where T : Control => w.FindControl<T>(name)!;
    private static T Visual<T>(Window w, string name) where T : Control => w.GetVisualDescendants().OfType<T>().SingleOrDefault(c => c.Name == name) ?? throw new InvalidOperationException("Missing " + name);
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Click(CheckBox check) => typeof(ToggleButton).GetMethod("OnClick", Hidden)!.Invoke(check, null);
    private static void Pump(int ms = 30) => ClockOverlayChecks.Pump(ms);
    private static void Wait(Func<bool> ready) { for (int i = 0; i < 100 && !ready(); i++) Pump(20); Require(ready(), "Timed out waiting for the isolated operation."); Pump(); }
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Hidden)!.Invoke(target, null);
    private static void Desktop(string type, string method, params object[] args) => typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
