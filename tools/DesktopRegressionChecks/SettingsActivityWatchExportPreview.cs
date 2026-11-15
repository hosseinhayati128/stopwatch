using System.Reflection;
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

// Drawing-only proposal: independent sample fields; no sync service, file export or network calls.
internal static class SettingsActivityWatchExportPreview
{
    private const string Output = "design/one-piece-v2/settings/activitywatch-export-native-preview";
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
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"stopwatch-aw-export-preview-{Guid.NewGuid():N}.json"));
        try { foreach (int scale in new[] { 70, 90, 125 }) foreach (bool compact in new[] { false, true }) Render(scale, compact); }
        finally { TelegramOutboxStore.SetCustomFilePath(null); }
    }

    private static void Render(int scale, bool compact)
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = scale,
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
            navigation.SelectedItem = navigation.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, "ActivityWatch"));
            typeof(SettingsWindow).GetMethod("RestoreActivityWatchPage", Hidden)!.Invoke(window, null);
            var host = window.FindControl<Grid>("SettingsColumns")!; host.IsVisible = true; host.Children.Clear(); host.Background = Brushes.Transparent;
            window.Show(); Pump(60);

            bool integration = true, hasFolder = true, busy = false;
            var afterTimer = Choice("With stopwatch exports", "Update activity when stopwatch time is exported.");
            var scheduled = Choice("On a schedule", "");
            var interval = Field("60", "Sync interval in minutes"); interval.Width = 78;
            var intervalRow = new WrapPanel { Children = { Label("Every", 13, Ink), interval, Label("minutes", 13, Ink) } };
            foreach (var child in intervalRow.Children) { child.Margin = new(0, 0, 8, 0); child.VerticalAlignment = VerticalAlignment.Center; }
            var intervalHint = Label("1–1,440 minutes · while Stopwatch is open", 11);
            var autoState = Label("Integration on · automatic sync preferences", 11, Teal);
            var auto = Card("Automatic sync", autoState, afterTimer, scheduled, intervalRow, intervalHint);

            var titles = Choice("App title summaries", "Show each app’s top window or document.");
            var web = Choice("Browser activity", "Include websites and page titles.");
            var minimum = Field("15", "Minimum activity in seconds"); minimum.Width = 78;
            var minimumRow = new WrapPanel { Children = { Label("Minimum activity", 12, Ink), minimum, Label("seconds", 12, Ink) } };
            foreach (var child in minimumRow.Children) { child.Margin = new(0, 0, 8, 0); child.VerticalAlignment = VerticalAlignment.Center; }
            var minimumHint = Label("Small totals are omitted; timeline filtering also applies.", 11);
            var content = Card("Report content", titles, web, minimumRow, minimumHint,
                Label("Title summaries affect the app table. Timeline details can still contain titles.", 11));
            var left = new StackPanel { Spacing = 14, Children = { auto, content } };

            var folder = Label(@"C:\Notes\My Vault", 13, Ink);
            var folderHint = Label("Shared with Obsidian / Markdown settings.", 11);
            var changeFolder = Action("Change folder"); changeFolder.HorizontalAlignment = HorizontalAlignment.Left;
            AutomationProperties.SetName(changeFolder, "Change export folder in Obsidian / Markdown settings");
            var folderRow = new Grid { ColumnDefinitions = new("*,10,Auto"), Children = {
                new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { folder, folderHint } }, changeFolder } };
            Grid.SetColumn(changeFolder, 2);
            var filename = Field("ActivityWatch Log.md", "ActivityWatch export file name");
            var fileHint = Label("Blank uses ActivityWatch Log.md; .md is added if needed.", 11);
            var sync = Action("Sync today", true); sync.HorizontalAlignment = HorizontalAlignment.Left;
            var destination = Card("Export to Markdown", Label("EXPORT FOLDER", 9), folderRow,
                new StackPanel { Spacing = 6, Children = { Label("File name", 12, Ink), filename } }, fileHint,
                new Border { Height = 1, Background = Rule },
                Label("Updates today’s section in this file. Other days stay in place.", 12), sync);

            var stateTitle = Label("Not synced in this window", 16, Amber);
            var stateDetail = Label("Use Sync today to update the report.", 12);
            var counts = Label("", 15, Ink); counts.IsVisible = false;
            var checkedAt = Label("", 11); checkedAt.IsVisible = false;
            var progress = new ProgressBar { Height = 4, IsIndeterminate = true, IsVisible = false, Foreground = Teal };
            var stateBox = new Border { BorderThickness = new(1), CornerRadius = new(5), Padding = new(12),
                Child = new StackPanel { Spacing = 6, Children = { stateTitle, stateDetail, counts, checkedAt, progress } } };
            var result = Card("Sync result", stateBox);
            var right = new StackPanel { Spacing = 14, Children = { destination, result } };
            var cards = new Grid { Children = { left, right } };
            var tabs = new WrapPanel();
            foreach (string title in new[] { "Connection & status", "Sync & export", "Review prompts" })
            {
                var tab = Action(title, title == "Sync & export"); tab.IsEnabled = title == "Sync & export";
                tab.Margin = new(0, 0, 8, 4); tabs.Children.Add(tab);
            }
            var heading = new StackPanel { Spacing = 4, Children = { Label("ActivityWatch", 23, Ink), Label("Choose what goes into your activity report and when it updates.", 12) } };
            var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Padding = new(0, 0, 5, 0), Content = new StackPanel { Spacing = 14, Children = { heading, tabs, cards } } };
            Grid.SetColumnSpan(scroll, 3); host.Children.Add(scroll);
            bool? stacked = null;
            void Resize()
            {
                bool next = host.Bounds.Width < 760 * settings.Typography.Resolve("Settings").SizePercent / 100;
                if (stacked == next) return; stacked = next;
                cards.ColumnDefinitions = new(next ? "*" : "1.1*,16,1*"); cards.RowDefinitions = new(next ? "Auto,14,Auto" : "Auto");
                Grid.SetColumn(right, next ? 0 : 2); Grid.SetRow(right, next ? 2 : 0);
            }
            scroll.SizeChanged += (_, _) => Resize(); heading.SizeChanged += (_, _) => Resize(); Resize();
            var done = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "SettingsDone");
            window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "SettingsSaveStatus").Text = "Design preview · sample export states only";

            void Refresh()
            {
                string raw = filename.Text?.Trim() ?? "";
                bool fileValid = raw.Length == 0 || (!raw.Contains('/') && !raw.Contains('\\') && raw.IndexOfAny(Path.GetInvalidFileNameChars()) < 0 && raw is not "." and not "..");
                bool minValid = int.TryParse(minimum.Text, out int seconds) && seconds is >= 1 and <= 3600;
                bool intervalValid = int.TryParse(interval.Text, out int minutes) && minutes is >= 1 and <= 1440;
                sync.IsEnabled = hasFolder && fileValid && minValid && !busy;
                sync.Content = busy ? "Syncing…" : "Sync today";
                foreach (Control control in new Control[] { filename, minimum, titles, web, changeFolder }) control.IsEnabled = !busy;
                afterTimer.IsEnabled = scheduled.IsEnabled = integration && !busy;
                interval.IsEnabled = integration && scheduled.IsChecked == true && !busy;
                intervalHint.Text = intervalValid ? "1–1,440 minutes · while Stopwatch is open" : "Enter a whole number from 1 to 1,440 minutes.";
                intervalHint.Foreground = intervalValid ? Muted : Error;
                minimumHint.Text = minValid ? "Small totals are omitted; timeline filtering also applies." : "Enter a whole number from 1 to 3,600 seconds.";
                minimumHint.Foreground = minValid ? Muted : Error;
                fileHint.Text = fileValid ? "Blank uses ActivityWatch Log.md; .md is added if needed." : "Use a file name, not a folder path or reserved character.";
                fileHint.Foreground = fileValid ? Muted : Error;
                folder.Text = hasFolder ? @"C:\Notes\My Vault" : "No export folder selected";
                folder.Foreground = hasFolder ? Ink : Amber;
                changeFolder.Content = hasFolder ? "Change folder" : "Choose folder";
                autoState.Text = integration ? "Integration on · automatic sync preferences" : "Automatic sync paused · integration is off";
                autoState.Foreground = integration ? Teal : Amber;
            }
            void State(string title, string detail, string tone = "warning", bool working = false, string? totals = null)
            {
                busy = working; stateTitle.Text = title; stateDetail.Text = detail;
                string hex = tone == "error" ? "913F32" : tone == "normal" ? "2D7169" : "865C15";
                stateTitle.Foreground = Brush.Parse("#" + hex); stateBox.Background = Brush.Parse("#11" + hex); stateBox.BorderBrush = Brush.Parse("#55" + hex);
                counts.Text = totals; counts.IsVisible = totals != null;
                checkedAt.Text = totals != null ? "Today · 16:20 · ActivityWatch Log.md" : ""; checkedAt.IsVisible = totals != null;
                progress.IsVisible = working; Refresh();
            }
            void Ready() => State("Not synced in this window", "Use Sync today to update the report.");
            void Success() => State("Today’s report updated", "Saved today’s section to the selected file.", "normal", totals: "8 apps · 6 web domains");
            filename.TextChanged += (_, _) => Ready(); minimum.TextChanged += (_, _) => Refresh(); interval.TextChanged += (_, _) => Refresh();
            scheduled.IsCheckedChanged += (_, _) => Refresh();
            sync.Click += (_, _) => { if (sync.IsEnabled) State("Syncing today’s activity…", "Reading activity and writing the Markdown report.", "normal", working: true); };
            changeFolder.Click += (_, _) => { hasFolder = true; Ready(); };
            void Capture(string name) { Pump(60); CheckFit(); if (scale == 90 && !compact) Save(window, name); }

            Ready(); Capture("ready");
            if (!compact && scale != 90) Save(window, "ready-" + scale);
            if (compact && scale == 90) Save(window, "compact-settings");
            Require(filename.Focus() && interval.Focus() && sync.Focus(), "Export fields or action cannot receive keyboard focus.");
            Click(sync); Require(busy && !sync.IsEnabled && !filename.IsEnabled && !minimum.IsEnabled, "Pending preview permits conflicting edits."); Capture("syncing");
            Click(sync); Require(busy, "A repeated sample click ended progress."); Success(); Capture("synced");
            var footer = done.TranslatePoint(default, window); scroll.ScrollToEnd(); Pump(50); CheckFit();
            Require(done.TranslatePoint(default, window) == footer, "Done moved with the scroll content.");
            if (compact && scale == 90) Save(window, "compact-export"); scroll.Offset = default;
            hasFolder = false; State("Choose an export folder", "Select a folder in Obsidian / Markdown settings, then return here.");
            Require(!sync.IsEnabled && changeFolder.IsEnabled, "Missing folder does not offer a recovery action."); Capture("missing-folder");
            Click(changeFolder); Require(hasFolder && sync.IsEnabled, "Sample folder selection did not restore Sync.");
            State("Couldn’t save the report", "The folder could not be written to. Check access or choose another folder, then try again.", "error"); Capture("write-failed");
            State("Report contains no entries", "No app or domain totals were returned. If you expected activity, check ActivityWatch and its watchers.", totals: "0 apps · 0 web domains"); Capture("empty-report");
            integration = false; Ready(); Require(sync.IsEnabled && !scheduled.IsEnabled && !interval.IsEnabled, "Integration off changed manual-export availability."); Capture("integration-off");
            integration = true; scheduled.IsChecked = false; afterTimer.IsChecked = false; Refresh(); Require(!interval.IsEnabled && interval.Text == "60", "Schedule off lost its saved interval."); Capture("manual-only");
            afterTimer.IsChecked = true; scheduled.IsChecked = true; interval.Text = "0"; Pump(); Require(sync.IsEnabled && intervalHint.Foreground == Error, "Invalid schedule blocked independent manual sync."); Capture("invalid-interval");
            interval.Text = "60"; filename.Text = @"..\Other.md"; Pump(); Require(!sync.IsEnabled, "Invalid filename enables export."); Capture("invalid-file");
            filename.Text = ""; Pump(); Require(sync.IsEnabled, "Blank filename lost its default."); filename.Text = "ActivityWatch Log.md"; Pump();
            minimum.Text = "0"; Pump(); Require(!sync.IsEnabled, "Invalid report filter enables export."); minimum.Text = "15"; Pump();
            Ready(); scroll.Offset = default;
            if (compact && scale == 125)
            {
                settings.Typography.Global.SizePercent = 175; Desktop("DesktopTypography", "Apply", settings.Typography);
                Resize(); Pump(80); filename.BringIntoView(); Pump(); CheckFit();
                var text = filename.GetVisualDescendants().OfType<TextPresenter>().Single();
                var fieldScroll = filename.GetVisualDescendants().OfType<ScrollViewer>().Single();
                Require(text.TextLayout.Height <= fieldScroll.Viewport.Height + 1, "Enlarged filename clips vertically."); Save(window, "large-text");
            }
            Require(settingsChanges == 0 && settings.ObsidianVaultFolder == "" && !settings.ActivityWatchEnabled && settings.ActivityWatchExportFileName == "ActivityWatch Log.md",
                "Drawing-only preview mutated the sample Settings model.");
            Console.WriteLine($"PASS ActivityWatch export preview states, validation, focus and fixed footer at {scale}% / {(compact ? "compact" : "regular")}");

            void CheckFit()
            {
                Require(scroll.Viewport.Height > 60 && scroll.Extent.Width <= scroll.Viewport.Width + 1, "Preview has horizontal overflow or no usable viewport.");
                Require(cards.Children.All(c => c.Bounds.Right <= cards.Bounds.Width + 1), "Export columns overlap.");
                foreach (var button in new[] { sync, changeFolder })
                {
                    var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First();
                    var label = presenter.GetVisualDescendants().OfType<TextBlock>().First();
                    Require(label.TextLayout.WidthIncludingTrailingWhitespace <= presenter.Bounds.Width + 1, "An export action caption is clipped.");
                }
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
    private static CheckBox Choice(string title, string detail)
    {
        var body = new StackPanel { Spacing = 3, Children = { Label(title, 13, Ink) } };
        if (detail.Length > 0) body.Children.Add(Label(detail, 11));
        var check = new CheckBox { IsChecked = true, Content = body, HorizontalContentAlignment = HorizontalAlignment.Stretch,
            VerticalContentAlignment = VerticalAlignment.Top };
        AutomationProperties.SetName(check, title); return check;
    }
    private static Button Action(string text, bool primary = false)
    {
        var button = new Button { Content = text, FontFamily = Body, FontSize = 13, MinHeight = 36, Padding = new(12, 6), Classes = { "editorAction" } };
        if (primary) button.Classes.Add("editorPrimary"); return button;
    }
    private static Border Card(string title, params Control[] controls)
    {
        var body = new StackPanel { Spacing = 9, Children = { Label(title, 18, Ink) } };
        foreach (var control in controls) body.Children.Add(control);
        return new Border { Background = Brush.Parse("#40FFF6E4"), BorderBrush = Rule, BorderThickness = new(1), CornerRadius = new(7),
            Padding = new(16), VerticalAlignment = VerticalAlignment.Top, Child = body };
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
    private static void Desktop(string type, string method, params object[] args) => typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!
        .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
