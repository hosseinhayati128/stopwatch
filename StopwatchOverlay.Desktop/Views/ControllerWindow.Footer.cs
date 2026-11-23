using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    private readonly record struct FooterState(string Status, Shortcut Leader,
        uint FirstKey, string FirstAction, uint SecondKey, string SecondAction);
    private FooterState? _lastFooter;

    private void UpdateControllerFooter(TimerSession? timer, string state)
    {
        bool clock = timer?.Mode == 1;
        var keys = _settings.CommandModeKeys;
        uint first = clock || StartStopButton.IsEnabled
            ? keys.GetValueOrDefault(clock ? ShortcutAction.ToggleClock : ShortcutAction.StartStop) : 0;
        uint second = timer == null ? 0 : keys.GetValueOrDefault(clock ? ShortcutAction.ToggleOverlay : ShortcutAction.Reset);
        string firstAction = clock ? timer!.LastNonClockMode switch
        {
            2 => "Switch to countdown",
            3 => "Switch to timecode",
            _ => "Switch to stopwatch"
        } : (string)StartStopButton.Content!;
        string secondAction = clock ? (IsTimerOverlayVisible(timer!) ? "Hide overlay" : "Show overlay") : "Reset";
        string status = timer == null ? "No timer" : clock ? "Live clock" : state;
        var view = new FooterState(status, _settings.LeaderShortcut, first, firstAction, second, secondAction);

        // The clock ticks at 30 Hz. Format keys and update the visuals only when the footer changes.
        if (_lastFooter == view) return;
        if (_lastFooter?.Status != status)
        {
            FooterMark.Symbol = state;
            FooterStatus.Text = status;
            FooterStatus.Foreground = Brush.Parse(status switch
            {
                "Live clock" => "#A9DBEB",
                "Running" or "Finished" => "#A6E4CC",
                "Paused" => "#F5D58C",
                _ => "#C4D2D8"
            });
        }
        _lastFooter = view;
        FooterHints.IsVisible = view.Leader.VirtualKey != 0 && (first != 0 || second != 0);
        if (!FooterHints.IsVisible) return;
        FooterLeader.Text = view.Leader.Format();
        FooterFirstHint.IsVisible = first != 0;
        FooterSecondHint.IsVisible = second != 0;
        FooterDivider.IsVisible = first != 0 && second != 0;
        if (first != 0)
        {
            FooterFirstKey.Text = Shortcut.FormatKeyName(first);
            FooterFirstAction.Text = firstAction;
        }
        if (second != 0)
        {
            FooterSecondKey.Text = Shortcut.FormatKeyName(second);
            FooterSecondAction.Text = secondAction;
        }
    }
}
