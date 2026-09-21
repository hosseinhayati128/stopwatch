using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using Expression = System.Linq.Expressions.Expression;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;
using StopwatchOverlay;
using StopwatchOverlay.Themes;

// Render the production XAML with synthetic data. No ControllerWindow constructor,
// App startup, native windows, settings, history, hotkeys or persistence are invoked.
internal static class Program
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    [STAThread]
    private static void Main(string[] args)
    {
        try { Run(args); }
        catch (Exception error) { Console.Error.WriteLine(error); Environment.Exit(1); }
    }

    private static void Run(string[] args)
    {
        string repo = Path.GetFullPath(args.FirstOrDefault(arg => !arg.StartsWith("--", StringComparison.Ordinal)) ?? ".");
        string output = Path.Combine(repo, "design", "pirate", "previews");
        Directory.CreateDirectory(output);
        // Fail locally rather than append a recoverable preview error to the user's log.
        typeof(App).Assembly.GetType("StopwatchOverlay.CrashLogger")!
            .GetField("_writeInProgress", BindingFlags.Static | BindingFlags.NonPublic)!.SetValue(null, 1);
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        XDocument application = XDocument.Load(Path.Combine(repo, "StopwatchOverlay", "App.xaml"));
        XElement dictionary = application.Descendants().First(e => e.Name.LocalName == "ResourceDictionary");
        foreach (XAttribute ns in application.Root!.Attributes().Where(a => a.IsNamespaceDeclaration))
            dictionary.SetAttributeValue(ns.Name, ns.Value);
        app.Resources = (ResourceDictionary)XamlReader.Parse(Prepare(dictionary.ToString()));
        AppThemeManager.Apply(AppThemeCatalog.Pirate);
        if (args.Contains("--typography-only"))
        {
            CaptureTypographySettings(output);
            return;
        }
        if (args.Contains("--notes-only"))
        {
            CaptureNotes(output);
            return;
        }
        CaptureFloatingClock(output);
        CaptureNavigatorSettings(output);
        if (args.Contains("--overlay-only")) return;

        XDocument markup = XDocument.Load(Path.Combine(repo, "StopwatchOverlay", "ControllerWindow.xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2006/xaml";
        markup.Root!.Attribute(x + "Class")!.Remove();
        foreach (XElement node in markup.Root.DescendantsAndSelf())
        {
            Type? type = typeof(Window).Assembly.GetType("System.Windows." + node.Name.LocalName)
                ?? typeof(Window).Assembly.GetType("System.Windows.Controls." + node.Name.LocalName)
                ?? typeof(Window).Assembly.GetType("System.Windows.Shapes." + node.Name.LocalName);
            foreach (XAttribute attribute in node.Attributes().ToArray())
                if (type?.GetEvent(attribute.Name.LocalName) != null) attribute.Remove();
        }
        var window = (Window)XamlReader.Parse(Prepare(markup.ToString()));
        var rail = Node<ListBox>(window, "TimerRailList");
        rail.ItemsSource = new[]
        {
            new { DisplayName = "Navigation", DisplaySummary = "00:00:00  ·  Paused" },
            new { DisplayName = "Reading", DisplaySummary = "00:00:00  ·  Paused" },
            new { DisplayName = "Illustration", DisplaySummary = "00:44:27  ·  Paused" },
            new { DisplayName = "Research", DisplaySummary = "00:00:00  ·  Paused" },
            new { DisplayName = "Programming", DisplaySummary = "00:00:00  ·  Paused" },
            new { DisplayName = "Writing", DisplaySummary = "00:00:00  ·  Paused" }
        };
        rail.SelectedIndex = 2;
        Node<TextBlock>(window, "ActiveWorkspaceTitle").Text = "Illustration";
        Node<TextBlock>(window, "TimeDisplay").Text = "00:44:27";
        Node<Button>(window, "ToggleOverlayButton").Content = "Hide overlay";
        Node<Button>(window, "LapButton").Content = "Add lap";
        Node<Button>(window, "StartStopButton").Content = "Start  ·  Win+F2 → Space";
        Node<Button>(window, "ResetButton").Content = "Reset  ·  Win+F2 → R";
        Node<TextBlock>(window, "CombinedRailStatus").Text = "Combined overlay · active timer only";
        Node<TextBlock>(window, "ShortcutHintText").Text = "Win+F2 → Space Start/Stop   R Reset   O Overlay   D Dashboard   W Controller";
        foreach ((int width, int height, string name) in new[]
        {
            (1200, 896, "controller-pirate"), (1040, 720, "controller-pirate-standard"),
            (640, 760, "controller-pirate-compact"), (560, 520, "controller-pirate-minimum")
        })
        {
            double logicalWidth = width / .9;
            bool compact = ControllerLayoutPolicy.UseCompactLayout(logicalWidth);
            Node<ColumnDefinition>(window, "TimerRailColumn").Width = new GridLength(compact ? 0 : Math.Clamp(logicalWidth * .29, 260, 348));
            Node<Border>(window, "TimerRail").Visibility = compact ? Visibility.Collapsed : Visibility.Visible;
            Capture(window, output, name, width, height);
            if (width == 560)
            {
                var scroll = Node<ScrollViewer>(window, "ControllerScrollViewer");
                scroll.ScrollToBottom();
                Capture(window, output, "controller-pirate-minimum-scrolled", width, height);
                if (scroll.VerticalOffset <= 0)
                    throw new InvalidOperationException("Minimum-size controller cannot scroll to its actions and laps.");
                scroll.ScrollToTop();
            }
        }

        // Exercise switching in the SAME tree and restore the selected theme.
        AppThemeManager.Apply(AppThemeCatalog.Midnight);
        Node<ColumnDefinition>(window, "TimerRailColumn").Width = new GridLength(260);
        Node<Border>(window, "TimerRail").Visibility = Visibility.Visible;
        Capture(window, output, "controller-midnight-regression", 1040, 720);
        if (PirateVisual.GetEnabled(window) || Node<TextBlock>(window, "TimeDisplay").Visibility != Visibility.Visible)
            throw new InvalidOperationException("Pirate styling did not reverse on theme switch.");
        if (((Grid)window.Content).Children.OfType<System.Windows.Shapes.Rectangle>().Any(c => c.Visibility != Visibility.Collapsed))
            throw new InvalidOperationException("Pirate corner artwork leaked into another theme.");
        AppThemeManager.Apply(AppThemeCatalog.Pirate);
        if (!PirateVisual.GetEnabled(window)) throw new InvalidOperationException("Pirate did not reapply.");
        Node<ColumnDefinition>(window, "TimerRailColumn").Width = new GridLength(348);
        Node<Border>(window, "TimerRail").Visibility = Visibility.Visible;
        Node<TextBlock>(window, "TimeDisplay").Text = "125:44:27.8";
        Node<TextBlock>(window, "ActiveWorkspaceTitle").Text = "A long project name that should stay inside its own header";
        Node<ListBox>(window, "LapListBox").ItemsSource = new[] { "03     00:44:27     +00:04:10", "02     00:40:17     +00:24:02", "01     00:16:15     +00:16:15" };
        Node<TextBlock>(window, "LapPlaceholder").Visibility = Visibility.Collapsed;
        Capture(window, output, "controller-pirate-long-timer-laps", 1200, 896);
        Node<RadioButton>(window, "CountdownModeRadio").IsChecked = true;
        Node<StackPanel>(window, "CountdownPanel").Visibility = Visibility.Visible;
        Node<TextBlock>(window, "TimeDisplay").Text = "00:05:00";
        Capture(window, output, "controller-pirate-countdown", 1040, 720);
        foreach (string control in new[] { "StartStopButton", "ResetButton", "LapButton", "ToggleOverlayButton" })
            Node<Button>(window, control).IsEnabled = false;
        Node<TextBlock>(window, "ActiveWorkspaceTitle").Text = "No active timer";
        Node<TextBlock>(window, "TimeDisplay").Text = "--:--";
        Node<StackPanel>(window, "CountdownPanel").Visibility = Visibility.Collapsed;
        Capture(window, output, "controller-pirate-empty", 1200, 896);
        CaptureSecondaryPages(output);
        CaptureNotes(output);
        Console.WriteLine("Rendered production-XAML previews; theme round-trip, minimum-size scrolling and scale interaction checks passed. User stores were not opened.");
    }

    private static void CaptureTypographySettings(string output)
    {
        var settings = new AppSettings
        {
            ThemeMode = AppThemeCatalog.Pirate, OverlayTheme = OverlayThemeCatalog.FollowApplicationTheme,
            UiScalePercent = 90, ObsidianAutoSyncEnabled = false, ObsidianVaultFolder = "", TextColor = "Theme default"
        };
        AppUiScale.Apply(settings.UiScalePercent);
        TypographyManager.Apply(settings.Typography);
        var inspector = new SettingsWindow(settings);
        var note = new NoteEntryWindow(NoteType.Note, settings);
        Node<TextBox>(note, "NoteInputBox").Text = "Chart the next island route and bring the sketchbook.";
        DateTime now = DateTime.Now;
        var history = new ProjectTimeHistory();
        history.AddManualInterval("Illustration", now.AddHours(-2).ToUniversalTime(), now.AddHours(-1).ToUniversalTime());
        var view = history.CreateView(now.ToUniversalTime());
        var dashboard = new ProjectDashboardWindow(() => view,
            (_, _, _) => throw new InvalidOperationException("Typography preview cannot edit records."),
            (_, _, _, _) => throw new InvalidOperationException("Typography preview cannot edit records."),
            _ => throw new InvalidOperationException("Typography preview cannot edit records."), () => true, () => null, settings);
        var dialog = new TimerNameWindow("Illustration", new[] { "Illustration", "Navigation" });
        var windows = new Window[] { inspector, note, dashboard, dialog };
        try
        {
            Subscribe(inspector, "SettingsChanged", () =>
            {
                AppThemeManager.Apply(settings.ThemeMode);
                TypographyManager.Apply(settings.Typography);
                foreach (Window window in windows) TypographyManager.ApplyWindow(window);
            });
            var navigation = Node<ListBox>(inspector, "NavigationList");
            navigation.SelectedItem = navigation.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, "Typography"));
            var expander = Node<Expander>(inspector, "TypographyOverridesExpander");
            if (expander.IsExpanded) throw new InvalidOperationException("Panel typography overrides must initially be collapsed.");
            var global = (StackPanel)Node<StackPanel>(inspector, "GlobalTypographyEditor").Children[0];
            var editors = Node<StackPanel>(inspector, "SectionTypographyEditors").Children.OfType<StackPanel>().ToArray();
            if (editors.Length != 6 || editors.Any(editor => LogicalControls<CheckBox>(editor).Single().IsChecked != false
                || editor.Children.OfType<StackPanel>().Single().IsEnabled))
                throw new InvalidOperationException("All six typography panels must initially inherit global settings.");
            CaptureTypography(inspector, output, "typography-settings-default", 1100, 850);
            var globalSize = LogicalControls<Slider>(global).Single();
            var globalColor = LogicalControls<ComboBox>(global).Single();
            var globalHex = LogicalControls<TextBox>(global).Single();
            var globalLabel = LogicalControls<TextBlock>(global).First(text => text.Text == "Font size");
            expander.IsExpanded = true;
            CaptureTypography(inspector, output, "typography-settings-expanded-top", 1100, 850);
            var scroll = Node<ScrollViewer>(inspector, "SettingsScrollViewer");
            scroll.ScrollToVerticalOffset(520);
            CaptureTypography(inspector, output, "typography-settings-expanded-middle", 1100, 850);
            scroll.ScrollToBottom();
            CaptureTypography(inspector, output, "typography-settings-expanded-bottom", 1100, 850);
            if (scroll.VerticalOffset <= 0) throw new InvalidOperationException("Typography panel overrides cannot scroll.");

            void CommitHex(TextBox text, string value)
            {
                text.Text = value;
                text.RaiseEvent(new KeyboardFocusChangedEventArgs(Keyboard.PrimaryDevice, Environment.TickCount, text, null)
                    { RoutedEvent = Keyboard.LostKeyboardFocusEvent });
            }
            CommitHex(globalHex, "#247c71");
            if (settings.Typography.Global.Color != "#247C71") throw new InvalidOperationException("Custom hex color did not commit through the editor.");
            CommitHex(globalHex, "not a color");
            if (settings.Typography.Global.Color != "#247C71") throw new InvalidOperationException("Invalid hex replaced the last valid color.");
            if (!LogicalControls<TextBlock>(global).Any(text => text.Text.StartsWith("Enter a color", StringComparison.Ordinal)
                && text.Visibility == Visibility.Visible)) throw new InvalidOperationException("Invalid custom color has no visible validation.");
            CommitHex(globalHex, "#247C71");
            if (globalColor.Items.Count < 8) throw new InvalidOperationException("Typography color presets are missing.");
            foreach (string theme in new[] { AppThemeCatalog.Midnight, AppThemeCatalog.Daylight, AppThemeCatalog.Pirate })
            {
                Node<ComboBox>(inspector, "ThemeCombo").SelectedItem = theme;
                foreach (var (choice, resource) in new[]
                {
                    (TypographySettings.ThemeText, "PrimaryTextBrush"),
                    (TypographySettings.ThemeAccent, "AccentBrush"),
                    (TypographySettings.ThemeMuted, "SecondaryTextBrush")
                })
                {
                    globalColor.SelectedItem = choice;
                    TypographyManager.ApplyWindow(inspector);
                    PumpTypography();
                    Color expected = ((SolidColorBrush)inspector.FindResource(resource)).Color;
                    if (((SolidColorBrush)globalLabel.Foreground).Color != expected)
                        throw new InvalidOperationException("Typography preset did not follow theme: " + theme + "/" + choice + " actual=" + globalLabel.Foreground + " expected=" + expected + " saved=" + settings.Typography.Global.Color);
                }
            }
            globalColor.SelectedItem = TypographySettings.ThemeDefault;
            globalSize.Value = 125;
            var notesEditor = editors[Array.FindIndex(TypographySettings.Scopes, scope => scope.Key == "Notes")];
            var manual = LogicalControls<CheckBox>(notesEditor).Single();
            var noteSize = LogicalControls<Slider>(notesEditor).Single();
            var noteHex = LogicalControls<TextBox>(notesEditor).Single();
            manual.IsChecked = true;
            noteSize.Value = 150;
            CommitHex(noteHex, "#963D26");
            CaptureTypography(note, output, "typography-notes-manual-150", 620, 620);
            var input = Node<TextBox>(note, "NoteInputBox");
            double baseSize = (double)input.GetAnimationBaseValue(Control.FontSizeProperty);
            if (Math.Abs(input.FontSize - baseSize * 1.5) > .01 || ((SolidColorBrush)input.Foreground).Color != Color.FromRgb(0x96, 0x3D, 0x26))
                throw new InvalidOperationException("Checked Notes typography did not reach the actual text input.");
            manual.IsChecked = false;
            TypographyManager.ApplyWindow(note);
            PumpTypography();
            if (Math.Abs(input.FontSize - baseSize * 1.25) > .01 || settings.Typography.Sections["Notes"].SizePercent != 150
                || settings.Typography.Sections["Notes"].Color != "#963D26")
                throw new InvalidOperationException("Unchecked Notes override did not inherit global while retaining its choices.");
            expander.IsExpanded = false;
            scroll.ScrollToTop();
            foreach (int percent in new[] { 125, 175 })
            {
                globalSize.Value = percent;
                CaptureTypography(inspector, output, "typography-settings-global-" + percent, 1100, 850);
                CaptureTypography(note, output, "typography-notes-global-" + percent, 620, 620);
                CaptureTypography(dashboard, output, "typography-dashboard-global-" + percent, 1120, 800);
                CaptureTypography(dialog, output, "typography-dialog-global-" + percent, 640, 460);
                if (Math.Abs(input.FontSize - baseSize * percent / 100d) > .01)
                    throw new InvalidOperationException("Global typography did not update the note input.");
                foreach (TextBlock text in new[] { Node<TextBlock>(dashboard, "TotalTrackedText"), Node<TextBlock>(dialog, "HeadingText"), globalLabel })
                    if (Math.Abs(text.FontSize - (double)text.GetAnimationBaseValue(TextBlock.FontSizeProperty) * percent / 100d) > .01)
                        throw new InvalidOperationException("Global typography did not reach a real page text presenter: " + text.Text);
            }
            globalSize.Value = 100;
            CommitHex(globalHex, "#247C71");
            CaptureTypography(inspector, output, "typography-settings-custom-color", 1100, 850);
            globalColor.SelectedItem = TypographySettings.ThemeDefault;
            CaptureTypography(inspector, output, "typography-settings-restored", 1100, 850);
            Console.WriteLine("Typography: collapsed defaults, six unchecked panels, scrollable overrides, real editor size/preset/custom/invalid hex interactions, three theme palettes, Notes inheritance with retained overrides, and 125/175% page captures passed. No user settings were read or written.");
        }
        finally
        {
            foreach (Window window in windows) window.Close();
            TypographyManager.Apply(new TypographySettings());
            AppThemeManager.Apply(AppThemeCatalog.Pirate);
            AppUiScale.Apply(90);
        }
    }

    private static IEnumerable<T> LogicalControls<T>(DependencyObject parent) where T : DependencyObject
    {
        foreach (object child in LogicalTreeHelper.GetChildren(parent))
        {
            if (child is T match) yield return match;
            if (child is DependencyObject dependency)
                foreach (T descendant in LogicalControls<T>(dependency)) yield return descendant;
        }
    }

    private static void CaptureTypography(Window window, string output, string name, int width, int height)
    {
        // Prime templates offscreen, then traverse the resulting real text presenters.
        Capture(window, output, name, width, height);
        TypographyManager.ApplyWindow(window);
        PumpTypography();
        Capture(window, output, name, width, height);
    }

    private static void PumpTypography()
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(40) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start();
        Dispatcher.PushFrame(frame);
    }

    private static void CaptureNotes(string output)
    {
        var settings = new AppSettings
        {
            ThemeMode = AppThemeCatalog.Pirate,
            OverlayTheme = OverlayThemeCatalog.FollowApplicationTheme,
            ObsidianAutoSyncEnabled = false,
            ObsidianVaultFolder = "",
            UiScalePercent = 100
        };
        AppThemeManager.Apply(AppThemeCatalog.Pirate);
        AppUiScale.Apply(100);
        string sample = "Plot the next island route.\nBring the sketchbook, spare pencils, and the blue compass.";
        foreach (NoteType mode in Enum.GetValues<NoteType>())
        {
            var entry = new NoteEntryWindow(mode, settings);
            try
            {
                if (!PirateVisual.GetEnabled(entry))
                    throw new InvalidOperationException("Note entry lost its resolved one piece overlay theme.");
                string prefix = "notes-add-" + mode.ToString().ToLowerInvariant();
                Capture(entry, output, prefix + "-empty", 620, 620);
                TextBox input = Node<TextBox>(entry, "NoteInputBox");
                input.ApplyTemplate();
                if (input.Template.FindName("PlaceholderText", input) is not TextBlock placeholder
                    || string.IsNullOrWhiteSpace(placeholder.Text))
                    throw new InvalidOperationException("Note entry placeholder is unavailable before Loaded: " + mode);
                input.Text = sample;
                Capture(entry, output, prefix + "-filled", 620, 620);
                VerifyNotesLayout(entry, "EntryHeader", "PaperSurface", "SaveButton");
                Capture(entry, output, prefix + "-compact", 420, 380);
                VerifyNotesLayout(entry, "EntryHeader", "PaperSurface", "SaveButton");
                AppUiScale.Apply(125);
                Capture(entry, output, prefix + "-125", 620, 620);
                VerifyNotesLayout(entry, "EntryHeader", "PaperSurface", "SaveButton");
                Capture(entry, output, prefix + "-compact-125", 420, 380);
                VerifyNotesLayout(entry, "EntryHeader", "PaperSurface", "SaveButton");
                TypographyManager.Apply(new TypographySettings { Global = new TypographyStyle { SizePercent = 175 } });
                TypographyManager.ApplyWindow(entry);
                Capture(entry, output, prefix + "-125-text-175", 620, 620);
                VerifyNotesLayout(entry, "EntryHeader", "PaperSurface", "SaveButton");
                Capture(entry, output, prefix + "-compact-125-text-175", 420, 380);
                VerifyNotesLayout(entry, "EntryHeader", "PaperSurface", "NoteInputBox", "SaveButton");
                if (input.Text != sample)
                    throw new InvalidOperationException("Entry text changed during layout/scale changes.");
            }
            finally { entry.Close(); AppUiScale.Apply(100); TypographyManager.Apply(new TypographySettings()); }
        }

        DateTime today = DateTime.Today;
        var entries = new List<NoteEntry>
        {
            new(NoteType.Note, today.AddHours(14).AddMinutes(20), "The harbor sketch is ready for tomorrow's journal."),
            new(NoteType.Todo, today.AddHours(13).AddMinutes(45), "Pack the watercolor kit"),
            new(NoteType.Todo, today.AddHours(12).AddMinutes(10), "Refill the compass notebook", true),
            new(NoteType.Reminder, today.AddHours(11).AddMinutes(30), "Review the tide chart before the afternoon sailing lesson, then bring the updated route and a spare notebook to the meeting."),
            new(NoteType.Note, today.AddDays(-1).AddHours(18), "Ideas for the next illustration: lanterns, a quiet pier, and a sky full of stars.")
        };
        var viewer = new NotesViewerWindow(settings);
        try
        {
            Node<TextBlock>(viewer, "VaultLocationText").Text = "Vault: Voyage/Notes";
            Node<TextBlock>(viewer, "CountdownText").Text = "Auto-closing in 29s";
            SetNotes(viewer, entries);
            foreach (var (name, filter) in new (string, NoteType?)[]
            {
                ("All", null), ("Todos", NoteType.Todo), ("Notes", NoteType.Note), ("Reminders", NoteType.Reminder)
            })
            {
                Node<RadioButton>(viewer, "Filter" + name + "Radio").IsChecked = true;
                Invoke(viewer, "RenderEntries");
                if ((NoteType?)viewer.GetType().GetField("_currentFilter", Hidden)!.GetValue(viewer) != filter)
                    throw new InvalidOperationException("Viewer filter did not update: " + name);
                VerifyNotesContents(viewer, entries.Where(e => filter is null || e.Type == filter).ToList());
                Capture(viewer, output, "notes-view-" + name.ToLowerInvariant(), 680, 650);
                VerifyNotesLayout(viewer, "ViewerHeader", "PaperSurface", "CloseButton");
            }
            Node<RadioButton>(viewer, "FilterAllRadio").IsChecked = true;
            Invoke(viewer, "RenderEntries");
            Capture(viewer, output, "notes-view-compact", 420, 380);
            VerifyNotesLayout(viewer, "ViewerHeader", "PaperSurface", "CloseButton");
            AppUiScale.Apply(125);
            Capture(viewer, output, "notes-view-125", 680, 650);
            VerifyNotesLayout(viewer, "ViewerHeader", "PaperSurface", "CloseButton");
            Capture(viewer, output, "notes-view-compact-125", 420, 380);
            VerifyNotesLayout(viewer, "ViewerHeader", "PaperSurface", "CloseButton");
            TypographyManager.Apply(new TypographySettings { Global = new TypographyStyle { SizePercent = 175 } });
            TypographyManager.ApplyWindow(viewer);
            Capture(viewer, output, "notes-view-125-text-175", 680, 650);
            VerifyNotesLayout(viewer, "ViewerHeader", "PaperSurface", "CloseButton");
            if (Ancestor<ScrollViewer>(Node<StackPanel>(viewer, "ItemsContainer")).ViewportHeight <= 0)
                throw new InvalidOperationException("Large typography consumed the note list viewport.");
            TypographyManager.Apply(new TypographySettings());
            AppUiScale.Apply(100);

            var overflow = Enumerable.Range(0, 48).Select(i => new NoteEntry((NoteType)(i % 3 + 1),
                today.AddDays(-i / 12).AddHours(8).AddMinutes(i),
                $"Voyage journal {i + 1}: " + string.Join(" ", Enumerable.Repeat("A fictional route detail to check wrapping and scrolling.", i % 4 + 1)),
                i % 6 == 0)).ToList();
            SetNotes(viewer, overflow);
            Capture(viewer, output, "notes-view-overflow", 680, 650);
            VerifyNotesContents(viewer, overflow);
            ScrollViewer listScroll = Ancestor<ScrollViewer>(Node<StackPanel>(viewer, "ItemsContainer"));
            if (listScroll.ViewportHeight <= 0 || listScroll.ScrollableHeight <= 0)
                throw new InvalidOperationException("Viewer list cannot scroll through long notes.");
            listScroll.ScrollToBottom();
            Capture(viewer, output, "notes-view-overflow-bottom", 680, 650);
            if (listScroll.VerticalOffset <= 0 || Math.Abs(listScroll.ScrollableHeight - listScroll.VerticalOffset) > 1)
                throw new InvalidOperationException("Viewer could not reach the final note.");
            SetNotes(viewer, []);
            Capture(viewer, output, "notes-view-empty", 680, 650);
            if (Node<TextBlock>(viewer, "EmptyMessageText").Visibility != Visibility.Visible)
                throw new InvalidOperationException("Viewer empty message is hidden.");
        }
        finally { viewer.Close(); }

        // The popup theme follows the resolved overlay, including an explicit override.
        foreach (var (appTheme, overlayTheme, pirate, suffix) in new[]
        {
            (AppThemeCatalog.Midnight, OverlayThemeCatalog.Pirate, true, "independent-one-piece"),
            (AppThemeCatalog.Pirate, OverlayThemeCatalog.Midnight, false, "independent-midnight"),
            (AppThemeCatalog.Midnight, OverlayThemeCatalog.FollowApplicationTheme, false, "midnight-regression")
        })
        {
            settings.ThemeMode = appTheme;
            settings.OverlayTheme = overlayTheme;
            AppThemeManager.Apply(appTheme);
            var entry = new NoteEntryWindow(NoteType.Note, settings);
            var notes = new NotesViewerWindow(settings);
            try
            {
                SetNotes(notes, entries);
                Node<TextBox>(entry, "NoteInputBox").Text = sample;
                Capture(entry, output, "notes-add-" + suffix, pirate ? 620 : 420, pirate ? 620 : 380);
                Capture(notes, output, "notes-view-" + suffix, pirate ? 680 : 600, pirate ? 650 : 500);
                if (PirateVisual.GetEnabled(entry) != pirate || PirateVisual.GetEnabled(notes) != pirate)
                    throw new InvalidOperationException("Note popup theme does not follow the resolved overlay: " + suffix);
            }
            finally { entry.Close(); notes.Close(); }
        }
        AppThemeManager.Apply(AppThemeCatalog.Pirate);
        AppUiScale.Apply(100);
        Console.WriteLine("Notes: three entry modes, placeholder/text retention, four filters, completed todos, compact/125% layouts, 175% typography, long-list scrolling, empty state and independent overlay themes passed. No vault, settings or native windows were opened.");
    }

    private static void SetNotes(NotesViewerWindow viewer, List<NoteEntry> entries)
    {
        viewer.GetType().GetField("_allEntries", Hidden)!.SetValue(viewer, entries);
        Invoke(viewer, "RenderEntries");
    }

    private static void VerifyNotesContents(NotesViewerWindow viewer, List<NoteEntry> entries)
    {
        var cards = Node<StackPanel>(viewer, "ItemsContainer").Children.OfType<Border>()
            .Where(border => border.Child is Grid).ToList();
        if (cards.Count != entries.Count)
            throw new InvalidOperationException($"Viewer rendered {cards.Count} cards for {entries.Count} filtered notes.");
        var contents = cards.Select(border => ((Grid)border.Child).Children.OfType<TextBlock>()
            .Single(text => Grid.GetColumn(text) == 2)).ToList();
        foreach (NoteEntry entry in entries)
        {
            TextBlock content = contents.Single(text => text.Text == entry.Text);
            if (entry.IsCompleted && !content.TextDecorations.Any(decoration => decoration.Location == TextDecorationLocation.Strikethrough))
                throw new InvalidOperationException("Completed todo lost its strikethrough.");
        }
    }

    private static void VerifyNotesLayout(Window window, params string[] names)
    {
        var root = (FrameworkElement)window.Content;
        foreach (string name in names)
        {
            var element = Node<FrameworkElement>(window, name);
            Rect bounds = element.TransformToAncestor(root).TransformBounds(new Rect(element.RenderSize));
            if (element.ActualWidth <= 0 || element.ActualHeight <= 0
                || bounds.Left < -1 || bounds.Right > root.ActualWidth + 1
                || bounds.Top < -1 || bounds.Bottom > root.ActualHeight + 1)
                throw new InvalidOperationException($"{window.GetType().Name}.{name} does not fit its window: {bounds}; root {root.ActualWidth}x{root.ActualHeight}.");
        }
    }

    private static T Ancestor<T>(DependencyObject child) where T : DependencyObject
    {
        for (DependencyObject? current = VisualTreeHelper.GetParent(child); current is not null; current = VisualTreeHelper.GetParent(current))
            if (current is T result) return result;
        throw new InvalidOperationException("Missing ancestor " + typeof(T).Name);
    }

    private static void CaptureFloatingClock(string output)
    {
        var overlay = new OverlayWindow();
        try
        {
            overlay.ApplyTheme(OverlayThemeCatalog.Pirate, AppThemeCatalog.Pirate);
            overlay.UpdateTime("00:32:02");
            overlay.SetTimerName("Grand Line Navigator");
            overlay.SetRunning(false);
            var root = (FrameworkElement)overlay.Content;
            foreach (double opacity in new[] { 1d, .5, 0d })
            {
                overlay.ApplySettings(Colors.White, Colors.Black, 96, 1, "Consolas", opacity, useThemeTextColor: true);
                root.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                int width = (int)Math.Ceiling(root.DesiredSize.Width);
                int height = (int)Math.Ceiling(root.DesiredSize.Height);
                Capture(overlay, output, "clock-floating-one-piece-" + opacity * 100, width, height);
                var popup = Node<Grid>(overlay, "ActionPopupRoot");
                Node<Border>(overlay, "ActionSurface").Opacity = 1;
                Node<TranslateTransform>(overlay, "ActionTranslate").Y = 0;
                popup.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                popup.Arrange(new Rect(popup.DesiredSize));
                popup.UpdateLayout();
                int totalHeight = height + (int)Math.Ceiling(popup.ActualHeight);
                var visual = new DrawingVisual();
                using (DrawingContext dc = visual.RenderOpen())
                {
                    dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, totalHeight));
                    dc.DrawRectangle(new VisualBrush(root) { Stretch = Stretch.Fill }, null, new Rect(0, 0, width, height));
                    dc.DrawRectangle(new VisualBrush(popup) { Stretch = Stretch.Fill }, null, new Rect(0, height, popup.ActualWidth, popup.ActualHeight));
                }
                var bitmap = new RenderTargetBitmap(width, totalHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using var stream = File.Create(Path.Combine(output, "clock-navigator-complete-" + opacity * 100 + ".png"));
                encoder.Save(stream);
            }
        }
        finally { overlay.Close(); }
    }

    private static void CaptureNavigatorSettings(string output)
    {
        var settings = new AppSettings
        {
            ThemeMode = AppThemeCatalog.Pirate, OverlayTheme = OverlayThemeCatalog.FollowApplicationTheme,
            BackgroundOpacity = 0, TextColor = "Theme default", TextSize = 48,
            ObsidianAutoSyncEnabled = false
        };
        var inspector = new SettingsWindow(settings);
        try
        {
            Node<ListBox>(inspector, "NavigationList").SelectedIndex = 1;
            var details = Node<Expander>(inspector, "TransparencyDetailsExpander");
            if (details.IsExpanded || details.Visibility != Visibility.Visible)
                throw new InvalidOperationException("Transparency details must start collapsed for one piece.");
            Capture(inspector, output, "settings-navigator-collapsed", 1100, 850);
            foreach (var (name, part) in new[]
            {
                ("ClockFrameOpaqueCheck", NavigatorOpaqueParts.ClockFrame), ("ClockMapOpaqueCheck", NavigatorOpaqueParts.ClockMap),
                ("MetalBorderOpaqueCheck", NavigatorOpaqueParts.MetalBorder), ("MetalFillOpaqueCheck", NavigatorOpaqueParts.MetalFill),
                ("TimerTextOpaqueCheck", NavigatorOpaqueParts.TimerText), ("ProjectNameOpaqueCheck", NavigatorOpaqueParts.ProjectName),
                ("ControlBoardOpaqueCheck", NavigatorOpaqueParts.ControlBoard), ("ControlMapOpaqueCheck", NavigatorOpaqueParts.ControlMap),
                ("ControlDialsOpaqueCheck", NavigatorOpaqueParts.ControlDials)
            })
            {
                var checkbox = Node<CheckBox>(inspector, name);
                bool original = (NavigatorOpaqueParts.Default & part) != 0;
                if (checkbox.IsChecked != original) throw new InvalidOperationException("Incorrect default for " + name);
                checkbox.IsChecked = !original;
                Invoke(inspector, "UpdatePreviewSafely");
                var expected = NavigatorOpaqueParts.Default ^ part;
                if (settings.OpaqueOverlayParts != expected
                    || NavigatorVisual.GetOpaqueParts(Node<Grid>(inspector, "PreviewThemeScope")) != expected
                    || NavigatorVisual.GetOpaqueParts(Node<Border>(inspector, "PreviewToolbarSurface")) != expected)
                    throw new InvalidOperationException("Transparency choice did not reach both preview layers: " + name);
                checkbox.IsChecked = original;
            }
            Invoke(inspector, "UpdatePreviewSafely");
            details.IsExpanded = true;
            Capture(inspector, output, "settings-navigator-expanded", 1100, 850);
            var scroll = Node<ScrollViewer>(inspector, "SettingsScrollViewer");
            scroll.ScrollToVerticalOffset(details.TranslatePoint(new Point(), (UIElement)scroll.Content).Y - 50);
            Capture(inspector, output, "settings-navigator-transparency-details", 1100, 850);
            Node<ComboBox>(inspector, "ThemeCombo").SelectedItem = AppThemeCatalog.Midnight;
            if (details.Visibility != Visibility.Collapsed)
                throw new InvalidOperationException("Navigator details leaked into another overlay theme.");
            Node<ComboBox>(inspector, "OverlayThemeCombo").SelectedItem = OverlayThemeCatalog.Pirate;
            if (details.Visibility != Visibility.Visible)
                throw new InvalidOperationException("Independent one piece overlay lost its transparency options.");
            Console.WriteLine("Nine transparency choices update live previews; default collapse and independent theme selection verified.");
        }
        finally { inspector.Close(); }
    }

    private static void CaptureSecondaryPages(string output)
    {
        DateTime now = DateTime.Now;
        var history = new ProjectTimeHistory();
        string[] names = ["Navigation", "Illustration", "Reading", "Research"];
        for (int day = 35; day >= 0; day--)
            for (int slot = 0; slot < 3; slot++)
            {
                DateTime start = now.Date.AddDays(-day).AddHours(7 + slot * 2);
                if (start.AddMinutes(80) >= now) continue;
                history.AddManualInterval(names[(day + slot) % names.Length], start.ToUniversalTime(),
                    start.AddMinutes(35 + (day * 13 + slot * 7) % 45).ToUniversalTime());
            }
        ProjectHistoryView view = history.CreateView(now.ToUniversalTime());
        var settings = new AppSettings
        {
            ThemeMode = AppThemeCatalog.Pirate, OverlayTheme = OverlayThemeCatalog.FollowApplicationTheme,
            TextColor = "Theme default", TextSize = 48, BorderWidth = 1, BackgroundOpacity = 88,
            Shortcuts = AppSettings.DefaultShortcuts(), CustomBackgrounds = new(),
            ObsidianAutoSyncEnabled = false, UiScalePercent = 90
        };
        AppThemeManager.Apply(settings.ThemeMode);
        AppUiScale.Apply(settings.UiScalePercent);
        var inspector = new SettingsWindow(settings, () => view);
        if (!PirateVisual.GetEnabled(inspector)) throw new InvalidOperationException("Settings lost its inherited Pirate style.");
        Node<ListBox>(inspector, "NavigationList").SelectedIndex = 1;
        foreach (int scale in new[] { 70, 90, 125 })
        {
            settings.UiScalePercent = scale;
            Invoke(inspector, "ReloadFromSettings");
            AppUiScale.Apply(scale);
            Invoke(inspector, "UpdatePreviewSafely");
            Capture(inspector, output, "settings-pirate-appearance-" + scale, 1040, 720);
        }
        Capture(inspector, output, "settings-pirate-minimum-125", 820, 600);
        AppThemeManager.Apply(AppThemeCatalog.Midnight);
        settings.ThemeMode = AppThemeCatalog.Midnight;
        Invoke(inspector, "ReloadFromSettings");
        Invoke(inspector, "UpdatePreviewSafely");
        Capture(inspector, output, "settings-midnight-regression-125", 1040, 720);
        AppThemeManager.Apply(AppThemeCatalog.Pirate);
        settings.ThemeMode = AppThemeCatalog.Pirate;
        VerifyScaleInteraction(inspector, settings);

        var dashboard = new ProjectDashboardWindow(() => view,
            (_, _, _) => throw new InvalidOperationException("Preview cannot edit records."),
            (_, _, _, _) => throw new InvalidOperationException("Preview cannot edit records."),
            _ => throw new InvalidOperationException("Preview cannot edit records."), () => true, () => null, settings);
        Node<Button>(dashboard, "SevenDaysButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        foreach (int scale in new[] { 90, 125 })
        {
            AppUiScale.Apply(scale);
            Capture(dashboard, output, "dashboard-pirate-" + scale, 1120, 800);
            Capture(dashboard, output, "dashboard-pirate-minimum-" + scale, 760, 560);
        }
        var recordsClick = dashboard.GetType().GetMethod("RecordsButton_Click", Hidden)!;
        recordsClick.Invoke(dashboard, [dashboard, new RoutedEventArgs()]);
        AppUiScale.Apply(90);
        Node<ScrollViewer>(dashboard, "DashboardScrollViewer").ScrollToBottom();
        Capture(dashboard, output, "records-pirate-90", 1120, 800);
        AppUiScale.Apply(125);
        Capture(dashboard, output, "records-pirate-minimum-125", 760, 560);
        Node<Expander>(dashboard, "ProjectRecordsExpander").IsExpanded = false;
        Node<ScrollViewer>(dashboard, "DashboardScrollViewer").ScrollToTop();
        AppThemeManager.Apply(AppThemeCatalog.Midnight);
        Invoke(dashboard, "RefreshFromHistory");
        Capture(dashboard, output, "dashboard-midnight-regression-125", 1120, 800);
        AppThemeManager.Apply(AppThemeCatalog.Pirate);
        AppUiScale.Apply(90);

        Window[] dialogs =
        [
            new TimerNameWindow("Illustration", names),
            new ShortcutsWindow(AppSettings.DefaultShortcuts()),
            new ProjectRecordEditorWindow(view.Projects, null, initialLocalDate: now.Date),
            new ProjectRecordDeleteWindow(view.Intervals.First()),
            new NoteEntryWindow(NoteType.Note, settings),
            new NotesViewerWindow(settings),
            new ConfirmationDialogWindow("Confirm action", "Delete this timer?", "This timer will be removed from the workspace.", "Delete timer", destructive: true)
        ];
        foreach (Window dialog in dialogs)
        {
            if (dialog is NotesViewerWindow) Invoke(dialog, "RenderEntries");
            int width = double.IsFinite(dialog.Width) ? (int)dialog.Width : 720;
            int height = double.IsFinite(dialog.Height) ? (int)dialog.Height : 640;
            Capture(dialog, output, dialog.GetType().Name.ToLowerInvariant() + "-pirate-90", width, height);
        }
    }

    private static void VerifyScaleInteraction(SettingsWindow window, AppSettings settings)
    {
        // Exercise the real Settings routed events with a tiny in-memory consumer.
        // Production ControllerWindow is intentionally not instantiated here.
        bool interacting = false;
        bool pending = false;
        int started = 0, completed = 0;
        void ApplyPending()
        {
            if (pending && !interacting) { pending = false; AppUiScale.Apply(settings.UiScalePercent); }
        }
        Subscribe(window, "SettingsInteractionStarted", () => { interacting = true; started++; });
        Subscribe(window, "SettingsChanged", () => { pending = true; ApplyPending(); });
        Subscribe(window, "SettingsInteractionCompleted", () => { interacting = false; completed++; ApplyPending(); });
        settings.UiScalePercent = 90;
        Invoke(window, "ReloadFromSettings");
        AppUiScale.Apply(90);
        object original = Application.Current.Resources["ApplicationScaleTransform"];
        Slider slider = Node<Slider>(window, "UiScaleSlider");
        slider.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left)
            { RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent });
        slider.Value = 70;
        if (settings.UiScalePercent != 70 || !ReferenceEquals(original, Application.Current.Resources["ApplicationScaleTransform"]))
            throw new InvalidOperationException("Scale was not committed independently/deferred during interaction.");
        slider.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice, Environment.TickCount)
            { RoutedEvent = Mouse.LostMouseCaptureEvent });
        if (started != 1 || completed != 1 || pending || interacting
            || ((ScaleTransform)Application.Current.Resources["ApplicationScaleTransform"]).ScaleX != .7
            || settings.TextSize != 48)
            throw new InvalidOperationException("Settings scale interaction did not complete cleanly.");
        object resized = Application.Current.Resources["ApplicationScaleTransform"];
        AppUiScale.Apply(70);
        if (!ReferenceEquals(resized, Application.Current.Resources["ApplicationScaleTransform"])
            || ((ScaleTransform)original).ScaleX != .9)
            throw new InvalidOperationException("Scale replacement mutated the old transform or lost idempotence.");
        Console.WriteLine("Settings routed scale interaction, overlay-size isolation and transform replacement passed.");
    }

    private static void Subscribe(object target, string eventName, Action action)
    {
        EventInfo info = target.GetType().GetEvent(eventName, Hidden)!;
        ParameterExpression[] parameters = info.EventHandlerType!.GetMethod("Invoke")!.GetParameters()
            .Select(p => Expression.Parameter(p.ParameterType)).ToArray();
        Delegate handler = Expression.Lambda(info.EventHandlerType, Expression.Invoke(Expression.Constant(action)), parameters).Compile();
        info.GetAddMethod(true)!.Invoke(target, [handler]);
    }

    private static void Invoke(object target, string name)
        => target.GetType().GetMethod(name, Hidden)!.Invoke(target, null);

    private static string Prepare(string xml) => xml
        .Replace("clr-namespace:StopwatchOverlay.Themes", "clr-namespace:StopwatchOverlay.Themes;assembly=StopwatchOverlay")
        .Replace("Source=\"Themes/", "Source=\"/StopwatchOverlay;component/Themes/");

    private static T Node<T>(Window window, string name) where T : class
        => window.FindName(name) as T ?? throw new InvalidOperationException("Missing " + name);

    private static void Capture(Window window, string output, string name, int width, int height)
    {
        var root = (FrameworkElement)window.Content;
        // Offscreen capture has no native window bounds. Supply the bound dialog
        // surface width explicitly; production obtains it from Window.ActualWidth.
        if (root is ScrollViewer { Content: Border dialogSurface })
            dialogSurface.MaxWidth = width;
        // Render a neutral parent: RenderTargetBitmap.Render(root) can omit the
        // root's own LayoutTransform. Keep Window ancestry for theme resources.
        window.Content = null;
        var host = new Border { Child = root, ClipToBounds = true };
        window.Content = host;
        host.Measure(new Size(width, height));
        host.Arrange(new Rect(0, 0, width, height));
        host.UpdateLayout();
        root.Dispatcher.Invoke(() => { }, DispatcherPriority.Render);
        root.UpdateLayout();
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(host);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(Path.Combine(output, name + ".png"));
        encoder.Save(stream);
        window.Content = null;
        host.Child = null;
        window.Content = root;
        Console.WriteLine($"{name}: {width}x{height}");
    }
}

