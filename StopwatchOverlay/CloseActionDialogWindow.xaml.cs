using System;
using System.Windows;

namespace StopwatchOverlay;

public enum CloseDialogResult
{
    Cancel,
    MinimizeToTray,
    CloseCompletely
}

public partial class CloseActionDialogWindow : Window
{
    public CloseDialogResult SelectedAction { get; private set; } = CloseDialogResult.Cancel;
    public bool RememberChoice => RememberCheck.IsChecked == true;

    public CloseActionDialogWindow()
    {
        InitializeComponent();
    }

    internal void ChooseCancel()
    {
        SelectedAction = CloseDialogResult.Cancel;
        try { DialogResult = false; } catch (InvalidOperationException) { Close(); }
    }

    internal void ChooseMinimize()
    {
        SelectedAction = CloseDialogResult.MinimizeToTray;
        try { DialogResult = true; } catch (InvalidOperationException) { Close(); }
    }

    internal void ChooseExit()
    {
        SelectedAction = CloseDialogResult.CloseCompletely;
        try { DialogResult = true; } catch (InvalidOperationException) { Close(); }
    }

    internal void CancelButton_Click(object sender, RoutedEventArgs e) => ChooseCancel();
    internal void MinimizeButton_Click(object sender, RoutedEventArgs e) => ChooseMinimize();
    internal void ExitButton_Click(object sender, RoutedEventArgs e) => ChooseExit();
}
