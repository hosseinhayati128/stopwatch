using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private static readonly IBrush BehaviorPaper = Brush.Parse("#F7E8C7");
    private static readonly IBrush BehaviorTeal = Brush.Parse("#2D7169");
    private static readonly IBrush BehaviorDanger = Brush.Parse("#8F3328");
    private readonly List<(Control Control, Panel Parent, int Index, Thickness Margin, int Column, int Row)> _behaviorMoved = new();
    private readonly Dictionary<CheckBox, object?> _behaviorCheckContents = new();
    private Grid? _behaviorPage;
    private Grid[] _behaviorBodies = [];
    private Button[] _behaviorTabs = [];
    private ScrollViewer? _behaviorScroll;
    private StackPanel? _behaviorHeading;
    private WrapPanel? _behaviorTabRow;
    private readonly ContentControl _behaviorBodyHost = new();
    private StackPanel? _behaviorScrollingPage;
    private ItemsControl? _behaviorCategories;
    private TextBlock? _behaviorCategoryEmpty;
    private ThemedClockFace? _behaviorClock;
    private TextBlock? _behaviorShortcutHint, _behaviorLeader, _behaviorStartKey, _behaviorFocusHint;
    private StackPanel? _behaviorFocusChoices;
    private Border[] _behaviorChoices = [];
    private int _behaviorSection;
    private bool? _behaviorStacked;
    private bool _behaviorScrollHeader;

    private void BuildBehaviorPage(Grid host)
    {
        // Record the legacy slots before removing anything so sibling indexes remain valid.
        Control[] controls = [AutoStartCheck, RecCheck, BlinkCheck, SmartInputCheck, CommandChainingTimeoutSlider,
            CommandChainingTimeoutValueText, IdleStopUnnamedCheck, IdleStopDefaultTimeoutTextBox, IdleStopSubtractCheck,
            IdleProjectComboBox, IdleProjectActiveCheck, IdleProjectTimeoutTextBox, IdleProjectStatusSummaryText,
            FocusTrackingEnabledCheck, FocusMinimumPauseTextBox, FocusDistractionThresholdTextBox,
            NewCategoryTextBox, AddCategorySettingsButton, CategoryValidationStatusText];
        foreach (var control in controls)
        {
            var parent = (Panel)control.Parent!;
            _behaviorMoved.Add((control, parent, parent.Children.IndexOf(control), control.Margin, Grid.GetColumn(control), Grid.GetRow(control)));
        }
        foreach (var item in _behaviorMoved)
        {
            item.Parent.Children.Remove(item.Control);
            item.Control.Margin = default;
            Grid.SetColumn(item.Control, 0);
            Grid.SetRow(item.Control, 0);
        }

        _behaviorClock = new ThemedClockFace { Name = "BehaviorClockSample", Text = "00:24:18", ProjectName = "Programming", IsRunning = true };
        _behaviorClock.SetStopwatchPresentationState(new(true, true, false));
        _behaviorShortcutHint = SettingsLabel("", 12);
        _behaviorLeader = SettingsLabel("", 12, BehaviorPaper);
        _behaviorStartKey = SettingsLabel("", 12, BehaviorPaper);
        AutomationProperties.SetName(CommandChainingTimeoutSlider, "Command chaining timeout");
        var timer = BehaviorColumns(
            BehaviorStack(BehaviorCard("Timer controls",
                    BehaviorChoice(AutoStartCheck, "Auto-start with overlay", "Start timing when you show the overlay."),
                    BehaviorChoice(RecCheck, "Show REC indicator", "Display a red dot on a running timer."),
                    BehaviorChoice(BlinkCheck, "Blink timer colon", "Blink the separators in clock mode."),
                    BehaviorChoice(SmartInputCheck, "Smart countdown input", "Enter a duration using time units.")),
                BehaviorCard("Keyboard shortcuts", BehaviorRow(SettingsLabel("Command chaining timeout", 13, SettingsInk), CommandChainingTimeoutValueText),
                    CommandChainingTimeoutSlider, SettingsLabel("Time to press the next command after one runs.", 12))),
            BehaviorStack(BehaviorCard("Clock sample", new Border
                {
                    Background = SettingsInk, CornerRadius = new(5), Height = 105, Padding = new(13),
                    Child = new Viewbox { Child = _behaviorClock, StretchDirection = StretchDirection.DownOnly }
                }),
                BehaviorCard("Chaining example", BehaviorRow(new WrapPanel { Children = { BehaviorKeycap(_behaviorLeader), BehaviorKeycap(_behaviorStartKey) } },
                    SettingsLabel("Start or pause", 12)), _behaviorShortcutHint)));

        var rules = new ItemsControl();
        rules.Bind(ItemsControl.ItemsSourceProperty, IdleProjectsItemsControl.GetObservable(ItemsControl.ItemsSourceProperty));
        rules.ItemTemplate = new FuncDataTemplate<ProjectIdleRuleViewModel>((rule, scope) =>
        {
            if (rule == null) return null;
            bool enabled = rule.ActionButtonText == "Deactivate";
            _settings.IsIdleStopActiveForProject(rule.ProjectName, out int minutes, out _);
            var action = BehaviorAction(enabled ? "Turn off" : "Turn on");
            action.Tag = rule.ProjectName;
            AutomationProperties.SetName(action, $"{action.Content} idle stop for {rule.ProjectName}");
            action.Click += IdleProjectQuickToggle_Click;
            var row = BehaviorRow(BehaviorName(rule.ProjectName), SettingsLabel(enabled ? $"On · {minutes} min" : "Off", 12, enabled ? BehaviorTeal : SettingsMuted), action);
            row.Margin = new(0, 0, 0, 4);
            return row;
        });
        var idle = BehaviorColumns(
            BehaviorStack(BehaviorCard("Idle defaults",
                    BehaviorChoice(IdleStopUnnamedCheck, "Apply to unnamed timers"),
                    BehaviorNumber("Default timeout", IdleStopDefaultTimeoutTextBox, "min"),
                    BehaviorChoice(IdleStopSubtractCheck, "Subtract inactive time", "Remove the idle period from this run.")),
                BehaviorCard("Which timeout applies?", SettingsLabel("A project rule can use its own timeout. New rules start with the default.", 12))),
            BehaviorStack(BehaviorCard("Project rule", BehaviorField(IdleProjectComboBox, "Project"),
                    BehaviorChoice(IdleProjectActiveCheck, "Enable idle stop for this project"),
                    BehaviorNumber("Project timeout", IdleProjectTimeoutTextBox, "min"), IdleProjectStatusSummaryText),
                BehaviorCard("Project rules", rules)));

        _behaviorChoices = new[] { "1   Valid break", "2   Distraction", "3   Quick idea" }.Select(choice => new Border
        {
            Background = BehaviorPaper, CornerRadius = new(4), Padding = new(10, 7), Child = SettingsLabel(choice, 13, SettingsInk)
        }).ToArray();
        foreach (var choice in _behaviorChoices) DesktopTypography.SetPreserveColor((Control)choice.Child!, true);
        _behaviorFocusChoices = BehaviorStack(_behaviorChoices);
        _behaviorFocusHint = SettingsLabel("", 12);
        var focus = BehaviorColumns(BehaviorCard("Track interruptions",
                BehaviorChoice(FocusTrackingEnabledCheck, "Ask about pauses on resume"),
                BehaviorNumber("Minimum pause", FocusMinimumPauseTextBox, "sec"), SettingsLabel("Shorter pauses are ignored.", 12),
                BehaviorNumber("Distraction threshold", FocusDistractionThresholdTextBox, "min"), SettingsLabel("Suggest Distraction for pauses shorter than this.", 12)),
            BehaviorCard("Resume prompt sample", SettingsLabel("Programming · paused for 2 min 40 sec", 13, SettingsInk), _behaviorFocusChoices, _behaviorFocusHint));

        var presets = new WrapPanel();
        foreach (string name in AppSettings.DefaultProjectCategories)
            presets.Children.Add(new Border { Background = BehaviorPaper, CornerRadius = new(4), Margin = new(0, 0, 6, 6), Padding = new(10, 5), Child = SettingsLabel(name, 12, SettingsInk) });
        _behaviorCategoryEmpty = SettingsLabel("Add a category to see it here.", 12);
        _behaviorCategories = new ItemsControl
        {
            ItemTemplate = new FuncDataTemplate<ProjectCategoryItemViewModel>((item, _) =>
            {
                if (item == null) return null;
                var remove = BehaviorAction("Remove");
                remove.Tag = item.Name;
                AutomationProperties.SetName(remove, $"Remove category {item.Name}");
                remove.Click += DeleteCategory_Click;
                var row = BehaviorRow(BehaviorName(item.Name), remove);
                row.Margin = new(0, 0, 0, 7);
                return row;
            })
        };
        CategoryValidationStatusText.TextWrapping = TextWrapping.Wrap;
        NavigatorFormStyles.StyleButton(AddCategorySettingsButton, primary: true);
        var categories = BehaviorColumns(BehaviorCard("Add a category",
                SettingsLabel("Use categories to group projects when you create or edit a timer.", 13),
                BehaviorField(NewCategoryTextBox, "New category name"), AddCategorySettingsButton, CategoryValidationStatusText),
            BehaviorStack(BehaviorCard("Built-in categories", presets), BehaviorCard("Custom categories", _behaviorCategoryEmpty, _behaviorCategories)));

        _behaviorBodies = [timer, idle, focus, categories];
        _behaviorHeading = BehaviorStack(SettingsLabel("Behavior", 23, SettingsInk), SettingsLabel("Choose how timers start, stop, and handle pauses.", 12));
        _behaviorHeading.Spacing = 4;
        _behaviorHeading.Margin = new(0, 0, 0, 12);
        _behaviorTabRow = new WrapPanel();
        _behaviorTabs = [BehaviorAction("Timer", "BehaviorTimerTab"), BehaviorAction("Idle stop", "BehaviorIdleTab"),
            BehaviorAction("Focus tracking", "BehaviorFocusTab"), BehaviorAction("Project categories", "BehaviorCategoriesTab")];
        for (int i = 0; i < _behaviorTabs.Length; i++)
        {
            int section = i;
            _behaviorTabs[i].Margin = new(0, 0, 8, 12);
            _behaviorTabs[i].Click += (_, _) => ShowBehaviorSection(section);
            _behaviorTabRow.Children.Add(_behaviorTabs[i]);
        }
        _behaviorScroll = new ScrollViewer { Name = "BehaviorScroll", HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        _behaviorPage = new Grid { Name = "BehaviorPage", RowDefinitions = new("Auto,Auto,*"), Children = { _behaviorHeading, _behaviorTabRow, _behaviorScroll } };
        Grid.SetRow(_behaviorTabRow, 1);
        Grid.SetRow(_behaviorScroll, 2);
        ApplyBehaviorStyles(_behaviorPage);
        _behaviorPage.SizeChanged += (_, _) => QueueBehaviorLayout();
        _behaviorScroll.SizeChanged += (_, _) => QueueBehaviorLayout();
        _behaviorHeading.SizeChanged += (_, _) => QueueBehaviorLayout();
        host.Children.Add(_behaviorPage);
        PopulateCategories();
        UpdateSelectedProjectIdleUi(IdleProjectComboBox.SelectedItem as string);
        UpdateBehaviorControlStates();
        ShowBehaviorSection(_behaviorSection);
    }

    private bool _behaviorLayoutPending;
    private void QueueBehaviorLayout()
    {
        if (_behaviorLayoutPending) return;
        _behaviorLayoutPending = true;
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _behaviorLayoutPending = false;
            if (_behaviorPage == null || _closed || !_behaviorPage.IsVisible || _behaviorPage.Bounds.Width <= 0) return;
            double textScale = _settings.Typography.Resolve("Settings").SizePercent / 100;
            bool stacked = _behaviorPage.Bounds.Width < 680 * textScale;
            if (_behaviorStacked != stacked)
            {
                _behaviorStacked = stacked;
                foreach (var body in _behaviorBodies)
                {
                    body.ColumnDefinitions = new(stacked ? "*" : "1.05*,16,1*");
                    body.RowDefinitions = new(stacked ? "Auto,12,Auto" : "Auto");
                    Grid.SetColumn(body.Children[1], stacked ? 0 : 2);
                    Grid.SetRow(body.Children[1], stacked ? 2 : 0);
                }
            }
            bool scrollHeader = _behaviorPage.Bounds.Height < 300 * textScale;
            if (_behaviorScrollHeader != scrollHeader)
            {
                _behaviorScrollHeader = scrollHeader;
                LayoutBehaviorHeader();
            }
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void ShowBehaviorSection(int section)
    {
        _behaviorSection = section;
        foreach (var button in _behaviorTabs) button.Classes.Set("editorPrimary", button == _behaviorTabs[section]);
        if (_behaviorScroll!.Content == null) LayoutBehaviorHeader();
        _behaviorBodyHost.Content = _behaviorBodies[section];
        _behaviorScroll.Offset = default;
        RefreshBehaviorPreview();
        QueueBehaviorLayout();
    }

    private void LayoutBehaviorHeader()
    {
        if (_behaviorScrollingPage != null) _behaviorScrollingPage.Children.Clear();
        _behaviorPage!.Children.Remove(_behaviorHeading!);
        _behaviorPage.Children.Remove(_behaviorTabRow!);
        _behaviorScroll!.Content = null;
        _behaviorPage.RowDefinitions = new(_behaviorScrollHeader ? "*" : "Auto,Auto,*");
        Grid.SetRow(_behaviorScroll, _behaviorScrollHeader ? 0 : 2);
        if (_behaviorScrollHeader)
        {
            _behaviorScrollingPage = BehaviorStack(_behaviorHeading!, _behaviorTabRow!, _behaviorBodyHost);
            _behaviorScroll.Content = _behaviorScrollingPage;
        }
        else
        {
            _behaviorPage.Children.Add(_behaviorHeading!);
            _behaviorPage.Children.Add(_behaviorTabRow!);
            _behaviorScroll.Content = _behaviorBodyHost;
        }
    }

    private void RefreshBehaviorPreview()
    {
        if (_behaviorPage == null || !_behaviorPage.IsVisible || _closed) return;
        _behaviorClock!.ApplySettings(_settings);
        _behaviorLeader!.Text = (_settings.LeaderShortcut ?? AppSettings.DefaultLeaderShortcut()).Format().Replace("+", " + ");
        _behaviorStartKey!.Text = Shortcut.FormatKeyName(_settings.CommandModeKeys.GetValueOrDefault(ShortcutAction.StartStop, ShortcutCommandMap.VK_SPACE));
        _behaviorShortcutHint!.Text = $"Press another command key within {_settings.CommandChainingTimeoutSeconds:0.0} seconds.";
        bool enabled = _settings.FocusTrackingEnabled;
        bool show = enabled && _settings.FocusMinimumPauseSeconds <= 160;
        _behaviorFocusChoices!.IsVisible = show;
        int suggested = _settings.FocusDistractionThresholdMinutes * 60 > 160 ? 1 : 0;
        for (int i = 0; i < _behaviorChoices.Length; i++)
        {
            _behaviorChoices[i].Background = i == suggested ? SettingsInk : BehaviorPaper;
            ((TextBlock)_behaviorChoices[i].Child!).Foreground = i == suggested ? BehaviorPaper : SettingsInk;
        }
        _behaviorFocusHint!.Text = !enabled ? "Focus tracking is off." : show
            ? $"Closes after {_settings.FocusPromptTimeoutSeconds} seconds."
            : "The sample pause is below the minimum.";
    }

    private void UpdateBehaviorControlStates()
    {
        FocusMinimumPauseTextBox.IsEnabled = _settings.FocusTrackingEnabled;
        FocusDistractionThresholdTextBox.IsEnabled = _settings.FocusTrackingEnabled;
    }

    private static bool TryBehaviorNumber(TextBox field, int maximum, out int value)
    {
        bool valid = int.TryParse(field.Text?.Trim(), out value) && value >= 1 && value <= maximum;
        field.Classes.Set("invalidTime", !valid);
        ToolTip.SetTip(field, valid ? null : $"Enter a whole number from 1 to {maximum}.");
        return valid;
    }

    private void RestoreBehaviorPage()
    {
        if (_behaviorPage == null) return;
        foreach (var item in _behaviorMoved.OrderBy(item => item.Index))
        {
            if (item.Control.Parent is Panel panel) panel.Children.Remove(item.Control);
            item.Control.Margin = item.Margin;
            Grid.SetColumn(item.Control, item.Column);
            Grid.SetRow(item.Control, item.Row);
            item.Control.Classes.Remove("behaviorField");
            item.Control.Classes.Remove("behaviorNumber");
            if (item.Control is CheckBox check && _behaviorCheckContents.TryGetValue(check, out var content))
            {
                check.Content = content;
                check.ClearValue(HorizontalContentAlignmentProperty);
            }
            item.Parent.Children.Insert(item.Index, item.Control);
        }
        _behaviorCheckContents.Clear();
        _behaviorBodyHost.Content = null;
        if (_behaviorScrollingPage != null) _behaviorScrollingPage.Children.Clear();
        _behaviorScrollingPage = null;
        _behaviorScroll!.Content = null;
        AddCategorySettingsButton.Classes.Remove("editorPrimary");
        AddCategorySettingsButton.Classes.Remove("editorAction");
        AddCategorySettingsButton.Classes.Add("modern");
        AddCategorySettingsButton.ClearValue(FontFamilyProperty);
        AddCategorySettingsButton.ClearValue(MinWidthProperty);
        AddCategorySettingsButton.ClearValue(MinHeightProperty);
        _behaviorMoved.Clear();
        if (_behaviorPage.Parent is Panel host) host.Children.Remove(_behaviorPage);
        _behaviorPage = null;
        _behaviorBodies = [];
        _behaviorTabs = [];
        _behaviorChoices = [];
        _behaviorScroll = null;
        _behaviorHeading = null;
        _behaviorTabRow = null;
        _behaviorCategories = null;
        _behaviorCategoryEmpty = null;
        _behaviorClock = null;
        _behaviorFocusChoices = null;
        _behaviorShortcutHint = _behaviorLeader = _behaviorStartKey = _behaviorFocusHint = null;
        _behaviorStacked = null;
        _behaviorScrollHeader = false;
        UpdateSelectedProjectIdleUi(IdleProjectComboBox.SelectedItem as string);
    }

    private static void ApplyBehaviorStyles(Grid page)
    {
        page.Styles.Add(new Style(s => s.Is<TemplatedControl>().Class("behaviorField")) { Setters =
        {
            new Setter(TemplatedControl.BackgroundProperty, BehaviorPaper),
            new Setter(TemplatedControl.ForegroundProperty, SettingsInk),
            new Setter(TemplatedControl.BorderBrushProperty, SettingsRule),
            new Setter(TemplatedControl.FontFamilyProperty, ClockPickerPresentation.Body),
            new Setter(FontSizeProperty, 13d), new Setter(MinWidthProperty, 0d)
        } });
        page.Styles.Add(new Style(s => s.OfType<TextBox>().Class("behaviorNumber")) { Setters =
        {
            new Setter(WidthProperty, 68d), new Setter(TextBox.TextAlignmentProperty, TextAlignment.Center)
        } });
        page.Styles.Add(new Style(s => s.OfType<TextBox>().Class("invalidTime").Template().OfType<Border>().Name("PART_BorderElement"))
            { Setters = { new Setter(Border.BorderBrushProperty, BehaviorDanger) } });
        page.Styles.Add(new Style(s => s.OfType<TextBox>().Class("behaviorField").Template().OfType<Border>().Name("PART_BorderElement")) { Setters =
        {
            new Setter(Border.BackgroundProperty, BehaviorPaper), new Setter(Border.BorderBrushProperty, SettingsRule)
        } });
    }

    private static Grid BehaviorColumns(Control first, Control second)
    {
        var grid = new Grid { ColumnDefinitions = new("1.05*,16,1*"), Children = { first, second } };
        Grid.SetColumn(second, 2);
        return grid;
    }

    private static StackPanel BehaviorStack(params Control[] children)
    {
        var stack = new StackPanel { Spacing = 10 };
        foreach (var child in children) stack.Children.Add(child);
        return stack;
    }

    private static Border BehaviorCard(string title, params Control[] children)
    {
        var card = SettingsCard(BehaviorStack([SettingsLabel(title, 18, SettingsInk), .. children]));
        card.VerticalAlignment = VerticalAlignment.Top;
        return card;
    }

    private Control BehaviorChoice(CheckBox check, string title, string? detail = null)
    {
        _behaviorCheckContents.Add(check, check.Content);
        var caption = SettingsLabel(title, 13, SettingsInk);
        check.Content = detail == null ? caption : new StackPanel { Spacing = 2, Children = { caption, SettingsLabel(detail, 11) } };
        check.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        AutomationProperties.SetName(check, title);
        return check;
    }

    private static Control BehaviorField(Control control, string label)
    {
        control.Classes.Add("behaviorField");
        AutomationProperties.SetName(control, label);
        return control;
    }

    private static Control BehaviorNumber(string title, TextBox field, string unit)
    {
        BehaviorField(field, $"{title} in {(unit == "min" ? "minutes" : "seconds")}");
        field.Classes.Add("behaviorNumber");
        return BehaviorRow(SettingsLabel(title, 13, SettingsInk), field, SettingsLabel(unit, 12));
    }

    private static Grid BehaviorRow(Control first, params Control[] rest)
    {
        var row = new Grid { ColumnDefinitions = new("*"), Children = { first } };
        foreach (var control in rest)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(8)));
            Grid.SetColumn(control, row.ColumnDefinitions.Count);
            row.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            row.Children.Add(control);
        }
        return row;
    }

    private static TextBlock BehaviorName(string name)
    {
        var text = SettingsLabel(name, 13, SettingsInk);
        text.TextWrapping = TextWrapping.NoWrap;
        text.TextTrimming = TextTrimming.CharacterEllipsis;
        ToolTip.SetTip(text, name);
        return text;
    }

    private static Button BehaviorAction(string title, string name = "")
    {
        var button = SettingsAction(title, name);
        button.Height = 32;
        return button;
    }

    private static Border BehaviorKeycap(TextBlock text)
    {
        DesktopTypography.SetPreserveColor(text, true);
        return new Border { Background = SettingsInk, CornerRadius = new(4), Margin = new(0, 0, 6, 3), Padding = new(8, 5), Child = text };
    }
}
