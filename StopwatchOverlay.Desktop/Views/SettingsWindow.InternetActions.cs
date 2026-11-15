using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using StopwatchOverlay.Internet;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private readonly Func<int, CancellationToken, Task<InternetCheckResult>> _internetProbe;
    private readonly Func<bool>? _internetTimerRunning;
    private CancellationTokenSource? _internetOperation;
    private InternetCheckResult? _internetResult;
    private string? _internetMessage;
    private bool _internetCanceled;

    private bool TryInternetInterval(out int interval) => int.TryParse(InternetCheckIntervalTextBox.Text?.Trim(), out interval) && interval is >= 1 and <= 120;

    private void CommitInternetInterval()
    {
        if (_loading || _committing || _closed) return;
        if (TryInternetInterval(out int interval) && interval != _settings.InternetMonitorIntervalMinutes)
        {
            _settings.InternetMonitorIntervalMinutes = interval;
            CommitControls(SettingsChangeKind.InternetMonitor);
        }
        RefreshInternetConnection();
    }

    internal void UpdateInternetConnection(InternetCheckResult result)
    {
        if (_closed || (_internetResult != null && result.Timestamp < _internetResult.Timestamp)) return;
        _internetResult = result; _internetMessage = null; RefreshInternetConnection();
        if (InternetHistoryVisible) _ = LoadInternetHistoryAsync();
    }

    internal void RefreshInternetConnection()
    {
        if (_closed) return;
        bool busy = _internetOperation != null, enabled = InternetMonitorEnabledCheck.IsChecked == true;
        bool valid = TryInternetInterval(out int interval), timerOnly = InternetMonitorOnlyDuringTimersCheck.IsChecked == true;
        TestInternetButton.IsEnabled = !busy;
        InternetCheckIntervalTextBox.IsEnabled = InternetMonitorOnlyDuringTimersCheck.IsEnabled = enabled;
        var result = _internetResult;
        string title = busy ? "Checking connection…" : _internetMessage != null ? _internetCanceled ? "Check canceled" : "Check failed"
            : result == null ? "Not checked yet" : result.Status == InternetStatus.Offline ? "Internet not reached"
            : result.Status == InternetStatus.Slow ? "Slow connection" : !result.DownloadMbps.HasValue ? "Online · speed unavailable" : "Online";
        string detail = busy ? "Measuring latency and a short download sample." : _internetMessage ?? (result == null ? "Run a check to see your connection quality."
            : result.Status == InternetStatus.Offline ? "The check did not reach the internet. The connection may be offline or the test may be blocked."
            : result.Status == InternetStatus.Slow ? "High latency or low download speed may make online work feel delayed."
            : !result.DownloadMbps.HasValue ? "Latency was measured, but download speed is unavailable." : "Internet responded to the last check.");
        string color = !busy && ((_internetMessage != null && !_internetCanceled) || result?.Status == InternetStatus.Offline) ? "913F32"
            : !busy && (result == null || _internetMessage != null || result.Status == InternetStatus.Slow || !result.DownloadMbps.HasValue) ? "865C15" : "2D7169";
        if (_internetPage == null)
        {
            if (busy || result != null || _internetMessage != null)
            { InternetStatusText.Text = title + " · " + detail + (result == null ? "" : $" · {result.NetworkInfo.DisplayText} · Ping: {result.PingDisplay} · Download: {result.SpeedDisplay}"); InternetStatusText.Foreground = Brush.Parse("#" + color); }
            return;
        }
        TestInternetButton.Content = busy ? "Checking…" : "Test now";
        _internetHeading!.Text = title; _internetHeading.Foreground = Brush.Parse("#" + color); _internetDetail!.Text = detail;
        _internetStateBox!.Background = Brush.Parse("#11" + color); _internetStateBox.BorderBrush = Brush.Parse("#55" + color); _internetProgress!.IsVisible = busy;
        var shown = busy || _internetMessage != null ? null : result;
        string name = shown == null ? "Not checked" : !string.IsNullOrWhiteSpace(shown.NetworkInfo.NetworkName) ? shown.NetworkInfo.NetworkName
            : !string.IsNullOrWhiteSpace(shown.NetworkInfo.AdapterDescription) ? shown.NetworkInfo.AdapterDescription : "Unknown network";
        _internetNetwork!.Text = name; _internetNetwork.FlowDirection = name.Any(c => c is >= '\u0590' and <= '\u08ff') ? Avalonia.Media.FlowDirection.RightToLeft : Avalonia.Media.FlowDirection.LeftToRight;
        _internetAdapter!.Text = shown == null ? "Network details appear after a completed check." : shown.NetworkInfo.ConnectionType
            + (shown.NetworkInfo.SignalPercent.HasValue ? $" · signal {shown.NetworkInfo.SignalPercent}%" : "");
        _internetPing!.Text = shown?.PingMs is {} ping ? $"{ping} ms" : "—";
        _internetSpeed!.Text = shown?.DownloadMbps is {} speed ? $"{speed:F1} Mbps" : shown?.Status is InternetStatus.Online or InternetStatus.Slow ? "Unavailable" : "—";
        _internetCheckedAt!.Text = busy ? "In progress" : shown != null ? shown.Timestamp.ToString("MMM d · HH:mm:ss") : _internetMessage != null ? "Not completed" : "Never";
        _internetIntervalHint!.Text = !valid ? "Enter a whole number from 1 to 120."
            : interval != _settings.InternetMonitorIntervalMinutes ? "Press Enter or leave this field to apply." : "1–120 minutes · default 10";
        _internetIntervalHint.Foreground = valid || !enabled ? SettingsMuted : Brush.Parse("#913F32");
        bool waiting = enabled && valid && timerOnly && _internetTimerRunning?.Invoke() == false;
        string scheduleColor = !enabled ? "655539" : !valid || waiting ? "865C15" : "2D7169";
        _internetScheduleHeading!.Foreground = Brush.Parse("#" + scheduleColor); _internetScheduleBox!.Background = Brush.Parse("#11" + scheduleColor);
        _internetScheduleHeading.Text = !enabled ? "Automatic checks off" : !valid ? "Check the interval" : waiting ? "Waiting for a stopwatch" : "Scheduled checks enabled";
        int effectiveInterval = Math.Clamp(_settings.InternetMonitorIntervalMinutes, 1, 120);
        _internetScheduleDetail!.Text = !enabled ? "Use Test now whenever you need a connection check." : !valid ? $"The current interval ({effectiveInterval} minutes) stays in use until corrected."
            : timerOnly ? $"Every {effectiveInterval} minutes while a stopwatch is running." : $"Every {effectiveInterval} minutes while Stopwatch is open.";
    }

    private void CancelInternetCheck()
    {
        var operation = _internetOperation; if (operation == null) return;
        _internetOperation = null; operation.Cancel();
        if (!_closed) { _internetMessage = "Test the connection again when you’re ready."; _internetCanceled = true; RefreshInternetConnection(); }
    }

    private async Task RunInternetCheckAsync()
    {
        if (_closed || _internetOperation != null) return;
        using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        _internetOperation = operation; _internetMessage = null; _internetCanceled = false;
        bool Current() => !_closed && ReferenceEquals(_internetOperation, operation);
        RefreshInternetConnection();
        try
        {
            // Adapter detection may invoke a platform command before its first await.
            var result = await Task.Run(() => _internetProbe(_settings.InternetSampleSizeBytes, operation.Token), operation.Token);
            if (Current()) { operation.Token.ThrowIfCancellationRequested(); UpdateInternetConnection(result); }
        }
        catch (OperationCanceledException) { if (Current()) _internetMessage = "The check timed out. Try again when you’re ready."; }
        catch (Exception ex) { if (Current()) _internetMessage = ex.Message; }
        finally { if (Current()) { _internetOperation = null; RefreshInternetConnection(); } }
    }
}
