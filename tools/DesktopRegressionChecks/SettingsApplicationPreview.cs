using System.Reflection;
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

// Native design fixture. The Settings window has no controller or persistence subscriber.
internal static class SettingsApplicationPreview
{
    private const string Output = "design/one-piece-v2/settings/application-native-preview";
    private static readonly FontFamily Body = StopwatchTypography.Resolve("modern-deck").NameFont;
    private static readonly IBrush Ink = Brush.Parse("#102B3B");
    private static readonly IBrush Muted = Brush.Parse("#655539");
    private static readonly IBrush Rule = Brush.Parse("#509A7B48");
    private static readonly IBrush Paper = Brush.Parse("#F7E8C7");

    internal static void Run()
    {
        Require(Application.Current is FrameSizePreviewApp, "The Application preview requires the isolated app.");
        Application.Current!.Styles.Add(new FluentTheme());
        foreach (string resource in new[] { "avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml",
            "avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml" })
            Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")) { Source = new Uri(resource) });
        Directory.CreateDirectory(Output);
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"stopwatch-application-preview-{Guid.NewGuid():N}.json"));
        try
        {
            foreach (int percent in new[] { 70, 90, 125 })
            foreach (bool compact in new[] { false, true })
                Render(percent, compact);
        }
        finally { TelegramOutboxStore.SetCustomFilePath(null); }
    }

    private static void Render(int percent, bool compact)
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = percent };
        CallDesktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
        CallDesktop("DesktopPageAppearance", "Initialize", settings);
        var window = new SettingsWindow(settings)
        {
            ShowActivated = false, ShowInTaskbar = false, Width = compact ? 840 : 1120, Height = compact ? 600 : 720
        };
        try
        {
            var navigation = window.FindControl<ListBox>("NavigationList")!;
            navigation.SelectedItem = navigation.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, "Application"));
            typeof(SettingsWindow).GetMethod("RestoreApplicationPage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            var host = window.FindControl<Grid>("SettingsColumns")!;
            host.IsVisible = true;
            var startup = window.FindControl<CheckBox>("StartWithWindowsCheck")!;
            var closeAction = window.FindControl<ComboBox>("CloseActionCombo")!;
            ((Panel)startup.Parent!).Children.Remove(startup);
            host.Children.Clear();
            host.Background = Brushes.Transparent;
            window.Show();
            ClockOverlayChecks.Pump(60);
            bool stacked = host.Bounds.Width < 680;

            startup.Content = new StackPanel { Spacing = 4, Children =
            {
                Label("Start with Windows", 14, Ink), Label("Open Stopwatch when you sign in.", 12)
            } };
            startup.HorizontalContentAlignment = HorizontalAlignment.Stretch;
            startup.VerticalContentAlignment = VerticalAlignment.Top;
            startup.Margin = new(0, 4, 0, 0);
            AutomationProperties.SetName(startup, "Start with Windows");
            var startupCard = Card("Startup", startup);

            string[] names = ["Ask every time", "Minimize to tray", "Exit Stopwatch"];
            string[] descriptions =
            [
                "Choose whether to minimize or exit each time you close the controller.",
                "Hide the controller. Timers and overlays keep running.",
                "Close the app and all its overlays."
            ];
            string[] values = [CloseActionChoice.Ask, CloseActionChoice.Minimize, CloseActionChoice.Exit];
            var options = new RadioButton[3];
            var rows = new Border[3];
            var titles = new TextBlock[3];
            var details = new TextBlock[3];
            for (int i = 0; i < options.Length; i++)
            {
                int index = i;
                titles[i] = Label(names[i], 14, Ink);
                details[i] = Label(descriptions[i], 12);
                options[i] = new RadioButton
                {
                    GroupName = "ApplicationCloseAction", HorizontalAlignment = HorizontalAlignment.Stretch,
                    HorizontalContentAlignment = HorizontalAlignment.Stretch, VerticalContentAlignment = VerticalAlignment.Top,
                    Content = new StackPanel { Spacing = 4, Children = { titles[i], details[i] } }
                };
                AutomationProperties.SetName(options[i], names[i]);
                rows[i] = new Border { Background = Paper, BorderBrush = Rule, BorderThickness = new(1), CornerRadius = new(5), Padding = new(12), Child = options[i] };
                options[i].IsCheckedChanged += (_, _) =>
                {
                    if (options[index].IsChecked != true) return;
                    closeAction.SelectedIndex = index;
                    for (int choice = 0; choice < rows.Length; choice++)
                    {
                        bool selected = choice == index;
                        rows[choice].Background = selected ? Ink : Paper;
                        titles[choice].Foreground = selected ? Paper : Ink;
                        details[choice].Foreground = selected ? Paper : Muted;
                    }
                };
            }
            var closeCard = Card("Close button", Label("When you close the main controller", 12), rows[0], rows[1], rows[2]);
            var cards = new Grid
            {
                ColumnDefinitions = new(stacked ? "*" : "0.85*,16,1.15*"),
                RowDefinitions = new(stacked ? "Auto,14,Auto" : "Auto"),
                Children = { startupCard, closeCard }
            };
            Grid.SetColumn(closeCard, stacked ? 0 : 2);
            Grid.SetRow(closeCard, stacked ? 2 : 0);
            var heading = new StackPanel { Spacing = 4, Margin = new(0, 0, 0, 16), Children =
            {
                Label("Application", 23, Ink), Label("Choose how Stopwatch opens and closes.", 12)
            } };
            var scroll = new ScrollViewer
            {
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                Content = new StackPanel { Children = { heading, cards } }
            };
            Grid.SetColumnSpan(scroll, 3);
            host.Children.Add(scroll);
            var footer = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "SettingsDone");
            window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SettingsSaveStatus").Text = "Design preview · sample settings";

            for (int choice = 0; choice < options.Length; choice++)
            {
                options[choice].IsChecked = true;
                startup.IsChecked = choice == 1;
                ClockOverlayChecks.Pump(50);
                Require(settings.CloseAction == values[choice] && settings.StartWithWindows == (choice == 1), "Sample settings did not follow the selected controls.");
                Require(scroll.Viewport.Height > 80 && scroll.Extent.Width <= scroll.Viewport.Width + 1, $"Application page overflows at {percent}%.");
                var footerPosition = footer.TranslatePoint(default, window)!.Value;
                var footerEnd = footer.TranslatePoint(new Point(footer.Bounds.Width, footer.Bounds.Height), window)!.Value;
                Require(footerEnd.X <= window.ClientSize.Width + 1 && footerEnd.Y <= window.ClientSize.Height + 1, "Done is outside the window.");
                if (!compact && percent == 90) Save(window, new[] { "ask", "tray", "exit" }[choice]);
                scroll.ScrollToEnd();
                ClockOverlayChecks.Pump(30);
                Require(footer.TranslatePoint(default, window)!.Value == footerPosition, "The Done footer moved when scrolling.");
                if (compact && percent == 90 && choice == 1) Save(window, "compact-tray");
                scroll.Offset = default;
            }
            Require(options[2].Focus(), "Close choices cannot receive keyboard focus.");
            Console.WriteLine($"PASS Application close choices and startup toggle at {percent}% / {window.ClientSize.Width:0} × {window.ClientSize.Height:0} / {(stacked ? "stacked" : "two columns")}");
        }
        finally
        {
            window.Close();
            CallDesktop("DesktopPageAppearance", "Shutdown");
        }
    }

    private static TextBlock Label(string text, double size, IBrush? color = null) => new()
    {
        Text = text, FontFamily = Body, FontSize = size, Foreground = color ?? Muted, TextWrapping = TextWrapping.Wrap
    };

    private static Border Card(string title, params Control[] content)
    {
        var body = new StackPanel { Spacing = 12, Children = { Label(title, 18, Ink) } };
        foreach (var control in content) body.Children.Add(control);
        return new Border
        {
            Background = Brush.Parse("#40FFF6E4"), BorderBrush = Rule, BorderThickness = new(1), CornerRadius = new(7),
            Padding = new(16), VerticalAlignment = VerticalAlignment.Top, Child = body
        };
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

    private static void CallDesktop(string type, string method, params object[] arguments) =>
        typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!
            .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, arguments);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
