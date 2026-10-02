using System;
using System.Threading;

namespace StopwatchOverlay.PeriodicReview;

public sealed class PeriodicReviewService : IDisposable
{
    private readonly Func<AppSettings> _settingsProvider;
    private Timer? _timer;
    private DateTime? _nextPromptUtc;
    private bool _isPromptShowing;
    private bool _disposed;

    public event Action<DateTime, DateTime>? PromptRequested;

    public DateTime? NextPromptUtc => _nextPromptUtc;
    public bool IsPromptShowing => _isPromptShowing;

    public PeriodicReviewService(Func<AppSettings> settingsProvider)
    {
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        RestartTimer();
    }

    public void RestartTimer()
    {
        var settings = _settingsProvider();
        _timer?.Dispose();
        _timer = null;

        if (!settings.PeriodicReviewEnabled)
        {
            _nextPromptUtc = null;
            return;
        }

        int intervalMin = Math.Clamp(settings.PeriodicReviewIntervalMinutes, 1, 1440);
        if (!_nextPromptUtc.HasValue || _nextPromptUtc.Value > DateTime.UtcNow.AddMinutes(intervalMin))
        {
            _nextPromptUtc = DateTime.UtcNow.AddMinutes(intervalMin);
        }

        // Check every 10 seconds
        _timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10));
    }

    private void OnTimerTick(object? state)
    {
        if (_disposed || _isPromptShowing) return;

        var settings = _settingsProvider();
        if (!settings.PeriodicReviewEnabled) return;

        if (_nextPromptUtc.HasValue && DateTime.UtcNow >= _nextPromptUtc.Value)
        {
            _isPromptShowing = true;
            GetReviewPeriod(out var startUtc, out var endUtc);
            PromptRequested?.Invoke(startUtc, endUtc);
        }
    }

    public void GetReviewPeriod(out DateTime startUtc, out DateTime endUtc)
    {
        var settings = _settingsProvider();
        endUtc = DateTime.UtcNow;

        if (settings.LastPeriodicReviewCompletedUtc.HasValue)
        {
            var last = ProjectTimeHistory.NormalizeUtc(settings.LastPeriodicReviewCompletedUtc.Value);
            // If the last report was within the last 24 hours and in the past, use it
            if (last < endUtc && (endUtc - last).TotalHours <= 24)
            {
                startUtc = last;
                return;
            }
        }

        int intervalMin = Math.Clamp(settings.PeriodicReviewIntervalMinutes, 1, 1440);
        startUtc = endUtc.AddMinutes(-intervalMin);
    }

    public void OnPromptIgnored()
    {
        _isPromptShowing = false;
        var settings = _settingsProvider();
        int snoozeMin = Math.Clamp(settings.PeriodicReviewSnoozeMinutes, 1, 1440);
        _nextPromptUtc = DateTime.UtcNow.AddMinutes(snoozeMin);
    }

    public void OnPromptSkipped()
    {
        _isPromptShowing = false;
        var settings = _settingsProvider();
        int intervalMin = Math.Clamp(settings.PeriodicReviewIntervalMinutes, 1, 1440);
        _nextPromptUtc = DateTime.UtcNow.AddMinutes(intervalMin);
    }

    public void OnReviewCompleted(DateTime completedUtc)
    {
        _isPromptShowing = false;
        var settings = _settingsProvider();
        int intervalMin = Math.Clamp(settings.PeriodicReviewIntervalMinutes, 1, 1440);
        _nextPromptUtc = DateTime.UtcNow.AddMinutes(intervalMin);
    }

    public void ResetPromptShowing()
    {
        _isPromptShowing = false;
    }

    public void SetNextPromptUtc(DateTime nextUtc)
    {
        _nextPromptUtc = ProjectTimeHistory.NormalizeUtc(nextUtc);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }
}
