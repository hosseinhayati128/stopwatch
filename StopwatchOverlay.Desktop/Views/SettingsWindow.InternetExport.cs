using System;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using StopwatchOverlay.Desktop.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private Grid? _internetExportCards;
    private TextBlock? _internetHistoryCount, _internetHistoryDates, _internetRetentionHint, _internetFolder,
        _internetFileHint, _internetExportHeading, _internetExportDetail, _internetExportStamp, _internetClearDetail;
    private Border? _internetExportBox, _internetClearBox;
    private ProgressBar? _internetExportProgress;
    private Button? _internetFolderButton, _internetClearCancel, _internetClearConfirm;
    private bool _internetShowingHistory;

    private void BuildInternetExport()
    {
        InternetFileNameTextBox.Classes.Add("editorField"); InternetFileNameTextBox.Height = double.NaN;
        InternetFileNameTextBox.Padding = new(10, 6);
        AutomationProperties.SetName(InternetFileNameTextBox, "Internet report file name");
        AutomationProperties.SetName(InternetRetentionCombo, "History retention period");
        InternetRetentionCombo.MinHeight = 36;
        foreach (var button in new[] { SyncInternetLogButton, ClearInternetLogButton })
        {
            NavigatorFormStyles.StyleButton(button, button == SyncInternetLogButton);
            button.Height = double.NaN; button.MinHeight = 36; button.Padding = new(12, 6); button.HorizontalAlignment = HorizontalAlignment.Left;
        }
        ClearInternetLogButton.Content = "Clear history…";
        ClearInternetLogButton.Foreground = ClearInternetLogButton.BorderBrush = Brush.Parse("#913F32");
        TextBlock Named(string name, double size, IBrush? brush = null)
        { var label = SettingsLabel("", size, brush); label.Name = name; return label; }
        Border Card(string title, params Control[] controls)
        { var card = TelegramCard(title, controls); ((StackPanel)card.Child!).Spacing = 11; return card; }
        _internetHistoryCount = Named("InternetHistoryCount", 25, SettingsInk); _internetHistoryCount.FontWeight = FontWeight.SemiBold;
        _internetHistoryDates = Named("InternetHistoryDates", 11);
        _internetRetentionHint = Named("InternetRetentionHint", 11);
        CreateInternetClearConfirmation();
        var history = Card("Saved history", new StackPanel { Spacing = 4, Children = { _internetHistoryCount, _internetHistoryDates } },
            SettingsLabel("Automatic checks are saved locally. Test now does not add history.", 11),
            new Border { Height = 1, Background = SettingsRule },
            new StackPanel { Spacing = 6, Children = { SettingsLabel("Keep checks for", 12, SettingsInk), InternetRetentionCombo, _internetRetentionHint } },
            new Border { Height = 1, Background = SettingsRule },
            SettingsLabel("Clear saved checks and empty this report. Monitoring settings stay unchanged.", 11), ClearInternetLogButton, _internetClearBox!);
        _internetFolder = Named("InternetExportFolder", 13, SettingsInk);
        _internetFolderButton = SettingsAction("Folder settings", "InternetFolderSettings");
        _internetFolderButton.Click += (_, _) => NavigationList.SelectedItem = NavigationList.Items.OfType<ListBoxItem>().Single(i => Equals(i.Tag, "Obsidian"));
        _internetFolderButton.Height = double.NaN; _internetFolderButton.MinHeight = 36; _internetFolderButton.Padding = new(12, 6);
        _internetFolderButton.VerticalAlignment = VerticalAlignment.Top;
        var folderRow = new Grid { ColumnDefinitions = new("*,12,Auto"), Children = {
            new StackPanel { Spacing = 4, Children = { _internetFolder, SettingsLabel("Shared with Obsidian / Markdown settings.", 11) } }, _internetFolderButton } };
        Grid.SetColumn(_internetFolderButton, 2);
        _internetFileHint = Named("InternetFileHint", 11);
        _internetExportHeading = Named("InternetExportHeading", 14);
        _internetExportDetail = Named("InternetExportDetail", 11); _internetExportStamp = Named("InternetExportStamp", 10);
        _internetExportProgress = new ProgressBar { Height = 3, IsIndeterminate = true, IsVisible = false, Foreground = Brush.Parse("#2D7169") };
        _internetExportBox = new Border { BorderThickness = new(1), CornerRadius = new(5), Padding = new(12),
            Child = new StackPanel { Spacing = 5, Children = { _internetExportHeading, _internetExportDetail, _internetExportStamp, _internetExportProgress } } };
        var export = Card("Export to Markdown", new StackPanel { Spacing = 5, Children = { SettingsLabel("OBSIDIAN VAULT", 9), folderRow } },
            new StackPanel { Spacing = 6, Children = { SettingsLabel("File name", 12, SettingsInk), InternetFileNameTextBox, _internetFileHint } },
            SettingsLabel("Keeps saved checks and their report notes. Scheduled checks also update this file.", 11), SyncInternetLogButton, _internetExportBox);
        _internetExportCards = new Grid { Name = "InternetExportCards", Children = { history, export } };
        RefreshInternetExport();
    }

    private void CreateInternetClearConfirmation()
    {
        if (_internetClearBox?.Parent is Panel oldParent) oldParent.Children.Remove(_internetClearBox);
        _internetClearDetail = SettingsLabel("", 12, SettingsInk); _internetClearDetail.Name = "InternetClearDetail";
        _internetClearCancel = SettingsAction("Cancel", "InternetClearCancel"); _internetClearConfirm = SettingsAction("Clear history", "InternetClearConfirm");
        foreach (var button in new[] { _internetClearCancel, _internetClearConfirm })
        { button.Height = double.NaN; button.MinHeight = 36; button.Padding = new(12, 6); button.Margin = new(0, 0, 8, 4); }
        _internetClearConfirm.Background = _internetClearConfirm.BorderBrush = Brush.Parse("#913F32"); _internetClearConfirm.Foreground = Brush.Parse("#FFF5DD");
        _internetClearCancel.Click += (_, _) => { _internetClearRequest = null; RefreshInternetExport(); ClearInternetLogButton.Focus(); };
        _internetClearConfirm.Click += async (_, _) => await RunInternetExportAsync(true);
        _internetClearBox = new Border { IsVisible = false, Padding = new(12), CornerRadius = new(5), BorderThickness = new(1),
            BorderBrush = Brush.Parse("#66913F32"), Background = Brush.Parse("#09913F32"),
            Child = new StackPanel { Spacing = 9, Children = { SettingsLabel("Clear saved history?", 16, Brush.Parse("#913F32")), _internetClearDetail,
                SettingsLabel("This cannot be undone here. Automatic checks can add new history.", 11),
                new WrapPanel { Children = { _internetClearCancel, _internetClearConfirm } } } } };
        _internetClearBox.KeyDown += (_, e) =>
        { if (e.Key == Key.Escape) { _internetClearRequest = null; RefreshInternetExport(); ClearInternetLogButton.Focus(); e.Handled = true; } };
    }

    private void RestoreInternetExport()
    {
        DismissInternetExport();
        if (_internetClearBox?.Parent is Panel parent) parent.Children.Remove(_internetClearBox);
        _internetClearBox = null; _internetExportCards = null;
        InternetRetentionCombo.ClearValue(MinHeightProperty);
        foreach (var button in new[] { SyncInternetLogButton, ClearInternetLogButton })
        { button.IsVisible = true; button.ClearValue(ForegroundProperty); button.ClearValue(BorderBrushProperty); }
    }
}
