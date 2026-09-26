using System;
using System.Threading;
using System.Threading.Tasks;

namespace StopwatchOverlay.Internet;

public sealed class InternetMonitorService : IDisposable
{
    private readonly Func<AppSettings> _settingsProvider;
    private readonly Func<bool> _isAnyTimerRunning;
    private Timer? _timer;
    private bool _isChecking;
    private bool _disposed;

    public event Action<InternetCheckResult>? CheckCompleted;

    public InternetMonitorService(
        Func<AppSettings> settingsProvider,
        Func<bool> isAnyTimerRunning)
    {
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _isAnyTimerRunning = isAnyTimerRunning ?? throw new ArgumentNullException(nameof(isAnyTimerRunning));

        RestartTimer();
    }

    public void RestartTimer()
    {
        var settings = _settingsProvider();
        _timer?.Dispose();
        _timer = null;

        if (!settings.InternetMonitorEnabled)
            return;

        int intervalMin = Math.Clamp(settings.InternetMonitorIntervalMinutes, 1, 120);
        var period = TimeSpan.FromMinutes(intervalMin);

        // First check in 30 seconds, then every period
        _timer = new Timer(OnTimerTick, null, TimeSpan.FromSeconds(30), period);
    }

    private async void OnTimerTick(object? state)
    {
        if (_disposed || _isChecking) return;

        var settings = _settingsProvider();
        if (!settings.InternetMonitorEnabled) return;

        // If only during timers, verify active timer running
        if (settings.InternetMonitorOnlyDuringTimers && !_isAnyTimerRunning())
            return;

        _isChecking = true;
        try
        {
            var result = await InternetSpeedProbe.CheckConnectionAsync(
                sampleBytes: settings.InternetSampleSizeBytes,
                pingHost: "1.1.1.1");

            InternetLogSync.AppendCheck(result, settings);
            CheckCompleted?.Invoke(result);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "InternetMonitorService.OnTimerTick");
        }
        finally
        {
            _isChecking = false;
        }
    }

    public async Task<InternetCheckResult> RunCheckOnceAsync()
    {
        var settings = _settingsProvider();
        var result = await InternetSpeedProbe.CheckConnectionAsync(
            sampleBytes: settings.InternetSampleSizeBytes,
            pingHost: "1.1.1.1");

        InternetLogSync.AppendCheck(result, settings);
        CheckCompleted?.Invoke(result);
        return result;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _timer?.Dispose();
        _timer = null;
    }
}
