using System.Reflection;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
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

// Design fixture for the private desktop runner. Category actions use only the sample model.
internal static class SettingsBehaviorPreview
{
    private const string Output = "design/one-piece-v2/settings/behavior-native-preview";
    private static readonly FontFamily Body = StopwatchTypography.Resolve("modern-deck").NameFont;
    private static readonly IBrush Ink = Brush.Parse("#102B3B");
    private static readonly IBrush Muted = Brush.Parse("#655539");
    private static readonly IBrush Rule = Brush.Parse("#509A7B48");
    private static readonly IBrush Paper = Brush.Parse("#F7E8C7");
    private static readonly IBrush Teal = Brush.Parse("#2D7169");
    private static readonly IBrush Danger = Brush.Parse("#8F3328");

    internal static void Run()
    {
        Require(Application.Current is FrameSizePreviewApp, "Behavior preview requires the isolated application.");
        Application.Current!.Styles.Add(new FluentTheme());
        foreach (var resource in new[] { "avares://StopwatchOverlay.Desktop/Styles/AppStyles.axaml",
            "avares://Avalonia.Controls.ColorPicker/Themes/Fluent/Fluent.xaml" })
        {
            Application.Current.Styles.Add(new StyleInclude(new Uri("avares://StopwatchOverlay.Desktop/")) { Source = new Uri(resource) });
        }

        Directory.CreateDirectory(Output);
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(Path.GetTempPath(), $"stopwatch-preview-{Guid.NewGuid():N}.json"));
        try
        {
            foreach (int percent in new[] { 70, 90, 125 })
                foreach (bool compact in new[] { false, true })
                    Render(percent, compact);
        }
        finally { TelegramOutboxStore.SetCustomFilePath(null); }
    }

    private static void Render(int percent, bool smallWindow)
    {
        var settings = new AppSettings
        {
            ThemeMode = AppThemeCatalog.Nika, UiScalePercent = percent, OverlayTheme = OverlayThemeCatalog.Nika,
            TextColor = "Theme default", TextSize = 60, ShowRecIndicator = true, UseSmartCountdownInput = true, FocusTrackingEnabled = true
        };
        settings.SetProjectIdleRule("Learning", true, 8);
        settings.SetProjectIdleRule("Programming", true, 15);
        settings.AddProjectCategory("Client work");
        settings.AddProjectCategory("Personal study");
        var history = new ProjectTimeHistory();
        foreach (var name in new[] { "Illustration", "Learning", "Programming" }) history.RegisterProject(name);
        var snapshot = history.CreateView(new DateTime(2026, 11, 14, 12, 0, 0, DateTimeKind.Utc));

        CallDesktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
        CallDesktop("DesktopPageAppearance", "Initialize", settings);
        var window = new SettingsWindow(settings, () => snapshot)
        {
            ShowActivated = false, ShowInTaskbar = false, Width = smallWindow ? 840 : 1120, Height = smallWindow ? 600 : 720
        };
        try
        {
            var nav = window.FindControl<ListBox>("NavigationList")!;
            nav.SelectedItem = nav.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, "Behavior"));
            typeof(SettingsWindow).GetMethod("RestoreBehaviorPage", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(window, null);
            var host = window.FindControl<Grid>("SettingsColumns")!;
            host.IsVisible = true;
            host.Children.Clear();
            host.Background = Brushes.Transparent;
            window.Show();
            ClockOverlayChecks.Pump(55);
            var transform = (LayoutTransformControl)window.Content!;
            Require(Math.Abs(transform.LayoutTransform!.Value.M11 - percent / 100d) < .001, "Incorrect page scale.");
            bool stacked = host.Bounds.Width < 680;

            T Take<T>(string name) where T : Control
            {
                var control = window.FindControl<T>(name)!;
                ((Panel)control.Parent!).Children.Remove(control);
                control.Margin = default;
                return control;
            }

            var rec = Take<CheckBox>("RecCheck");
            var timeout = Take<Slider>("CommandChainingTimeoutSlider");
            var timeoutValue = Label("", 13, Ink);
            var shortcutHint = Label("", 12);
            var clock = new ThemedClockFace { Text = "00:24:18", ProjectName = "Programming", IsRunning = true };
            clock.ApplySettings(settings);
            clock.SetStopwatchPresentationState(new(true, true, false));
            rec.IsCheckedChanged += (_, _) => clock.ShowRec = rec.IsChecked == true;
            void UpdateTimeout()
            {
                timeoutValue.Text = $"{timeout.Value:0.0} s";
                shortcutHint.Text = $"Press another command key within {timeout.Value:0.0} seconds.";
            }
            timeout.ValueChanged += (_, _) => UpdateTimeout();
            UpdateTimeout();
            AutomationProperties.SetName(timeout, "Command chaining timeout");
            var timer = Columns(stacked,
                Stack(Card("Timer controls",
                        Choice(Take<CheckBox>("AutoStartCheck"), "Auto-start with overlay", "Start timing when the overlay appears."),
                        Choice(rec, "Show REC indicator", "Display a red dot on a running timer."),
                        Choice(Take<CheckBox>("BlinkCheck"), "Blink timer colon"),
                        Choice(Take<CheckBox>("SmartInputCheck"), "Smart countdown input", "Enter a duration using time units.")),
                    Card("Keyboard shortcuts", Row(Label("Command chaining timeout", 13, Ink), timeoutValue), timeout,
                        Label("Time to press the next command after one runs.", 12))),
                Stack(Card("Clock sample", new Border
                    {
                        Background = Ink, CornerRadius = new(5), Height = 105, Padding = new(16),
                        Child = new Viewbox { Child = clock, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly }
                    }),
                    Card("Chaining example", Row(Keycaps("Win + F2", "Space"), Label("Start or pause", 12)), shortcutHint)));

            var project = Field(Take<ComboBox>("IdleProjectComboBox"), "Project");
            project.PlaceholderText = "Select a project";
            var projectEnabled = Take<CheckBox>("IdleProjectActiveCheck");
            var projectTimeout = Field(Take<TextBox>("IdleProjectTimeoutTextBox"), "Project timeout in minutes");
            var projectRules = Take<ItemsControl>("IdleProjectsItemsControl");
            var updateIdleProject = typeof(SettingsWindow).GetMethod("UpdateSelectedProjectIdleUi", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var toggleIdleProject = typeof(SettingsWindow).GetMethod("IdleProjectQuickToggle_Click", BindingFlags.Instance | BindingFlags.NonPublic)!;
            project.SelectionChanged += (_, _) => updateIdleProject.Invoke(window, [project.SelectedItem as string]);
            projectRules.ItemTemplate = new FuncDataTemplate<ProjectIdleRuleViewModel>((rule, _) =>
            {
                if (rule is null) return null;
                bool enabled = rule.ActionButtonText == "Deactivate";
                var action = ActionButton(enabled ? "Turn off" : "Turn on");
                action.Tag = rule.ProjectName;
                AutomationProperties.SetName(action, $"{action.Content} idle stop for {rule.ProjectName}");
                action.Click += (_, e) => toggleIdleProject.Invoke(window, [action, e]);
                var row = Row(TrimmingLabel(rule.ProjectName), Label(enabled ? $"On · {settings.ProjectIdleRules[rule.ProjectName].IdleMinutes} min" : "Off", 12, enabled ? Teal : Muted), action);
                row.Margin = new(0, 0, 0, 4);
                return row;
            });
            project.SelectedItem = "Programming";
            var idle = Columns(stacked,
                Stack(Card("Idle defaults",
                    Choice(Take<CheckBox>("IdleStopUnnamedCheck"), "Apply to unnamed timers"),
                    NumberRow("Default timeout", Field(Take<TextBox>("IdleStopDefaultTimeoutTextBox"), "Default timeout in minutes"), "min"),
                    Choice(Take<CheckBox>("IdleStopSubtractCheck"), "Subtract inactive time", "Remove the idle period from the recorded time.")),
                    Card("Which timeout applies?", Label("A project rule can use its own timeout. New project rules start with the default.", 12))),
                Stack(Card("Project rule", project,
                        Choice(projectEnabled, "Enable idle stop for this project"), NumberRow("Project timeout", projectTimeout, "min")),
                    Card("Project rules", projectRules)));

            var focusEnabled = Take<CheckBox>("FocusTrackingEnabledCheck");
            var minimumPause = Field(Take<TextBox>("FocusMinimumPauseTextBox"), "Minimum pause in seconds");
            var threshold = Field(Take<TextBox>("FocusDistractionThresholdTextBox"), "Distraction threshold in minutes");
            // The current Desktop page does not populate these controls on load.
            focusEnabled.IsChecked = settings.FocusTrackingEnabled;
            minimumPause.Text = settings.FocusMinimumPauseSeconds.ToString();
            threshold.Text = settings.FocusDistractionThresholdMinutes.ToString();
            var promptChoices = Stack();
            var promptHint = Label("", 12);
            void UpdateFocus()
            {
                bool enabled = focusEnabled.IsChecked == true;
                minimumPause.IsEnabled = threshold.IsEnabled = enabled;
                bool prompt = enabled && settings.FocusMinimumPauseSeconds <= 160;
                bool distraction = settings.FocusDistractionThresholdMinutes * 60d > 160;
                promptChoices.IsVisible = prompt;
                ((Border)promptChoices.Children[0]).Background = distraction ? Paper : Ink;
                ((Border)promptChoices.Children[1]).Background = distraction ? Ink : Paper;
                ((TextBlock)((Border)promptChoices.Children[0]).Child!).Foreground = distraction ? Ink : Paper;
                ((TextBlock)((Border)promptChoices.Children[1]).Child!).Foreground = distraction ? Paper : Ink;
                promptHint.Text = !enabled ? "Focus tracking is off." : !prompt ? "The sample pause is below the minimum." : $"Closes after {settings.FocusPromptTimeoutSeconds} seconds.";
            }
            foreach (string choice in new[] { "1   Valid break", "2   Distraction", "3   Quick idea" })
                promptChoices.Children.Add(new Border { Background = Paper, CornerRadius = new(4), Padding = new(10, 7), Child = Label(choice, 13, Ink) });
            focusEnabled.IsCheckedChanged += (_, _) => UpdateFocus();
            minimumPause.TextChanged += (_, _) => UpdateFocus();
            threshold.TextChanged += (_, _) => UpdateFocus();
            UpdateFocus();
            var focus = Columns(stacked,
                Card("Track interruptions",
                    Choice(focusEnabled, "Ask about pauses on resume"),
                    NumberRow("Minimum pause", minimumPause, "sec"), Label("Shorter pauses are ignored.", 12),
                    NumberRow("Distraction threshold", threshold, "min"), Label("Suggest Distraction for pauses shorter than this.", 12)),
                Card("Resume prompt sample", Label("Programming · paused for 2 min 40 sec", 13, Ink), promptChoices, promptHint));

            var category = Field(new TextBox { Watermark = "Category name", MaxLength = 40 }, "New category name");
            var add = ActionButton("Add category", primary: true);
            var feedback = Label("", 12, Danger);
            feedback.IsVisible = false;
            var custom = Stack();
            void RefreshCategories()
            {
                custom.Children.Clear();
                foreach (var name in settings.GetNormalizedProjectCategories().Except(AppSettings.DefaultProjectCategories, StringComparer.OrdinalIgnoreCase))
                {
                    var remove = ActionButton("Remove");
                    AutomationProperties.SetName(remove, $"Remove category {name}");
                    remove.Click += (_, _) => { settings.ProjectCategories.Remove(name); RefreshCategories(); };
                    custom.Children.Add(Row(TrimmingLabel(name), remove));
                }
                if (custom.Children.Count == 0) custom.Children.Add(Label("Add a category to see it here.", 12));
            }
            void AddCategory()
            {
                string name = category.Text?.Trim() ?? "";
                feedback.IsVisible = !settings.AddProjectCategory(name);
                feedback.Text = name.Length == 0 ? "Enter a category name." : "That category already exists.";
                if (feedback.IsVisible) { category.Focus(); return; }
                category.Text = "";
                RefreshCategories();
            }
            add.Click += (_, _) => AddCategory();
            category.KeyDown += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { AddCategory(); e.Handled = true; } };
            RefreshCategories();
            var builtIns = new WrapPanel();
            foreach (var name in AppSettings.DefaultProjectCategories)
                builtIns.Children.Add(new Border { Background = Paper, CornerRadius = new(4), Margin = new(0, 0, 6, 6), Padding = new(10, 5), Child = Label(name, 12, Ink) });
            var categories = Columns(stacked,
                Card("Add a category", Label("Use categories to group projects when you create or edit a timer.", 13), category, add, feedback),
                Stack(Card("Built-in categories", builtIns), Card("Custom categories", custom)));

            var tabs = new WrapPanel();
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled };
            var page = new Grid { RowDefinitions = new("Auto,Auto,*"), Children =
            {
                new StackPanel { Spacing = 4, Margin = new(0, 0, 0, 12), Children =
                {
                    Label("Behavior", 23, Ink), Label("Choose how timers start, stop, and handle pauses.", 12)
                } }, tabs, scroll
            } };
            Grid.SetRow(tabs, 1);
            Grid.SetRow(scroll, 2);
            Grid.SetColumnSpan(page, 3);
            host.Children.Add(page);
            window.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "SettingsSaveStatus").Text = "Design preview · sample settings";
            var done = window.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "SettingsDone");

            var views = new[] { ("Timer", "timer", timer), ("Idle stop", "idle", idle),
                ("Focus tracking", "focus", focus), ("Project categories", "categories", categories) };
            var actions = views.Select(view => ActionButton(view.Item1)).ToArray();
            for (int i = 0; i < actions.Length; i++)
            {
                var tab = actions[i];
                var body = views[i].Item3;
                tab.Margin = new(0, 0, 8, 12);
                tab.Click += (_, _) =>
                {
                    foreach (var button in actions) button.Classes.Set("editorPrimary", button == tab);
                    scroll.Content = body;
                    scroll.Offset = default;
                };
                tabs.Children.Add(tab);
            }

            for (int i = 0; i < views.Length; i++)
            {
                actions[i].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ClockOverlayChecks.Pump(55);
                Require(scroll.Viewport.Height > 80 && scroll.Extent.Width <= scroll.Viewport.Width + 1, $"{views[i].Item1} overflows at {percent}%.");
                var footer = done.TranslatePoint(default, window)!.Value;
                if (!smallWindow && percent == 90)
                {
                    if (i == 1) Require(projectEnabled.IsChecked == true && projectTimeout.Text == "15", "Selected project rule is missing from the preview.");
                    if (i == 2) Require(focusEnabled.IsChecked == true && promptChoices.IsVisible && threshold.Text == "5", "Enabled focus sample is missing.");
                    Save(window, views[i].Item2);
                }
                scroll.ScrollToEnd();
                ClockOverlayChecks.Pump(30);
                Require(done.TranslatePoint(default, window)!.Value == footer, "Scrolling moved the Done footer.");
                if (smallWindow && percent == 90 && views[i].Item2 == "idle") Save(window, "compact-idle");
            }

            if (!smallWindow && percent == 90)
            {
                category.Text = "Work";
                add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ClockOverlayChecks.Pump(30);
                Require(feedback.IsVisible && custom.Children.Count == 2, "Duplicate sample category was accepted.");
                Save(window, "category-validation");
                actions[2].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                focusEnabled.IsChecked = false;
                ClockOverlayChecks.Pump(35);
                Require(!minimumPause.IsEnabled && !promptChoices.IsVisible, "Focus-off state did not update.");
                Save(window, "focus-off");
            }
            Console.WriteLine($"PASS 4 Behavior tabs at {percent}% ({window.ClientSize.Width:0} × {window.ClientSize.Height:0}, {(stacked ? "stacked" : "two columns")})");
        }
        finally
        {
            window.Close();
            CallDesktop("DesktopPageAppearance", "Shutdown");
        }
    }

    private static void CallDesktop(string type, string method, params object[] arguments) =>
        typeof(SettingsWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!
            .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, arguments);

    private static Grid Columns(bool stacked, Control first, Control second)
    {
        var grid = new Grid { ColumnDefinitions = new(stacked ? "*" : "1.05*,16,1*"), RowDefinitions = new(stacked ? "Auto,12,Auto" : "Auto"), Children = { first, second } };
        Grid.SetColumn(second, stacked ? 0 : 2);
        Grid.SetRow(second, stacked ? 2 : 0);
        return grid;
    }

    private static StackPanel Stack(params Control[] children)
    {
        var stack = new StackPanel { Spacing = 10 };
        foreach (var child in children) stack.Children.Add(child);
        return stack;
    }

    private static Border Card(string title, params Control[] children) => new()
    {
        Background = Brush.Parse("#40FFF6E4"), BorderBrush = Rule, BorderThickness = new(1), CornerRadius = new(7),
        Padding = new(14), VerticalAlignment = VerticalAlignment.Top, Child = Stack([Label(title, 18, Ink), .. children])
    };

    private static TextBlock Label(string text, double size, IBrush? brush = null) => new()
    {
        Text = text, FontFamily = Body, FontSize = size, Foreground = brush ?? Muted,
        TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center
    };

    private static TextBlock TrimmingLabel(string text)
    {
        var label = Label(text, 13, Ink);
        label.TextWrapping = TextWrapping.NoWrap;
        label.TextTrimming = TextTrimming.CharacterEllipsis;
        ToolTip.SetTip(label, text);
        return label;
    }

    private static CheckBox Choice(CheckBox check, string caption, string? detail = null)
    {
        check.Content = detail == null ? Label(caption, 13, Ink) : new StackPanel { Spacing = 2, Children = { Label(caption, 13, Ink), Label(detail, 11) } };
        check.MinHeight = 28;
        check.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetName(check, caption);
        return check;
    }

    private static T Field<T>(T field, string label) where T : TemplatedControl
    {
        field.Classes.Add("editorField");
        field.Height = 34;
        field.MinWidth = 0;
        field.FontFamily = Body;
        field.FontSize = 13;
        field.HorizontalAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetName(field, label);
        return field;
    }

    private static Button ActionButton(string text, bool primary = false)
    {
        var button = new Button { Content = text, FontFamily = Body, MinWidth = 0, MinHeight = 0, Height = 32, Padding = new(11, 0), Classes = { "editorAction" } };
        button.Classes.Set("editorPrimary", primary);
        return button;
    }

    private static Grid Row(Control first, params Control[] rest)
    {
        var grid = new Grid { ColumnDefinitions = new("*"), Children = { first } };
        foreach (var control in rest)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(8)));
            Grid.SetColumn(control, grid.ColumnDefinitions.Count);
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.Children.Add(control);
        }
        return grid;
    }

    private static Grid NumberRow(string label, TextBox input, string unit)
    {
        input.Width = 68;
        input.TextAlignment = TextAlignment.Center;
        return Row(Label(label, 13, Ink), input, Label(unit, 12));
    }

    private static StackPanel Keycaps(string first, string second) => new()
    {
        Orientation = Orientation.Horizontal, Spacing = 6, Children =
        {
            new Border { CornerRadius = new(4), Background = Ink, Padding = new(8, 5), Child = Label(first, 12, Paper) },
            new Border { CornerRadius = new(4), Background = Ink, Padding = new(8, 5), Child = Label(second, 12, Paper) }
        }
    };

    private static void Save(SettingsWindow window, string name)
    {
        var frame = window.GetVisualDescendants().OfType<StopwatchFrameSurface>().First();
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
        var scale = ((LayoutTransformControl)window.Content!).LayoutTransform!.Value.M11;
        if (scale >= 1) bitmap.Render(window);
        else
        {
            // The brush includes the page scale. Direct bitmap rendering culls downscaled children.
            using var context = bitmap.CreateDrawingContext();
            context.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(frame.Bounds, RelativeUnit.Absolute) }, null, new Rect(frame.Bounds.Size));
        }
        bitmap.Save(Path.Combine(Output, name + ".png"));
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
