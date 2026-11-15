using System.Reflection;
using System.Globalization;
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

// Drawing-only proposal: independent sample fields; no scheduler, real prompt, review window or network calls.
internal static class SettingsActivityWatchReviewPreview
{
    private const string Output = "design/one-piece-v2/settings/activitywatch-review-native-preview";
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
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"stopwatch-aw-review-preview-{Guid.NewGuid():N}.json"));
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

            var enabled = Choice("Show review prompts", "While Stopwatch is open."); enabled.IsChecked = false;
            var interval = Field("30", "Review interval in minutes");
            var snooze = Field("30", "Snooze after an ignored prompt, in minutes");
            var dismiss = Field("5", "Prompt display time in seconds");
            var minimum = Field("15", "Short activity threshold in seconds");
            var percent = Field("3", "Group activities below this percentage");
            var intervalHint = Label("", 11, Error); var snoozeHint = Label("", 11, Error); var dismissHint = Label("", 11, Error);
            var minimumHint = Label("", 11, Error); var percentHint = Label("", 11, Error);
            var autoDescription = ((StackPanel)enabled.Content!).Children.OfType<TextBlock>().Last();
            var timing = Card("Prompt timing", enabled,
                NumberRow("Review every", "", interval, "min", intervalHint),
                NumberRow("If ignored, remind after", "When the prompt closes untouched.", snooze, "min", snoozeHint),
                NumberRow("Keep prompt visible for", "Then snooze if there is no response.", dismiss, "sec", dismissHint));
            var durationHelp = Label("Rejoin matching work across quick switches and hide short activity.", 11);
            var percentHelp = Label("Combine smaller entries into Others.", 11);
            var detail = Card("Activity detail",
                NumberRow("Short activity threshold", durationHelp, minimum, "sec", minimumHint),
                NumberRow("Group activities below", percentHelp, percent, "%", percentHint));
            var left = new StackPanel { Spacing = 14, Children = { timing, detail } };

            var period = Label("14:00 – 14:30 · 30 minutes", 12, Ink);
            var reviewChoice = Action("Y  Review", true); var skipChoice = Action("Esc  Skip");
            foreach (var button in new[] { reviewChoice, skipChoice }) button.Margin = new(0, 0, 8, 0);
            AutomationProperties.SetName(reviewChoice, "Example: press Y to review"); AutomationProperties.SetName(skipChoice, "Example: press Escape to skip");
            var closing = Label("", 11);
            var prompt = new Border { Background = Brush.Parse("#80FFF8E8"), BorderBrush = Rule, BorderThickness = new(1), CornerRadius = new(5), Padding = new(12),
                Child = new StackPanel { Spacing = 8, Children = { Label("Time for a review", 17, Ink), period,
                    new WrapPanel { Children = { reviewChoice, skipChoice } }, closing } } };
            var outcomeTitle = Label("", 12, Teal); var outcomeDetail = Label("", 11);
            var example = Card("Prompt example", Label("EXAMPLE ONLY", 9), prompt, outcomeTitle, outcomeDetail);
            var manual = Action("Review now", true); manual.HorizontalAlignment = HorizontalAlignment.Left;
            var availability = Label("ActivityWatch detail is included when available.", 11);
            var onDemand = Card("Review on demand", Label("Review now also works when automatic prompts are off.", 12), manual,
                Label("Default shortcut: Win + F2, then V", 11), availability);
            var right = new StackPanel { Spacing = 14, Children = { example, onDemand } };
            var cards = new Grid { Children = { left, right } };
            var tabs = new WrapPanel();
            foreach (string title in new[] { "Connection & status", "Sync & export", "Review prompts" })
            {
                var tab = Action(title, title == "Review prompts"); tab.IsEnabled = title == "Review prompts";
                tab.Margin = new(0, 0, 8, 4); tabs.Children.Add(tab);
            }
            var heading = new StackPanel { Spacing = 4, Children = { Label("ActivityWatch", 23, Ink), Label("Decide when to review your activity and project time.", 12) } };
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
            window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Name == "SettingsSaveStatus").Text = "Design preview · sample settings only";
            string scenario = "ready"; bool activityWatch = true;
            bool Integer(TextBox field, TextBlock hint, int low, int high, string units, out int number)
            {
                bool valid = int.TryParse(field.Text, out number) && number >= low && number <= high;
                hint.Text = $"Enter a whole number from {low:N0} to {high:N0} {units}."; hint.IsVisible = !valid;
                return valid;
            }
            void Refresh()
            {
                bool automatic = enabled.IsChecked == true;
                interval.IsEnabled = snooze.IsEnabled = dismiss.IsEnabled = automatic;
                autoDescription.Text = automatic ? "While Stopwatch is open." : "Off by default · manual review still works.";
                autoDescription.Foreground = automatic ? Teal : Muted;
                bool intervalValid = Integer(interval, intervalHint, 1, 1440, "minutes", out int every);
                bool snoozeValid = Integer(snooze, snoozeHint, 1, 1440, "minutes", out int ignored);
                bool dismissValid = Integer(dismiss, dismissHint, 1, 120, "seconds", out int visible);
                if (!automatic) intervalHint.IsVisible = snoozeHint.IsVisible = dismissHint.IsVisible = false;
                bool durationValid = Integer(minimum, minimumHint, 0, 3600, "seconds", out int seconds);
                bool percentValid = double.TryParse(percent.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double percentage) &&
                    double.IsFinite(percentage) && percentage is >= 0 and <= 50;
                percentHint.Text = "Enter a number from 0 to 50%."; percentHint.IsVisible = !percentValid;
                durationHelp.Text = durationValid && seconds == 0 ? "0 uses the smallest filter: 1 second." : "Rejoin matching work across quick switches and hide short activity.";
                percentHelp.Text = percentValid && percentage == 0 ? "Show each activity separately." : "Combine smaller entries into Others.";
                manual.IsEnabled = durationValid && percentValid;
                bool timingValid = intervalValid && snoozeValid && dismissValid;
                reviewChoice.IsEnabled = skipChoice.IsEnabled = timingValid;
                period.Text = timingValid ? $"14:00 – {new TimeOnly(14, 0).AddMinutes(every):HH:mm} · {every} minutes" : "Correct the timing values to update this example.";
                closing.Text = timingValid ? $"Visible for {visible} sec · no response snoozes {ignored} min." : "Enter valid timing values to preview the prompt.";
                availability.Text = activityWatch ? "ActivityWatch detail is included when available." : "ActivityWatch is off. Reviews can still use stopwatch time.";
                availability.Foreground = activityWatch ? Muted : Amber;
                outcomeTitle.Foreground = scenario == "ignored" || scenario == "skipped" ? Amber : Teal;
                outcomeTitle.Text = scenario switch
                {
                    "ignored" => $"No response → remind again in {ignored} min",
                    "skipped" => $"Esc → prompt again in {every} min",
                    "review" => "Y → open Activity & Time Review",
                    "manual" => "Review now → open Activity & Time Review",
                    _ => automatic ? "Y reviews · Esc waits for the next interval" : "Automatic prompts are off"
                };
                outcomeDetail.Text = scenario == "review" || scenario == "manual"
                    ? "Review projects, allocate time, then add feelings if enabled."
                    : "Skipping or ignoring a prompt does not delete recorded time.";
            }
            foreach (var field in new[] { interval, snooze, dismiss, minimum, percent }) field.TextChanged += (_, _) => { scenario = "ready"; Refresh(); };
            enabled.IsCheckedChanged += (_, _) => { scenario = "ready"; Refresh(); };
            reviewChoice.Click += (_, _) => { if (reviewChoice.IsEnabled) { scenario = "review"; Refresh(); } };
            skipChoice.Click += (_, _) => { if (skipChoice.IsEnabled) { scenario = "skipped"; Refresh(); } };
            manual.Click += (_, _) => { if (manual.IsEnabled) { scenario = "manual"; Refresh(); } };
            void Capture(string name) { Pump(50); CheckFit(); if (scale == 90 && !compact) Save(window, name); }
            void CheckFit()
            {
                Require(scroll.Viewport.Height > 60 && scroll.Extent.Width <= scroll.Viewport.Width + 1, "Review preview overflows horizontally.");
                Require(cards.Children.All(c => c.Bounds.Right <= cards.Bounds.Width + 1), "Review columns overlap.");
                foreach (var button in new[] { manual, reviewChoice, skipChoice })
                {
                    var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().First();
                    var label = presenter.GetVisualDescendants().OfType<TextBlock>().First();
                    Require(label.TextLayout.WidthIncludingTrailingWhitespace <= presenter.Bounds.Width + 1, "A prompt action caption is clipped.");
                }
                var end = done.TranslatePoint(new Point(done.Bounds.Width, done.Bounds.Height), window)!.Value;
                Require(end.X <= window.ClientSize.Width + 1 && end.Y <= window.ClientSize.Height + 1, "Done is outside the window.");
            }

            Refresh(); Capture("default-off");
            Require(!interval.IsEnabled && !snooze.IsEnabled && !dismiss.IsEnabled && manual.IsEnabled && minimum.IsEnabled,
                "Disabling prompts disabled manual review or detail preferences.");
            enabled.IsChecked = true; Refresh(); Capture("enabled");
            Require(interval.Focus() && snooze.Focus() && dismiss.Focus() && minimum.Focus() && percent.Focus() && manual.Focus(), "Review controls cannot receive keyboard focus.");
            if (!compact && scale != 90) Save(window, "enabled-" + scale);
            if (compact && scale == 90) Save(window, "compact-timing");
            var footer = done.TranslatePoint(default, window); scroll.ScrollToEnd(); Pump(); CheckFit();
            Require(footer == done.TranslatePoint(default, window), "Done moved while scrolling the settings.");
            if (compact && scale == 90) Save(window, "compact-example"); scroll.Offset = default;
            interval.Text = "45"; snooze.Text = "10"; dismiss.Text = "8"; Pump(); Capture("timing-example");
            Require(closing.Text!.Contains("8 sec") && closing.Text.Contains("10 min"), "Example does not follow display and snooze settings.");
            Click(skipChoice); Require(outcomeTitle.Text!.Contains("45 min"), "Esc did not use the review interval."); Capture("skipped-example");
            scenario = "ignored"; Refresh(); Require(outcomeTitle.Text!.Contains("10 min"), "Ignoring did not use the snooze interval."); Capture("ignored-example");
            Click(reviewChoice); Require(scenario == "review", "Y example did not explain the review action.");
            Click(manual); Require(scenario == "manual", "Manual example did not explain the review action."); Capture("manual-example");
            activityWatch = false; scenario = "ready"; Refresh(); Require(manual.IsEnabled && interval.IsEnabled, "Integration state incorrectly gates review prompts."); Capture("activitywatch-off");
            activityWatch = true; interval.Text = "0"; Pump(); Require(manual.IsEnabled && intervalHint.IsVisible && !reviewChoice.IsEnabled, "Invalid timing blocks manual review or looks valid."); Capture("invalid-timing");
            interval.Text = "30"; snooze.Text = "30"; dismiss.Text = "5"; percent.Text = "NaN"; Pump();
            Require(!manual.IsEnabled && percentHint.IsVisible, "Non-finite activity percentage is accepted."); Capture("invalid-detail");
            percent.Text = "3"; minimum.Text = "3601"; Pump(); Require(!manual.IsEnabled, "Invalid duration is accepted.");
            minimum.Text = "0"; percent.Text = "0"; Pump(); Require(manual.IsEnabled && durationHelp.Text!.Contains("1 second") && percentHelp.Text!.Contains("separately"), "Zero thresholds have misleading semantics."); Capture("minimum-filters");
            minimum.Text = "15"; percent.Text = "3"; scenario = "ready"; Refresh(); scroll.Offset = default;
            if (compact && scale == 125)
            {
                settings.Typography.Global.SizePercent = 175; Desktop("DesktopTypography", "Apply", settings.Typography);
                Resize(); Pump(80); interval.BringIntoView(); Pump(); CheckFit();
                var text = interval.GetVisualDescendants().OfType<TextPresenter>().Single();
                var fieldScroll = interval.GetVisualDescendants().OfType<ScrollViewer>().Single();
                Require(text.TextLayout.Height <= fieldScroll.Viewport.Height + 1, "Enlarged numeric field clips text."); Save(window, "large-text");
            }
            Require(settingsChanges == 0 && !settings.PeriodicReviewEnabled && settings.PeriodicReviewIntervalMinutes == 30 &&
                settings.PeriodicReviewSnoozeMinutes == 30 && settings.PeriodicReviewAutoDismissSeconds == 5 &&
                settings.PeriodicReviewMinDurationSeconds == 15 && settings.PeriodicReviewMinActivityPercent == 3 && settings.LastPeriodicReviewCompletedUtc == null,
                "Drawing-only preview changed review settings or history.");
            Console.WriteLine($"PASS ActivityWatch review preview states, timing, focus and fixed footer at {scale}% / {(compact ? "compact" : "regular")}");
        }
        finally { window.Close(); Desktop("DesktopTypography", "Shutdown"); Desktop("DesktopPageAppearance", "Shutdown"); }
    }

    private static StackPanel NumberRow(string title, string detail, TextBox field, string unit, TextBlock hint)
        => NumberRow(title, Label(detail, 11), field, unit, hint);
    private static StackPanel NumberRow(string title, TextBlock detail, TextBox field, string unit, TextBlock hint)
    {
        detail.IsVisible = !string.IsNullOrEmpty(detail.Text);
        var label = new StackPanel { Spacing = 3, VerticalAlignment = VerticalAlignment.Center, Children = { Label(title, 13, Ink), detail } };
        var grid = new Grid { ColumnDefinitions = new("*,10,72,6,Auto"), Children = { label, field, Label(unit, 12, Ink) } };
        Grid.SetColumn(field, 2); Grid.SetColumn(grid.Children[2], 4);
        field.VerticalAlignment = grid.Children[2].VerticalAlignment = VerticalAlignment.Center;
        return new StackPanel { Spacing = 4, Children = { grid, hint } };
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
