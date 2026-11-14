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

// Native design fixture. No controller, persistence subscriber, file picker or real export.
internal static class SettingsObsidianPreview
{
    private const string Output = "design/one-piece-v2/settings/obsidian-native-preview";
    private const string SampleFolder = @"C:\Notes\My vault\Time";
    private static readonly FontFamily Body = StopwatchTypography.Resolve("modern-deck").NameFont;
    private static readonly IBrush Ink = Brush.Parse("#102B3B"), Muted = Brush.Parse("#655539"),
        Rule = Brush.Parse("#509A7B48"), Paper = Brush.Parse("#F7E8C7"), Teal = Brush.Parse("#2D7169"), Error = Brush.Parse("#913F32");

    internal static void Run()
    {
        Require(Application.Current is FrameSizePreviewApp, "The Obsidian preview requires the isolated app.");
        Application.Current!.Styles.Add(new FluentTheme());
        foreach (string resource in new[] { "avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml",
            "avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml" })
            Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")) { Source = new Uri(resource) });
        Directory.CreateDirectory(Output);
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"stopwatch-obsidian-preview-{Guid.NewGuid():N}.json"));
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
            ObsidianAutoSyncEnabled = true, ObsidianLogUnnamedTimers = true,
            ObsidianVaultFolder = SampleFolder, ObsidianExportFileName = ObsidianLogSync.DefaultFileName };
        CallDesktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
        CallDesktop("DesktopPageAppearance", "Initialize", settings);
        var window = new SettingsWindow(settings)
        {
            ShowActivated = false, ShowInTaskbar = false, Width = compact ? 840 : 1120, Height = compact ? 600 : 720
        };
        try
        {
            var navigation = window.FindControl<ListBox>("NavigationList")!;
            navigation.SelectedItem = navigation.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, "Obsidian"));
            typeof(SettingsWindow).GetMethod("RestoreObsidianPage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            var host = window.FindControl<Grid>("SettingsColumns")!;
            host.IsVisible = true;
            var automatic = window.FindControl<CheckBox>("ObsidianAutoSyncCheck")!;
            var unnamed = window.FindControl<CheckBox>("ObsidianLogUnnamedCheck")!;
            var folder = window.FindControl<TextBox>("ObsidianFolderTextBox")!;
            var filename = window.FindControl<TextBox>("ObsidianFileNameTextBox")!;
            var path = window.FindControl<TextBlock>("ObsidianPathPreviewText")!;
            foreach (var control in new Control[] { automatic, unnamed, folder, filename, path })
                ((Panel)control.Parent!).Children.Remove(control);
            host.Children.Clear();
            host.Background = Brushes.Transparent;
            window.Show();
            ClockOverlayChecks.Pump(60);
            bool stacked = host.Bounds.Width < 680;

            Choice(automatic, "Automatic export", "Keep completed timer sessions up to date in your Markdown folder.");
            Choice(unnamed, "Include unnamed timers", "Use names such as “Timer 1” and “Timer 2”.");
            var preferences = Card("Export preferences", automatic, new Border { Height = 1, Background = Rule }, unnamed,
                Label("You can export all history manually even when automatic export is off.", 12));
            foreach (var field in new[] { folder, filename })
            {
                field.Classes.Add("editorField");
                field.FontFamily = Body; field.FontSize = 13; field.Height = 36;
                field.MinWidth = 0; field.Margin = new(0); field.Padding = new(10, 6);
            }
            folder.Watermark = "Choose your vault or a Markdown folder";
            filename.Watermark = ObsidianLogSync.DefaultFileName;
            AutomationProperties.SetName(folder, "Target folder");
            AutomationProperties.SetName(filename, "File name");
            var browse = Action("Browse…");
            browse.Click += (_, _) => folder.Text = SampleFolder;
            var folderRow = new Grid { ColumnDefinitions = new("*,8,Auto"), Children = { folder, browse } };
            Grid.SetColumn(browse, 2);
            path.FontFamily = Body; path.FontSize = 12; path.Foreground = Ink;
            path.Margin = new(0); path.TextWrapping = TextWrapping.Wrap;
            var pathBox = new Border { Background = Paper, BorderBrush = Rule, BorderThickness = new(1),
                CornerRadius = new(5), Padding = new(12), Child = new StackPanel { Spacing = 6, Children = { Label("FULL FILE PATH", 10), path } } };
            var export = Action("Export all history");
            export.Classes.Add("editorPrimary");
            export.HorizontalAlignment = HorizontalAlignment.Left;
            var statusTitle = Label("Destination selected", 13, Teal);
            var statusDetail = Label("Export completed sessions to this Markdown file.", 12);
            var status = new Border { Background = Brush.Parse("#112D7169"), BorderBrush = Brush.Parse("#552D7169"),
                BorderThickness = new(1), CornerRadius = new(5), Padding = new(12),
                Child = new StackPanel { Spacing = 4, Children = { statusTitle, statusDetail } } };
            // shortcut: export feedback is sample data, wire the real result only after design approval.
            void Feedback(string title, string detail, bool failed = false)
            {
                statusTitle.Text = title; statusDetail.Text = detail;
                statusTitle.Foreground = failed ? Error : Teal;
                status.Background = Brush.Parse(failed ? "#11913F32" : "#112D7169");
                status.BorderBrush = Brush.Parse(failed ? "#55913F32" : "#552D7169");
            }
            export.Click += (_, _) => Feedback("Export complete", "42 completed sessions saved · 14:32");
            var destination = Card("Destination & export", Field("Target folder", folderRow),
                Label("Choose your Obsidian vault, a subfolder, or any Markdown folder.", 12),
                Field("File name", filename), pathBox, export, status);
            var cards = new Grid { ColumnDefinitions = new(stacked ? "*" : "0.85*,16,1.15*"),
                RowDefinitions = new(stacked ? "Auto,14,Auto" : "Auto"), Children = { preferences, destination } };
            Grid.SetColumn(destination, stacked ? 0 : 2);
            Grid.SetRow(destination, stacked ? 2 : 0);
            var heading = new StackPanel { Spacing = 4, Margin = new(0, 0, 0, 16), Children =
                { Label("Obsidian / Markdown", 23, Ink), Label("Keep your time history in your notes.", 12) } };
            var scroll = new ScrollViewer { HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 5, 0),
                Content = new StackPanel { Children = { heading, cards } } };
            Grid.SetColumnSpan(scroll, 3);
            host.Children.Add(scroll);
            var done = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "SettingsDone");
            window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SettingsSaveStatus").Text = "Design preview · sample settings";
            ClockOverlayChecks.Pump(60);
            Require(path.Text == Path.Combine(SampleFolder, ObsidianLogSync.DefaultFileName), "The full path does not match the destination.");
            Require(scroll.Viewport.Height > 80 && scroll.Extent.Width <= scroll.Viewport.Width + 1, $"Obsidian page overflows at {percent}%.");
            var donePosition = done.TranslatePoint(default, window)!.Value;
            var doneEnd = done.TranslatePoint(new Point(done.Bounds.Width, done.Bounds.Height), window)!.Value;
            Require(doneEnd.X <= window.ClientSize.Width + 1 && doneEnd.Y <= window.ClientSize.Height + 1, "Done is outside the window.");
            if (percent == 90) Save(window, compact ? "compact-top" : "configured");
            export.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ClockOverlayChecks.Pump(30);
            if (!compact && percent == 90) Save(window, "exported");
            Feedback("Export failed", "Could not write to this folder. Check folder permissions or choose another location.", true);
            ClockOverlayChecks.Pump(30);
            if (!compact && percent == 90) Save(window, "export-error");
            folder.Text = "";
            automatic.IsChecked = false;
            unnamed.IsChecked = false;
            export.IsEnabled = false;
            Feedback("Choose a destination", "Select a folder before exporting your history.");
            ClockOverlayChecks.Pump(30);
            Require(!settings.ObsidianAutoSyncEnabled && !settings.ObsidianLogUnnamedTimers && settings.ObsidianVaultFolder == "", "The controls did not update the sample settings.");
            if (!compact && percent == 90) Save(window, "unconfigured");
            folder.Text = @"C:\Notes\My vault\Projects\Learning and development\Time tracking\Daily sessions";
            filename.Text = "Weekly review";
            ClockOverlayChecks.Pump(30);
            Require(path.Text == Path.Combine(folder.Text, "Weekly review.md"), "The Markdown extension was not added to the path preview.");
            Require(scroll.Extent.Width <= scroll.Viewport.Width + 1 && path.Bounds.Width <= pathBox.Bounds.Width, "The long destination overflowed.");
            export.IsEnabled = true;
            export.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            scroll.ScrollToEnd();
            ClockOverlayChecks.Pump(30);
            Require(done.TranslatePoint(default, window)!.Value == donePosition, "Done moved with the page content.");
            Require(export.Focus(), "The export button cannot receive keyboard focus.");
            if (compact && percent == 90) Save(window, "compact-destination");
            Console.WriteLine($"PASS Obsidian sample preferences, path, layout and fixed footer at {percent}% / {window.ClientSize.Width:0} × {window.ClientSize.Height:0} / {(stacked ? "stacked" : "two columns")}");
        }
        finally { window.Close(); CallDesktop("DesktopPageAppearance", "Shutdown"); }
    }

    private static TextBlock Label(string text, double size, IBrush? color = null) => new()
    { Text = text, FontFamily = Body, FontSize = size, Foreground = color ?? Muted, TextWrapping = TextWrapping.Wrap };

    private static void Choice(CheckBox check, string title, string detail)
    {
        check.Content = new StackPanel { Spacing = 4, Children = { Label(title, 14, Ink), Label(detail, 12) } };
        check.HorizontalContentAlignment = HorizontalAlignment.Stretch; check.VerticalContentAlignment = VerticalAlignment.Top;
        check.Margin = new(0); AutomationProperties.SetName(check, title);
    }

    private static Button Action(string text) => new() { Content = text, FontFamily = Body, FontSize = 13,
        Height = 36, Padding = new(12, 0), Classes = { "editorAction" } };

    private static StackPanel Field(string title, Control control) => new()
    { Spacing = 6, Children = { Label(title, 12, Ink), control } };

    private static Border Card(string title, params Control[] controls)
    {
        var body = new StackPanel { Spacing = 12, Children = { Label(title, 18, Ink) } };
        foreach (var control in controls) body.Children.Add(control);
        return new Border { Background = Brush.Parse("#40FFF6E4"), BorderBrush = Rule, BorderThickness = new(1),
            CornerRadius = new(7), Padding = new(16), VerticalAlignment = VerticalAlignment.Top, Child = body };
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
    { if (!condition) throw new InvalidOperationException(message); }
}
