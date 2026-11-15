using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Styling;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private ScrollViewer? _activityWatchPage;
    private Grid? _activityWatchCards;
    private bool? _activityWatchStacked;
    private TextBlock? _activityWatchIntegration, _activityWatchAddressHint, _activityWatchLaunchHint,
        _activityWatchHeading, _activityWatchDetail, _activityWatchVersion, _activityWatchCheckedAt;
    private Border? _activityWatchStateBox;
    private ProgressBar? _activityWatchProgress;
    private readonly List<Button> _activityWatchTabs = new();
    private readonly List<(Control Control, Panel Parent, int Index, Thickness Margin, double Height,
        object? Content, Thickness Padding, int Column, int Row, VerticalAlignment Alignment)> _activityWatchMoved = new();

    private void BuildActivityWatchPage(Grid host)
    {
        foreach (var control in new Control[] { ActivityWatchEnabledCheck, ActivityWatchServerUrlTextBox,
            TestActivityWatchButton, StartActivityWatchAppButton, ActivityWatchSyncCard, ActivityWatchReviewCard,
            ActivityWatchSyncOnStopwatchCheck, ActivityWatchPeriodicSyncCheck, ActivityWatchPeriodicSyncIntervalTextBox,
            ActivityWatchIncludeTitlesCheck, ActivityWatchIncludeWebCheck, ActivityWatchFileNameTextBox,
            ActivityWatchMinDurationTextBox, SyncActivityWatchNowButton, PeriodicReviewEnabledCheck, PeriodicReviewIntervalTextBox,
            PeriodicReviewSnoozeTextBox, PeriodicReviewAutoDismissTextBox, PeriodicReviewMinDurationTextBox, PeriodicReviewMinPercentTextBox, ReviewNowButton })
        {
            var parent = (Panel)control.Parent!;
            _activityWatchMoved.Add((control, parent, parent.Children.IndexOf(control), control.Margin, control.Height,
                (control as ContentControl)?.Content, (control as TemplatedControl)?.Padding ?? default, Grid.GetColumn(control), Grid.GetRow(control), control.VerticalAlignment));
        }
        foreach (var item in _activityWatchMoved) { item.Parent.Children.Remove(item.Control); item.Control.Margin = default; }
        ActivityWatchServerLabel.IsVisible = ActivityWatchServerHint.IsVisible = false;
        ActivityWatchEnabledCheck.Content = SettingsLabel("Enable integration in Stopwatch", 13, SettingsInk);
        ActivityWatchEnabledCheck.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        ActivityWatchEnabledCheck.VerticalContentAlignment = VerticalAlignment.Center;
        AutomationProperties.SetName(ActivityWatchEnabledCheck, "Enable ActivityWatch integration in Stopwatch");
        ActivityWatchServerUrlTextBox.Classes.Add("editorField"); ActivityWatchServerUrlTextBox.Height = double.NaN;
        ActivityWatchServerUrlTextBox.Padding = new(10, 6); ActivityWatchServerUrlTextBox.Watermark = "http://localhost:5600";
        AutomationProperties.SetName(ActivityWatchServerUrlTextBox, "ActivityWatch server address");
        foreach (var button in new[] { TestActivityWatchButton, StartActivityWatchAppButton })
        {
            NavigatorFormStyles.StyleButton(button, button == TestActivityWatchButton);
            button.Height = double.NaN; button.MinHeight = 36; button.Padding = new(12, 6); button.Margin = new(0, 0, 8, 4);
        }
        TestActivityWatchButton.Content = "Test connection";
        _activityWatchIntegration = SettingsLabel("", 12);
        _activityWatchAddressHint = SettingsLabel("", 12); _activityWatchLaunchHint = SettingsLabel("", 12);
        var connection = TelegramCard("Connection", ActivityWatchEnabledCheck, _activityWatchIntegration,
            SettingsLabel("Use ActivityWatch history in your reports and activity reviews. Turning this off leaves ActivityWatch running.", 12),
            new Border { Height = 1, Background = Brush.Parse("#509A7B48"), Margin = new(0, 2) },
            ObsidianField("Server address", ActivityWatchServerUrlTextBox), _activityWatchAddressHint,
            new WrapPanel { Children = { TestActivityWatchButton, StartActivityWatchAppButton } }, _activityWatchLaunchHint);
        _activityWatchHeading = SettingsLabel("", 18); _activityWatchHeading.Name = "ActivityWatchConnectionHeading";
        _activityWatchDetail = SettingsLabel("", 13); _activityWatchDetail.Name = "ActivityWatchConnectionDetail";
        _activityWatchProgress = new ProgressBar { Height = 4, IsIndeterminate = true, Foreground = Brush.Parse("#2D7169"), IsVisible = false };
        _activityWatchStateBox = new Border { BorderThickness = new(1), CornerRadius = new(5), Padding = new(12),
            Child = new StackPanel { Spacing = 7, Children = { _activityWatchHeading, _activityWatchDetail, _activityWatchProgress } } };
        _activityWatchVersion = SettingsLabel("", 14, SettingsInk); _activityWatchVersion.Name = "ActivityWatchServerVersion";
        _activityWatchCheckedAt = SettingsLabel("", 14, SettingsInk);
        var metrics = new Grid { ColumnDefinitions = new("*,12,*"), Children = {
            new StackPanel { Spacing = 4, Children = { SettingsLabel("SERVER VERSION", 9), _activityWatchVersion } },
            new StackPanel { Spacing = 4, Children = { SettingsLabel("LAST CHECK", 9), _activityWatchCheckedAt } } } };
        Grid.SetColumn(metrics.Children[1], 2);
        var status = TelegramCard("Connection status", _activityWatchStateBox, metrics,
            SettingsLabel("A successful check confirms the server is reachable. It does not verify that activity is being recorded.", 12));
        var help = TelegramCard("How it works", SettingsLabel("ActivityWatch records app, window and browser activity through its watchers.", 12),
            SettingsLabel("Stopwatch reads that history for reports. Export choices and review prompts have their own settings.", 12));
        _activityWatchCards = new Grid { Children = { connection, new StackPanel { Spacing = 14, Children = { status, help } } } };
        BuildActivityWatchExport(); BuildActivityWatchReview();
        var tabs = new WrapPanel();
        foreach (var (title, name) in new[] { ("Connection & status", "Connection"), ("Sync & export", "Sync"), ("Review prompts", "Review") })
        {
            int index = _activityWatchTabs.Count;
            var tab = SettingsAction(title, "ActivityWatch" + name + "Tab"); tab.Margin = new(0, 0, 8, 4);
            tab.Height = double.NaN; tab.MinHeight = 36; tab.Padding = new(12, 6);
            tab.Click += (_, _) => SelectActivityWatchTab(index); _activityWatchTabs.Add(tab); tabs.Children.Add(tab);
        }
        var heading = new StackPanel { Spacing = 4, Children = { SettingsLabel("ActivityWatch", 23, SettingsInk),
            SettingsLabel("Connect your activity history and choose how it updates your reports.", 12) } };
        _activityWatchPage = new ScrollViewer { Name = "ActivityWatchScroll", HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 5, 0), Content = new StackPanel { Spacing = 14,
                Children = { heading, tabs, _activityWatchCards, _activityWatchExportCards!, _activityWatchReviewCards!, ActivityWatchSyncCard, ActivityWatchReviewCard } } };
        _activityWatchPage.Styles.Add(new Style(s => s.OfType<TextBox>().Class("editorField")) { Setters = {
            new Setter(FontFamilyProperty, ClockPickerPresentation.Body), new Setter(FontSizeProperty, 13d),
            new Setter(MinWidthProperty, 0d), new Setter(MinHeightProperty, 36d) } });
        _activityWatchPage.SizeChanged += (_, _) => ResizeActivityWatchPage(); heading.SizeChanged += (_, _) => ResizeActivityWatchPage();
        host.Children.Add(_activityWatchPage); SelectActivityWatchTab(0); RefreshActivityWatchConnection(); RefreshActivityWatchExport();
    }

    private void SelectActivityWatchTab(int index)
    {
        if (index != 0) CancelActivityWatchConnection();
        if (index != 1) CancelActivityWatchExport();
        _activityWatchCards!.IsVisible = index == 0;
        _activityWatchExportCards!.IsVisible = index == 1;
        ActivityWatchSyncCard.IsVisible = ActivityWatchReviewCard.IsVisible = false;
        _activityWatchReviewCards!.IsVisible = index == 2;
        for (int i = 0; i < _activityWatchTabs.Count; i++) _activityWatchTabs[i].Classes.Set("editorPrimary", i == index);
        _activityWatchPage!.Offset = default; RefreshActivityWatchExport(); RefreshActivityWatchReview();
    }

    private void ResizeActivityWatchPage()
    {
        if (_closed || _activityWatchPage == null || _activityWatchPage.Bounds.Width <= 0) return;
        double textScale = _settings.Typography.Resolve("Settings").SizePercent / 100;
        bool stacked = _activityWatchPage.Bounds.Width < 760 * textScale;
        foreach (var field in new[] { PeriodicReviewIntervalTextBox, PeriodicReviewSnoozeTextBox,
            PeriodicReviewAutoDismissTextBox, PeriodicReviewMinDurationTextBox, PeriodicReviewMinPercentTextBox })
            if (field.Parent is Grid row && row.ColumnDefinitions.Count == 5)
                row.ColumnDefinitions[2].Width = new GridLength(72 * Math.Max(1, textScale));
        if (_activityWatchStacked == stacked) return;
        _activityWatchStacked = stacked;
        foreach (var cards in new[] { _activityWatchCards!, _activityWatchExportCards!, _activityWatchReviewCards! })
        {
            cards.ColumnDefinitions = new(stacked ? "*" : "1.1*,16,1*");
            cards.RowDefinitions = new(stacked ? "Auto,14,Auto" : "Auto");
            Grid.SetColumn(cards.Children[1], stacked ? 0 : 2); Grid.SetRow(cards.Children[1], stacked ? 2 : 0);
        }
    }

    private void RestoreActivityWatchPage()
    {
        CancelActivityWatchConnection(); CancelActivityWatchExport();
        if (_activityWatchPage == null) return;
        foreach (var item in _activityWatchMoved.OrderBy(item => item.Index))
        {
            if (item.Control.Parent is Panel panel) panel.Children.Remove(item.Control);
            item.Control.Margin = item.Margin; item.Control.Height = item.Height;
            Grid.SetColumn(item.Control, item.Column); Grid.SetRow(item.Control, item.Row); item.Control.VerticalAlignment = item.Alignment;
            item.Control.Classes.Remove("editorField");
            if (item.Control is ContentControl content) content.Content = item.Content;
            if (item.Control is TemplatedControl templated) templated.Padding = item.Padding;
            if (item.Control is TextBox field) field.ClearValue(TextBox.WatermarkProperty);
            if (item.Control is CheckBox check)
            { check.ClearValue(HorizontalContentAlignmentProperty); check.ClearValue(VerticalContentAlignmentProperty); }
            else if (item.Control is Button button)
            {
                button.Classes.Remove("editorAction"); button.Classes.Remove("editorPrimary"); button.Classes.Add("modern");
                button.ClearValue(HorizontalAlignmentProperty); button.ClearValue(FontFamilyProperty); button.ClearValue(MinWidthProperty); button.ClearValue(MinHeightProperty);
            }
            item.Parent.Children.Insert(item.Index, item.Control);
        }
        ActivityWatchServerLabel.IsVisible = ActivityWatchServerHint.IsVisible = true;
        ActivityWatchSyncCard.IsVisible = ActivityWatchReviewCard.IsVisible = true;
        ((Panel)_activityWatchPage.Parent!).Children.Remove(_activityWatchPage);
        _activityWatchMoved.Clear(); _activityWatchTabs.Clear(); _activityWatchPage = null; _activityWatchCards = null;
        _activityWatchStacked = null; _activityWatchIntegration = _activityWatchAddressHint = _activityWatchLaunchHint = null;
        _activityWatchHeading = _activityWatchDetail = _activityWatchVersion = _activityWatchCheckedAt = null;
        _activityWatchExportCards = null; _activityWatchChangeFolder = null; _activityWatchReviewCards = null;
        _activityWatchStateBox = null; _activityWatchProgress = null;
        RefreshActivityWatchConnection(); RefreshActivityWatchExport();
    }
}
