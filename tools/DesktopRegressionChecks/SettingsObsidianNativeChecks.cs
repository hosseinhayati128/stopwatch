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

internal static class SettingsObsidianNativeChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "design/one-piece-v2/settings/obsidian-implemented";

    internal static void Controls()
    {
        var model = new AppSettings { ThemeMode = AppThemeCatalog.Nika, ObsidianVaultFolder = "", ObsidianAutoSyncEnabled = false };
        WithWindow(model, false, window =>
        {
            var export = Find<Button>(window, "ExportNowButton");
            Require(!export.IsEnabled, "Export must be disabled until a destination is selected.");
            var changes = new List<SettingsChangeKind>();
            Action<SettingsChangeKind> changed = changes.Add;
            typeof(SettingsWindow).GetEvent("SettingsChanged", Hidden)!.GetAddMethod(true)!.Invoke(window, [changed]);
            var automatic = Find<CheckBox>(window, "ObsidianAutoSyncCheck");
            var unnamed = Find<CheckBox>(window, "ObsidianLogUnnamedCheck");
            Click(automatic); Click(unnamed);
            Require(model.ObsidianAutoSyncEnabled && !model.ObsidianLogUnnamedTimers && changes.Count == 2 && changes.All(c => c == SettingsChangeKind.ObsidianExport), "Preference changes did not use one existing callback per change.");
            var folder = Find<TextBox>(window, "ObsidianFolderTextBox");
            var file = Find<TextBox>(window, "ObsidianFileNameTextBox");
            folder.Text = @"C:\Notes\My vault\Time";
            file.Text = "Weekly review";
            ClockOverlayChecks.Pump(40);
            Require(export.IsEnabled && model.ObsidianVaultFolder == folder.Text && model.ObsidianExportFileName == file.Text, "Destination edits did not commit or enable export.");
            Require(Find<TextBlock>(window, "ObsidianPathPreviewText").Text == @"C:\Notes\My vault\Time\Weekly review.md", "Full output path lost the Markdown extension.");
            export.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(Find<TextBlock>(window, "ExportStatusText").Text!.Contains("not available"), "Unavailable history did not produce feedback.");
            file.Text = "";
            ClockOverlayChecks.Pump(30);
            Require(Find<TextBlock>(window, "ObsidianPathPreviewText").Text!.EndsWith("Stopwatch Log.md") && !Find<TextBlock>(window, "ExportStatusText").Text!.Contains("not available"), "A new destination retained stale status or lost the default filename.");
            int beforeReload = changes.Count;
            model.ObsidianAutoSyncEnabled = false;
            Call(window, "ReloadFromSettings");
            ClockOverlayChecks.Pump(45);
            Require(automatic.IsChecked == false && changes.Count == beforeReload, "Reload changed settings or lost stored preferences.");
            foreach (string theme in new[] { AppThemeCatalog.Midnight, AppThemeCatalog.Nika, AppThemeCatalog.Pirate, AppThemeCatalog.Nika })
            {
                model.ThemeMode = theme;
                Desktop("ThemeResources", "ApplyApplication", theme);
                Call(window, "ReloadFromSettings"); Call(window, "UpdatePreviewSafely");
                ClockOverlayChecks.Pump(60);
                Require(folder.IsEffectivelyVisible && export.IsEffectivelyVisible, "Theme restoration lost the Obsidian editors.");
                if (theme != AppThemeCatalog.Nika)
                    Require(automatic.Content is string && export.Classes.Contains("modern") && !export.Classes.Contains("editorAction"), "Legacy controls retained the new page's styling.");
            }
            Require(changes.Count == beforeReload, "Theme restoration committed settings.");
            Click(automatic);
            Require(changes.Count == beforeReload + 1, "Theme round trips duplicated preference handlers.");
            Visual<Button>(window, "SettingsNav_Application").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ClockOverlayChecks.Pump(30);
            Require(!folder.IsEffectivelyVisible, "The Obsidian page remained visible in another category.");
            Visual<Button>(window, "SettingsNav_Obsidian").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ClockOverlayChecks.Pump(30);
            Require(folder.Focus() && export.Focus(), "Destination and export cannot receive keyboard focus.");
            folder.Text = "  ";
            ClockOverlayChecks.Pump(30);
            Require(!export.IsEnabled, "Whitespace destination enabled export.");
        });
    }

    internal static void Export()
    {
        string root = Path.GetFullPath(Path.Combine(".superpowers/sdd/2026-11-14-settings-obsidian", "vault-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        var history = new ProjectTimeHistory();
        var start = new DateTime(2026, 11, 14, 10, 0, 0, DateTimeKind.Utc);
        history.AddManualInterval("Learning", start, start.AddMinutes(25));
        var model = new AppSettings { ThemeMode = AppThemeCatalog.Nika, ObsidianVaultFolder = root,
            ObsidianExportFileName = "Review", ObsidianAutoSyncEnabled = false };
        WithWindow(model, false, window =>
        {
            var export = Find<Button>(window, "ExportNowButton");
            export.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(File.Exists(Path.Combine(root, "Review.md")) && File.ReadAllText(Path.Combine(root, "Review.md")).Contains("Learning"), "Manual export with automatic export off did not write completed history.");
            Require(Visual<TextBlock>(window, "ObsidianStatusTitle").Text == "Export complete" && Find<TextBlock>(window, "ExportStatusText").Text!.Contains("1 session"), "The real export result did not reach the status card.");
            string blocked = Path.Combine(root, "not-a-folder");
            File.WriteAllText(blocked, "preserve this file");
            Find<TextBox>(window, "ObsidianFolderTextBox").Text = blocked;
            ClockOverlayChecks.Pump(30);
            export.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(Visual<TextBlock>(window, "ObsidianStatusTitle").Text == "Export failed" && File.ReadAllText(blocked) == "preserve this file", "An inaccessible target lost error feedback or damaged the existing file.");
            Find<TextBox>(window, "ObsidianFolderTextBox").Text = root;
            ClockOverlayChecks.Pump(30);
            export.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(Visual<TextBlock>(window, "ObsidianStatusTitle").Text == "Export complete", "Export did not recover after choosing a valid destination.");
        }, () => history.CreateView(start.AddHours(1)));
    }

    internal static void Layout()
    {
        foreach (int percent in new[] { 70, 90, 125 })
        foreach (bool compact in new[] { false, true })
        {
            var model = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = percent,
                ObsidianVaultFolder = @"C:\Notes\My vault\Time", ObsidianExportFileName = "Stopwatch Log.md" };
            WithWindow(model, compact, window =>
            {
                var scroll = Visual<ScrollViewer>(window, "ObsidianScroll");
                var done = Visual<Button>(window, "SettingsDone");
                CheckFit();
                if (percent == 90) Save(window, compact ? "compact-top" : "configured");
                Find<TextBox>(window, "ObsidianFolderTextBox").Text = @"C:\Notes\My vault\Projects\Learning and development\Time tracking\Daily sessions";
                ClockOverlayChecks.Pump(35);
                var position = done.TranslatePoint(default, window);
                scroll.ScrollToEnd();
                ClockOverlayChecks.Pump(30);
                CheckFit();
                Require(done.TranslatePoint(default, window) == position, "Done moved with the page content.");
                if (compact && percent == 90) Save(window, "compact-destination");
                if (compact && percent == 125)
                {
                    model.Typography.Global.SizePercent = 175;
                    Desktop("DesktopTypography", "Apply", model.Typography);
                    ClockOverlayChecks.Pump(80);
                    CheckFit();
                    scroll.ScrollToEnd();
                    ClockOverlayChecks.Pump(30);
                    Save(window, "large-text");
                    var field = Find<TextBox>(window, "ObsidianFileNameTextBox");
                    field.BringIntoView();
                    ClockOverlayChecks.Pump(30);
                    Save(window, "large-fields");
                    foreach (var textBox in new[] { field, Find<TextBox>(window, "ObsidianFolderTextBox") })
                    {
                        var presenter = textBox.GetVisualDescendants().OfType<TextPresenter>().Single();
                        var viewport = textBox.GetVisualDescendants().OfType<ScrollViewer>().Single();
                        Require(presenter.TextLayout.Height <= viewport.Viewport.Height + 1,
                            $"{textBox.Name} clips enlarged text: line {presenter.TextLayout.Height:0.0}, viewport {viewport.Viewport.Height:0.0}.");
                    }
                }
                void CheckFit()
                {
                    Require(scroll.Viewport.Height > 65 && scroll.Extent.Width <= scroll.Viewport.Width + 1, $"Obsidian page overflows at {percent}% / compact={compact}.");
                    var end = done.TranslatePoint(new Point(done.Bounds.Width, done.Bounds.Height), window)!.Value;
                    Require(end.X <= window.ClientSize.Width + 1 && end.Y <= window.ClientSize.Height + 1, "Done is outside the client area.");
                }
            });
        }
    }

    private static void WithWindow(AppSettings model, bool compact, Action<SettingsWindow> check, Func<ProjectHistoryView>? history = null)
    {
        Desktop("ThemeResources", "ApplyApplication", model.ThemeMode);
        Desktop("DesktopPageAppearance", "Initialize", model);
        Desktop("DesktopTypography", "Initialize", model.Typography);
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"stopwatch-obsidian-{Guid.NewGuid():N}.json"));
        var window = new SettingsWindow(model, history) { ShowActivated = false, ShowInTaskbar = false,
            Width = compact ? 840 : 1120, Height = compact ? 600 : 720 };
        try
        {
            window.Show(); ClockOverlayChecks.Pump(40);
            Visual<Button>(window, "SettingsNav_Obsidian").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ClockOverlayChecks.Pump(50); check(window);
        }
        finally
        {
            window.Close(); TelegramOutboxStore.SetCustomFilePath(null);
            Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown");
        }
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

    private static T Find<T>(Window window, string name) where T : Control => window.FindControl<T>(name)!;
    private static T Visual<T>(Window window, string name) where T : Control => window.GetVisualDescendants().OfType<T>().SingleOrDefault(c => c.Name == name) ?? throw new InvalidOperationException("Missing " + name);
    private static void Click(ToggleButton button) => typeof(ToggleButton).GetMethod("OnClick", Hidden)!.Invoke(button, null);
    private static void Call(object target, string method) => target.GetType().GetMethod(method, Hidden)!.Invoke(target, null);
    private static void Desktop(string type, string method, params object[] args) => typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!.GetMethod(method, BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
