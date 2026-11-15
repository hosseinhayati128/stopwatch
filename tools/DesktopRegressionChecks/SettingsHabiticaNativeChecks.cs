using System.Net;
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
using StopwatchOverlay.Habitica;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static partial class SettingsHabiticaNativeChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    private const string Output = "design/one-piece-v2/settings/habitica-connection-implemented";
    private const string Account = "11111111-2222-4333-8444-555555555555", Token = "sample-habitica-token";

    internal static void Controls()
    {
        using var f = new Fixture(); var panel = f.Panel; var token = Field<TextBox>(panel, "_token");
        var reveal = Visual<Button>(f.Window, "HabiticaRevealToken");
        Require(Visual<TextBlock>(f.Window, "SettingsSaveStatus").Text!.Contains("Save"), "Habitica footer incorrectly claims connection drafts apply immediately.");
        Require(token.PasswordChar != '\0' && !reveal.IsEnabled && f.Secrets.Reads == 0, "Rendering retrieved or exposed a stored token.");
        var account = Field<TextBox>(panel, "_account"); var client = Field<TextBox>(panel, "_client");
        account.Text = Account; client.Text = Account + "-StopwatchOverlay"; token.Text = "  " + Token + "  "; Pump();
        Require(f.Saves == 0, "Editing saved connection drafts immediately.");
        Click(reveal); Require(token.PasswordChar == '\0', "Show did not reveal the entered draft.");
        Click(Field<Button>(panel, "_saveConnection")); Pump();
        Require(f.Saves == 1 && f.Model.HabiticaUserId == Account && token.Text == "" && token.PasswordChar != '\0' && f.Secrets.Value == Token, "Save did not persist the connection and clear/mask its token draft.");
        Click(Field<Button>(panel, "_saveConnection")); Pump();
        Require(f.Saves == 2 && f.Secrets.Writes == 1 && f.Secrets.Value == Token, "Blank Save replaced the stored token.");
        Click(Field<Button>(panel, "_pasteToken")); Pump();
        Require(token.Text == Token && f.ClipboardReads == 1, "Paste lost the existing injected clipboard path.");
        Click(reveal); Click(Visual<Button>(f.Window, "SettingsNav_Application")); Pump();
        Require(token.PasswordChar != '\0' && !Field<Avalonia.Threading.DispatcherTimer>(panel, "_refresh").IsEnabled, "Hidden Habitica page exposes a draft or keeps polling.");
        Click(Visual<Button>(f.Window, "SettingsNav_Habitica")); Pump();
        foreach (string theme in new[] { AppThemeCatalog.Midnight, AppThemeCatalog.Nika, AppThemeCatalog.Pirate, AppThemeCatalog.Nika })
        {
            f.Model.ThemeMode = theme; Desktop("ThemeResources", "ApplyApplication", theme); Call(f.Window, "ReloadFromSettings"); Call(f.Window, "UpdatePreviewSafely"); Pump(50);
            Require(account.IsEffectivelyVisible && token.PasswordChar != '\0', "Theme restoration hid fields or exposed the token.");
            if (theme != AppThemeCatalog.Nika) Require(Field<Button>(panel, "_test").Classes.Contains("modern"), "Legacy button styling was not restored.");
        }
        Require(f.Saves == 2, "Theme restoration saved drafts.");
        Require(account.Focus() && Field<Button>(panel, "_test").Focus(), "Habitica fields/actions lost keyboard focus.");
        account.Text = "invalid"; Pump();
        Require(!Field<Button>(panel, "_saveConnection").IsEnabled && !Field<Button>(panel, "_test").IsEnabled, "Invalid account enables Save/Test.");
        Require(Field<TextBlock>(panel, "_feedback").Text!.Contains(HabiticaApiClient.ConfigurationError("invalid", Account + "-StopwatchOverlay")!), "Invalid drafts do not explain why connection actions are disabled.");
    }

    internal static void Actions()
    {
        using var f = new Fixture(); f.Save();
        var test = Field<Button>(f.Panel, "_test");
        Click(test); Wait(() => !Field<bool>(f.Panel, "_testing"));
        Require(f.Http.Requests == 1 && Text(f.Panel).Contains("succeeded", StringComparison.OrdinalIgnoreCase), "Connection requests=" + f.Http.Requests + "; feedback=" + Field<TextBlock>(f.Panel, "_feedback").Text);
        Require(Field<Button>(f.Panel, "_saveConnection").IsEnabled, "Connection test left connection actions disabled.");
        Click(Field<CheckBox>(f.Panel, "_enabled")); Pump();
        Require(f.Model.HabiticaEnabled && f.Integration.GetStatus(f.Model).IsOwner, "Sync toggle lost local vault ownership behavior.");
        Save(f.Window, "connected");
        f.Now += TimeSpan.FromMinutes(2); f.Http.Code = HttpStatusCode.Unauthorized;
        Click(test); Wait(() => !Field<bool>(f.Panel, "_testing"));
        Require(Text(f.Panel).Contains("rejected") && f.Secrets.Value == Token, "Failed test lost credentials or accurate feedback.");
        Save(f.Window, "connection-error");
        f.Now += TimeSpan.FromMinutes(2); f.Http.Code = HttpStatusCode.OK; f.Http.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Click(test); Wait(() => f.Http.Requests == 3); Click(test);
        Require(f.Http.Requests == 3, "Duplicate test issued a second request.");
        Field<TextBox>(f.Panel, "_account").Text = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee"; Pump();
        string changed = Field<TextBlock>(f.Panel, "_feedback").Text!; f.Http.Delay.SetResult(true); Wait(() => !Field<bool>(f.Panel, "_testing"));
        Require(Field<TextBlock>(f.Panel, "_feedback").Text == changed && !Field<TextBlock>(f.Panel, "_navigatorFeedbackTitle").Text!.Contains("succeeded", StringComparison.OrdinalIgnoreCase), "An old test overwrote edited account feedback.");
        f.Http.Delay = null; f.Secrets.RejectWrite = true; Field<TextBox>(f.Panel, "_token").Text = "replacement"; Pump();
        Click(Field<Button>(f.Panel, "_saveConnection")); Pump();
        Require(f.Model.HabiticaUserId == Account, "Failed token storage partially committed a different account.");
    }

    internal static void StatusAndRecovery()
    {
        using var f = new Fixture(sessionOnly: true); f.Save();
        Require(Text(f.Panel).Contains("session", StringComparison.OrdinalIgnoreCase), "Session-only storage is not identified.");
        Save(f.Window, "session-only");
        Click(Field<CheckBox>(f.Panel, "_enabled")); Pump();
        AddRecoveryItems(f, 3); OpenRecovery(f);
        Require(Field<Button>(f.Panel, "_syncExisting").IsEffectivelyVisible && Visual<Button>(f.Window, "TransferHabiticaVaultButton").IsVisible, "Existing recovery controls disappeared.");
        Click(Field<Button>(f.Panel, "_syncExisting")); Pump();
        Require(Field<StackPanel>(f.Panel, "_confirmPanel").IsEffectivelyVisible && f.Http.Requests == 0, "Confirmation missing: " + Field<TextBlock>(f.Panel, "_feedback").Text);
        Call(f.Panel, "ClearConfirmation"); Click(Visual<Button>(f.Window, "HabiticaConnectionTab"));
        f.Now = DateTimeOffset.UtcNow; f.Http.Code = HttpStatusCode.TooManyRequests;
        Click(Field<Button>(f.Panel, "_test")); Wait(() => !Field<bool>(f.Panel, "_testing"));
        Require(!Field<Button>(f.Panel, "_test").IsEnabled && Text(f.Panel).Contains("wait", StringComparison.OrdinalIgnoreCase), "Rate-limit feedback or saved deadline is missing.");
        Pump(100);
        var feedback = Field<TextBlock>(f.Panel, "_feedback");
        Require(feedback.TextLayout.TextLines.Sum(line => line.Length) >= feedback.Text!.Length,
            $"Retry feedback omits text: {feedback.TextLayout.TextLines.Sum(line => line.Length)}/{feedback.Text.Length}; bounds {feedback.Bounds}; desired {feedback.DesiredSize}; layout {feedback.TextLayout.MaxWidth}/{feedback.TextLayout.MaxHeight}; lines {feedback.MaxLines}.");
        Require(feedback.TextLayout.Height <= feedback.Bounds.Height + 1, $"Retry feedback clips: text {feedback.TextLayout.Height}, bounds {feedback.Bounds.Height}.");
        var cards = Field<Grid>(f.Panel, "_navigatorCards");
        Require(cards.Children.All(c => c.Bounds.Bottom <= cards.Bounds.Height + 1), "Growing feedback leaves its connection cards.");
        Save(f.Window, "waiting");
    }

    internal static void ClosedResult()
    {
        using var f = new Fixture(); f.Save(); f.Http.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Click(Field<Button>(f.Panel, "_test")); Wait(() => f.Http.Requests == 1);
        f.Window.Close(); string before = Field<TextBlock>(f.Panel, "_feedback").Text!;
        f.Http.Delay.SetResult(true); Pump(100);
        Require(Field<TextBlock>(f.Panel, "_feedback").Text == before, "Closed panel received stale test feedback.");
    }

    internal static void RecoveryExamples()
    {
        const string output = "design/one-piece-v2/settings/habitica-recovery-examples";
        using var f = new Fixture(); f.Save();
        var time = new DateTime(2026, 11, 15, 9, 0, 0);
        foreach (var (type, title) in new[] { (NoteType.Todo, "Read chapter 2"), (NoteType.Todo, "Practice drawing"), (NoteType.Reminder, "Review weekly plan") })
            ObsidianNotesSync.AppendEntry(f.Model.ObsidianVaultFolder, type, title, f.Model.NotesSubfolder, time = time.AddMinutes(1));
        Click(Field<CheckBox>(f.Panel, "_enabled")); Pump();
        Require(f.Integration.CountExisting(f.Model) == 3, "Recovery example must contain exactly three sample items.");
        OpenRecovery(f); var recovery = Field<StackPanel>(f.Panel, "_recoveryRoot");
        Capture("expanded-structure");
        Click(Field<Button>(f.Panel, "_syncExisting")); Pump(400);
        Require(Field<StackPanel>(f.Panel, "_confirmPanel").IsEffectivelyVisible &&
            Field<TextBlock>(f.Panel, "_confirmation").Text!.Contains("Sync 3 existing"), "Missing three-item confirmation example.");
        Capture("sync-confirmation");
        Click(Visual<Button>(f.Window, "ConfirmHabiticaActionButton")); Wait(() => !Field<bool>(f.Panel, "_queueing"));
        var progress = f.Integration.GetStatus(f.Model).ImportProgress;
        Require(progress is { Total: 3, Completed: 0, Queued: 3 }, "Sample items were not queued locally.");
        Capture("queued-progress");
        Require(f.Http.Requests == 0, "Recovery examples must not issue network requests.");
        void Capture(string name)
        {
            Pump(500); Visual<ScrollViewer>(f.Window, "HabiticaScroll").ScrollToEnd(); Pump(100);
            Require(recovery.IsEffectivelyVisible, "Recovery example is not visible.");
            Save(f.Window, name, output);
        }
    }

    internal static void AccountStorage()
    {
        using var f = new Fixture(sessionOnly: true); f.Save();
        f.Secrets.SessionOnly = false;
        const string second = "aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee";
        Field<TextBox>(f.Panel, "_account").Text = second;
        Field<TextBox>(f.Panel, "_client").Text = second + "-StopwatchOverlay";
        Field<TextBox>(f.Panel, "_token").Text = "second-sample-token"; Pump();
        Click(Field<Button>(f.Panel, "_saveConnection")); Pump();
        Field<TextBox>(f.Panel, "_account").Text = Account;
        Field<TextBox>(f.Panel, "_client").Text = Account + "-StopwatchOverlay"; Pump();
        Click(Field<Button>(f.Panel, "_test")); Wait(() => !Field<bool>(f.Panel, "_testing"));
        Require(Field<TextBlock>(f.Panel, "_navigatorCredentialTitle").Text != "Token saved securely" &&
            !Field<TextBlock>(f.Panel, "_navigatorCredentialDetail").Text!.Contains("Windows"), "A cached session-only token inherited another account's protected-storage status.");
        f.Secrets.Delete(Account); f.Now += TimeSpan.FromMinutes(2);
        Click(Field<Button>(f.Panel, "_test")); Wait(() => !Field<bool>(f.Panel, "_testing"));
        Require(Field<TextBlock>(f.Panel, "_navigatorCredentialTitle").Text == "No token available" && f.Http.Requests == 1, "Missing-token feedback is inaccurate or issued an HTTP request.");
    }

    internal static void CanceledResult()
    {
        using var f = new Fixture(); f.Save(); f.Http.Delay = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Click(Field<Button>(f.Panel, "_test")); Wait(() => f.Http.Requests == 1);
        Click(Visual<Button>(f.Window, "SettingsNav_Application")); Pump();
        Click(Visual<Button>(f.Window, "SettingsNav_Habitica")); Pump();
        f.Http.Delay.SetResult(true); Wait(() => !Field<bool>(f.Panel, "_testing"));
        Require(Field<TextBlock>(f.Panel, "_navigatorFeedbackTitle").Text != "Testing connection" &&
            !Field<TextBlock>(f.Panel, "_feedback").Text!.StartsWith("Checking"), "Canceled test retained running feedback after returning to the page.");
    }

    internal static void PauseWithInvalidDraft()
    {
        using var f = new Fixture(); f.Save(); var enabled = Field<CheckBox>(f.Panel, "_enabled");
        Click(enabled); Pump(); Field<TextBox>(f.Panel, "_account").Text = "invalid"; Pump();
        Require(enabled.IsEnabled, "Invalid connection drafts prevent pausing the committed connection.");
        Click(enabled); Pump();
        Require(!f.Model.HabiticaEnabled && f.Model.HabiticaUserId == Account, "Pause saved the invalid draft or left committed sync enabled.");
    }

    internal static void Layout()
    {
        foreach (int percent in new[] { 70, 90, 125 }) foreach (bool compact in new[] { false, true })
        {
            using var f = new Fixture(percent, compact); f.Save();
            var scroll = Visual<ScrollViewer>(f.Window, "HabiticaScroll"); var done = Visual<Button>(f.Window, "SettingsDone");
            CheckFit(); if (percent == 90) Save(f.Window, compact ? "compact-connection" : "configured");
            var position = done.TranslatePoint(default, f.Window); scroll.ScrollToEnd(); Pump();
            Require(done.TranslatePoint(default, f.Window) == position, "Done moved with Habitica scroll.");
            if (compact && percent == 90) Save(f.Window, "compact-status");
            if (compact && percent == 125)
            {
                f.Model.Typography.Global.SizePercent = 175; Desktop("DesktopTypography", "Apply", f.Model.Typography); Pump(80); CheckFit();
                foreach (string name in new[] { "_account", "_client", "_token" })
                {
                    var field = Field<TextBox>(f.Panel, name); var text = field.GetVisualDescendants().OfType<TextPresenter>().Single();
                    var viewport = field.GetVisualDescendants().OfType<ScrollViewer>().Single();
                    Require(text.TextLayout.Height <= viewport.Viewport.Height + 1, "Habitica editor clips enlarged text.");
                }
                foreach (var button in new[] { Field<Button>(f.Panel, "_saveConnection"), Field<Button>(f.Panel, "_test"), Field<Button>(f.Panel, "_pasteToken"), Visual<Button>(f.Window, "HabiticaRevealToken") })
                {
                    var text = button.GetVisualDescendants().OfType<TextBlock>().Single();
                    var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().Single();
                    Require(text.TextLayout.WidthIncludingTrailingWhitespace <= presenter.Bounds.Width + 1, "Habitica action clips its enlarged caption.");
                }
                Field<TextBox>(f.Panel, "_token").BringIntoView(); Pump(); Save(f.Window, "large-fields");
            }
            void CheckFit()
            {
                var cards = Field<Grid>(f.Panel, "_navigatorCards");
                var first = cards.Children[0].Bounds; var second = cards.Children[1].Bounds;
                Require(first.Right <= second.X + 1 || first.Bottom <= second.Y + 1, "Habitica connection and status cards overlap.");
                Require(scroll.Viewport.Height > 65 && scroll.Extent.Width <= scroll.Viewport.Width + 1, "Habitica page overflows.");
                var end = done.TranslatePoint(new Point(done.Bounds.Width, done.Bounds.Height), f.Window)!.Value;
                Require(end.X <= f.Window.ClientSize.Width + 1 && end.Y <= f.Window.ClientSize.Height + 1, "Done left the window.");
            }
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly AppSettings Model = new() { ThemeMode = AppThemeCatalog.Nika };
        internal readonly MemorySecrets Secrets;
        internal readonly FakeHttp Http = new();
        internal readonly HabiticaIntegration Integration;
        internal readonly HabiticaSettingsPanel Panel;
        internal readonly SettingsWindow Window;
        internal DateTimeOffset Now = DateTimeOffset.UtcNow.AddHours(-1);
        internal int Saves, ClipboardReads;
        private readonly HttpClient _client;
        internal Fixture(int percent = 90, bool compact = false, bool sessionOnly = false)
        {
            string root = Path.Combine(Path.GetTempPath(), "StopwatchHabiticaNative-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root); TelegramOutboxStore.SetCustomFilePath(Path.Combine(root, "outbox.json"));
            Model.UiScalePercent = percent; Desktop("ThemeResources", "ApplyApplication", Model.ThemeMode);
            Desktop("DesktopPageAppearance", "Initialize", Model); Desktop("DesktopTypography", "Initialize", Model.Typography);
            Window = new SettingsWindow(Model) { ShowActivated = false, ShowInTaskbar = false, Width = compact ? 840 : 1120, Height = compact ? 600 : 720 };
            Secrets = new(sessionOnly); _client = new(Http);
            Integration = new(new HabiticaApiClient(_client), Secrets, Path.Combine(root, "state.json"), () => Now);
            Panel = new(() => { ClipboardReads++; return Task.FromResult<string?>(" \n" + Token + " \n"); }, Integration) { Name = "HabiticaPanel", IsVisible = false };
            var original = Field<HabiticaSettingsPanel>(Window, "HabiticaPanel"); var parent = (Panel)original.Parent!;
            int index = parent.Children.IndexOf(original); parent.Children.Remove(original); parent.Children.Insert(index, Panel);
            typeof(SettingsWindow).GetField("HabiticaPanel", Hidden)!.SetValue(Window, Panel);
            Model.ObsidianVaultFolder = Path.Combine(root, "vault"); Directory.CreateDirectory(Model.ObsidianVaultFolder);
            Panel.Bind(Model, () => Saves++);
            Call(Window, "ReloadFromSettings");
            Window.Show(); Pump(); Click(Visual<Button>(Window, "SettingsNav_Habitica")); Pump(50);
        }
        internal void Save()
        {
            Require(Directory.Exists(Model.ObsidianVaultFolder), "Fixture vault is unavailable: " + Model.ObsidianVaultFolder);
            Field<TextBox>(Panel, "_account").Text = Account; Field<TextBox>(Panel, "_client").Text = Account + "-StopwatchOverlay";
            Field<TextBox>(Panel, "_token").Text = Token; Pump();
            Click(Field<Button>(Panel, "_saveConnection")); Pump();
        }
        public void Dispose()
        { Window.Close(); _client.Dispose(); TelegramOutboxStore.SetCustomFilePath(null); Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }
    private sealed class MemorySecrets(bool sessionOnly) : IHabiticaSecretStore
    {
        internal int Reads, Writes; internal string Account = ""; internal bool RejectWrite, SessionOnly = sessionOnly;
        private readonly Dictionary<string, string> _tokens = new();
        internal string? Value => _tokens.GetValueOrDefault(Account);
        public string Status { get; private set; } = "Token not saved.";
        public string? Read(string account) { Reads++; return _tokens.GetValueOrDefault(account); }
        public bool Write(string account, string token)
        {
            if (RejectWrite) return false;
            Writes++; Account = account; _tokens[account] = token;
            Status = SessionOnly ? "Protected storage unavailable. Token is session-only; enter it again after restarting." : "Windows account-protected token storage."; return true;
        }
        public void Delete(string account) => _tokens.Remove(account);
    }
    private sealed class FakeHttp : HttpMessageHandler
    {
        internal int Requests; internal HttpStatusCode Code = HttpStatusCode.OK; internal TaskCompletionSource<bool>? Delay;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++; Require(request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath == "/api/v3/user", "Unexpected live mutation path in fake transport.");
            if (Delay != null) await Delay.Task;
            var response = new HttpResponseMessage(Code) { Content = new StringContent("{\"success\":true,\"data\":{\"profile\":{\"name\":\"Sample account\"}}}") };
            if (Code == HttpStatusCode.TooManyRequests) response.Headers.TryAddWithoutValidation("Retry-After", "45");
            return response;
        }
    }
    private static string Text(Control control) => string.Join("\n", control.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text));
    private static T Field<T>(object o, string name)
    {
        var field = o.GetType().GetField(name, Hidden);
        if (field != null) return (T)field.GetValue(o)!;
        throw new InvalidOperationException("Missing " + name);
    }
    private static T Visual<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().SingleOrDefault(c => c.Name == name) ?? throw new InvalidOperationException("Missing " + name);
    private static void Click(Button b) => b.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Click(CheckBox b) => typeof(ToggleButton).GetMethod("OnClick", Hidden)!.Invoke(b, null);
    private static void Call(object o, string method) => o.GetType().GetMethod(method, Hidden)!.Invoke(o, null);
    private static void Pump(int ms = 30) => ClockOverlayChecks.Pump(ms);
    private static void Wait(Func<bool> ready) { for (int i = 0; i < 150 && !ready(); i++) Pump(20); Require(ready(), "Timed out waiting for Habitica fixture."); Pump(); }
    private static void Desktop(string type, string method, params object[] args) => typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    private static void Require(bool value, string text) { if (!value) throw new InvalidOperationException(text); }
    private static void Save(SettingsWindow window, string name, string output = Output)
    {
        Directory.CreateDirectory(output); var frame = window.GetVisualDescendants().OfType<StopwatchFrameSurface>().First();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
        if (((LayoutTransformControl)window.Content!).LayoutTransform!.Value.M11 >= 1) bitmap.Render(window);
        else { using var context = bitmap.CreateDrawingContext(); context.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
        bitmap.Save(Path.Combine(output, name + ".png"));
    }
}
