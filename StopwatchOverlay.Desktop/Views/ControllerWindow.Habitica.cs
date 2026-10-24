using System;
using System.Threading;
using Avalonia.Threading;
using StopwatchOverlay.Habitica;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    private DispatcherTimer? _habiticaTimer;
    private CancellationTokenSource? _habiticaCancellation;
    private bool _habiticaTickRunning;

    private void StartHabiticaSync()
    {
        if (_habiticaTimer != null) return;
        _habiticaCancellation = new CancellationTokenSource();
        _habiticaTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _habiticaTimer.Tick += OnHabiticaTick;
        _habiticaTimer.Start();
    }

    private async void OnHabiticaTick(object? sender, EventArgs e)
    {
        if (_habiticaTickRunning || _habiticaCancellation == null) return;
        _habiticaTickRunning = true;
        try
        {
            HabiticaIntegration.Current.Configure(_settings);
            await HabiticaIntegration.Current.TickAsync(_settings, _habiticaCancellation.Token);
        }
        catch (OperationCanceledException) { }
        // The coordinator exposes safe status in Settings; never log credentials or raw HTTP errors.
        catch { }
        finally { _habiticaTickRunning = false; }
    }

    private void StopHabiticaSync()
    {
        _habiticaTimer?.Stop();
        if (_habiticaTimer != null) _habiticaTimer.Tick -= OnHabiticaTick;
        _habiticaTimer = null;
        _habiticaCancellation?.Cancel();
        _habiticaCancellation?.Dispose();
        _habiticaCancellation = null;
        HabiticaIntegration.Current.CancelPendingRequests();
    }
}
