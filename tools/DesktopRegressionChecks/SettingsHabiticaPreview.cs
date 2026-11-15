using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
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

// Design spike only: independent sample controls, no Habitica service, secret-store or clipboard actions.
internal static class SettingsHabiticaPreview
{
    private const string Output = "design/one-piece-v2/settings/habitica-connection-native-preview";
    private const string Account = "11111111-2222-4333-8444-555555555555";
    private const string Client = Account + "-StopwatchOverlay";
    private static readonly FontFamily Body = StopwatchTypography.Resolve("modern-deck").NameFont;
    private static readonly IBrush Ink = Brush.Parse("#102B3B"), Muted = Brush.Parse("#655539"),
        Rule = Brush.Parse("#509A7B48"), Teal = Brush.Parse("#2D7169"), Error = Brush.Parse("#913F32"), Amber = Brush.Parse("#865C15");

    internal static void Run()
    {
        Require(Application.Current is FrameSizePreviewApp, "Habitica previews require the isolated app.");
        Application.Current!.Styles.Add(new FluentTheme());
        foreach (string resource in new[] { "avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml",
            "avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml" })
            Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")) { Source = new Uri(resource) });
        Directory.CreateDirectory(Output);
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"stopwatch-habitica-preview-{Guid.NewGuid():N}.json"));
        try
        {
            foreach (int percent in new[] { 70, 90, 125 })
            foreach (bool compact in new[] { false, true }) Render(percent, compact);
        }
        finally { TelegramOutboxStore.SetCustomFilePath(null); }
    }

    private static void Render(int percent, bool compact)
    {
        // Empty production account/vault avoids loading account credentials or sync metadata in the shell.
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = percent,
            HabiticaUserId = "", HabiticaClientId = "", HabiticaEnabled = false, ObsidianVaultFolder = "" };
        Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
        Desktop("DesktopPageAppearance", "Initialize", settings);
        Desktop("DesktopTypography", "Initialize", settings.Typography);
        var window = new SettingsWindow(settings) { ShowActivated = false, ShowInTaskbar = false,
            Width = compact ? 840 : 1120, Height = compact ? 600 : 720 };
        try
        {
            var navigation = window.FindControl<ListBox>("NavigationList")!;
            navigation.SelectedItem = navigation.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, "Habitica"));
            typeof(SettingsWindow).GetMethod("RestoreHabiticaPage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            var host = window.FindControl<Grid>("SettingsColumns")!;
            host.IsVisible = true;
            host.Children.Clear(); host.Background = Brushes.Transparent;
            window.Show(); Pump(50);

            var account = Editor("User ID", Account, "User ID from Habitica’s API settings");
            var client = Editor("Public client identifier", Client, "Author UUID-StopwatchOverlay");
            var token = Editor("API token", "", "Paste a token to replace it"); token.PasswordChar = '●';
            var paste = Action("Paste"); var reveal = Action("Show"); reveal.MinWidth = 60;
            AutomationProperties.SetName(paste, "Paste API token"); AutomationProperties.SetName(reveal, "Show entered API token");
            paste.Click += (_, _) => token.Text = "sample-api-token-preview";
            reveal.Click += (_, _) =>
            {
                bool show = token.PasswordChar != '\0'; token.PasswordChar = show ? '\0' : '●';
                reveal.Content = show ? "Hide" : "Show";
                AutomationProperties.SetName(reveal, show ? "Hide entered API token" : "Show entered API token");
            };
            var tokenRow = new Grid { ColumnDefinitions = new("*,8,Auto,8,Auto"), Children = { token, paste, reveal } };
            Grid.SetColumn(paste, 2); Grid.SetColumn(reveal, 4);
            var save = Action("Save connection", true); var test = Action("Test connection");
            save.Margin = test.Margin = new(0, 0, 8, 4);
            var actions = new WrapPanel { Orientation = Orientation.Horizontal, Children = { save, test } };
            var tokenHelp = Label("", 12);
            var connection = Card("Connection", Field("User ID", account), Field("Public client identifier", client),
                Label("Author’s Habitica User ID + -StopwatchOverlay. This is a public app identifier.", 12),
                Field("API token", tokenRow), tokenHelp,
                actions, Label("Test connection saves these details before checking your account.", 12));

            var testFeedback = Status();
            var credentialTitle = Label("", 13, Teal); var credentialDetail = Label("", 12);
            var status = Card("Connection status", testFeedback.Box,
                new StackPanel { Spacing = 4, Children = { credentialTitle, credentialDetail } });
            var enabled = new CheckBox { HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top, Content = new StackPanel { Spacing = 4, Children =
                { Label("Enable Habitica sync", 14, Ink), Label("For this computer and its local vault.", 12) } } };
            AutomationProperties.SetName(enabled, "Enable Habitica sync on this computer");
            var syncState = Label("", 13, Teal); var syncDetail = Label("", 12);
            var pending = Label("0", 22, Ink); var lastSync = Label("Today · 14:32", 14, Ink);
            var metrics = new Grid { ColumnDefinitions = new("0.7*,14,1.3*"), Children =
            {
                new StackPanel { Spacing = 4, Children = { Label("PENDING CHANGES", 9), pending } },
                new StackPanel { Spacing = 4, Children = { Label("LAST SUCCESSFUL SYNC", 9), lastSync } }
            } };
            Grid.SetColumn(metrics.Children[1], 2);
            var sync = Card("Sync on this computer", enabled,
                new StackPanel { Spacing = 4, Children = { syncState, syncDetail } },
                new Border { Height = 1, Background = Rule }, metrics,
                Label("Syncs todos, reminders, and checklist items. Standalone notes stay local.", 12));
            var right = new StackPanel { Spacing = 14, Children = { status, sync } };
            var cards = new Grid { Children = { connection, right } };
            var heading = new StackPanel { Spacing = 4, Margin = new(0, 0, 0, 16), Children =
            { Label("Habitica", 23, Ink), Label("Connect your account and follow sync on this computer.", 12) } };
            var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 5, 0),
                Content = new StackPanel { Children = { heading, cards } } };
            bool? stacked = null;
            void Resize()
            {
                bool next = host.Bounds.Width < 720 * settings.Typography.Global.SizePercent / 100;
                if (stacked == next) return; stacked = next;
                cards.ColumnDefinitions = new(next ? "*" : "1.1*,16,1*");
                cards.RowDefinitions = new(next ? "Auto,14,Auto" : "Auto");
                Grid.SetColumn(right, next ? 0 : 2); Grid.SetRow(right, next ? 2 : 0);
            }
            scroll.SizeChanged += (_, _) => Resize(); heading.SizeChanged += (_, _) => Resize();
            Grid.SetColumnSpan(scroll, 3); host.Children.Add(scroll); Resize();
            var done = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "SettingsDone");
            window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SettingsSaveStatus").Text = "Design preview · sample account and sync status";

            bool stored = true, busy = false;
            void RefreshActions()
            {
                bool valid = Guid.TryParse(account.Text, out _) && !string.IsNullOrWhiteSpace(client.Text);
                save.IsEnabled = !busy && valid;
                test.IsEnabled = !busy && valid && (stored || !string.IsNullOrWhiteSpace(token.Text));
                reveal.IsEnabled = !string.IsNullOrEmpty(token.Text);
                if (!reveal.IsEnabled) { token.PasswordChar = '●'; reveal.Content = "Show"; }
            }
            void Saved()
            {
                stored = stored || !string.IsNullOrWhiteSpace(token.Text); token.Text = "";
                credentialTitle.Text = stored ? "Token saved securely" : "No token saved";
                credentialTitle.Foreground = stored ? Teal : Muted;
                credentialDetail.Text = stored ? "Protected by this Windows account." : "Paste the API token for this User ID.";
                token.Watermark = stored ? "Paste a token to replace it" : "Paste your API token";
                tokenHelp.Text = stored ? "Blank keeps the current token. The field clears after saving." : "Enter your API token, then save. The field clears after saving.";
                RefreshActions();
            }
            save.Click += (_, _) => { Saved(); Feedback(testFeedback, "Connection saved", "Ready to test your account."); };
            test.Click += (_, _) => { Saved(); Feedback(testFeedback, "Last test succeeded", "Habitica accepted this account and API token."); };
            foreach (var field in new[] { account, client, token }) field.TextChanged += (_, _) => RefreshActions();
            enabled.IsCheckedChanged += (_, _) =>
            {
                syncState.Text = enabled.IsChecked == true ? "Sync enabled" : "Sync paused";
                syncState.Foreground = enabled.IsChecked == true ? Teal : Amber;
                syncDetail.Text = enabled.IsChecked == true ? "Runs while Stopwatch is open, including in the tray." : "New changes wait locally until you enable sync again.";
            };
            void Connected()
            {
                busy = false; stored = true; account.Text = Account; client.Text = Client; token.Text = "";
                enabled.IsChecked = false; enabled.IsChecked = true; enabled.IsEnabled = true;
                Saved(); pending.Text = "0"; lastSync.Text = "Today · 14:32"; test.Content = "Test connection";
                Feedback(testFeedback, "Last test succeeded", "Habitica accepted this account and API token.");
            }
            Connected(); Pump(70); CheckFit();
            if (percent == 90) Save(window, compact ? "compact-connection" : "connected");
            Require(!reveal.IsEnabled && token.PasswordChar != '\0', "A stored token appears in the draft field.");
            Click(paste); Click(reveal); Require(token.PasswordChar == '\0', "Show does not reveal the entered sample token.");
            Click(reveal); Require(token.PasswordChar != '\0', "Hide does not mask the entered token.");
            Feedback(testFeedback, "Unsaved changes", "Choose Save connection or Test connection to apply these details.", "warning");
            if (!compact && percent == 90) { Pump(); Save(window, "token-draft"); }
            Click(save); Require(token.Text == "" && !reveal.IsEnabled && test.IsEnabled, "Saving did not clear the sample token field.");
            Click(test); Require(testFeedback.Title.Text == "Last test succeeded", "Sample connection feedback did not update.");
            Require(account.Focus() && token.Focus() && test.Focus(), "Connection controls cannot receive keyboard focus.");
            enabled.IsChecked = false; pending.Text = "3"; Pump(); CheckFit();
            if (!compact && percent == 90) Save(window, "paused");
            enabled.IsChecked = true; syncState.Text = "Sync needs attention"; syncState.Foreground = Error;
            syncDetail.Text = "Waiting for a valid connection. Pending changes are kept.";
            Feedback(testFeedback, "Check your account details", "Habitica rejected the User ID or API token. Make sure both belong to the same account.", "error");
            Pump(); CheckFit(); if (!compact && percent == 90) Save(window, "connection-error");
            var donePosition = done.TranslatePoint(default, window); scroll.ScrollToEnd(); Pump(); CheckFit();
            Require(done.TranslatePoint(default, window) == donePosition, "Done moved with the preview content.");
            if (compact && percent == 90) Save(window, "compact-status");
            scroll.Offset = default; Connected();
            credentialTitle.Text = "Token kept for this session"; credentialTitle.Foreground = Amber;
            credentialDetail.Text = "Protected storage is unavailable. Enter the token again after restarting Stopwatch.";
            Pump(); CheckFit(); if (!compact && percent == 90) Save(window, "session-only");
            Connected(); busy = true; RefreshActions(); test.Content = "Try again in 45s";
            Feedback(testFeedback, "Waiting to retry", "Habitica asked us to wait. Your saved connection is kept.", "warning");
            Pump(); CheckFit(); if (!compact && percent == 90) Save(window, "waiting");
            Connected(); stored = false; account.Text = ""; client.Text = ""; token.Text = ""; Saved();
            enabled.IsChecked = false; enabled.IsEnabled = false; pending.Text = "0"; lastSync.Text = "Never";
            syncDetail.Text = "Save a connection before enabling sync on this computer.";
            Feedback(testFeedback, "Connect your account", "Enter your User ID, public client identifier, and API token.");
            Pump(); CheckFit(); Require(!save.IsEnabled && !test.IsEnabled, "Empty connection enables actions.");
            if (!compact && percent == 90) Save(window, "setup");
            if (compact && percent == 125)
            {
                Connected(); Click(paste);
                settings.Typography.Global.SizePercent = 175;
                Desktop("DesktopTypography", "Apply", settings.Typography); Resize(); Pump(80); CheckFit();
                foreach (var field in new[] { account, client, token })
                {
                    var text = field.GetVisualDescendants().OfType<TextPresenter>().Single();
                    var viewport = field.GetVisualDescendants().OfType<ScrollViewer>().Single();
                    Require(text.TextLayout.Height <= viewport.Viewport.Height + 1, "An enlarged Habitica field clips text.");
                }
                foreach (var button in new[] { save, test, paste, reveal })
                {
                    var text = button.GetVisualDescendants().OfType<TextBlock>().Single();
                    var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().Single();
                    Require(text.TextLayout.WidthIncludingTrailingWhitespace <= presenter.Bounds.Width + 1, "An enlarged Habitica action clips its caption.");
                }
                token.BringIntoView(); Pump(); Save(window, "large-fields");
            }
            Console.WriteLine($"PASS Habitica sample states, token draft, focus, fit and fixed footer at {percent}% / {window.ClientSize.Width:0} x {window.ClientSize.Height:0}");
            void CheckFit()
            {
                Require(scroll.Viewport.Height > 65 && scroll.Extent.Width <= scroll.Viewport.Width + 1, "Habitica preview overflows horizontally.");
                var end = done.TranslatePoint(new Point(done.Bounds.Width, done.Bounds.Height), window)!.Value;
                Require(end.X <= window.ClientSize.Width + 1 && end.Y <= window.ClientSize.Height + 1, "Done is outside the window.");
            }
        }
        finally { window.Close(); Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }

    private static TextBlock Label(string text, double size, IBrush? color = null) => new()
    { Text = text, FontFamily = Body, FontSize = size, Foreground = color ?? Muted, TextWrapping = TextWrapping.Wrap };
    private static TextBox Editor(string name, string text, string watermark)
    {
        var field = new TextBox { Text = text, Watermark = watermark, FontFamily = Body, FontSize = 13,
            MinHeight = 36, MinWidth = 0, Padding = new(10, 6), VerticalContentAlignment = VerticalAlignment.Center, Classes = { "editorField" } };
        AutomationProperties.SetName(field, name); return field;
    }
    private static Button Action(string text, bool primary = false)
    {
        var button = new Button { Content = text, FontFamily = Body, FontSize = 13, MinHeight = 36, Padding = new(12, 6), Classes = { "editorAction" } };
        if (primary) button.Classes.Add("editorPrimary"); return button;
    }
    private static StackPanel Field(string title, Control field) => new() { Spacing = 6, Children = { Label(title, 12, Ink), field } };
    private static Border Card(string title, params Control[] controls)
    {
        var body = new StackPanel { Spacing = 10, Children = { Label(title, 18, Ink) } };
        foreach (var control in controls) body.Children.Add(control);
        return new Border { Background = Brush.Parse("#40FFF6E4"), BorderBrush = Rule, BorderThickness = new(1), CornerRadius = new(7),
            Padding = new(16), VerticalAlignment = VerticalAlignment.Top, Child = body };
    }
    private static (Border Box, TextBlock Title, TextBlock Detail) Status()
    {
        var state = (Box: new Border { BorderThickness = new(1), CornerRadius = new(5), Padding = new(12) }, Title: Label("", 13), Detail: Label("", 12));
        state.Box.Child = new StackPanel { Spacing = 4, Children = { state.Title, state.Detail } }; return state;
    }
    private static void Feedback((Border Box, TextBlock Title, TextBlock Detail) state, string title, string detail, string tone = "normal")
    {
        state.Title.Text = title; state.Detail.Text = detail; state.Title.Foreground = tone == "error" ? Error : tone == "warning" ? Amber : Teal;
        state.Box.Background = Brush.Parse(tone == "error" ? "#11913F32" : tone == "warning" ? "#11865C15" : "#112D7169");
        state.Box.BorderBrush = Brush.Parse(tone == "error" ? "#55913F32" : tone == "warning" ? "#55865C15" : "#552D7169");
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
