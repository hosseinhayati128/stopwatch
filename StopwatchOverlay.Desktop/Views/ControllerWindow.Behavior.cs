using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia.Controls;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    private readonly Dictionary<Guid, TimeSpan> _behaviorRunStarts = new();
    private readonly Queue<(TimerSession Timer, string Project, DateTime PausedAt, DateTime ResumedAt)> _behaviorFocusQueue = new();
    private FocusInterruptionDialogWindow? _behaviorFocusPrompt;
    private Guid? _countdownErrorTimer;

    private bool ShowTimerRec(TimerSession timer) => _settings.ShowRecIndicator && timer.IsRunning && timer.Mode != 1;

    private void AutoStartShownTimer(TimerSession timer)
    {
        if (!OverlayPresentationPolicy.ShouldAutoStartOnShow(false, _settings.AutoStart, timer.IsRunning, timer.Mode)) return;
        if (!StartBehaviorTimer(timer)) return;
        UpdateActiveTimerDisplay();
        RefreshTimerRail();
    }

    private bool StartBehaviorTimer(TimerSession timer)
    {
        var now = DateTime.UtcNow;
        if (timer.Mode == 2 && !PrepareCountdown(timer, now)) return false;
        DateTime? pauseStart = timer.LastPauseUtc;
        timer.LastPauseUtc = null;
        _behaviorRunStarts[timer.Id] = timer.Elapsed;
        timer.IsRunning = true;
        timer.Stopwatch.Start();
        timer.LastCountdownUpdateUtc = now;
        if (timer.Mode == 0 && pauseStart is { } pausedAt)
            ShowBehaviorFocusPrompt(timer, pausedAt, now);
        return true;
    }

    private void PauseBehaviorTimer(TimerSession timer, DateTime pausedAt)
    {
        timer.Stopwatch.Stop();
        timer.IsRunning = false;
        timer.LastCountdownUpdateUtc = default;
        int mode = timer.Mode == 1 ? timer.LastNonClockMode : timer.Mode;
        timer.LastPauseUtc = mode == 0 ? pausedAt : null;
        _behaviorRunStarts.Remove(timer.Id);
    }

    private void ApplyIdleStop(TimeSpan idle, DateTime nowUtc)
    {
        bool stopped = false;
        bool historyChanged = false;
        foreach (var timer in _timerManager.Sessions)
        {
            int mode = timer.Mode == 1 ? timer.LastNonClockMode : timer.Mode;
            if (!timer.IsRunning || mode != 0 || !_settings.IsIdleStopActiveForProject(timer.Name, out int timeout, out bool subtract)
                || idle < TimeSpan.FromMinutes(timeout)) continue;

            // The offset is all we can safely protect for a run restored from disk.
            var beforeRun = _behaviorRunStarts.GetValueOrDefault(timer.Id, timer.ElapsedOffset);
            timer.Stopwatch.Stop();
            var elapsed = timer.Elapsed;
            var currentRun = elapsed > beforeRun ? elapsed - beforeRun : TimeSpan.Zero;
            var adjustment = currentRun < idle ? currentRun : idle;
            var stoppedAt = subtract ? nowUtc - adjustment : nowUtc;
            PauseBehaviorTimer(timer, stoppedAt);
            if (subtract) timer.RestoreElapsed(elapsed - adjustment, false);
            historyChanged |= _projectHistory.StopTracking(timer.Id, stoppedAt);
            stopped = true;
        }
        if (!stopped) return;
        if (historyChanged && !_previewMode) _projectTimeStore.Save(_projectHistory);
        UpdateActiveTimerDisplay();
        RefreshTimerRail();
        UpdateOverlayStates();
    }

    private void ShowBehaviorFocusPrompt(TimerSession timer, DateTime pausedAt, DateTime resumedAt)
    {
        TimeSpan duration = resumedAt - pausedAt;
        if (!_settings.FocusTrackingEnabled || _isExiting
            || duration < TimeSpan.FromSeconds(_settings.FocusMinimumPauseSeconds)
            || duration >= TimeSpan.FromMinutes(_settings.MaxResumePauseMinutes > 0 ? _settings.MaxResumePauseMinutes : 30)) return;
        _behaviorFocusQueue.Enqueue((timer, timer.DisplayName, pausedAt, resumedAt));
        ShowNextBehaviorFocusPrompt();
    }

    private void ShowNextBehaviorFocusPrompt()
    {
        if (_isExiting || !_settings.FocusTrackingEnabled)
        {
            _behaviorFocusQueue.Clear();
            return;
        }
        if (_behaviorFocusPrompt != null) return;
        while (_behaviorFocusQueue.TryDequeue(out var entry))
        {
            if (!_timerManager.Sessions.Contains(entry.Timer)) continue;
            OpenBehaviorFocusPrompt(entry.Timer, entry.Project, entry.PausedAt, entry.ResumedAt);
            return;
        }
    }

    private void OpenBehaviorFocusPrompt(TimerSession timer, string project, DateTime pausedAt, DateTime resumedAt)
    {
        var prompt = new FocusInterruptionDialogWindow(project, resumedAt - pausedAt, _settings.FocusDistractionThresholdMinutes,
            _settings.FocusPromptTimeoutSeconds, reason =>
            {
                if (_isExiting || !_settings.FocusTrackingEnabled || reason == FocusPauseReason.Unspecified || !_timerManager.Sessions.Contains(timer)) return;
                _projectHistory.RecordFocusPause(timer.Id, project, pausedAt, resumedAt, reason, maxTrackedPauseMinutes: _settings.MaxTrackedPauseMinutes);
                if (!_previewMode) _projectTimeStore.Save(_projectHistory);
            });
        _behaviorFocusPrompt = prompt;
        prompt.Closed += (_, _) =>
        {
            _behaviorFocusPrompt = null;
            Avalonia.Threading.Dispatcher.UIThread.Post(ShowNextBehaviorFocusPrompt);
        };
        if (IsVisible) prompt.Show(this);
        else prompt.Show();
    }

    private void UpdateCountdownInputs(TimerSession timer)
    {
        bool textInput = _settings.UseSmartCountdownInput || timer.UseClockTarget;
        CountdownDurationPanel.IsVisible = !textInput;
        CountdownDurationRadio.IsChecked = !timer.UseClockTarget;
        CountdownUntilRadio.IsChecked = timer.UseClockTarget;
        SmartCountdownInputBox.IsVisible = textInput;
        SmartCountdownInputBox.Watermark = timer.UseClockTarget ? "e.g. 14:30" : "e.g. 5m 30s";
        SmartCountdownInputBox.IsEnabled = CountdownDurationPanel.IsEnabled = !timer.IsRunning;
        CountdownDurationRadio.IsEnabled = CountdownUntilRadio.IsEnabled = !timer.IsRunning;
        string input = CountdownInput(timer);
        if (SmartCountdownInputBox.Text != input) SmartCountdownInputBox.Text = input;
        if (int.TryParse(timer.CountdownMinutesText, out int minutes)) CountdownMinutesBox.Value = Math.Clamp(minutes, 0, 999);
        if (int.TryParse(timer.CountdownSecondsText, out int seconds)) CountdownSecondsBox.Value = Math.Clamp(seconds, 0, 59);
        CountdownInputError.IsVisible = !timer.IsRunning && _countdownErrorTimer == timer.Id;
    }

    private void SmartCountdownInput_TextChanging(object? sender, TextChangingEventArgs e)
    {
        if (_updatingUi || _timerManager.Active is not { Mode: 2, IsRunning: false } timer || !SmartCountdownInputBox.IsVisible) return;
        string value = SmartCountdownInputBox.Text ?? "";
        if (value == timer.SmartInputText) return;
        timer.SmartInputText = value;
        timer.CountdownInitialized = false;
        timer.CountdownRemaining = TimeSpan.Zero;
        ClearCountdownError(timer);
    }

    private void CountdownDuration_ValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
    {
        if (_updatingUi || _timerManager.Active is not { Mode: 2, IsRunning: false } timer
            || CountdownMinutesBox?.Value is not decimal minutes || CountdownSecondsBox?.Value is not decimal seconds) return;
        timer.CountdownMinutesText = minutes.ToString("0", CultureInfo.InvariantCulture);
        timer.CountdownSecondsText = seconds.ToString("00", CultureInfo.InvariantCulture);
        timer.CountdownDuration = TimeSpan.FromSeconds((double)(minutes * 60 + seconds));
        timer.CountdownRemaining = timer.CountdownDuration;
        timer.CountdownInitialized = false;
        ClearCountdownError(timer);
    }

    private static string CountdownInput(TimerSession timer)
    {
        if (timer.UseClockTarget && string.IsNullOrWhiteSpace(timer.SmartInputText)
            && int.TryParse(timer.ClockTargetHoursText, out int hours) && int.TryParse(timer.ClockTargetMinutesText, out int minutes)
            && int.TryParse(timer.ClockTargetSecondsText, out int seconds)
            && hours is >= 0 and < 24 && minutes is >= 0 and < 60 && seconds is >= 0 and < 60)
            return seconds == 0 ? $"{hours:00}:{minutes:00}" : $"{hours:00}:{minutes:00}:{seconds:00}";
        return timer.SmartInputText;
    }

    private bool PrepareCountdown(TimerSession timer, DateTime nowUtc)
    {
        // Older desktop saves may have remaining time without the initialization flag.
        if (timer.CountdownRemaining > TimeSpan.Zero)
        {
            if (timer.UseClockTarget)
            {
                DateTime now = nowUtc.ToLocalTime();
                if (timer.CountdownRemaining > DateTime.MaxValue - now)
                    return ShowCountdownError(timer, "Enter a valid target clock time.");
                timer.ClockTarget = now + timer.CountdownRemaining;
            }
            timer.CountdownInitialized = true;
            ClearCountdownError(timer);
            return true;
        }
        TimeSpan duration = timer.CountdownDuration;
        DateTime? clockTarget = null;
        try
        {
            string input = CountdownInput(timer).Trim();
            if ((_settings.UseSmartCountdownInput || timer.UseClockTarget) && input.Length > 0)
            {
                DateTime now = nowUtc.ToLocalTime();
                var result = CountdownParser.Parse(input, now);
                if (timer.UseClockTarget && result.Target == null) result = CountdownParser.Parse("until " + input, now);
                if (!result.Success) return ShowCountdownError(timer, result.Error ?? "Enter a duration or time.");
                clockTarget = result.Target;
                duration = clockTarget is { } target ? target - now : result.Duration!.Value;
            }
            else if (timer.UseClockTarget) return ShowCountdownError(timer, "Enter the target clock time.");
        }
        catch (Exception error) when (error is ArgumentOutOfRangeException or OverflowException or FormatException)
        {
            return ShowCountdownError(timer, "Enter a valid duration or time.");
        }
        if (duration <= TimeSpan.Zero) return ShowCountdownError(timer, "Enter a positive duration or a future time.");
        timer.CountdownDuration = timer.CountdownRemaining = duration;
        timer.UseClockTarget = clockTarget.HasValue;
        timer.ClockTarget = clockTarget.GetValueOrDefault();
        timer.CountdownInitialized = true;
        ClearCountdownError(timer);
        return true;
    }

    private bool ShowCountdownError(TimerSession timer, string message)
    {
        if (timer == _timerManager.Active)
        {
            _countdownErrorTimer = timer.Id;
            CountdownInputError.Text = message;
            CountdownInputError.IsVisible = true;
        }
        return false;
    }

    private void ClearCountdownError(TimerSession timer)
    {
        if (_countdownErrorTimer != timer.Id) return;
        _countdownErrorTimer = null;
        CountdownInputError.IsVisible = false;
    }

    private void ResetBehaviorRunBoundary(TimerSession timer)
    {
        if (timer.IsRunning) _behaviorRunStarts[timer.Id] = timer.Elapsed;
        else _behaviorRunStarts.Remove(timer.Id);
    }
}
