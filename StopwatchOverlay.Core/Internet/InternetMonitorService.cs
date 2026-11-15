using System;
using System.Threading;
using System.Threading.Tasks;

namespace StopwatchOverlay.Internet;

public sealed class InternetMonitorService : IDisposable
{
    private readonly Func<AppSettings> _settingsProvider;
    private readonly Func<bool> _isAnyTimerRunning;
    private readonly Func<int, CancellationToken, Task<InternetCheckResult>> _probe;
    private readonly Action<InternetCheckResult, AppSettings> _save;
    private readonly object _gate = new();
    private readonly SemaphoreSlim _probeGate = new(1, 1);
    private readonly CancellationTokenSource _lifetime = new();
    private Timer? _timer;
    private CancellationTokenSource? _operation;
    private int _generation;
    private bool _disposed;

    public event Action<InternetCheckResult>? CheckCompleted;

    public InternetMonitorService(
        Func<AppSettings> settingsProvider,
        Func<bool> isAnyTimerRunning)
        : this(settingsProvider, isAnyTimerRunning,
            (bytes, token) => InternetSpeedProbe.CheckConnectionAsync(bytes, cancellationToken: token),
            (result, settings) => InternetLogSync.AppendCheck(result, settings)) { }

    internal InternetMonitorService(Func<AppSettings> settingsProvider, Func<bool> isAnyTimerRunning,
        Func<int, CancellationToken, Task<InternetCheckResult>> probe, Action<InternetCheckResult, AppSettings> save)
    {
        _settingsProvider = settingsProvider ?? throw new ArgumentNullException(nameof(settingsProvider));
        _isAnyTimerRunning = isAnyTimerRunning ?? throw new ArgumentNullException(nameof(isAnyTimerRunning));
        _probe = probe ?? throw new ArgumentNullException(nameof(probe));
        _save = save ?? throw new ArgumentNullException(nameof(save));
        RestartTimer();
    }

    public void RestartTimer()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _generation++; _operation?.Cancel(); _timer?.Dispose(); _timer = null;
            var settings = _settingsProvider();
            if (!settings.InternetMonitorEnabled) return;
            int generation = _generation;
            _timer = new Timer(OnTimerTick, generation, TimeSpan.FromSeconds(30),
                TimeSpan.FromMinutes(Math.Clamp(settings.InternetMonitorIntervalMinutes, 1, 120)));
        }
    }

    private async void OnTimerTick(object? state)
    {
        try
        {
            if (state is int version) await CheckAsync(true, version);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "InternetMonitorService.OnTimerTick");
        }
    }

    public async Task<InternetCheckResult> RunCheckOnceAsync()
    {
        return await CheckAsync(false) ?? throw new InvalidOperationException("A connection check is already running or the monitor is closed.");
    }

    public async Task<InternetCheckResult> TestConnectionAsync(int sampleBytes, CancellationToken cancellationToken)
    {
        CancellationTokenSource linked;
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
        }
        using (linked) return await RunProbeAsync(sampleBytes, linked.Token).ConfigureAwait(false);
    }

    private async Task<InternetCheckResult> RunProbeAsync(int sampleBytes, CancellationToken token)
    {
        // Manual and automatic download samples must not compete for the same bandwidth.
        await _probeGate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            var result = await _probe(sampleBytes, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested(); return result;
        }
        finally { _probeGate.Release(); }
    }

    private async Task<InternetCheckResult?> CheckAsync(bool scheduled, int? expectedGeneration = null)
    {
        AppSettings settings; CancellationTokenSource operation; int generation;
        lock (_gate)
        {
            if (_disposed || _operation != null || (expectedGeneration.HasValue && expectedGeneration != _generation)) return null;
            settings = _settingsProvider();
            if (scheduled && (!settings.InternetMonitorEnabled || (settings.InternetMonitorOnlyDuringTimers && !_isAnyTimerRunning()))) return null;
            operation = _operation = new CancellationTokenSource(TimeSpan.FromSeconds(90)); generation = _generation;
        }
        using (operation)
        try
        {
            var result = await RunProbeAsync(settings.InternetSampleSizeBytes, operation.Token).ConfigureAwait(false);
            lock (_gate)
            {
                operation.Token.ThrowIfCancellationRequested();
                if (_disposed || generation != _generation) throw new OperationCanceledException(operation.Token);
                _save(result, settings); CheckCompleted?.Invoke(result);
            }
            return result;
        }
        finally
        {
            lock (_gate) { if (ReferenceEquals(_operation, operation)) _operation = null; }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true; _generation++; _operation?.Cancel(); _lifetime.Cancel(); _lifetime.Dispose(); _timer?.Dispose(); _timer = null;
        }
    }
}
