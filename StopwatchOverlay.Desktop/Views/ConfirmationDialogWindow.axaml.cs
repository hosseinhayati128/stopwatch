using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace StopwatchOverlay.Desktop.Views;

public partial class ConfirmationDialogWindow : Window
{
    public bool Result { get; private set; }

    public ConfirmationDialogWindow()
    {
        InitializeComponent();
    }

    public ConfirmationDialogWindow(
        string title,
        string heading,
        string message,
        string confirmText,
        bool destructive = false) : this()
    {
        Title = title;
        HeadingText.Text = heading;
        MessageText.Text = message;
        ConfirmButton.Content = confirmText;

        if (destructive)
        {
            ConfirmButton.Classes.Remove("primary");
            ConfirmButton.Classes.Add("danger");
        }
    }

    public static async Task<bool> ShowAsync(
        Window? owner,
        string title,
        string heading,
        string message,
        string confirmText,
        bool destructive = false)
    {
        var dialog = new ConfirmationDialogWindow(title, heading, message, confirmText, destructive);
        if (owner != null)
        {
            return await dialog.ShowDialog<bool>(owner);
        }

        var tcs = new TaskCompletionSource<bool>();
        dialog.Closed += (_, _) => tcs.TrySetResult(dialog.Result);
        dialog.Show();
        return await tcs.Task;
    }

    private void OnConfirmClicked(object? sender, RoutedEventArgs e) => Complete(true);
    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Complete(false);
    private void Complete(bool confirmed)
    {
        if (_closeTimerConfigured && _closeTimerCompleted) return;
        if (_deleteProjectConfigured && _deleteProjectCompleted) return;
        if (_noteDeletionConfigured && _noteDeletionCompleted) return;
        if (_closeTimerConfigured) _closeTimerCompleted = true;
        if (_deleteProjectConfigured) { _deleteProjectCompleted = true; confirmed &= !_deleteProjectBlocked; }
        if (_noteDeletionConfigured) _noteDeletionCompleted = true;
        Result = confirmed;
        Close(confirmed);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        // These confirmations rely on the focused button; generic dialogs retain their shortcut.
        if (_closeTimerConfigured || _deleteProjectConfigured || _noteDeletionConfigured) return;
        if (e.Key == Key.Enter)
        {
            OnConfirmClicked(this, e);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            OnCancelClicked(this, e);
            e.Handled = true;
        }
    }
}
