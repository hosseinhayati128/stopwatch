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

// Native design spike: independent sample controls; no ActivityWatch client, launcher or saved settings.
internal static class SettingsActivityWatchPreview
{
    private const string Output = "design/one-piece-v2/settings/activitywatch-connection-native-preview";
    private const string LocalAddress = "http://localhost:5600";
    private static readonly FontFamily Body = StopwatchTypography.Resolve("modern-deck").NameFont;
    private static readonly IBrush Ink = Brush.Parse("#102B3B"), Muted = Brush.Parse("#655539"),
        Rule = Brush.Parse("#509A7B48"), Teal = Brush.Parse("#2D7169"), Error = Brush.Parse("#913F32"), Amber = Brush.Parse("#865C15");

    internal static void Run()
    {
        Require(Application.Current is FrameSizePreviewApp, "Preview must use the isolated application.");
        Application.Current!.Styles.Add(new FluentTheme());
        foreach (string resource in new[] { "avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml", "avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml" })
            Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")) { Source = new Uri(resource) });
        Directory.CreateDirectory(Output);
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"stopwatch-activitywatch-preview-{Guid.NewGuid():N}.json"));
        try
        {
            foreach (int percent in new[] { 70, 90, 125 })
            foreach (bool compact in new[] { false, true }) Render(percent, compact);
        }
        finally { TelegramOutboxStore.SetCustomFilePath(null); }
    }

    private static void Render(int percent, bool compact)
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = percent,
            HabiticaUserId = "", HabiticaClientId = "", HabiticaEnabled = false, ObsidianVaultFolder = "" };
        Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
        Desktop("DesktopPageAppearance", "Initialize", settings);
        Desktop("DesktopTypography", "Initialize", settings.Typography);
        var window = new SettingsWindow(settings) { ShowActivated = false, ShowInTaskbar = false,
            Width = compact ? 840 : 1120, Height = compact ? 600 : 720 };
        // Suppress deferred legacy field events while this drawing-only fixture owns the content.
        typeof(SettingsWindow).GetField("_loading", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(window, true);
        var settingsChanges = new List<SettingsChangeKind>();
        typeof(SettingsWindow).GetEvent("SettingsChanged", BindingFlags.Instance | BindingFlags.NonPublic)!.GetAddMethod(true)!
            .Invoke(window, new object[] { new Action<SettingsChangeKind>(settingsChanges.Add) });
        try
        {
            var navigation = window.FindControl<ListBox>("NavigationList")!;
            navigation.SelectedItem = navigation.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, "ActivityWatch"));
            typeof(SettingsWindow).GetMethod("RestoreActivityWatchPage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            var host = window.FindControl<Grid>("SettingsColumns")!;
            host.IsVisible = true; host.Children.Clear(); host.Background = Brushes.Transparent;
            window.Show(); Pump(60);

            var enabled = new CheckBox { Content = Label("Enable integration in Stopwatch", 13, Ink),
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch };
            AutomationProperties.SetName(enabled, "Enable ActivityWatch integration in Stopwatch");
            var integration = Label("Integration off", 12, Amber);
            var address = new TextBox { Text = LocalAddress, Watermark = LocalAddress, FontFamily = Body, FontSize = 13,
                MinHeight = 36, MinWidth = 0, Padding = new(10, 6), VerticalContentAlignment = VerticalAlignment.Center, Classes = { "editorField" } };
            AutomationProperties.SetName(address, "ActivityWatch server address");
            var addressHint = Label("Usually http://localhost:5600 on this computer.", 12);
            var test = Action("Test connection", true); var start = Action("Start ActivityWatch");
            test.Margin = start.Margin = new(0, 0, 8, 4);
            var launchHelp = Label("Start opens ActivityWatch on this computer.", 12);
            var connection = Card("Connection", enabled, integration,
                Label("Use ActivityWatch history in your reports and activity reviews. Turning this off leaves ActivityWatch running.", 12),
                new Border { Height = 1, Background = Rule, Margin = new(0, 2) },
                new StackPanel { Spacing = 6, Children = { Label("Server address", 12, Ink), address, addressHint } },
                new WrapPanel { Children = { test, start } }, launchHelp);

            var stateTitle = Label("Not checked yet", 18, Amber);
            var stateDetail = Label("Test the server to verify this address.", 13);
            var progress = new ProgressBar { Height = 4, IsIndeterminate = true, Foreground = Teal, IsVisible = false };
            var stateBox = new Border { Background = Brush.Parse("#11865C15"), BorderBrush = Brush.Parse("#55865C15"),
                BorderThickness = new(1), CornerRadius = new(5), Padding = new(12),
                Child = new StackPanel { Spacing = 7, Children = { stateTitle, stateDetail, progress } } };
            var version = Label("—", 14, Ink); var checkedAt = Label("Never", 14, Ink);
            var metrics = new Grid { ColumnDefinitions = new("*,12,*"), Children = {
                new StackPanel { Spacing = 4, Children = { Label("SERVER VERSION", 9), version } },
                new StackPanel { Spacing = 4, Children = { Label("LAST CHECK", 9), checkedAt } } } };
            Grid.SetColumn(metrics.Children[1], 2);
            var status = Card("Connection status", stateBox, metrics,
                Label("A successful check confirms the server is reachable. It does not verify that activity is being recorded.", 12));
            var help = Card("How it works",
                Label("ActivityWatch records app, window and browser activity through its watchers.", 12),
                Label("Stopwatch reads that history for reports. Export choices and review prompts have their own settings.", 12));
            var right = new StackPanel { Spacing = 14, Children = { status, help } };
            var cards = new Grid { Children = { connection, right } };
            var tabs = new WrapPanel();
            foreach (string name in new[] { "Connection & status", "Sync & export", "Review prompts" })
            {
                var tab = Action(name, name == "Connection & status"); tab.Margin = new(0, 0, 8, 4);
                tab.IsEnabled = name == "Connection & status"; tabs.Children.Add(tab);
            }
            var heading = new StackPanel { Spacing = 4, Children = {
                Label("ActivityWatch", 23, Ink), Label("Connect your local activity history to Stopwatch.", 12) } };
            var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 5, 0),
                Content = new StackPanel { Spacing = 14, Children = { heading, tabs, cards } } };
            bool? stacked = null;
            void Resize()
            {
                bool next = host.Bounds.Width < 760 * settings.Typography.Global.SizePercent / 100;
                if (stacked == next) return; stacked = next;
                cards.ColumnDefinitions = new(next ? "*" : "1.1*,16,1*"); cards.RowDefinitions = new(next ? "Auto,14,Auto" : "Auto");
                Grid.SetColumn(right, next ? 0 : 2); Grid.SetRow(right, next ? 2 : 0);
            }
            scroll.SizeChanged += (_, _) => Resize(); heading.SizeChanged += (_, _) => Resize();
            Grid.SetColumnSpan(scroll, 3); host.Children.Add(scroll); Resize();
            var done = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "SettingsDone");
            window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SettingsSaveStatus").Text = "Design preview · sample connection states";

            bool busy = false;
            void RefreshActions()
            {
                string raw = address.Text?.Trim() ?? "";
                if (raw.Length == 0) raw = LocalAddress;
                if (!raw.Contains("://")) raw = "http://" + raw;
                bool valid = Uri.TryCreate(raw, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && !string.IsNullOrWhiteSpace(uri.Host);
                bool local = valid && uri!.IsLoopback;
                test.IsEnabled = valid && !busy; start.IsEnabled = local && !busy; address.IsEnabled = !busy;
                addressHint.Text = valid ? "Blank uses the local default: http://localhost:5600." : "Enter a valid HTTP or HTTPS server address.";
                addressHint.Foreground = valid ? Muted : Error;
                launchHelp.Text = local ? "Start opens ActivityWatch on this computer." : "Start works only for a server on this computer.";
            }
            void State(string title, string detail, string tone = "normal", bool working = false, string serverVersion = "—", string lastCheck = "Today · 14:32")
            {
                busy = working; stateTitle.Text = title; stateDetail.Text = detail;
                stateTitle.Foreground = tone == "error" ? Error : tone == "warning" ? Amber : Teal;
                stateBox.Background = Brush.Parse(tone == "error" ? "#11913F32" : tone == "warning" ? "#11865C15" : "#112D7169");
                stateBox.BorderBrush = Brush.Parse(tone == "error" ? "#55913F32" : tone == "warning" ? "#55865C15" : "#552D7169");
                progress.IsVisible = working; version.Text = serverVersion; checkedAt.Text = lastCheck;
                test.Content = working ? "Checking…" : "Test connection"; RefreshActions();
            }
            void Connected() => State("Server reachable", "ActivityWatch answered at this server address.", serverVersion: "v0.13.2");
            enabled.IsCheckedChanged += (_, _) => { integration.Text = enabled.IsChecked == true ? "Integration on" : "Integration off"; integration.Foreground = enabled.IsChecked == true ? Teal : Amber; };
            address.TextChanged += (_, _) => State("Address changed", "Test this address to check the new connection.", "warning", lastCheck: "Not checked");
            test.Click += (_, _) => State("Checking the server…", "Waiting for ActivityWatch to respond.", working: true, lastCheck: "In progress");
            start.Click += (_, _) => State("Starting ActivityWatch…", "Waiting for the local server to respond.", working: true, lastCheck: "In progress");
            void Capture(string name) { Pump(60); CheckFit(); if (percent == 90 && !compact) Save(window, name); }

            State("Not checked yet", "Test the server to verify this address.", "warning", lastCheck: "Never"); Capture("setup");
            enabled.IsChecked = true; Click(test); Require(!test.IsEnabled && !start.IsEnabled, "Busy preview permits duplicate actions."); Capture("checking");
            Connected(); Capture("connected");
            if (compact && percent == 90) Save(window, "compact-connection");
            if (!compact && percent != 90) Save(window, $"connected-{percent}");
            Require(address.Focus() && test.Focus() && enabled.Focus(), "Connection controls cannot receive keyboard focus.");
            enabled.IsChecked = false; Capture("integration-off");
            Require(stateTitle.Text == "Server reachable", "Turning integration off erased the successful server check.");
            enabled.IsChecked = true;
            State("Couldn’t reach the server", "The server did not respond. Start ActivityWatch, check the address, then test again.", "error"); Capture("unreachable");
            var donePosition = done.TranslatePoint(default, window); scroll.ScrollToEnd(); Pump(50); CheckFit();
            Require(done.TranslatePoint(default, window) == donePosition, "Done moved when the page scrolled.");
            if (compact && percent == 90) Save(window, "compact-status"); scroll.Offset = default;
            Click(start); Require(stateTitle.Text == "Starting ActivityWatch…" && busy, "Start preview lost its waiting state."); Capture("starting");
            State("Open ActivityWatch manually", "The app wasn’t found in its usual install locations. Open it yourself, then test the connection.", "warning", lastCheck: "Not checked"); Capture("app-not-found");
            Connected(); address.Text = "not a valid address"; Pump(); Require(!test.IsEnabled && !start.IsEnabled, "Invalid address enables actions."); Capture("invalid-address");
            address.Text = "http://activitywatch.example:5600"; Pump();
            Require(test.IsEnabled && !start.IsEnabled && version.Text == "—", "Remote edit kept stale success or enabled a local launch."); Capture("remote-address");
            address.Text = ""; Pump(); Require(test.IsEnabled && start.IsEnabled, "Blank address lost the existing local default.");
            address.Text = LocalAddress; Connected();
            if (compact && percent == 125)
            {
                settings.Typography.Global.SizePercent = 175; Desktop("DesktopTypography", "Apply", settings.Typography);
                Resize(); Pump(90); CheckFit();
                var text = address.GetVisualDescendants().OfType<TextPresenter>().Single();
                var viewport = address.GetVisualDescendants().OfType<ScrollViewer>().Single();
                Require(text.TextLayout.Height <= viewport.Viewport.Height + 1, "Enlarged address clips vertically.");
                address.BringIntoView(); Pump(); Save(window, "large-text");
            }
            Require(settingsChanges.Count == 0 && !settings.ActivityWatchEnabled && settings.ActivityWatchServerUrl == LocalAddress,
                $"Preview changed sample Settings model: events={string.Join(',', settingsChanges)}, enabled={settings.ActivityWatchEnabled}, url={settings.ActivityWatchServerUrl}.");
            Console.WriteLine($"PASS ActivityWatch sample states, address edits, busy actions, focus and footer at {percent}% / {(compact ? "compact" : "regular")}");
            void CheckFit()
            {
                Require(scroll.Viewport.Height > 65 && scroll.Extent.Width <= scroll.Viewport.Width + 1, "Preview overflows horizontally or has no viewport.");
                Require(cards.Children.All(c => c.Bounds.Right <= cards.Bounds.Width + 1), "Connection cards overlap horizontally.");
                foreach (var button in new[] { test, start })
                {
                    var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First();
                    var label = presenter.GetVisualDescendants().OfType<TextBlock>().First();
                    Require(label.TextLayout.WidthIncludingTrailingWhitespace <= presenter.Bounds.Width + 1, "Action caption is clipped.");
                }
                var end = done.TranslatePoint(new Point(done.Bounds.Width, done.Bounds.Height), window)!.Value;
                Require(end.X <= window.ClientSize.Width + 1 && end.Y <= window.ClientSize.Height + 1, "Done is outside the window.");
            }
        }
        finally { window.Close(); Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }

    private static TextBlock Label(string text, double size, IBrush? color = null) => new()
    { Text = text, FontFamily = Body, FontSize = size, Foreground = color ?? Muted, TextWrapping = TextWrapping.Wrap };
    private static Button Action(string text, bool primary = false)
    {
        var button = new Button { Content = text, FontFamily = Body, FontSize = 13, MinHeight = 36, Padding = new(12, 6), Classes = { "editorAction" } };
        if (primary) button.Classes.Add("editorPrimary"); return button;
    }
    private static Border Card(string title, params Control[] controls)
    {
        var body = new StackPanel { Spacing = 10, Children = { Label(title, 18, Ink) } };
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
