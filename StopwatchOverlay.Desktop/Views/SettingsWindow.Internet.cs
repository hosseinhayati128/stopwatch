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
    private ScrollViewer? _internetPage;
    private Grid? _internetCards;
    private bool? _internetStacked;
    private TextBlock? _internetHeading, _internetDetail, _internetNetwork, _internetAdapter, _internetPing, _internetSpeed,
        _internetCheckedAt, _internetIntervalHint, _internetScheduleHeading, _internetScheduleDetail;
    private Border? _internetStateBox, _internetScheduleBox;
    private ProgressBar? _internetProgress;
    private Button? _internetConnectionTab, _internetHistoryTab;
    private readonly List<(Control Control, Panel Parent, int Index, Thickness Margin, double Height,
        object? Content, Thickness Padding)> _internetMoved = new();

    private void BuildInternetPage(Grid host)
    {
        foreach (var control in new Control[] { InternetMonitorEnabledCheck, InternetMonitorOnlyDuringTimersCheck,
            InternetCheckIntervalTextBox, TestInternetButton, InternetExportPanel })
        {
            var parent = (Panel)control.Parent!;
            _internetMoved.Add((control, parent, parent.Children.IndexOf(control), control.Margin, control.Height,
                (control as ContentControl)?.Content, (control as TemplatedControl)?.Padding ?? default));
        }
        foreach (var item in _internetMoved) { item.Parent.Children.Remove(item.Control); item.Control.Margin = default; }
        void Choice(CheckBox check, string title, string detail)
        {
            check.Content = new StackPanel { Spacing = 3, Children = { SettingsLabel(title, 13, SettingsInk), SettingsLabel(detail, 11) } };
            check.HorizontalContentAlignment = HorizontalAlignment.Stretch; check.VerticalContentAlignment = VerticalAlignment.Top;
            AutomationProperties.SetName(check, title);
        }
        Choice(InternetMonitorEnabledCheck, "Check automatically", "While Stopwatch is open.");
        Choice(InternetMonitorOnlyDuringTimersCheck, "Only while a stopwatch is running", "Skip scheduled checks when no stopwatch is running.");
        InternetCheckIntervalTextBox.Height = double.NaN; InternetCheckIntervalTextBox.Width = 70;
        InternetCheckIntervalTextBox.Padding = new(10, 6); InternetCheckIntervalTextBox.Classes.Add("editorField");
        AutomationProperties.SetName(InternetCheckIntervalTextBox, "Check interval in minutes");
        NavigatorFormStyles.StyleButton(TestInternetButton, true); TestInternetButton.Height = double.NaN;
        TestInternetButton.MinHeight = 36; TestInternetButton.Padding = new(12, 6); TestInternetButton.HorizontalAlignment = HorizontalAlignment.Left;
        TextBlock Named(string name, double size, IBrush? brush = null)
        { var label = SettingsLabel("", size, brush); label.Name = name; return label; }
        _internetHeading = Named("InternetConnectionHeading", 21); _internetHeading.FontWeight = FontWeight.SemiBold;
        _internetDetail = Named("InternetConnectionDetail", 12);
        _internetProgress = new ProgressBar { Height = 3, IsIndeterminate = true, Foreground = Brush.Parse("#2D7169"), IsVisible = false };
        _internetStateBox = new Border { BorderThickness = new(1), CornerRadius = new(5), Padding = new(12),
            Child = new StackPanel { Spacing = 5, Children = { _internetHeading, _internetDetail, _internetProgress } } };
        _internetNetwork = Named("InternetNetworkName", 16, SettingsInk); _internetAdapter = Named("InternetNetworkDetail", 11);
        _internetPing = Named("InternetLatency", 23, SettingsInk); _internetSpeed = Named("InternetDownload", 23, SettingsInk);
        _internetPing.FontWeight = _internetSpeed.FontWeight = FontWeight.SemiBold;
        Border Metric(string title, TextBlock value) => new() { Padding = new(12), CornerRadius = new(5), Background = Brush.Parse("#55FFF8E8"),
            Child = new StackPanel { Spacing = 5, Children = { SettingsLabel(title, 9), value } } };
        var metrics = new Grid { ColumnDefinitions = new("*,10,*"), Children = { Metric("LATENCY", _internetPing), Metric("DOWNLOAD", _internetSpeed) } };
        Grid.SetColumn(metrics.Children[1], 2);
        _internetCheckedAt = Named("InternetLastChecked", 11);
        Border Card(string title, params Control[] controls) { var card = TelegramCard(title, controls); ((StackPanel)card.Child!).Spacing = 12; return card; }
        var status = Card("Last connection check", _internetStateBox,
            new StackPanel { Spacing = 4, Children = { SettingsLabel("NETWORK AT LAST CHECK", 9), _internetNetwork, _internetAdapter } }, metrics,
            new StackPanel { Spacing = 3, Children = { SettingsLabel("LAST CHECKED", 9), _internetCheckedAt } }, TestInternetButton,
            SettingsLabel("Uses a short download sample. Results describe the check, not a continuous live connection.", 11));
        _internetIntervalHint = Named("InternetIntervalHint", 11);
        _internetScheduleHeading = Named("InternetScheduleHeading", 13); _internetScheduleHeading.FontWeight = FontWeight.SemiBold;
        _internetScheduleDetail = Named("InternetScheduleDetail", 11);
        _internetScheduleBox = new Border { Padding = new(12), CornerRadius = new(5),
            Child = new StackPanel { Spacing = 5, Children = { _internetScheduleHeading, _internetScheduleDetail } } };
        var monitoring = Card("Background monitoring", InternetMonitorEnabledCheck, new Border { Height = 1, Background = SettingsRule },
            new StackPanel { Spacing = 6, Children = { SettingsLabel("Check every", 12, SettingsInk),
                new StackPanel { Orientation = Orientation.Horizontal, Spacing = 9, Children = { InternetCheckIntervalTextBox, SettingsLabel("minutes", 12) } }, _internetIntervalHint } },
            InternetMonitorOnlyDuringTimersCheck, _internetScheduleBox, SettingsLabel("Test now works even when automatic checks are off.", 11));
        _internetCards = new Grid { Children = { status, monitoring } };
        _internetConnectionTab = SettingsAction("Connection & monitoring", "InternetConnectionTab");
        _internetHistoryTab = SettingsAction("History & export", "InternetHistoryTab");
        foreach (var tab in new[] { _internetConnectionTab, _internetHistoryTab })
        { tab.Height = double.NaN; tab.MinHeight = 36; tab.Padding = new(12, 6); tab.Margin = new(0, 0, 8, 4); }
        _internetConnectionTab.Click += (_, _) => SelectInternetTab(false); _internetHistoryTab.Click += (_, _) => SelectInternetTab(true);
        var heading = new StackPanel { Spacing = 4, Children = { SettingsLabel("Internet & Network", 23, SettingsInk),
            SettingsLabel("Check your connection and choose when to monitor it.", 12) } };
        _internetPage = new ScrollViewer { Name = "InternetScroll", HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new(0, 0, 5, 0),
            Content = new StackPanel { Spacing = 14, Children = { heading, new WrapPanel { Children = { _internetConnectionTab, _internetHistoryTab } }, _internetCards, InternetExportPanel } } };
        _internetPage.Styles.Add(new Style(s => s.OfType<TextBox>().Class("editorField")) { Setters = {
            new Setter(FontFamilyProperty, ClockPickerPresentation.Body), new Setter(FontSizeProperty, 13d), new Setter(MinWidthProperty, 0d), new Setter(MinHeightProperty, 36d) } });
        _internetPage.SizeChanged += (_, _) => ResizeInternetPage(); heading.SizeChanged += (_, _) => ResizeInternetPage();
        host.Children.Add(_internetPage); SelectInternetTab(false); RefreshInternetConnection();
    }

    private void SelectInternetTab(bool history)
    {
        if (history) CancelInternetCheck();
        _internetCards!.IsVisible = !history; InternetExportPanel.IsVisible = history;
        _internetConnectionTab!.Classes.Set("editorPrimary", !history); _internetHistoryTab!.Classes.Set("editorPrimary", history);
        _internetPage!.Offset = default; RefreshInternetConnection();
    }

    private void ResizeInternetPage()
    {
        if (_closed || _internetPage == null || _internetPage.Bounds.Width <= 0) return;
        double textScale = _settings.Typography.Resolve("Settings").SizePercent / 100;
        InternetCheckIntervalTextBox.Width = 70 * Math.Max(1, textScale);
        bool stacked = _internetPage.Bounds.Width < 740 * textScale;
        if (_internetStacked == stacked) return; _internetStacked = stacked;
        _internetCards!.ColumnDefinitions = new(stacked ? "*" : "1.05*,16,1*"); _internetCards.RowDefinitions = new(stacked ? "Auto,14,Auto" : "Auto");
        Grid.SetColumn(_internetCards.Children[1], stacked ? 0 : 2); Grid.SetRow(_internetCards.Children[1], stacked ? 2 : 0);
    }

    private void RestoreInternetPage()
    {
        CancelInternetCheck(); if (_internetPage == null) return;
        foreach (var item in _internetMoved.OrderBy(item => item.Index))
        {
            ((Panel)item.Control.Parent!).Children.Remove(item.Control);
            item.Control.Margin = item.Margin; item.Control.Height = item.Height; item.Control.Classes.Remove("editorField");
            if (item.Control is ContentControl content) content.Content = item.Content;
            if (item.Control is TemplatedControl templated) templated.Padding = item.Padding;
            if (item.Control is CheckBox check) { check.ClearValue(HorizontalContentAlignmentProperty); check.ClearValue(VerticalContentAlignmentProperty); }
            if (item.Control is TextBox field) field.ClearValue(WidthProperty);
            if (item.Control is Button button)
            {
                button.Classes.Remove("editorAction"); button.Classes.Remove("editorPrimary"); button.Classes.Add("modern");
                button.ClearValue(HorizontalAlignmentProperty); button.ClearValue(FontFamilyProperty); button.ClearValue(MinWidthProperty); button.ClearValue(MinHeightProperty);
            }
            item.Parent.Children.Insert(item.Index, item.Control);
        }
        InternetExportPanel.IsVisible = true;
        ((Panel)_internetPage.Parent!).Children.Remove(_internetPage);
        _internetMoved.Clear(); _internetPage = null; _internetCards = null; _internetStacked = null;
        _internetConnectionTab = _internetHistoryTab = null;
        RefreshInternetConnection();
    }
}
