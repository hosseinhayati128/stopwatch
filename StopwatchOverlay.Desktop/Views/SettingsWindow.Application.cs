using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private ScrollViewer? _applicationPage;
    private Grid? _applicationCards;
    private RadioButton[] _applicationCloseChoices = [];
    private Border[] _applicationCloseRows = [];
    private TextBlock[] _applicationCloseTitles = [], _applicationCloseDetails = [];
    private (Panel Parent, int Index, object? Content, Thickness Margin)? _applicationStartupSlot;
    private bool _syncingApplicationChoices;
    private bool? _applicationStacked;
    private static readonly IBrush ApplicationPaper = Brush.Parse("#F7E8C7");

    private void BuildApplicationPage(Grid host)
    {
        var parent = (Panel)StartWithWindowsCheck.Parent!;
        _applicationStartupSlot = (parent, parent.Children.IndexOf(StartWithWindowsCheck), StartWithWindowsCheck.Content, StartWithWindowsCheck.Margin);
        parent.Children.Remove(StartWithWindowsCheck);
        StartWithWindowsCheck.Content = new StackPanel { Spacing = 4, Children =
        {
            SettingsLabel("Start with Windows", 14, SettingsInk), SettingsLabel("Open Stopwatch when you sign in.", 12)
        } };
        StartWithWindowsCheck.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        StartWithWindowsCheck.VerticalContentAlignment = VerticalAlignment.Top;
        StartWithWindowsCheck.Margin = new(0, 4, 0, 0);
        AutomationProperties.SetName(StartWithWindowsCheck, "Start with Windows");

        string[] titles = ["Ask every time", "Minimize to tray", "Exit Stopwatch"];
        string[] details =
        [
            "Choose whether to minimize or exit each time you close the controller.",
            "Hide the controller. Timers and overlays keep running.",
            "Close the app and all its overlays."
        ];
        _applicationCloseChoices = new RadioButton[3];
        _applicationCloseRows = new Border[3];
        _applicationCloseTitles = new TextBlock[3];
        _applicationCloseDetails = new TextBlock[3];
        for (int i = 0; i < titles.Length; i++)
        {
            int index = i;
            _applicationCloseTitles[i] = SettingsLabel(titles[i], 14, SettingsInk);
            _applicationCloseDetails[i] = SettingsLabel(details[i], 12);
            var option = new RadioButton
            {
                Name = "ApplicationClose" + i, GroupName = "ApplicationCloseAction",
                HorizontalAlignment = HorizontalAlignment.Stretch, HorizontalContentAlignment = HorizontalAlignment.Stretch,
                VerticalContentAlignment = VerticalAlignment.Top,
                Content = new StackPanel { Spacing = 4, Children = { _applicationCloseTitles[i], _applicationCloseDetails[i] } }
            };
            AutomationProperties.SetName(option, titles[i]);
            _applicationCloseChoices[i] = option;
            _applicationCloseRows[i] = new Border
            {
                Background = ApplicationPaper, BorderBrush = SettingsRule, BorderThickness = new(1),
                CornerRadius = new(5), Padding = new(12), Child = option
            };
            option.IsCheckedChanged += (_, _) =>
            {
                if (!_syncingApplicationChoices && !_loading && !_closed && option.IsChecked == true)
                    CloseActionCombo.SelectedIndex = index;
            };
        }
        var startup = ApplicationCard("Startup", StartWithWindowsCheck);
        var close = ApplicationCard("Close button", SettingsLabel("When you close the main controller", 12),
            _applicationCloseRows[0], _applicationCloseRows[1], _applicationCloseRows[2]);
        _applicationCards = new Grid { Children = { startup, close } };
        var heading = new StackPanel { Spacing = 4, Margin = new(0, 0, 0, 16), Children =
        {
            SettingsLabel("Application", 23, SettingsInk), SettingsLabel("Choose how Stopwatch opens and closes.", 12)
        } };
        _applicationPage = new ScrollViewer
        {
            Name = "ApplicationScroll", HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = new StackPanel { Children = { heading, _applicationCards } }
        };
        _applicationPage.SizeChanged += (_, _) => ResizeApplicationPage();
        heading.SizeChanged += (_, _) => ResizeApplicationPage();
        host.Children.Add(_applicationPage);
        RefreshApplicationChoices();
    }

    private void RefreshApplicationChoices()
    {
        if (_applicationPage == null) return;
        _syncingApplicationChoices = true;
        try
        {
            for (int i = 0; i < _applicationCloseChoices.Length; i++)
            {
                bool selected = i == CloseActionCombo.SelectedIndex;
                _applicationCloseChoices[i].IsChecked = selected;
                _applicationCloseRows[i].Background = selected ? SettingsInk : ApplicationPaper;
                _applicationCloseTitles[i].Foreground = selected ? ApplicationPaper : SettingsInk;
                _applicationCloseDetails[i].Foreground = selected ? ApplicationPaper : SettingsMuted;
            }
        }
        finally { _syncingApplicationChoices = false; }
        ResizeApplicationPage();
    }

    private void ResizeApplicationPage()
    {
        if (_closed || _applicationPage == null || _applicationPage.Bounds.Width <= 0) return;
        bool stacked = _applicationPage.Bounds.Width < 680 * _settings.Typography.Resolve("Settings").SizePercent / 100;
        if (_applicationStacked == stacked) return;
        _applicationStacked = stacked;
        _applicationCards!.ColumnDefinitions = new(stacked ? "*" : "0.85*,16,1.15*");
        _applicationCards.RowDefinitions = new(stacked ? "Auto,14,Auto" : "Auto");
        Grid.SetColumn(_applicationCards.Children[1], stacked ? 0 : 2);
        Grid.SetRow(_applicationCards.Children[1], stacked ? 2 : 0);
    }

    private void RestoreApplicationPage()
    {
        if (_applicationPage == null || _applicationStartupSlot is not { } slot) return;
        ((Panel)StartWithWindowsCheck.Parent!).Children.Remove(StartWithWindowsCheck);
        StartWithWindowsCheck.Content = slot.Content;
        StartWithWindowsCheck.Margin = slot.Margin;
        StartWithWindowsCheck.ClearValue(HorizontalContentAlignmentProperty);
        StartWithWindowsCheck.ClearValue(VerticalContentAlignmentProperty);
        slot.Parent.Children.Insert(slot.Index, StartWithWindowsCheck);
        ((Panel)_applicationPage.Parent!).Children.Remove(_applicationPage);
        _applicationPage = null;
        _applicationCards = null;
        _applicationStartupSlot = null;
        _applicationStacked = null;
        _applicationCloseChoices = [];
        _applicationCloseRows = [];
        _applicationCloseTitles = _applicationCloseDetails = [];
    }

    private static Border ApplicationCard(string title, params Control[] content)
    {
        var body = new StackPanel { Spacing = 12, Children = { SettingsLabel(title, 18, SettingsInk) } };
        foreach (var control in content) body.Children.Add(control);
        var card = SettingsCard(body);
        card.Padding = new(16);
        card.VerticalAlignment = VerticalAlignment.Top;
        return card;
    }
}
