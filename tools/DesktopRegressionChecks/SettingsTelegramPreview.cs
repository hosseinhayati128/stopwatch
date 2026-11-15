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

// Native design fixture: sample settings, isolated outbox, no controller or real Telegram actions.
internal static class SettingsTelegramPreview
{
    private const string Output = "design/one-piece-v2/settings/telegram-native-preview";
    private const string SampleToken = "sample-preview-token";
    private static readonly FontFamily Body = StopwatchTypography.Resolve("modern-deck").NameFont;
    private static readonly IBrush Ink = Brush.Parse("#102B3B"), Muted = Brush.Parse("#655539"),
        Rule = Brush.Parse("#509A7B48"), Teal = Brush.Parse("#2D7169"), Error = Brush.Parse("#913F32");

    internal static void Run()
    {
        Require(Application.Current is FrameSizePreviewApp, "Telegram previews require the isolated app.");
        Application.Current!.Styles.Add(new FluentTheme());
        foreach (string resource in new[] { "avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml",
            "avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml" })
            Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")) { Source = new Uri(resource) });
        Directory.CreateDirectory(Output);
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"stopwatch-telegram-preview-{Guid.NewGuid():N}.json"));
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
            TelegramEnabled = true, TelegramBotToken = SampleToken, TelegramChatId = "-1000000000000",
            TelegramNotesTopicId = "12", TelegramTodosTopicId = "24", TelegramRemindersTopicId = "",
            ObsidianVaultFolder = @"C:\Notes\My vault" };
        Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
        Desktop("DesktopPageAppearance", "Initialize", settings);
        Desktop("DesktopTypography", "Initialize", settings.Typography);
        var window = new SettingsWindow(settings) { ShowActivated = false, ShowInTaskbar = false,
            Width = compact ? 840 : 1120, Height = compact ? 600 : 720 };
        try
        {
            var nav = window.FindControl<ListBox>("NavigationList")!;
            nav.SelectedItem = nav.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, "Telegram"));
            typeof(SettingsWindow).GetMethod("RestoreTelegramPage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            var host = window.FindControl<Grid>("SettingsColumns")!;
            host.IsVisible = true;
            var enabled = window.FindControl<CheckBox>("TelegramEnabledCheck")!;
            var token = window.FindControl<TextBox>("TelegramBotTokenTextBox")!;
            var chat = window.FindControl<TextBox>("TelegramChatIdTextBox")!;
            var notes = window.FindControl<TextBox>("TelegramNotesTopicTextBox")!;
            var todos = window.FindControl<TextBox>("TelegramTodosTopicTextBox")!;
            var reminders = window.FindControl<TextBox>("TelegramRemindersTopicTextBox")!;
            TextBox[] fields = [token, chat, notes, todos, reminders];
            Control[] moved = [enabled, .. fields];
            foreach (var control in moved) ((Panel)control.Parent!).Children.Remove(control);
            host.Children.Clear(); host.Background = Brushes.Transparent;
            window.Show(); ClockOverlayChecks.Pump(50);
            bool stacked = host.Bounds.Width < 720;
            enabled.Content = new StackPanel { Spacing = 3, Children =
                { Label("Enable Telegram sync", 14, Ink), Label("Send notes, todos, and reminders to Telegram.", 12) } };
            enabled.Margin = default; enabled.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            enabled.VerticalContentAlignment = VerticalAlignment.Top;
            AutomationProperties.SetName(enabled, "Enable Telegram sync");
            foreach (var field in fields)
            {
                field.Classes.Add("editorField"); field.FontFamily = Body; field.FontSize = 13;
                field.Height = double.NaN; field.MinHeight = 36; field.MinWidth = 0;
                field.Margin = default; field.Padding = new(10, 6); Grid.SetColumn(field, 0);
            }
            token.PasswordChar = '●'; token.Watermark = "Bot token from @BotFather";
            chat.Watermark = "Group or chat ID";
            AutomationProperties.SetName(token, "Bot token"); AutomationProperties.SetName(chat, "Group or chat ID");
            var reveal = Action("Show");
            reveal.MinWidth = 60;
            AutomationProperties.SetName(reveal, "Show bot token");
            reveal.Click += (_, _) =>
            {
                bool show = token.PasswordChar != '\0';
                token.PasswordChar = show ? '\0' : '●'; reveal.Content = show ? "Hide" : "Show";
                AutomationProperties.SetName(reveal, show ? "Hide bot token" : "Show bot token");
            };
            var tokenRow = new Grid { ColumnDefinitions = new("*,8,Auto"), Children = { token, reveal } };
            Grid.SetColumn(reveal, 2);
            var test = Action("Test connection", true);
            test.HorizontalAlignment = HorizontalAlignment.Left;
            var connectionStatus = Status("Ready to test", "Check the bot and your chosen destination.");
            // shortcut: network and delivery feedback are samples, wire real handlers after design approval.
            test.Click += (_, _) => Feedback(connectionStatus, "Connection verified", "@sample_bot · Test message sent to Quick notes.");
            var connection = Card("Connection", enabled, Field("Bot token", tokenRow),
                Field("Group / chat ID", chat), Label("Add your bot to the group before testing.", 12),
                test, Label("Sends a test message to the Quick notes destination.", 12), connectionStatus.Box);

            var topicRows = new StackPanel { Spacing = 8 };
            foreach (var (title, field) in new[] { ("Quick notes", notes), ("Todos", todos), ("Reminders", reminders) })
            {
                field.Watermark = "Main chat";
                AutomationProperties.SetName(field, title + " topic ID");
                var label = Label(title, 13, Ink); label.VerticalAlignment = VerticalAlignment.Center;
                var row = new Grid { ColumnDefinitions = new("*,12,130"), Children = { label, field } };
                Grid.SetColumn(field, 2); topicRows.Children.Add(row);
            }
            var topics = Card("Topics", Label("Choose a topic ID for each type of message.", 12), topicRows,
                Label("Leave blank to use General / the main chat.", 12));
            var deliveryStatus = Status("All caught up", "No items waiting to be sent.");
            var send = Action("Send pending", true);
            var scan = Action("Scan vault");
            send.Margin = scan.Margin = new(0, 0, 8, 0);
            var actions = new WrapPanel { Orientation = Orientation.Horizontal, Children = { send, scan } };
            var delivery = Card("Delivery", deliveryStatus.Box, actions,
                Label("Scan your vault’s last 3 days. Review unsent notes before sending.", 12));
            int pending = 0;
            void RefreshActions()
            {
                bool ready = !string.IsNullOrWhiteSpace(token.Text) && !string.IsNullOrWhiteSpace(chat.Text);
                test.IsEnabled = !string.IsNullOrWhiteSpace(token.Text);
                send.IsEnabled = ready && enabled.IsChecked == true && pending > 0;
                scan.IsEnabled = ready;
            }
            send.Click += (_, _) => { pending = 0; Feedback(deliveryStatus, "All caught up", "3 sample items delivered. Nothing waiting."); RefreshActions(); };
            scan.Click += (_, _) => Feedback(deliveryStatus, "2 unsent notes found", "Review the matching notes before sending them.");
            token.TextChanged += (_, _) => RefreshActions(); chat.TextChanged += (_, _) => RefreshActions();
            enabled.IsCheckedChanged += (_, _) => RefreshActions(); RefreshActions();
            var right = new StackPanel { Spacing = 14, Children = { topics, delivery } };
            var cards = new Grid { ColumnDefinitions = new(stacked ? "*" : "1.05*,16,1*"),
                RowDefinitions = new(stacked ? "Auto,14,Auto" : "Auto"), Children = { connection, right } };
            Grid.SetColumn(right, stacked ? 0 : 2); Grid.SetRow(right, stacked ? 2 : 0);
            var heading = new StackPanel { Spacing = 4, Margin = new(0, 0, 0, 16), Children =
                { Label("Telegram", 23, Ink), Label("Connect your notes and keep delivery in view.", 12) } };
            var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 5, 0),
                Content = new StackPanel { Children = { heading, cards } } };
            Grid.SetColumnSpan(scroll, 3); host.Children.Add(scroll);
            var done = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "SettingsDone");
            window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SettingsSaveStatus").Text = "Design preview · sample settings and delivery";
            ClockOverlayChecks.Pump(60); CheckFit();
            var donePosition = done.TranslatePoint(default, window);
            Require(token.PasswordChar != '\0' && !send.IsEnabled, "The initial token or empty-queue state is wrong.");
            if (percent == 90) Save(window, compact ? "compact-top" : "configured");
            reveal.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(token.PasswordChar == '\0', "Show token did not reveal the sample value.");
            reveal.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            test.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ClockOverlayChecks.Pump(30);
            if (!compact && percent == 90) Save(window, "connected");
            pending = 3; RefreshActions();
            Feedback(connectionStatus, "Last test succeeded", "@sample_bot · Test message sent to Quick notes.");
            Feedback(deliveryStatus, "3 items pending", "Telegram couldn’t be reached. Items stay saved locally for retry.", true);
            ClockOverlayChecks.Pump(30); CheckFit();
            if (!compact && percent == 90) Save(window, "pending");
            scroll.ScrollToEnd(); ClockOverlayChecks.Pump(30);
            Require(done.TranslatePoint(default, window) == donePosition, "Done moved with the scroll content.");
            if (compact && percent == 90) Save(window, "compact-delivery");
            Require(send.Focus() && scan.Focus(), "Delivery actions cannot receive keyboard focus.");
            send.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(!send.IsEnabled, "The empty sample queue still enables Send pending.");
            scan.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ClockOverlayChecks.Pump(30);
            if (!compact && percent == 90) Save(window, "scan-result");
            Feedback(connectionStatus, "Check the bot token", "The sample token could not be verified. Update it and try again.", true);
            scroll.Offset = default; ClockOverlayChecks.Pump(30);
            if (!compact && percent == 90) Save(window, "connection-error");
            enabled.IsChecked = false; token.Text = ""; chat.Text = ""; notes.Text = ""; todos.Text = "";
            Feedback(connectionStatus, "Add your bot", "Enter a bot token and destination to get started.");
            Feedback(deliveryStatus, "Sync is off", "Enable sync when you’re ready to send new items.");
            ClockOverlayChecks.Pump(30); CheckFit();
            Require(!settings.TelegramEnabled && settings.TelegramBotToken == "" && settings.TelegramNotesTopicId == "" && !test.IsEnabled && !send.IsEnabled,
                "Sample preferences or unconfigured button states did not update.");
            if (!compact && percent == 90) Save(window, "unconfigured");
            if (compact && percent == 125)
            {
                token.Text = SampleToken; chat.Text = "-1000000000000";
                settings.Typography.Global.SizePercent = 175;
                Desktop("DesktopTypography", "Apply", settings.Typography);
                ClockOverlayChecks.Pump(80); CheckFit();
                foreach (var field in fields)
                {
                    var presenter = field.GetVisualDescendants().OfType<TextPresenter>().Single();
                    var viewport = field.GetVisualDescendants().OfType<ScrollViewer>().Single();
                    Require(presenter.TextLayout.Height <= viewport.Viewport.Height + 1, "A Telegram field clips enlarged text.");
                }
                foreach (var button in new[] { reveal, test, send, scan })
                {
                    var caption = button.GetVisualDescendants().OfType<TextBlock>().Single();
                    var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().Single();
                    Require(caption.TextLayout.WidthIncludingTrailingWhitespace <= presenter.Bounds.Width + 1,
                        "A Telegram action clips its enlarged label.");
                }
                chat.BringIntoView(); ClockOverlayChecks.Pump(30); Save(window, "large-fields");
            }
            Console.WriteLine($"PASS Telegram sample states, token masking, field fit and fixed footer at {percent}% / {window.ClientSize.Width:0} × {window.ClientSize.Height:0} / {(stacked ? "stacked" : "two columns")}");
            void CheckFit()
            {
                Require(scroll.Viewport.Height > 65 && scroll.Extent.Width <= scroll.Viewport.Width + 1, $"Telegram page overflows at {percent}%.");
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
        var button = new Button { Content = text, FontFamily = Body, FontSize = 13, Height = 36, Padding = new(12, 0), Classes = { "editorAction" } };
        if (primary) button.Classes.Add("editorPrimary");
        return button;
    }
    private static StackPanel Field(string title, Control field) => new() { Spacing = 6, Children = { Label(title, 12, Ink), field } };
    private static Border Card(string title, params Control[] controls)
    {
        var body = new StackPanel { Spacing = 10, Children = { Label(title, 18, Ink) } };
        foreach (var control in controls) body.Children.Add(control);
        return new Border { Background = Brush.Parse("#40FFF6E4"), BorderBrush = Rule, BorderThickness = new(1),
            CornerRadius = new(7), Padding = new(16), VerticalAlignment = VerticalAlignment.Top, Child = body };
    }
    private static (Border Box, TextBlock Title, TextBlock Detail) Status(string title, string detail)
    {
        var state = (Box: new Border { BorderThickness = new(1), CornerRadius = new(5), Padding = new(12) }, Title: Label(title, 13), Detail: Label(detail, 12));
        state.Box.Child = new StackPanel { Spacing = 4, Children = { state.Title, state.Detail } };
        Feedback(state, title, detail); return state;
    }
    private static void Feedback((Border Box, TextBlock Title, TextBlock Detail) state, string title, string detail, bool failed = false)
    {
        state.Title.Text = title; state.Detail.Text = detail; state.Title.Foreground = failed ? Error : Teal;
        state.Box.Background = Brush.Parse(failed ? "#11913F32" : "#112D7169"); state.Box.BorderBrush = Brush.Parse(failed ? "#55913F32" : "#552D7169");
    }
    private static void Save(SettingsWindow window, string name)
    {
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
    private static void Desktop(string type, string method, params object[] args) => typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!
        .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
