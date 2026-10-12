using System;
using System.Collections.Generic;

namespace StopwatchOverlay;

public sealed record BackgroundTimerAlert(
    Guid TimerSessionId,
    string TimerName,
    int TimerNumber,
    TimeSpan Elapsed,
    int ReminderIndex,
    int MaxRemindersBeforeStop,
    TimeSpan Duration);

/// <summary>
/// Monitors timers running in the background (active timers that are not the currently selected foreground timer)
/// and manages periodic reminders, user acknowledgments, and auto-stopping unacknowledged timers.
/// </summary>
public sealed class BackgroundTimerReminderService
{
    private class TimerReminderState
    {
        public DateTime NextReminderUtc { get; set; }
        public int UnacknowledgedCount { get; set; }
        public bool IsPopupOpen { get; set; }
    }

    private readonly Dictionary<Guid, TimerReminderState> _state = new();

    public bool IsEnabled { get; set; } = true;
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(3);
    public TimeSpan AcknowledgedInterval { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan PopupDuration { get; set; } = TimeSpan.FromSeconds(30);
    public int MaxRemindersBeforeStop { get; set; } = 3;

    public BackgroundTimerReminderService()
    {
    }

    public BackgroundTimerReminderService(
        bool enabled,
        TimeSpan interval,
        TimeSpan popupDuration,
        int maxRemindersBeforeStop,
        TimeSpan? acknowledgedInterval = null)
    {
        IsEnabled = enabled;
        Interval = interval;
        AcknowledgedInterval = acknowledgedInterval ?? TimeSpan.FromMinutes(30);
        PopupDuration = popupDuration;
        MaxRemindersBeforeStop = Math.Max(1, maxRemindersBeforeStop);
    }

    /// <summary>
    /// Checks whether a background running timer is due for a periodic reminder popup.
    /// </summary>
    public bool ShouldShowReminder(
        TimerSession? timer,
        Guid? activeTimerId,
        DateTime nowUtc,
        out BackgroundTimerAlert? alert)
    {
        alert = null;
        if (!IsEnabled || timer == null || !timer.IsRunning || timer.Id == activeTimerId)
        {
            if (timer != null && timer.Id == activeTimerId)
            {
                // Active timer in foreground cannot be in background alert state
                RecordTimerDeactivatedOrStopped(timer.Id);
            }
            return false;
        }

        if (!_state.TryGetValue(timer.Id, out var state))
        {
            state = new TimerReminderState
            {
                NextReminderUtc = nowUtc + Interval,
                UnacknowledgedCount = 0,
                IsPopupOpen = false
            };
            _state[timer.Id] = state;
            return false;
        }

        if (state.IsPopupOpen)
        {
            return false;
        }

        if (nowUtc >= state.NextReminderUtc)
        {
            int reminderIndex = state.UnacknowledgedCount + 1;
            state.IsPopupOpen = true;
            state.NextReminderUtc = nowUtc + Interval;

            alert = new BackgroundTimerAlert(
                timer.Id,
                timer.DisplayName,
                timer.Number,
                timer.Elapsed,
                reminderIndex,
                MaxRemindersBeforeStop,
                PopupDuration);

            return true;
        }

        return false;
    }

    /// <summary>
    /// Records that the reminder popup timed out without any reaction from the user.
    /// Next reminder is scheduled after the standard interval (e.g. 3 minutes).
    /// </summary>
    public bool RecordPopupTimedOut(Guid timerId, out int unacknowledgedCount, out bool shouldAutoStop, DateTime? nowUtc = null)
    {
        unacknowledgedCount = 0;
        shouldAutoStop = false;

        if (!_state.TryGetValue(timerId, out var state))
            return false;

        state.IsPopupOpen = false;
        state.UnacknowledgedCount++;
        unacknowledgedCount = state.UnacknowledgedCount;

        if (state.UnacknowledgedCount >= MaxRemindersBeforeStop)
        {
            shouldAutoStop = true;
            state.UnacknowledgedCount = 0; // Reset after auto-stopping
        }
        else if (nowUtc.HasValue)
        {
            state.NextReminderUtc = nowUtc.Value + Interval;
        }

        return true;
    }

    /// <summary>
    /// Records that the user actively acknowledged the reminder ("Keep Running" / "Yes").
    /// Resets the unacknowledged counter and schedules the next reminder after AcknowledgedInterval (default: 30 minutes).
    /// </summary>
    public void RecordUserAcknowledged(Guid timerId, DateTime nowUtc)
    {
        if (_state.TryGetValue(timerId, out var state))
        {
            state.IsPopupOpen = false;
            state.UnacknowledgedCount = 0;
            state.NextReminderUtc = nowUtc + AcknowledgedInterval;
        }
        else
        {
            _state[timerId] = new TimerReminderState
            {
                NextReminderUtc = nowUtc + AcknowledgedInterval,
                UnacknowledgedCount = 0,
                IsPopupOpen = false
            };
        }
    }

    /// <summary>
    /// Removes background state when the timer is paused, stopped, or becomes active.
    /// </summary>
    public void RecordTimerDeactivatedOrStopped(Guid timerId)
    {
        _state.Remove(timerId);
    }

    /// <summary>
    /// Closes the popup without altering the unacknowledged counter (e.g. manual window dismissal).
    /// </summary>
    public void RecordPopupDismissed(Guid timerId)
    {
        if (_state.TryGetValue(timerId, out var state))
        {
            state.IsPopupOpen = false;
            state.NextReminderUtc = DateTime.UtcNow + Interval;
        }
    }

    public int GetUnacknowledgedCount(Guid timerId)
    {
        return _state.TryGetValue(timerId, out var state) ? state.UnacknowledgedCount : 0;
    }

    public bool IsPopupOpen(Guid timerId)
    {
        return _state.TryGetValue(timerId, out var state) && state.IsPopupOpen;
    }

    public DateTime? GetNextReminderUtc(Guid timerId)
    {
        return _state.TryGetValue(timerId, out var state) ? state.NextReminderUtc : null;
    }

    public void Reset()
    {
        _state.Clear();
    }
}
