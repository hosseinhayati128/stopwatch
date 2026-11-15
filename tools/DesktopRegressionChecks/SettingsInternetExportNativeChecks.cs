using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using StopwatchOverlay.Desktop.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Internet;
using StopwatchOverlay.Desktop.Views;

internal static class SettingsInternetExportNativeChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    internal static void PageAndActions()
    {
        Require(Application.Current is not StopwatchOverlay.Desktop.App, "Fixture must not load the live App.");
        var root = Path.Combine(Path.GetTempPath(), "StopwatchInternetNative-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); InternetHistoryStore.SetCustomFilePath(Path.Combine(root, "history.json"));
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(root, "outbox.json"));
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = 90, InternetMonitorEnabled = false,
            ObsidianVaultFolder = root, InternetLogFileName = "Network journal.md" };
        Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
        Desktop("DesktopPageAppearance", "Initialize", settings); Desktop("DesktopTypography", "Initialize", settings.Typography);
        var w = new SettingsWindow(settings) { ShowActivated = false, ShowInTaskbar = false, Width = 1120, Height = 720 };
        try
        {
            InternetHistoryStore.Add(new(new DateTime(2026, 11, 16, 14, 32, 0), InternetStatus.Online, 24, 38.6,
                new(true, "Wi-Fi", "Sample", 88, "Sample", "Sample"), "sample"));
            w.Show(); Pump(); Click(Visual<Button>(w, "SettingsNav_Internet")); Pump(); Click(Visual<Button>(w, "InternetHistoryTab")); Pump();
            Require(w.GetVisualDescendants().OfType<TextBlock>().Any(c => c.Name == "InternetHistoryCount"), "Production history summary is missing.");
            Wait(() => Visual<TextBlock>(w, "InternetHistoryCount").Text == "1 saved check");
            var sync = Find<Button>(w, "SyncInternetLogButton"); var clear = Find<Button>(w, "ClearInternetLogButton");
            Click(sync); Wait(() => sync.IsEnabled);
            Require(File.ReadAllText(Path.Combine(root, "Network journal.md")).Contains("14:32"), "Real isolated export failed.");
            Click(clear); Pump(); Require(Visual<TextBlock>(w, "InternetClearDetail").Text!.Contains("Network journal.md"), "Confirmation names the wrong target.");
            Require(InternetHistoryStore.GetAll().Count == 1, "Opening confirmation deleted records.");
            Click(Visual<Button>(w, "InternetClearCancel")); Require(InternetHistoryStore.GetAll().Count == 1, "Cancel deleted records.");
            Click(clear); Click(Visual<Button>(w, "InternetClearConfirm"));
            Wait(() => Visual<TextBlock>(w, "InternetHistoryCount").Text == "No saved checks");
            Require(InternetHistoryStore.GetAll().Count == 0 && !File.ReadAllText(Path.Combine(root, "Network journal.md")).Contains("14:32"), "Clear restored old Markdown history.");
            Require(!settings.InternetMonitorEnabled && settings.InternetMonitorIntervalMinutes == 10, "Export changed monitoring settings.");
        }
        finally { w.Close(); InternetHistoryStore.SetCustomFilePath(null); TelegramOutboxStore.SetCustomFilePath(null);
            Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); Directory.Delete(root, true); }
    }
    internal static void StatesAndLifecycle()
    {
        using var f = new Fixture(); var w = f.Window;
        var sync = Find<Button>(w, "SyncInternetLogButton"); var clear = Find<Button>(w, "ClearInternetLogButton");
        var filename = Find<TextBox>(w, "InternetFileNameTextBox"); var retention = Find<ComboBox>(w, "InternetRetentionCombo");
        filename.Focus(); filename.Text = "Draft"; Pump(); Require(f.Model.InternetLogFileName == "Internet Log.md", "Draft filename was persisted while typing.");
        filename.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter }); Pump();
        Require(f.Model.InternetLogFileName == "Draft", "Enter did not commit a valid filename.");
        foreach (string invalid in new[] { "../elsewhere", "CON.md", "report.", "bad:name" })
        { filename.Text = invalid; Pump(); sync.Focus(); Require(!sync.IsEnabled && !clear.IsEnabled && f.Model.InternetLogFileName == "Draft", "Invalid draft was saved or enabled writes."); }
        Save(w, "invalid-file"); filename.Text = "Internet Log.md"; filename.Focus(); sync.Focus(); Pump();
        retention.SelectedIndex = 3; Pump(); Require(f.Model.InternetLogRetentionDays == 7 && f.History.Count == 168, "Retention did not persist or deleted on selection."); Save(w, "retention"); retention.SelectedIndex = 0;
        Click(clear); Pump(); Require(Visual<Button>(w, "InternetClearCancel").IsFocused && !sync.IsEnabled, "Cancel is not focused or Sync remains enabled during confirmation."); Save(w, "clear-confirmation");
        Visual<Button>(w, "InternetClearCancel").RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Escape }); Pump();
        Require(sync.IsEnabled && f.ClearCalls == 0, "Escape failed to dismiss without deleting.");
        f.Model.ObsidianVaultFolder = ""; Call(w, "RefreshInternetExport");
        Require(!sync.IsEnabled && clear.IsEnabled, "Missing folder should allow only local clear."); Save(w, "missing-folder");
        Click(clear); Pump(); Require(Visual<TextBlock>(w, "InternetClearDetail").Text!.Contains("cannot be updated"), "Local-only confirmation overclaims export clearing.");
        Click(Visual<Button>(w, "InternetClearCancel")); Click(Visual<Button>(w, "InternetFolderSettings")); Pump();
        Require(Find<ListBox>(w, "NavigationList").SelectedItem is ListBoxItem item && Equals(item.Tag, "Obsidian"), "Folder action did not navigate to shared settings.");
        f.Model.ObsidianVaultFolder = @"C:\Notes\My Vault"; f.HistoryTab();
        using var gate = new ManualResetEventSlim();
        f.Sync = options => { f.Captured = options; gate.Wait(TimeSpan.FromSeconds(10)); return new(true, "old.md", "Old result") { CheckCount = 168 }; };
        Click(sync); Wait(() => f.SyncCalls == 1); Require(!sync.IsEnabled && !filename.IsEnabled && !retention.IsEnabled && !clear.IsEnabled, "Pending export permits conflicting controls."); Save(w, "syncing");
        Click(sync); Require(f.SyncCalls == 1, "Duplicate sync started.");
        Click(Visual<Button>(w, "SettingsNav_Application")); Pump(); f.HistoryTab();
        filename.Text = "Later.md"; Pump(); Click(sync); Require(f.SyncCalls == 1 && !sync.IsEnabled, "Navigation unlocked a running disk operation.");
        gate.Set(); Wait(() => sync.IsEnabled);
        Require(f.Captured!.InternetLogFileName == "Internet Log.md" && Text(w, "InternetExportHeading") != "Report updated", "Late completion changed destination or updated obsolete UI.");
        f.Sync = _ => new(false, "", "The report is read-only."); Click(sync); Wait(() => sync.IsEnabled); Require(Text(w, "InternetExportHeading") == "Couldn’t update the report", "Write failure is hidden."); Save(w, "write-failed");
        f.Sync = _ => new(true, "Later.md", "168 saved checks synced.") { CheckCount = 168 }; Click(sync); Wait(() => sync.IsEnabled); Save(w, "synced");
        f.Clear = _ => new(false, true, "Later.md", "The report was emptied, but local history could not be cleared.");
        Click(clear); Click(Visual<Button>(w, "InternetClearConfirm")); Wait(() => sync.IsEnabled);
        Require(Text(w, "InternetExportHeading") == "Report emptied · local history kept" && f.History.Count == 168, "Partial clear reported complete success."); Save(w, "clear-partial");
        f.History = Array.Empty<InternetCheckResult>(); Reload(w); Require(!clear.IsEnabled && sync.IsEnabled && Text(w, "InternetHistoryCount") == "No saved checks", "Empty history blocks import or enables clear."); Save(w, "empty-history");
        f.History = Fixture.Samples(2); Reload(w);
        Click(clear); f.Theme(AppThemeCatalog.Daylight); Require(Find<TextBox>(w, "InternetFileNameTextBox").IsEffectivelyVisible, "Legacy fields missing.");
        Require(typeof(SettingsWindow).GetField("_internetClearRequest", Hidden)!.GetValue(w) == null, "Theme change retained destructive confirmation.");
        f.Theme(AppThemeCatalog.Nika); f.HistoryTab();
        using var closeGate = new ManualResetEventSlim();
        f.Sync = _ => { closeGate.Wait(TimeSpan.FromSeconds(10)); return new(true, "later.md", "Late close result"); };
        int beforeCalls = f.SyncCalls; Click(sync); Wait(() => f.SyncCalls > beforeCalls);
        var title = Visual<TextBlock>(w, "InternetExportHeading"); string? beforeTitle = title.Text;
        w.Close(); closeGate.Set(); Pump(100); Require(title.Text == beforeTitle, "Closed Settings received a completion.");
    }

    internal static void LayoutAndThemes()
    {
        foreach (int scale in new[] { 70, 90, 125 }) foreach (bool compact in new[] { false, true })
        {
            using var f = new Fixture(scale, compact); var w = f.Window;
            var scroll = Visual<ScrollViewer>(w, "InternetScroll"); var done = Visual<Button>(w, "SettingsDone");
            Fit(); Save(w, (compact ? "compact-" : "wide-") + scale);
            var footer = done.TranslatePoint(default, w); scroll.ScrollToEnd(); Pump(); Fit();
            Require(footer == done.TranslatePoint(default, w), "Done moved while scrolling."); Save(w, (compact ? "compact-" : "wide-") + scale + "-export");
            Require(Find<TextBox>(w, "InternetFileNameTextBox").Focus() && Find<ComboBox>(w, "InternetRetentionCombo").Focus(), "Fields cannot receive keyboard focus.");
            if (scale == 90 && !compact)
            {
                for (int i = 0; i < 2; i++) { f.Theme(AppThemeCatalog.Daylight); f.Theme(AppThemeCatalog.Nika); f.HistoryTab(); }
                Require(w.GetVisualDescendants().OfType<Grid>().Count(c => c.Name == "InternetExportCards") == 1, "Theme rebuild duplicated export cards.");
                scroll = Visual<ScrollViewer>(w, "InternetScroll"); done = Visual<Button>(w, "SettingsDone");
                f.Model.ObsidianVaultFolder = @"C:\Notes\شبکه و اینترنت\آرشیو طولانی برای بررسی خوانایی و نمایش کامل مسیر";
                Call(w, "RefreshInternetExport"); Pump(); Fit(); scroll.ScrollToHome(); Save(w, "long-path");
            }
            if (scale == 125 && compact)
            {
                f.Model.Typography.Global.SizePercent = 175; Desktop("DesktopTypography", "Apply", f.Model.Typography); Pump(90); Fit();
                var field = Find<TextBox>(w, "InternetFileNameTextBox"); field.BringIntoView(); Pump();
                var presenter = field.GetVisualDescendants().OfType<TextPresenter>().Single(); var inside = field.GetVisualDescendants().OfType<ScrollViewer>().Single();
                Require(presenter.TextLayout.Height <= inside.Viewport.Height + 1, "Large filename text clips vertically."); Save(w, "large-text");
            }
            void Fit()
            {
                Require(scroll.Viewport.Height > 60 && scroll.Extent.Width <= scroll.Viewport.Width + 1, "Export page overflows horizontally.");
                var end = done.TranslatePoint(new Point(done.Bounds.Width, done.Bounds.Height), w)!.Value;
                Require(end.X <= w.ClientSize.Width + 1 && end.Y <= w.ClientSize.Height + 1, "Footer is outside the window.");
            }
        }
    }

    private sealed class Fixture : IDisposable
    {
        internal readonly AppSettings Model = new() { ThemeMode = AppThemeCatalog.Nika, InternetMonitorEnabled = false, ObsidianVaultFolder = @"C:\Notes\My Vault" };
        internal readonly SettingsWindow Window;
        internal IReadOnlyList<InternetCheckResult> History = Samples(168);
        internal Func<AppSettings, InternetSyncResult>? Sync;
        internal Func<AppSettings, InternetClearResult>? Clear;
        internal int SyncCalls, ClearCalls; internal AppSettings? Captured;
        internal Fixture(int scale = 90, bool compact = false)
        {
            TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), "InternetExportOutbox-" + Guid.NewGuid().ToString("N")));
            Model.UiScalePercent = scale; Desktop("ThemeResources", "ApplyApplication", Model.ThemeMode);
            Desktop("DesktopPageAppearance", "Initialize", Model); Desktop("DesktopTypography", "Initialize", Model.Typography);
            var ctor = typeof(SettingsWindow).GetConstructor(Hidden, null, [typeof(AppSettings), typeof(Func<IReadOnlyList<InternetCheckResult>>), typeof(Func<AppSettings, InternetSyncResult>), typeof(Func<AppSettings, InternetClearResult>)], null);
            Require(ctor != null, "Isolated history/export constructor missing.");
            Func<AppSettings, InternetSyncResult> sync = options => { Interlocked.Increment(ref SyncCalls); return Sync?.Invoke(options) ?? new(true, "Internet Log.md", "Saved checks synced.") { CheckCount = History.Count }; };
            Func<AppSettings, InternetClearResult> clear = options => { Interlocked.Increment(ref ClearCalls); if (Clear != null) return Clear(options); History = Array.Empty<InternetCheckResult>(); return new(true, true, "Internet Log.md", "Local checks and report cleared."); };
            Window = (SettingsWindow)ctor!.Invoke([Model, (Func<IReadOnlyList<InternetCheckResult>>)(() => History), sync, clear]);
            Window.ShowActivated = false; Window.ShowInTaskbar = false; Window.Width = compact ? 840 : 1120; Window.Height = compact ? 600 : 720;
            Window.Show(); Pump(60); HistoryTab();
        }
        internal static IReadOnlyList<InternetCheckResult> Samples(int count) => Enumerable.Range(0, count).Select(i => new InternetCheckResult(new DateTime(2026, 11, 16, 14, 32, 0).AddHours(-i), InternetStatus.Online, 24, 38.6, new(true, "Wi-Fi", "Sample", 88, "Sample", "Sample"), "sample")).ToArray();
        internal void HistoryTab() { Click(Visual<Button>(Window, "SettingsNav_Internet")); Pump(); Click(Visual<Button>(Window, "InternetHistoryTab")); Pump(); Reload(Window); }
        internal void Theme(string theme) { Model.ThemeMode = theme; Desktop("ThemeResources", "ApplyApplication", theme); Call(Window, "ReloadFromSettings"); Call(Window, "UpdatePreviewSafely"); Pump(80); }
        public void Dispose() { Window.Close(); TelegramOutboxStore.SetCustomFilePath(null); Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }
    private static void Reload(SettingsWindow w) { var task = (Task)typeof(SettingsWindow).GetMethod("LoadInternetHistoryAsync", Hidden)!.Invoke(w, null)!; Wait(() => task.IsCompleted); }
    private static string? Text(SettingsWindow w, string name) => Visual<TextBlock>(w, name).Text;
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Hidden)!.Invoke(target, null);
    private static void Save(SettingsWindow w, string name)
    {
        const string output = "design/one-piece-v2/settings/internet-export-implemented"; Directory.CreateDirectory(output);
        var frame = w.GetVisualDescendants().OfType<StopwatchFrameSurface>().First();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width), (int)Math.Ceiling(w.ClientSize.Height)), new Vector(96, 96));
        if (((LayoutTransformControl)w.Content!).LayoutTransform!.Value.M11 >= 1) bitmap.Render(w);
        else { using var context = bitmap.CreateDrawingContext(); context.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
        bitmap.Save(Path.Combine(output, name + ".png"));
    }

    private static T Find<T>(SettingsWindow w, string name) => (T)typeof(SettingsWindow).GetField(name, Hidden)!.GetValue(w)!;
    private static T Visual<T>(Control root, string name) where T : Control => root.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump(int ms = 40) => ClockOverlayChecks.Pump(ms);
    private static void Wait(Func<bool> ready) { for (int i = 0; i < 200 && !ready(); i++) Pump(25); Require(ready(), "Timed out waiting for export fixture."); Pump(); }
    private static void Desktop(string type, string method, params object[] args) => typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
