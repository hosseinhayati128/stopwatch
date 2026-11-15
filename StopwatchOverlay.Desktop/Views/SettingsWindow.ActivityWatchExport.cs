using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private Grid? _activityWatchExportCards;
    private TextBlock? _activityWatchAutoState, _activityWatchIntervalHint, _activityWatchMinimumHint, _activityWatchFileHint,
        _activityWatchFolder, _activityWatchExportHeading, _activityWatchExportDetail, _activityWatchExportCounts, _activityWatchExportTime;
    private Border? _activityWatchExportBox;
    private ProgressBar? _activityWatchExportProgress;
    private Button? _activityWatchChangeFolder;

    private void BuildActivityWatchExport()
    {
        var controls = new Control[] { ActivityWatchSyncOnStopwatchCheck, ActivityWatchPeriodicSyncCheck,
            ActivityWatchPeriodicSyncIntervalTextBox, ActivityWatchIncludeTitlesCheck, ActivityWatchIncludeWebCheck,
            ActivityWatchFileNameTextBox, ActivityWatchMinDurationTextBox, SyncActivityWatchNowButton };
        foreach (var control in controls)
        {
            if (control is TextBox field) { field.Classes.Add("editorField"); field.Height = double.NaN; field.Padding = new(10, 6); }
        }
        ObsidianChoice(ActivityWatchSyncOnStopwatchCheck, "With stopwatch exports", "Update activity when stopwatch time is exported.");
        ObsidianChoice(ActivityWatchPeriodicSyncCheck, "On a schedule", "");
        ObsidianChoice(ActivityWatchIncludeTitlesCheck, "App title summaries", "Show each app’s top window or document.");
        ObsidianChoice(ActivityWatchIncludeWebCheck, "Browser activity", "Include websites and page titles.");
        foreach (var check in controls.OfType<CheckBox>())
        {
            var labels = (StackPanel)check.Content!; labels.Spacing = 3;
            ((TextBlock)labels.Children[0]).FontSize = 13;
            var detail = (TextBlock)labels.Children[1]; detail.FontSize = 11; detail.IsVisible = !string.IsNullOrEmpty(detail.Text);
        }
        AutomationProperties.SetName(ActivityWatchPeriodicSyncIntervalTextBox, "Sync interval in minutes");
        AutomationProperties.SetName(ActivityWatchMinDurationTextBox, "Minimum activity in seconds");
        AutomationProperties.SetName(ActivityWatchFileNameTextBox, "ActivityWatch export file name");
        _activityWatchAutoState = SettingsLabel("", 11);
        _activityWatchIntervalHint = SettingsLabel("", 11); _activityWatchIntervalHint.Name = "ActivityWatchIntervalHint";
        var automatic = TelegramCard("Automatic sync", _activityWatchAutoState, ActivityWatchSyncOnStopwatchCheck,
            ActivityWatchPeriodicSyncCheck, ExportNumberRow("Every", ActivityWatchPeriodicSyncIntervalTextBox, "minutes"), _activityWatchIntervalHint);
        _activityWatchMinimumHint = SettingsLabel("", 11);
        var content = TelegramCard("Report content", ActivityWatchIncludeTitlesCheck, ActivityWatchIncludeWebCheck,
            ExportNumberRow("Minimum activity", ActivityWatchMinDurationTextBox, "seconds"), _activityWatchMinimumHint,
            SettingsLabel("Title summaries affect the app table. Timeline details can still contain titles.", 11));
        _activityWatchFolder = SettingsLabel("", 13, SettingsInk); _activityWatchFolder.Name = "ActivityWatchFolderText";
        _activityWatchFolder.MaxLines = 2; _activityWatchFolder.TextTrimming = TextTrimming.CharacterEllipsis;
        _activityWatchChangeFolder = SettingsAction("Change folder", "ActivityWatchChangeFolder");
        _activityWatchChangeFolder.Click += (_, _) => NavigationList.SelectedItem = NavigationList.Items.OfType<ListBoxItem>().Single(item => Equals(item.Tag, "Obsidian"));
        AutomationProperties.SetName(_activityWatchChangeFolder, "Change export folder in Obsidian / Markdown settings");
        var folderRow = new Grid { ColumnDefinitions = new("*,10,Auto"), Children = {
            new StackPanel { Spacing = 4, VerticalAlignment = VerticalAlignment.Center, Children = { _activityWatchFolder,
                SettingsLabel("Shared with Obsidian / Markdown settings.", 11) } }, _activityWatchChangeFolder } };
        Grid.SetColumn(_activityWatchChangeFolder, 2);
        NavigatorFormStyles.StyleButton(SyncActivityWatchNowButton, true);
        foreach (var button in new[] { SyncActivityWatchNowButton, _activityWatchChangeFolder })
        { button.Height = double.NaN; button.MinHeight = 36; button.Padding = new(12, 6); button.HorizontalAlignment = HorizontalAlignment.Left; }
        _activityWatchFileHint = SettingsLabel("", 11);
        var destination = TelegramCard("Export to Markdown", SettingsLabel("EXPORT FOLDER", 9), folderRow,
            ObsidianField("File name", ActivityWatchFileNameTextBox), _activityWatchFileHint,
            new Border { Height = 1, Background = Brush.Parse("#509A7B48") },
            SettingsLabel("Updates today’s section in this file. Other days stay in place.", 12), SyncActivityWatchNowButton);
        _activityWatchExportHeading = SettingsLabel("", 16); _activityWatchExportHeading.Name = "ActivityWatchExportHeading";
        _activityWatchExportDetail = SettingsLabel("", 12); _activityWatchExportDetail.Name = "ActivityWatchExportDetail";
        _activityWatchExportCounts = SettingsLabel("", 15, SettingsInk); _activityWatchExportTime = SettingsLabel("", 11);
        _activityWatchExportProgress = new ProgressBar { Height = 4, IsIndeterminate = true, Foreground = Brush.Parse("#2D7169"), IsVisible = false };
        _activityWatchExportBox = new Border { BorderThickness = new(1), CornerRadius = new(5), Padding = new(12),
            Child = new StackPanel { Spacing = 6, Children = { _activityWatchExportHeading, _activityWatchExportDetail,
                _activityWatchExportCounts, _activityWatchExportTime, _activityWatchExportProgress } } };
        var result = TelegramCard("Sync result", _activityWatchExportBox);
        foreach (var card in new[] { automatic, content, destination, result }) ((StackPanel)card.Child!).Spacing = 9;
        _activityWatchExportCards = new Grid { Name = "ActivityWatchExportCards", Children = {
            new StackPanel { Spacing = 14, Children = { automatic, content } },
            new StackPanel { Spacing = 14, Children = { destination, result } } } };
    }

    private static WrapPanel ExportNumberRow(string label, TextBox field, string unit)
    {
        var row = new WrapPanel { Children = { SettingsLabel(label, 12, SettingsInk), new Grid { Width = 78, Children = { field } }, SettingsLabel(unit, 12, SettingsInk) } };
        foreach (var child in row.Children) { child.Margin = new(0, 0, 8, 0); child.VerticalAlignment = VerticalAlignment.Center; }
        return row;
    }
}
