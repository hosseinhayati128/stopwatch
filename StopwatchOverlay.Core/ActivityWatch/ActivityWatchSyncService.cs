using System;
using System.Threading;
using System.Threading.Tasks;

namespace StopwatchOverlay.ActivityWatch;

public sealed class ActivityWatchSyncService : IDisposable
{
    private readonly Func<AppSettings> _settingsProvider;
    private Timer? _timer;
    private bool _isSyncing;
    private bool _disposed;

    public event Action<ActivityWatchSyncResult>? SyncCompleted;

    public ActivityWatchSyncService(Func<AppSettings> settingsProvider)
    {
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        RestartTimer();
    }

    public void RestartTimer()
    {
        var settings = _settingsProvider();
        _timer?.Dispose();
        _timer = null;

        if (!settings.ActivityWatchEnabled || !settings.ActivityWatchPeriodicSyncEnabled)
            return;

        int intervalMin = Math.Clamp(settings.ActivityWatchPeriodicSyncIntervalMinutes, 1, 1440);
        var period = TimeSpan.FromMinutes(intervalMin);

        // First sync 1 minute after startup, then every period (default: 60 minutes / 1 hour)
        _timer = new Timer(OnTimerTick, null, TimeSpan.FromMinutes(1), period);
    }

    private async void OnTimerTick(object? state)
    {
        if (_disposed || _isSyncing) return;

        var settings = _settingsProvider();
        if (!settings.ActivityWatchEnabled || !settings.ActivityWatchPeriodicSyncEnabled) return;
        if (string.IsNullOrWhiteSpace(settings.ObsidianVaultFolder)) return;

        _isSyncing = true;
        try
        {
            var result = await ActivityWatchSync.SyncAsync(settings).ConfigureAwait(false);
            SyncCompleted?.Invoke(result);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "ActivityWatchSyncService.OnTimerTick");
        }
        finally
        {
            _isSyncing = false;
        }
    }

    public async Task<ActivityWatchSyncResult> TriggerSyncOnceAsync()
    {
        var settings = _settingsProvider();
        var result = await ActivityWatchSync.SyncAsync(settings).ConfigureAwait(false);
        SyncCompleted?.Invoke(result);
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
