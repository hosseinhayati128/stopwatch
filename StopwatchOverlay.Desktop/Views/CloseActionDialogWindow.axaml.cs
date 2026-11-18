using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace StopwatchOverlay.Desktop.Views;

public partial class CloseActionDialogWindow : Window
{
    private bool _completed;
    public CloseDialogResult SelectedAction { get; private set; } = CloseDialogResult.Cancel;
    public bool RememberChoice => SelectedAction != CloseDialogResult.Cancel && RememberCheck.IsChecked == true;

    public CloseActionDialogWindow() : this(null, SettingsStore.Load()) { }

    public CloseActionDialogWindow(string? themeMode, AppSettings? settings = null)
    {
        InitializeComponent();
        settings ??= new AppSettings { ThemeMode = AppThemeCatalog.Normalize(themeMode) };
        var theme = AppThemeCatalog.Normalize(themeMode ?? settings.ThemeMode);
        Classes.Set("pirate", theme == AppThemeCatalog.Pirate);
        if (theme == AppThemeCatalog.Nika) ApplyCloseAppearance(settings);
        AddHandler(KeyDownEvent, HandleCloseKey, RoutingStrategies.Tunnel);
        Closed += (_, _) => _completed = true;
    }

    public static async Task<(CloseDialogResult Action, bool RememberChoice)> ShowAsync(Window? owner, string? themeMode = null, AppSettings? settings = null)
    {
        var dialog = settings == null && themeMode == null
            ? new CloseActionDialogWindow()
            : new CloseActionDialogWindow(themeMode, settings);
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

    public void ChooseCancel() => Choose(CloseDialogResult.Cancel);
    public void ChooseMinimize() => Choose(CloseDialogResult.MinimizeToTray);
    public void ChooseExit() => Choose(CloseDialogResult.CloseCompletely);
    private void Choose(CloseDialogResult action)
    {
        if (_completed) return;
        _completed = true;
        SelectedAction = action;
        Close(action);
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => ChooseCancel();
    private void OnMinimizeClicked(object? sender, RoutedEventArgs e) => ChooseMinimize();
    private void OnExitClicked(object? sender, RoutedEventArgs e) => ChooseExit();

    private void HandleCloseKey(object? sender, KeyEventArgs e)
    {
        // Handle the advertised default before a focused Exit button consumes Enter.
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
