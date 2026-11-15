using System;
using System.Threading;

namespace StopwatchOverlay.PeriodicReview;

public sealed class PeriodicReviewService : IDisposable
{
    private readonly Func<AppSettings> _settingsProvider;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private ITimer? _timer;
    private DateTime? _nextPromptUtc, _lastCompletedUtc;
    private bool _enabled, _isPromptShowing, _disposed;
    private int _intervalMinutes, _snoozeMinutes, _generation;

    public event Action<DateTime, DateTime>? PromptRequested;
    public DateTime? NextPromptUtc { get { lock (_gate) return _nextPromptUtc; } }
    public bool IsPromptShowing { get { lock (_gate) return _isPromptShowing; } }

    public PeriodicReviewService(Func<AppSettings> settingsProvider, TimeProvider? timeProvider = null)
    {
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _clock = timeProvider ?? TimeProvider.System;
        RestartTimer();
    }

    private DateTime UtcNow => _clock.GetUtcNow().UtcDateTime;

    // Read mutable app settings on the caller's thread, never from a timer callback.
    private void ReadSettings()
    {
        var settings = _settingsProvider();
        _enabled = settings.PeriodicReviewEnabled;
        _intervalMinutes = Math.Clamp(settings.PeriodicReviewIntervalMinutes, 1, 1440);
        _snoozeMinutes = Math.Clamp(settings.PeriodicReviewSnoozeMinutes, 1, 1440);
        _lastCompletedUtc = settings.LastPeriodicReviewCompletedUtc;
    }

    public void RestartTimer(bool cancelPendingPrompt = false)
    {
        lock (_gate)
        {
            if (_disposed) return;
            int previousInterval = _intervalMinutes;
            ReadSettings();
            if (cancelPendingPrompt && _isPromptShowing) { _isPromptShowing = false; _nextPromptUtc = null; }
            if (!_enabled) { _nextPromptUtc = null; _isPromptShowing = false; }
            else if (previousInterval != _intervalMinutes || !_nextPromptUtc.HasValue || _nextPromptUtc > UtcNow.AddMinutes(_intervalMinutes))
                _nextPromptUtc = UtcNow.AddMinutes(_intervalMinutes);
            ArmTimer();
        }
    }

    private void ArmTimer()
    {
        _timer?.Dispose(); _timer = null; _generation++;
        if (_disposed || !_enabled || _isPromptShowing || !_nextPromptUtc.HasValue) return;
        TimeSpan due = _nextPromptUtc.Value - UtcNow;
        _timer = _clock.CreateTimer(OnTimerTick, _generation, due > TimeSpan.Zero ? due : TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    private void OnTimerTick(object? state)
    {
        lock (_gate)
        {
            if (_disposed || !_enabled || _isPromptShowing || !Equals(state, _generation)) return;
            if (_nextPromptUtc > UtcNow) { ArmTimer(); return; }
            if (PromptRequested == null) { ScheduleAfter(_intervalMinutes); return; }
            _isPromptShowing = true;
            GetPeriod(out var start, out var end);
            try { PromptRequested.Invoke(start, end); }
            catch (Exception ex)
            {
                CrashLogger.LogRecoverable(ex, "PeriodicReviewService.PromptRequested");
                ScheduleAfter(_snoozeMinutes);
            }
        }
    }

    public void GetReviewPeriod(out DateTime startUtc, out DateTime endUtc)
    {
        lock (_gate) { if (!_disposed) ReadSettings(); GetPeriod(out startUtc, out endUtc); }
    }

    private void GetPeriod(out DateTime startUtc, out DateTime endUtc)
    {
        endUtc = UtcNow;
        var last = _lastCompletedUtc.HasValue ? ProjectTimeHistory.NormalizeUtc(_lastCompletedUtc.Value) : (DateTime?)null;
        startUtc = last.HasValue && last < endUtc && endUtc - last.Value <= TimeSpan.FromDays(1)
            ? last.Value : endUtc.AddMinutes(-_intervalMinutes);
    }

    public void BeginReview()
    {
        lock (_gate) { if (_disposed) return; _isPromptShowing = true; ArmTimer(); }
    }

    public void OnPromptIgnored() => Reschedule(true);
    public void OnPromptSkipped() => Reschedule(false);
    public void ResetPromptShowing() => Reschedule(false);

    private void Reschedule(bool snooze)
    {
        lock (_gate)
        {
            if (_disposed) return;
            ReadSettings(); ScheduleAfter(snooze ? _snoozeMinutes : _intervalMinutes);
        }
    }

    private void ScheduleAfter(int minutes)
    {
        _isPromptShowing = false;
        _nextPromptUtc = _enabled ? UtcNow.AddMinutes(minutes) : null;
        ArmTimer();
    }

    public void OnReviewCompleted(DateTime completedUtc)
    {
        lock (_gate)
        {
            if (_disposed) return;
            ReadSettings(); _lastCompletedUtc = ProjectTimeHistory.NormalizeUtc(completedUtc);
            ScheduleAfter(_intervalMinutes);
        }
    }

    public void SetNextPromptUtc(DateTime nextUtc)
    {
        lock (_gate)
        {
            if (_disposed || !_enabled) return;
            _nextPromptUtc = ProjectTimeHistory.NormalizeUtc(nextUtc); ArmTimer();
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true; _nextPromptUtc = null; _isPromptShowing = false; ArmTimer();
        }
    }
}
