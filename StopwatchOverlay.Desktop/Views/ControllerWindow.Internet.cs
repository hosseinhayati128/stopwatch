using System;
using System.Linq;
using System.Threading;
using Avalonia.Threading;
using StopwatchOverlay.Internet;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    private InternetMonitorService? _internetMonitorService;
    private Action<InternetCheckResult>? _internetMonitorHandler;
    private InternetCheckResult? _internetLastResult;
    private int _internetHasRunningTimer, _internetMonitorGeneration;

    private void StartInternetMonitor()
    {
        if (_previewMode || _isExiting || _internetMonitorService != null) return;
        UpdateInternetTimerState();
        AttachInternetMonitor(new(() => _settings, () => Volatile.Read(ref _internetHasRunningTimer) != 0));
    }

    private void AttachInternetMonitor(InternetMonitorService service)
    {
        StopInternetMonitor();
        if (_isExiting) { service.Dispose(); return; }
        _internetMonitorService = service;
        _internetMonitorHandler = result =>
        {
            int generation = Volatile.Read(ref _internetMonitorGeneration);
            Dispatcher.UIThread.Post(() =>
            {
                if (_isExiting || generation != _internetMonitorGeneration || !ReferenceEquals(service, _internetMonitorService)) return;
                _internetLastResult = result; _settingsWindow?.UpdateInternetConnection(result);
            });
        };
        service.CheckCompleted += _internetMonitorHandler;
    }

    private void RestartInternetMonitor()
    {
        Interlocked.Increment(ref _internetMonitorGeneration);
        _internetMonitorService?.RestartTimer();
        _settingsWindow?.RefreshInternetConnection();
    }

    private void UpdateInternetTimerState()
    {
        // Worker callbacks read this cache, never the UI-owned timer collection.
        int running = _timerManager.Sessions.Any(timer => timer.IsRunning) ? 1 : 0;
        if (Interlocked.Exchange(ref _internetHasRunningTimer, running) != running)
            _settingsWindow?.RefreshInternetConnection();
    }

    private void StopInternetMonitor()
    {
        Interlocked.Increment(ref _internetMonitorGeneration);
        if (_internetMonitorService == null) return;
        _internetMonitorService.CheckCompleted -= _internetMonitorHandler;
        _internetMonitorService.Dispose(); _internetMonitorService = null; _internetMonitorHandler = null;
    }
}
