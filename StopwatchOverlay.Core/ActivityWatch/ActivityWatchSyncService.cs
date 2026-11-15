using System;
using System.Threading;
using System.Threading.Tasks;

namespace StopwatchOverlay.ActivityWatch;

public sealed class ActivityWatchSyncService : IDisposable
{
    private readonly Func<AppSettings> _settingsProvider;
    private readonly Func<AppSettings, CancellationToken, Task<ActivityWatchSyncResult>> _sync;
    private readonly TimeProvider _clock;
    private readonly object _gate = new();
    private AppSettings _settings = new();
    private ITimer? _timer;
    private CancellationTokenSource? _operation;
    private int _generation;
    private bool _disposed;
    private enum Trigger { Manual, Scheduled, StopwatchExport }

    public event Action<ActivityWatchSyncResult>? SyncCompleted;

    public ActivityWatchSyncService(Func<AppSettings> settingsProvider,
        Func<AppSettings, CancellationToken, Task<ActivityWatchSyncResult>>? sync = null, TimeProvider? timeProvider = null)
    {
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _sync = sync ?? ((settings, ct) => ActivityWatchSync.SyncAsync(settings, ct: ct));
        _clock = timeProvider ?? TimeProvider.System;
        RestartTimer();
    }

    public void RestartTimer()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _generation++;
            _operation?.Cancel();
            _timer?.Dispose(); _timer = null;
            _settings = ActivityWatchSync.SnapshotSettings(_settingsProvider());
            if (_settings.ActivityWatchEnabled && _settings.ActivityWatchPeriodicSyncEnabled)
                _timer = _clock.CreateTimer(state => { _ = RunAsync(Trigger.Scheduled); }, null, TimeSpan.FromMinutes(1),
                    TimeSpan.FromMinutes(Math.Clamp(_settings.ActivityWatchPeriodicSyncIntervalMinutes, 1, 1440)));
        }
    }

    public Task<ActivityWatchSyncResult> TriggerSyncOnceAsync() => RunAsync(Trigger.Manual);
    public void SyncAfterStopwatchExport() => _ = RunAsync(Trigger.StopwatchExport);

    private async Task<ActivityWatchSyncResult> RunAsync(Trigger trigger)
    {
        AppSettings settings; CancellationTokenSource operation; int generation;
        lock (_gate)
        {
            if (_disposed || _operation != null) return new(false, 0, 0, "", "ActivityWatch sync is unavailable or already running.");
            settings = ActivityWatchSync.SnapshotSettings(_settings);
            if (trigger != Trigger.Manual && (!settings.ActivityWatchEnabled || string.IsNullOrWhiteSpace(settings.ObsidianVaultFolder) ||
                    (trigger == Trigger.Scheduled ? !settings.ActivityWatchPeriodicSyncEnabled : !settings.ActivityWatchSyncOnStopwatchSync)))
                return new(false, 0, 0, "", "Automatic sync is paused.");
            generation = _generation;
            operation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            _operation = operation;
        }
        try
        {
            var result = await _sync(settings, operation.Token).ConfigureAwait(false);
            lock (_gate)
                if (!_disposed && generation == _generation && !operation.IsCancellationRequested) SyncCompleted?.Invoke(result);
            return result;
        }
        catch (OperationCanceledException) { return new(false, 0, 0, "", "ActivityWatch sync was canceled."); }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "ActivityWatchSyncService");
            return new(false, 0, 0, "", "ActivityWatch sync failed: " + ex.Message);
        }
        finally
        {
            lock (_gate)
            {
                if (ReferenceEquals(_operation, operation)) _operation = null;
                operation.Dispose();
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; _generation++;
            _timer?.Dispose(); _timer = null;
            _operation?.Cancel();
        }
    }
}
