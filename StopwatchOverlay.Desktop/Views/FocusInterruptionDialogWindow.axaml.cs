using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace StopwatchOverlay.Desktop.Views;

public partial class FocusInterruptionDialogWindow : Window
{
    private readonly TimeSpan _pauseDuration;
    private readonly int _thresholdMinutes;
    private readonly Action<FocusPauseReason>? _onResult;
    private readonly DispatcherTimer _timer;
    private readonly bool _isTaskSwitch;
    private readonly string? _otherProjectName;
    private readonly TimeSpan? _otherProjectDuration;
    private int _remainingSeconds;
    private bool _handled;

    public FocusPauseReason? SelectedReason { get; private set; }

    public FocusInterruptionDialogWindow()
        : this("Timer", TimeSpan.FromMinutes(3), 5, 12, null)
    {
    }

    public FocusInterruptionDialogWindow(
        string projectName,
        TimeSpan pauseDuration,
        int thresholdMinutes,
        int timeoutSeconds,
        Action<FocusPauseReason>? onResult,
        bool isTaskSwitch = false,
        string? otherProjectName = null,
        TimeSpan? otherProjectDuration = null)
    {
        InitializeComponent();

        _pauseDuration = pauseDuration;
        _thresholdMinutes = thresholdMinutes > 0 ? thresholdMinutes : 5;
        _onResult = onResult;
        _remainingSeconds = timeoutSeconds > 0 ? timeoutSeconds : 12;
        _isTaskSwitch = isTaskSwitch;
        _otherProjectName = otherProjectName;
        _otherProjectDuration = otherProjectDuration;

        string durationFormatted = FormatDuration(pauseDuration);
        string displayProj = string.IsNullOrWhiteSpace(projectName) ? "Timer" : projectName.Trim();

        if (isTaskSwitch && !string.IsNullOrWhiteSpace(otherProjectName))
        {
            string otherDurStr = otherProjectDuration.HasValue ? FormatDuration(otherProjectDuration.Value) : "";
            PauseSummaryText.Text = $"Paused for {durationFormatted} on {displayProj}\n🔀 Active in meantime: {otherProjectName} ({otherDurStr})";
            TaskSwitchDefaultBadge.IsVisible = true;
            TaskSwitchSubText.Text = $"Resumed/worked on {otherProjectName} for {otherDurStr}";
            SuggestedBadge.IsVisible = false;
            FooterHintText.Text = "Press 4 or S for Switch (or Enter to accept default) · Esc to skip";
        }
        else
        {
            PauseSummaryText.Text = $"Paused for {durationFormatted} on {displayProj}";
            if (pauseDuration < TimeSpan.FromMinutes(_thresholdMinutes))
            {
                SuggestedBadge.IsVisible = true;
            }
        }

        CountdownText.Text = $"Closing in {_remainingSeconds}s";

        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _timer.Tick += Timer_Tick;

        Opened += OnWindowOpened;
    }

    public static async Task<FocusPauseReason> ShowAsync(
        Window? owner,
        string projectName,
        TimeSpan pauseDuration,
        int thresholdMinutes,
        int timeoutSeconds,
        bool isTaskSwitch = false,
        string? otherProjectName = null,
        TimeSpan? otherProjectDuration = null)
    {
        var tcs = new TaskCompletionSource<FocusPauseReason>();
        var dialog = new FocusInterruptionDialogWindow(
            projectName,
            pauseDuration,
            thresholdMinutes,
            timeoutSeconds,
            reason => tcs.TrySetResult(reason),
            isTaskSwitch,
            otherProjectName,
            otherProjectDuration);

        if (owner != null)
            dialog.Show(owner);
        else
            dialog.Show();

        return await tcs.Task;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero) duration = TimeSpan.Zero;
        if (duration.TotalMinutes < 1)
        {
            return $"{Math.Max(1, (int)duration.TotalSeconds)}s";
        }
        int minutes = (int)duration.TotalMinutes;
        int seconds = duration.Seconds;
        return seconds > 0 ? $"{minutes}m {seconds}s" : $"{minutes}m";
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        _timer.Start();
        Activate();
        Focus();
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _remainingSeconds--;
        if (_remainingSeconds <= 0)
        {
            _timer.Stop();
            ApplyDefaultAndClose();
            return;
        }

        CountdownText.Text = $"Closing in {_remainingSeconds}s";
    }

    private void ApplyDefaultAndClose()
    {
        if (_handled) return;
        _handled = true;
        _timer.Stop();

        FocusPauseReason defaultReason;
        if (_isTaskSwitch)
        {
            defaultReason = FocusPauseReason.TaskSwitch;
        }
        else
        {
            defaultReason = _pauseDuration < TimeSpan.FromMinutes(_thresholdMinutes)
                ? FocusPauseReason.Distraction
                : FocusPauseReason.ValidBreak;
        }

        SelectedReason = defaultReason;
        _onResult?.Invoke(defaultReason);
        Close();
    }

    private void SelectReason(FocusPauseReason reason)
    {
        if (_handled) return;
        _handled = true;
        _timer.Stop();
        SelectedReason = reason;
        _onResult?.Invoke(reason);
        Close();
    }

    private void OnValidBreakClicked(object? sender, RoutedEventArgs e)
        => SelectReason(FocusPauseReason.ValidBreak);

    private void OnDistractionClicked(object? sender, RoutedEventArgs e)
        => SelectReason(FocusPauseReason.Distraction);

    private void OnIdeaClicked(object? sender, RoutedEventArgs e)
        => SelectReason(FocusPauseReason.IdeaBrainstorming);

    private void OnTaskSwitchClicked(object? sender, RoutedEventArgs e)
        => SelectReason(FocusPauseReason.TaskSwitch);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        switch (e.Key)
        {
            case Key.D1:
            case Key.NumPad1:
                SelectReason(FocusPauseReason.ValidBreak);
                e.Handled = true;
                break;
            case Key.D2:
            case Key.NumPad2:
                SelectReason(FocusPauseReason.Distraction);
                e.Handled = true;
                break;
            case Key.D3:
            case Key.NumPad3:
                SelectReason(FocusPauseReason.IdeaBrainstorming);
                e.Handled = true;
                break;
            case Key.D4:
            case Key.NumPad4:
            case Key.S:
                SelectReason(FocusPauseReason.TaskSwitch);
                e.Handled = true;
                break;
            case Key.Enter:
            case Key.Space:
            case Key.Escape:
                ApplyDefaultAndClose();
                e.Handled = true;
                break;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        _timer.Stop();
        if (!_handled)
        {
            ApplyDefaultAndClose();
        }
    }
}
