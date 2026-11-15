using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
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

// Drawing-only proposal: synthetic results, no network probe, monitor, persistence or export.
internal static class SettingsInternetPreview
{
    private const string Output = "design/one-piece-v2/settings/internet-connection-native-preview";
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
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"stopwatch-internet-preview-{Guid.NewGuid():N}.json"));
        try { foreach (int scale in new[] { 70, 90, 125 }) foreach (bool compact in new[] { false, true }) Render(scale, compact); }
        finally { TelegramOutboxStore.SetCustomFilePath(null); }
        File.WriteAllText(Path.Combine(Output, "verification.json"), JsonSerializer.Serialize(new {
            status = "native-design-preview-not-production", scales = new[] { 70, 90, 125 }, windows = new[] { "1120 x 720", "840 x 600" },
            states = new[] { "not-checked", "checking", "online", "slow", "unreachable", "partial-result", "monitoring-off", "waiting-for-timer", "invalid-interval" },
            checks = "Layout bounds, fixed footer, keyboard focus, sample action states, interval validation and zero settings events",
            networkRequests = 0, persistedSettings = 0, execution = "Private Windows desktop; synthetic data only"
        }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void Render(int scale, bool compact)
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = scale,
            HabiticaEnabled = false, HabiticaUserId = "", HabiticaClientId = "", ObsidianVaultFolder = "", InternetMonitorEnabled = false };
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
            typeof(SettingsWindow).GetMethod("RestoreInternetPage", Hidden)?.Invoke(window, null);
            var host = window.FindControl<Grid>("SettingsColumns")!;
            host.Children.Clear(); host.IsVisible = true; host.Background = Brushes.Transparent;
            window.Show(); Pump(60);

            var stateTitle = Label("Online", 21, Teal); stateTitle.FontWeight = FontWeight.SemiBold;
            var stateDetail = Label("Internet responded to the last check.", 12);
            var progress = new ProgressBar { Height = 3, IsIndeterminate = true, Foreground = Teal, IsVisible = false };
            var stateBox = new Border { Background = Brush.Parse("#122D7169"), BorderBrush = Brush.Parse("#552D7169"),
                BorderThickness = new(1), CornerRadius = new(5), Padding = new(12),
                Child = new StackPanel { Spacing = 5, Children = { stateTitle, stateDetail, progress } } };
            var network = Label("Home Wi-Fi", 16, Ink); var adapter = Label("Wi-Fi · signal 88%", 11);
            var networkBlock = new StackPanel { Spacing = 4, Children = { Label("NETWORK AT LAST CHECK", 9), network, adapter } };
            var ping = Label("24 ms", 23, Ink); var speed = Label("38.6 Mbps", 23, Ink);
            ping.FontWeight = speed.FontWeight = FontWeight.SemiBold;
            Border Metric(string title, TextBlock value) => new() { Padding = new(12), CornerRadius = new(5), Background = Brush.Parse("#55FFF8E8"),
                Child = new StackPanel { Spacing = 5, Children = { Label(title, 9), value } } };
            var metrics = new Grid { ColumnDefinitions = new("*,10,*"), Children = { Metric("LATENCY", ping), Metric("DOWNLOAD", speed) } };
            Grid.SetColumn(metrics.Children[1], 2);
            var checkedAt = Label("Today · 14:32", 11);
            var test = Action("Test now", true); test.HorizontalAlignment = HorizontalAlignment.Left;
            var status = Card("Last connection check", stateBox, networkBlock, metrics,
                new StackPanel { Spacing = 3, Children = { Label("LAST CHECKED", 9), checkedAt } }, test,
                Label("Uses a short download sample. Results describe the check, not a continuous live connection.", 11));

            var enabled = Choice("Check automatically", "While Stopwatch is open."); enabled.IsChecked = true;
            var onlyTimers = Choice("Only while a stopwatch is running", "Skip scheduled checks when no stopwatch is running.");
            var interval = new TextBox { Text = "10", Width = 70, MinHeight = 36, Padding = new(10, 6), FontFamily = Body,
                FontSize = 13, VerticalContentAlignment = VerticalAlignment.Center, Classes = { "editorField" } };
            AutomationProperties.SetName(interval, "Check interval in minutes");
            var minutes = Label("minutes", 12);
            var intervalRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9, Children = { interval, minutes } };
            var intervalHint = Label("1–120 minutes · default 10", 11);
            var frequency = new StackPanel { Spacing = 6, Children = { Label("Check every", 12, Ink), intervalRow, intervalHint } };
            var scheduleTitle = Label("Scheduled checks enabled", 13, Teal); scheduleTitle.FontWeight = FontWeight.SemiBold;
            var scheduleDetail = Label("Every 10 minutes while Stopwatch is open.", 11);
            var schedule = new Border { Padding = new(12), CornerRadius = new(5), Background = Brush.Parse("#122D7169"),
                Child = new StackPanel { Spacing = 5, Children = { scheduleTitle, scheduleDetail } } };
            var monitoring = Card("Background monitoring", enabled, new Border { Height = 1, Background = Rule }, frequency, onlyTimers, schedule,
                Label("Test now works even when automatic checks are off.", 11));
            var cards = new Grid { Children = { status, monitoring } };
            var tabs = new WrapPanel();
            foreach (string title in new[] { "Connection & monitoring", "History & export" })
            {
                var tab = Action(title, title == "Connection & monitoring"); tab.IsEnabled = title == "Connection & monitoring";
                tab.Margin = new(0, 0, 8, 4); tabs.Children.Add(tab);
            }
            var heading = new StackPanel { Spacing = 4, Children = {
                Label("Internet & Network", 23, Ink), Label("Check your connection and choose when to monitor it.", 12) } };
            var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 5, 0),
                Content = new StackPanel { Spacing = 14, Children = { heading, tabs, cards } } };
            Grid.SetColumnSpan(scroll, 3); host.Children.Add(scroll);
            bool? stacked = null;
            void Resize()
            {
                bool next = host.Bounds.Width < 740 * settings.Typography.Resolve("Settings").SizePercent / 100;
                if (stacked == next) return; stacked = next;
                cards.ColumnDefinitions = new(next ? "*" : "1.05*,16,1*"); cards.RowDefinitions = new(next ? "Auto,14,Auto" : "Auto");
                Grid.SetColumn(monitoring, next ? 0 : 2); Grid.SetRow(monitoring, next ? 2 : 0);
            }
            scroll.SizeChanged += (_, _) => Resize(); heading.SizeChanged += (_, _) => Resize(); Resize();
            var done = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "SettingsDone");
            window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SettingsSaveStatus").Text = "Design preview · sample network results";

            bool busy = false, runningTimer = true;
            void RefreshSchedule()
            {
                bool on = enabled.IsChecked == true;
                interval.IsEnabled = minutes.IsEnabled = onlyTimers.IsEnabled = on;
                bool valid = int.TryParse(interval.Text, out int value) && value is >= 1 and <= 120;
                intervalHint.Text = valid ? "1–120 minutes · default 10" : "Enter a whole number from 1 to 120.";
                intervalHint.Foreground = valid || !on ? Muted : Error;
                scheduleTitle.Foreground = !on || !valid || (onlyTimers.IsChecked == true && !runningTimer) ? Amber : Teal;
                scheduleTitle.Text = !on ? "Automatic checks off" : !valid ? "Check the interval" : onlyTimers.IsChecked == true && !runningTimer ? "Waiting for a stopwatch" : "Scheduled checks enabled";
                scheduleDetail.Text = !on ? "Use Test now whenever you need a connection check." : !valid ? "The last valid interval stays in use until corrected."
                    : onlyTimers.IsChecked == true ? $"Every {value} minutes while a stopwatch is running." : $"Every {value} minutes while Stopwatch is open.";
                test.IsEnabled = !busy;
            }
            void State(string title, string detail, string tone = "normal", string latency = "—", string download = "—", string when = "Today · 14:32", bool checking = false)
            {
                busy = checking; stateTitle.Text = title; stateDetail.Text = detail;
                stateTitle.Foreground = tone == "error" ? Error : tone == "warning" ? Amber : Teal;
                stateBox.Background = Brush.Parse(tone == "error" ? "#11913F32" : tone == "warning" ? "#11865C15" : "#122D7169");
                stateBox.BorderBrush = Brush.Parse(tone == "error" ? "#55913F32" : tone == "warning" ? "#55865C15" : "#552D7169");
                ping.Text = latency; speed.Text = download; checkedAt.Text = when; progress.IsVisible = checking;
                test.Content = checking ? "Checking…" : "Test now"; RefreshSchedule();
            }
            void Online() => State("Online", "Internet responded to the last check.", latency: "24 ms", download: "38.6 Mbps");
            enabled.IsCheckedChanged += (_, _) => RefreshSchedule(); onlyTimers.IsCheckedChanged += (_, _) => RefreshSchedule();
            interval.TextChanged += (_, _) => RefreshSchedule();
            test.Click += (_, _) => State("Checking connection…", "Measuring latency and a short download sample.", when: "In progress", checking: true);
            string prefix = compact ? "compact" : "wide";
            void Capture(string name) { Pump(70); Fit(); Save(window, name); }
            void Example(string name) { if (scale == 90 && !compact) Capture(name); }

            Online(); Capture($"{prefix}-{scale}");
            double footer = done.TranslatePoint(default, window)!.Value.Y;
            scroll.ScrollToEnd(); Pump(40); Capture($"{prefix}-{scale}-monitoring");
            Require(Math.Abs(done.TranslatePoint(default, window)!.Value.Y - footer) < 1, "Scrolling moves Done.");
            Require(test.Focus() && enabled.Focus() && interval.Focus() && onlyTimers.Focus(), "Preview controls cannot receive keyboard focus.");
            scroll.ScrollToHome(); Pump();
            Click(test); Require(!test.IsEnabled, "Checking allows duplicate actions."); Example("checking");
            State("Not checked yet", "Run a check to see your connection quality.", "warning", when: "Never");
            network.Text = "Not checked"; adapter.Text = "Network details appear after a check."; Example("not-checked");
            network.Text = "Home Wi-Fi"; adapter.Text = "Wi-Fi · signal 88%";
            State("Slow connection", "High latency may make online work feel delayed.", "warning", "318 ms", "4.2 Mbps"); Example("slow");
            State("Internet not reached", "The check timed out. The connection may be offline or the test may be blocked.", "error"); Example("unreachable");
            State("Online · speed unavailable", "Latency was measured, but the download test did not finish.", "warning", "24 ms", "Unavailable"); Example("partial-result");
            Online(); enabled.IsChecked = false; Example("monitoring-off");
            Require(test.IsEnabled && !interval.IsEnabled && !onlyTimers.IsEnabled && stateTitle.Text == "Online", "Monitoring preference changed the connection result or blocked manual testing.");
            enabled.IsChecked = true; onlyTimers.IsChecked = true; runningTimer = false; RefreshSchedule(); Example("waiting-for-timer");
            interval.Text = "0"; Pump(); Require(intervalHint.Foreground == Error && test.IsEnabled, "Invalid interval blocks testing or has no feedback."); Example("invalid-interval");
            interval.Text = "10"; onlyTimers.IsChecked = false;
            if (scale == 90 && !compact)
            {
                network.Text = "Home office — extended workspace network with a long name";
                adapter.Text = "Wi-Fi · signal 88% · sample network"; Capture("long-network-name");
            }
            Require(settingsChanges == 0 && !settings.InternetMonitorEnabled && settings.InternetMonitorIntervalMinutes == 10,
                "Design preview changed settings.");
            Console.WriteLine($"PASS Internet sample states, layout, focus, fixed footer and isolation at {scale}% / {prefix}; viewport={scroll.Viewport}; stacked={stacked}");
            void Fit()
            {
                Require(scroll.Viewport.Height > 65 && scroll.Extent.Width <= scroll.Viewport.Width + 1, "Page overflows horizontally or has no viewport.");
                Require(cards.Children.All(c => c.Bounds.Right <= cards.Bounds.Width + 1), "Cards escape their container.");
                Require(metrics.Children.All(c => c.Bounds.Width > 70 && c.Bounds.Right <= metrics.Bounds.Width + 1), "Metrics collapse or overflow.");
                var end = done.TranslatePoint(new Point(done.Bounds.Width, done.Bounds.Height), window)!.Value;
                Require(end.X <= window.ClientSize.Width + 1 && end.Y <= window.ClientSize.Height + 1, "Done is outside the window.");
            }
        }
        finally { window.Close(); Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }

    private static TextBlock Label(string text, double size, IBrush? color = null) => new()
    { Text = text, FontFamily = Body, FontSize = size, Foreground = color ?? Muted, TextWrapping = TextWrapping.Wrap };
    private static CheckBox Choice(string title, string detail)
    {
        var control = new CheckBox { Content = new StackPanel { Spacing = 3, Children = { Label(title, 13, Ink), Label(detail, 11) } },
            HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top };
        AutomationProperties.SetName(control, title); return control;
    }
    private static Button Action(string text, bool primary = false)
    {
        var button = new Button { Content = text, FontFamily = Body, FontSize = 13, MinHeight = 36, Padding = new(12, 6), Classes = { "editorAction" } };
        if (primary) button.Classes.Add("editorPrimary"); return button;
    }
    private static Border Card(string title, params Control[] controls)
    {
        var body = new StackPanel { Spacing = 12, Children = { Label(title, 18, Ink) } };
        foreach (var control in controls) body.Children.Add(control);
        return new Border { Background = Brush.Parse("#40FFF6E4"), BorderBrush = Rule, BorderThickness = new(1), CornerRadius = new(7),
            Padding = new(16), VerticalAlignment = VerticalAlignment.Top, Child = body };
    }
    private static void Save(SettingsWindow window, string name)
    {
        var frame = window.GetVisualDescendants().OfType<StopwatchFrameSurface>().First();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
        if (((LayoutTransformControl)window.Content!).LayoutTransform!.Value.M11 >= 1) bitmap.Render(window);
        else { using var context = bitmap.CreateDrawingContext(); context.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill,
            SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size)); }
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }
    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    private static void Pump(int milliseconds = 30) => ClockOverlayChecks.Pump(milliseconds);
    private static void Desktop(string type, string method, params object[] args) => typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!
        .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
