using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media;
using StopwatchOverlay.ActivityWatch;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private readonly HttpClient? _activityWatchHttpClient;
    private readonly Func<bool> _activityWatchLauncher;
    private CancellationTokenSource? _activityWatchOperation;
    private string? _activityWatchAddress;
    private string _activityWatchTitle = "Not checked yet", _activityWatchMessage = "Test the server to verify this address.",
        _activityWatchTone = "warning", _activityWatchServer = "—", _activityWatchLastCheck = "Never";

    private bool TryActivityWatchAddress(out string address, out bool local)
    {
        address = ActivityWatchServerUrlTextBox.Text?.Trim() ?? "";
        if (address.Length == 0) address = "http://localhost:5600";
        if (!address.Contains("://", StringComparison.Ordinal)) address = "http://" + address;
        address = address.TrimEnd('/');
        bool valid = Uri.TryCreate(address, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https" && !string.IsNullOrWhiteSpace(uri.Host);
        local = valid && uri!.IsLoopback;
        return valid;
    }

    private void RefreshActivityWatchConnection()
    {
        if (_closed) return;
        bool valid = TryActivityWatchAddress(out var address, out bool local);
        if (_activityWatchAddress != address)
        {
            bool changed = _activityWatchAddress != null;
            _activityWatchAddress = address;
            CancelActivityWatchConnection();
            if (changed) SetActivityWatchState("Address changed", "Test this address to check the new connection.", "warning", lastCheck: "Not checked");
        }
        bool busy = _activityWatchOperation != null;
        TestActivityWatchButton.IsEnabled = valid && !busy;
        StartActivityWatchAppButton.IsEnabled = local && !busy;
        ActivityWatchServerUrlTextBox.IsEnabled = !busy;
        if (_activityWatchPage == null) return;
        TestActivityWatchButton.Content = busy ? "Checking…" : "Test connection";
        _activityWatchIntegration!.Text = ActivityWatchEnabledCheck.IsChecked == true ? "Integration on" : "Integration off";
        _activityWatchIntegration.Foreground = Brush.Parse(ActivityWatchEnabledCheck.IsChecked == true ? "#2D7169" : "#865C15");
        _activityWatchAddressHint!.Text = valid ? "Blank uses the local default: http://localhost:5600." : "Enter a valid HTTP or HTTPS server address.";
        _activityWatchAddressHint.Foreground = valid ? SettingsMuted : Brush.Parse("#913F32");
        _activityWatchLaunchHint!.Text = local ? "Start opens ActivityWatch on this computer." : "Start works only for a server on this computer.";
        PaintActivityWatchState();
    }

    private void SetActivityWatchState(string title, string message, string tone = "normal", string version = "—", string? lastCheck = null)
    {
        if (_closed) return;
        _activityWatchTitle = title; _activityWatchMessage = message; _activityWatchTone = tone;
        _activityWatchServer = version; _activityWatchLastCheck = lastCheck ?? DateTime.Now.ToString("HH:mm:ss");
        ActivityWatchStatusText.Text = title + " · " + message;
        ActivityWatchStatusText.Foreground = tone == "error" ? Brushes.OrangeRed : tone == "warning" ? Brushes.Goldenrod : Brushes.ForestGreen;
        PaintActivityWatchState();
    }

    private void PaintActivityWatchState()
    {
        if (_activityWatchPage == null || _closed) return;
        string color = _activityWatchTone == "error" ? "913F32" : _activityWatchTone == "warning" ? "865C15" : "2D7169";
        _activityWatchHeading!.Text = _activityWatchTitle; _activityWatchHeading.Foreground = Brush.Parse("#" + color);
        _activityWatchDetail!.Text = _activityWatchMessage;
        _activityWatchStateBox!.Background = Brush.Parse("#11" + color); _activityWatchStateBox.BorderBrush = Brush.Parse("#55" + color);
        _activityWatchVersion!.Text = _activityWatchServer; _activityWatchCheckedAt!.Text = _activityWatchLastCheck;
        _activityWatchProgress!.IsVisible = _activityWatchOperation != null;
    }

    private void CancelActivityWatchConnection()
    {
        var operation = _activityWatchOperation;
        if (operation == null) return;
        _activityWatchOperation = null; operation.Cancel();
        if (!_closed)
        {
            SetActivityWatchState("Check canceled", "Test the connection again when you’re ready.", "warning", lastCheck: "Not completed");
            RefreshActivityWatchConnection();
        }
    }

    private async Task RunActivityWatchConnectionAsync(bool launch)
    {
        if (_closed || _activityWatchOperation != null || !TryActivityWatchAddress(out var address, out bool local) || (launch && !local)) return;
        using var operation = new CancellationTokenSource(TimeSpan.FromSeconds(launch ? 30 : 15));
        _activityWatchOperation = operation;
        bool Current() => !_closed && ReferenceEquals(_activityWatchOperation, operation);
        SetActivityWatchState(launch ? "Starting ActivityWatch…" : "Checking the server…",
            launch ? "Waiting for the local server to respond." : "Waiting for ActivityWatch to respond.", lastCheck: "In progress");
        RefreshActivityWatchConnection();
        try
        {
            if (launch && !_activityWatchLauncher())
            {
                SetActivityWatchState("Open ActivityWatch manually", "Stopwatch couldn’t open ActivityWatch automatically. Open it yourself, then test the connection.", "warning", lastCheck: "Not checked");
                return;
            }
            var client = new ActivityWatchClient(address, _activityWatchHttpClient);
            string? error = null;
            for (int i = 0; i < (launch ? 12 : 1); i++)
            {
                if (launch) await Task.Delay(500, operation.Token);
                var result = await client.TestConnectionAsync(operation.Token);
                if (!Current()) return;
                operation.Token.ThrowIfCancellationRequested();
                if (result.Success)
                {
                    SetActivityWatchState("Server reachable", "ActivityWatch answered at this server address.", version: result.Version);
                    return;
                }
                error = result.ErrorMessage;
            }
            SetActivityWatchState(launch ? "Server not ready yet" : "Couldn’t reach the server",
                launch ? "ActivityWatch was started, but its connection check failed. " + (error ?? "Test the connection again shortly.")
                    : error ?? "Start ActivityWatch, check the address, then test again.",
                launch ? "warning" : "error");
        }
        catch (OperationCanceledException)
        {
            if (Current()) SetActivityWatchState(launch ? "Server not ready yet" : "Couldn’t reach the server",
                launch ? "ActivityWatch was started, but the server hasn’t responded. Test the connection again shortly." : "The connection timed out. Check that ActivityWatch is running.", "warning");
        }
        catch (Exception ex)
        {
            if (Current()) SetActivityWatchState(launch ? "Couldn’t start ActivityWatch" : "Couldn’t reach the server", ex.Message, "error");
        }
        finally
        {
            // An older completion must not unlock controls owned by a newer check.
            if (Current()) { _activityWatchOperation = null; RefreshActivityWatchConnection(); }
        }
    }
}
