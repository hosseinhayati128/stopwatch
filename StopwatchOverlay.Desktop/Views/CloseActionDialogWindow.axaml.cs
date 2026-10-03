using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace StopwatchOverlay.Desktop.Views;

public partial class CloseActionDialogWindow : Window
{
    public CloseDialogResult SelectedAction { get; private set; } = CloseDialogResult.Cancel;
    public bool RememberChoice => RememberCheck.IsChecked == true;

    public CloseActionDialogWindow()
    {
        InitializeComponent();
    }

    public static async Task<(CloseDialogResult Action, bool RememberChoice)> ShowAsync(Window? owner)
    {
        var dialog = new CloseActionDialogWindow();
        if (owner != null)
        {
            var res = await dialog.ShowDialog<CloseDialogResult>(owner);
            return (res, dialog.RememberChoice);
        }

        var tcs = new TaskCompletionSource<(CloseDialogResult, bool)>();
        dialog.Closed += (_, _) => tcs.TrySetResult((dialog.SelectedAction, dialog.RememberChoice));
        dialog.Show();
        return await tcs.Task;
    }

    public void ChooseCancel()
    {
        SelectedAction = CloseDialogResult.Cancel;
        Close(CloseDialogResult.Cancel);
    }

    public void ChooseMinimize()
    {
        SelectedAction = CloseDialogResult.MinimizeToTray;
        Close(CloseDialogResult.MinimizeToTray);
    }

    public void ChooseExit()
    {
        SelectedAction = CloseDialogResult.CloseCompletely;
        Close(CloseDialogResult.CloseCompletely);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => ChooseCancel();
    private void OnMinimizeClicked(object? sender, RoutedEventArgs e) => ChooseMinimize();
    private void OnExitClicked(object? sender, RoutedEventArgs e) => ChooseExit();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Key == Key.Escape)
        {
            ChooseCancel();
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            ChooseMinimize();
            e.Handled = true;
        }
    }
}
