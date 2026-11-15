using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Themes.Fluent;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

// Drawing-only proposal: independent sample fields, no history reads, exports, deletes or probes.
internal static class SettingsInternetExportPreview
{
    private const string Output = "design/one-piece-v2/settings/internet-export-native-preview";
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly FontFamily Body = StopwatchTypography.Resolve("modern-deck").NameFont;
    private static readonly IBrush Ink = Brush.Parse("#102B3B"), Muted = Brush.Parse("#655539"),
        Rule = Brush.Parse("#509A7B48"), Teal = Brush.Parse("#2D7169"), Amber = Brush.Parse("#865C15"), Error = Brush.Parse("#913F32");

    internal static void Run()
    {
        Require(Application.Current is FrameSizePreviewApp, "Preview requires the isolated app.");
        Application.Current!.Styles.Add(new FluentTheme());
        foreach (string resource in new[] { "avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml", "avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml" })
            Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")) { Source = new Uri(resource) });
        Directory.CreateDirectory(Output);
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"stopwatch-internet-export-preview-{Guid.NewGuid():N}.json"));
        try { foreach (int scale in new[] { 70, 90, 125 }) foreach (bool compact in new[] { false, true }) Render(scale, compact); }
        finally { TelegramOutboxStore.SetCustomFilePath(null); }
        File.WriteAllText(Path.Combine(Output, "verification.json"), JsonSerializer.Serialize(new {
            status = "native-design-preview-not-production", scales = new[] { 70, 90, 125 }, windows = new[] { "1120 x 720", "840 x 600" },
            states = new[] { "ready", "syncing", "synced", "missing-folder", "write-failed", "empty-history", "invalid-file", "retention", "clear-confirmation", "clear-partial", "cleared", "long-path", "large-text" },
            checks = "Six layout cases, fixed footer, keyboard focus, sample validation and action states, confirmation scope, zero settings events",
            realHistoryReads = 0, fileExports = 0, historyDeletes = 0, networkRequests = 0,
            execution = "Private Windows desktop; synthetic data only"
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Render(int scale, bool compact)
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = scale, InternetMonitorEnabled = false,
            HabiticaEnabled = false, HabiticaUserId = "", HabiticaClientId = "", ObsidianVaultFolder = "" };
        Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
        Desktop("DesktopPageAppearance", "Initialize", settings); Desktop("DesktopTypography", "Initialize", settings.Typography);
        var window = new SettingsWindow(settings) { ShowActivated = false, ShowInTaskbar = false,
            Width = compact ? 840 : 1120, Height = compact ? 600 : 720 };
        typeof(SettingsWindow).GetField("_loading", Hidden)!.SetValue(window, true);
        int settingsChanges = 0;
        typeof(SettingsWindow).GetEvent("SettingsChanged", Hidden)!.GetAddMethod(true)!.Invoke(window, [new Action<SettingsChangeKind>(_ => settingsChanges++)]);
        try
        {
            var navigation = window.FindControl<ListBox>("NavigationList")!;
            navigation.SelectedItem = navigation.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, "Internet"));
            typeof(SettingsWindow).GetMethod("RestoreInternetPage", Hidden)!.Invoke(window, null);
            var host = window.FindControl<Grid>("SettingsColumns")!; host.Children.Clear(); host.IsVisible = true; host.Background = Brushes.Transparent;
            window.Show(); Pump(60);

            bool hasFolder = true, busy = false, confirming = false; int saved = 168;
            string sampleFolder = @"C:\Notes\My Vault";
            var count = Label("168 saved checks", 25, Ink); count.FontWeight = FontWeight.SemiBold;
            var dates = Label("Nov 9–16 · latest today at 14:32", 11);
            var retention = new ComboBox { MinHeight = 36, HorizontalAlignment = HorizontalAlignment.Stretch,
                FontFamily = Body, FontSize = 13, ItemsSource = new[] { "Keep all history", "Keep last 30 days", "Keep last 14 days", "Keep last 7 days" }, SelectedIndex = 0 };
            AutomationProperties.SetName(retention, "History retention period");
            var retentionHint = Label("No automatic removal. You can clear history below.", 11);
            var clear = Action("Clear history…"); clear.Foreground = clear.BorderBrush = Error;
            var confirm = Action("Clear history"); confirm.Background = Error; confirm.Foreground = Brush.Parse("#FFF5DD"); confirm.BorderBrush = Error;
            var cancel = Action("Cancel");
            var confirmDetail = Label("", 12, Ink);
            var confirmActions = new WrapPanel { Children = { cancel, confirm } };
            cancel.Margin = new(0, 0, 8, 4); confirm.Margin = new(0, 0, 0, 4);
            var confirmation = new Border { IsVisible = false, BorderBrush = Brush.Parse("#66913F32"), BorderThickness = new(1),
                Background = Brush.Parse("#09913F32"), CornerRadius = new(5), Padding = new(12),
                Child = new StackPanel { Spacing = 9, Children = { Label("Clear saved history?", 16, Error), confirmDetail,
                    Label("This cannot be undone here. Automatic checks can add new history.", 11), confirmActions } } };
            var history = Card("Saved history", new StackPanel { Spacing = 4, Children = { count, dates } },
                Label("Automatic checks are saved locally. Test now does not add history.", 11),
                new Border { Height = 1, Background = Rule },
                new StackPanel { Spacing = 6, Children = { Label("Keep checks for", 12, Ink), retention, retentionHint } },
                new Border { Height = 1, Background = Rule }, Label("Clear saved checks and empty this report. Monitoring settings stay unchanged.", 11), clear, confirmation);

            var folder = Label(sampleFolder, 13, Ink);
            var manage = Action("Folder settings");
            var folderRow = new Grid { ColumnDefinitions = new("*,12,Auto"), Children = {
                new StackPanel { Spacing = 4, Children = { folder, Label("Shared with Obsidian / Markdown settings.", 11) } }, manage } };
            Grid.SetColumn(manage, 2); manage.VerticalAlignment = VerticalAlignment.Top;
            var filename = Field("Internet Log.md", "Internet report file name");
            var fileHint = Label("Blank uses Internet Log.md; .md is added if needed.", 11);
            var sync = Action("Sync now", true);
            var resultTitle = Label("Not synced in this window", 14, Amber);
            var resultDetail = Label("Sync saved checks to the selected report.", 11);
            var resultTime = Label("", 10); resultTime.IsVisible = false;
            var progress = new ProgressBar { Height = 3, IsIndeterminate = true, IsVisible = false, Foreground = Teal };
            var result = new Border { CornerRadius = new(5), BorderThickness = new(1), Padding = new(12),
                Child = new StackPanel { Spacing = 5, Children = { resultTitle, resultDetail, resultTime, progress } } };
            var export = Card("Export to Markdown", new StackPanel { Spacing = 5, Children = { Label("OBSIDIAN VAULT", 9), folderRow } },
                new StackPanel { Spacing = 6, Children = { Label("File name", 12, Ink), filename, fileHint } },
                Label("Keeps saved checks and their report notes. Scheduled checks also update this file.", 11), sync, result);
            var cards = new Grid { Children = { history, export } };
            var tabs = new WrapPanel();
            foreach (string title in new[] { "Connection & monitoring", "History & export" })
            { var tab = Action(title, title == "History & export"); tab.IsEnabled = title == "History & export"; tab.Margin = new(0, 0, 8, 4); tabs.Children.Add(tab); }
            var heading = new StackPanel { Spacing = 4, Children = { Label("Internet & Network", 23, Ink), Label("Keep your connection history and its Markdown report in order.", 12) } };
            var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new(0, 0, 5, 0), Content = new StackPanel { Spacing = 14, Children = { heading, tabs, cards } } };
            Grid.SetColumnSpan(scroll, 3); host.Children.Add(scroll);
            bool? stacked = null;
            void Resize()
            {
                bool next = host.Bounds.Width < 740 * settings.Typography.Resolve("Settings").SizePercent / 100;
                if (stacked == next) return; stacked = next;
                cards.ColumnDefinitions = new(next ? "*" : "1*,16,1.05*"); cards.RowDefinitions = new(next ? "Auto,14,Auto" : "Auto");
                Grid.SetColumn(export, next ? 0 : 2); Grid.SetRow(export, next ? 2 : 0);
            }
            scroll.SizeChanged += (_, _) => Resize(); heading.SizeChanged += (_, _) => Resize(); Resize();
            var done = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SettingsDone");
            window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "SettingsSaveStatus").Text = "Design preview · sample history and export states";

            void Refresh()
            {
                string raw = filename.Text?.Trim() ?? "";
                bool valid = raw.Length == 0 || (!raw.Contains('/') && !raw.Contains('\\') && raw.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && raw is not "." and not "..");
                string effective = raw.Length == 0 ? "Internet Log.md" : raw.EndsWith(".md", StringComparison.OrdinalIgnoreCase) ? raw : raw + ".md";
                fileHint.Text = valid ? "Blank uses Internet Log.md; .md is added if needed." : "Use a file name, not a folder path or reserved character.";
                fileHint.Foreground = valid ? Muted : Error;
                folder.Text = hasFolder ? sampleFolder : "No export folder selected"; folder.Foreground = hasFolder ? Ink : Amber;
                manage.Content = hasFolder ? "Folder settings" : "Choose folder";
                sync.IsEnabled = hasFolder && valid && !busy && !confirming; sync.Content = busy ? "Syncing…" : "Sync now";
                filename.IsEnabled = manage.IsEnabled = retention.IsEnabled = !busy && !confirming;
                clear.IsEnabled = !busy && saved > 0 && valid; clear.IsVisible = !confirming; confirmation.IsVisible = confirming;
                count.Text = saved == 0 ? "No saved checks" : $"{saved} saved checks";
                dates.Text = saved == 0 ? "New automatic checks will appear here." : "Nov 9–16 · latest today at 14:32";
                retentionHint.Text = retention.SelectedIndex == 0 ? "No automatic removal. You can clear history below." : "Older checks are removed when the report is synced. This also changes local history.";
                confirmDetail.Text = hasFolder ? $"Remove {saved} saved checks locally and empty “{effective}” in {sampleFolder}. Other files stay unchanged."
                    : $"Remove {saved} saved checks locally. No export folder is selected, so existing Markdown copies cannot be updated.";
            }
            void State(string title, string detail, string tone = "warning", bool working = false, bool completed = false)
            {
                busy = working; resultTitle.Text = title; resultDetail.Text = detail;
                string hex = tone == "error" ? "913F32" : tone == "normal" ? "2D7169" : "865C15";
                resultTitle.Foreground = Brush.Parse("#" + hex); result.Background = Brush.Parse("#11" + hex); result.BorderBrush = Brush.Parse("#55" + hex);
                resultTime.Text = completed ? "Today · 14:40 · sample result" : ""; resultTime.IsVisible = completed; progress.IsVisible = working; Refresh();
            }
            void Ready() => State("Not synced in this window", "Sync saved checks to the selected report.");
            filename.TextChanged += (_, _) => Ready(); retention.SelectionChanged += (_, _) => Refresh();
            sync.Click += (_, _) => { if (sync.IsEnabled) State("Syncing saved checks…", "Updating the Markdown report.", "normal", working: true); };
            manage.Click += (_, _) => { hasFolder = true; Ready(); };
            clear.Click += (_, _) => { if (clear.IsEnabled) { confirming = true; Refresh(); cancel.Focus(); } };
            cancel.Click += (_, _) => { confirming = false; Refresh(); clear.Focus(); };
            confirm.Click += (_, _) => { confirming = false; saved = 0; State("History cleared", hasFolder ? "Local checks removed and the selected report emptied." : "Local checks removed. Existing Markdown copies were not changed.", "normal", completed: true); };
            string prefix = compact ? "compact" : "wide";
            void Capture(string name) { Pump(60); Fit(); Save(window, name); }
            void Example(string name) { if (scale == 90 && !compact) Capture(name); }

            Ready(); Capture($"{prefix}-{scale}"); var footer = done.TranslatePoint(default, window);
            scroll.ScrollToEnd(); Pump(); Capture($"{prefix}-{scale}-export");
            Require(done.TranslatePoint(default, window) == footer, "Done moved with scrolling.");
            Require(retention.Focus() && filename.Focus() && sync.Focus(), "Fields/actions cannot receive keyboard focus.");
            scroll.ScrollToHome(); Click(sync); Require(busy && !sync.IsEnabled && !clear.IsEnabled && !filename.IsEnabled, "Sync permits conflicting actions."); Example("syncing");
            State("Report updated", "168 saved checks synced to Internet Log.md.", "normal", completed: true); Example("synced");
            hasFolder = false; State("Choose an export folder", "Set your vault folder in Obsidian / Markdown settings.");
            Require(!sync.IsEnabled && manage.IsEnabled && clear.IsEnabled, "Missing folder blocks recovery or local clearing."); Example("missing-folder");
            Click(manage); Require(sync.IsEnabled, "Sample folder recovery did not enable sync.");
            State("Couldn’t update the report", "Check folder access and try again. The Markdown report may be out of date.", "error"); Example("write-failed");
            saved = 0; State("No local checks saved", "Sync can recover checks already in the report. Otherwise it creates an empty report.");
            Require(!clear.IsEnabled && sync.IsEnabled, "Empty history lost import behavior or allows an unnecessary clear."); Example("empty-history");
            saved = 168; Ready(); filename.Text = @"..\Other.md"; Pump(); Require(!sync.IsEnabled && !clear.IsEnabled, "Invalid destination allows an action."); Example("invalid-file");
            filename.Text = ""; Pump(); Require(sync.IsEnabled, "Blank file name lost its default."); filename.Text = "Internet Log.md";
            retention.SelectedIndex = 3; Pump(); Example("retention"); retention.SelectedIndex = 0;
            filename.Text = "Network journal.md"; Pump(); Click(clear); Pump();
            Require(confirming && saved == 168 && !sync.IsEnabled && confirmDetail.Text!.Contains("Network journal.md"), "Confirmation mutated data or named the wrong report.");
            confirmation.BringIntoView(); Pump(); Example("clear-confirmation");
            Click(cancel); Require(saved == 168 && !confirming && sync.IsEnabled, "Cancel did not preserve history.");
            hasFolder = false; Refresh(); Click(clear);
            Require(confirmDetail.Text!.Contains("cannot be updated"), "Local-only clear claims to update exports.");
            Click(cancel); hasFolder = true; Refresh();
            Click(clear); Click(confirm); Require(saved == 0 && !clear.IsEnabled, "Sample clear did not finish."); scroll.ScrollToEnd(); Example("cleared");
            State("Local history cleared", "The Markdown report could not be updated and may still contain older checks. Clearing is only partly complete.", "error"); Example("clear-partial");
            saved = 168; filename.Text = "Internet Log.md"; Ready(); scroll.ScrollToHome();
            if (scale == 90 && !compact) { sampleFolder = @"C:\Notes\Personal archive\Research and learning\Weekly records\My shared vault"; Refresh(); Example("long-path"); }
            if (compact && scale == 125)
            {
                settings.Typography.Global.SizePercent = 175; Desktop("DesktopTypography", "Apply", settings.Typography); Resize(); Pump(80);
                filename.BringIntoView(); Pump(); Fit();
                var presenter = filename.GetVisualDescendants().OfType<TextPresenter>().Single(); var fieldScroll = filename.GetVisualDescendants().OfType<ScrollViewer>().Single();
                Require(presenter.TextLayout.Height <= fieldScroll.Viewport.Height + 1, "Large file-name text clips vertically."); Save(window, "large-text");
            }
            Require(settingsChanges == 0 && settings.ObsidianVaultFolder == "" && !settings.InternetMonitorEnabled && settings.InternetLogRetentionDays == 0,
                "Design preview mutated Settings.");
            Console.WriteLine($"PASS Internet export sample states, confirmation, layout, focus and isolation at {scale}% / {prefix}; stacked={stacked}");
            void Fit()
            {
                Require(scroll.Viewport.Height > 60 && scroll.Extent.Width <= scroll.Viewport.Width + 1, "Page has horizontal overflow or no viewport.");
                Require(cards.Children.All(c => c.Bounds.Right <= cards.Bounds.Width + 1), "Cards escape their container.");
                var end = done.TranslatePoint(new Point(done.Bounds.Width, done.Bounds.Height), window)!.Value;
                Require(end.X <= window.ClientSize.Width + 1 && end.Y <= window.ClientSize.Height + 1, "Done is outside the window.");
            }
        }
        finally { window.Close(); Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }

    private static TextBlock Label(string text, double size, IBrush? color = null) => new()
    { Text = text, FontFamily = Body, FontSize = size, Foreground = color ?? Muted, TextWrapping = TextWrapping.Wrap };
    private static TextBox Field(string text, string name)
    {
        var field = new TextBox { Text = text, FontFamily = Body, FontSize = 13, MinWidth = 0, MinHeight = 36,
            Padding = new(10, 6), VerticalContentAlignment = VerticalAlignment.Center, Classes = { "editorField" } };
        AutomationProperties.SetName(field, name); return field;
    }
    private static Button Action(string text, bool primary = false)
    {
        var button = new Button { Content = text, FontFamily = Body, FontSize = 13, MinHeight = 36, Padding = new(12, 6), HorizontalAlignment = HorizontalAlignment.Left, Classes = { "editorAction" } };
        if (primary) button.Classes.Add("editorPrimary"); return button;
    }
    private static Border Card(string title, params Control[] controls)
    {
        var body = new StackPanel { Spacing = 11, Children = { Label(title, 18, Ink) } };
        foreach (var control in controls) body.Children.Add(control);
        return new Border { Background = Brush.Parse("#40FFF6E4"), BorderBrush = Rule, BorderThickness = new(1), CornerRadius = new(7), Padding = new(16), VerticalAlignment = VerticalAlignment.Top, Child = body };
    }
    private static void Save(SettingsWindow window, string name)
    {
        var frame = window.GetVisualDescendants().OfType<StopwatchFrameSurface>().First();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
        if (((LayoutTransformControl)window.Content!).LayoutTransform!.Value.M11 >= 1) bitmap.Render(window);
        else { using var context = bitmap.CreateDrawingContext(); context.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump(int milliseconds = 30) => ClockOverlayChecks.Pump(milliseconds);
    private static void Desktop(string type, string method, params object[] args) => typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
