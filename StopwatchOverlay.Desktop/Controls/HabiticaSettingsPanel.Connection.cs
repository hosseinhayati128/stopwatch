using System;
using Avalonia.Controls;
using Avalonia.Media;
using StopwatchOverlay.Habitica;

namespace StopwatchOverlay.Desktop.Controls;

public sealed partial class HabiticaSettingsPanel
{
    private bool _testing, _savingConnection, _connectionFailed;
    private string _connectionHeading = "Connect your account", _credentialAccount = "", _credentialStatus = "";
    private bool _credentialMissing;
    private (string Account, string Client, string Token) _lastDraft;
    private HabiticaSyncStatus? _lastSyncStatus;
    private (string Account, string Client, string Token) Draft() => (_account.Text ?? "", _client.Text ?? "", _token.Text ?? "");

    private void ConnectionDraftChanged()
    {
        var next = Draft();
        if (_lastDraft == next) return;
        bool identityChanged = _lastDraft.Account != next.Account || _lastDraft.Client != next.Client;
        _lastDraft = next;
        if (_loading || _savingConnection) return;
        ClearConfirmation(); _testCancellation?.Cancel();
        if (identityChanged) SetTokenVisible(false);
        string? error = HabiticaApiClient.ConfigurationError(next.Account.Trim(), next.Client.Trim());
        if (error != null && (!string.IsNullOrWhiteSpace(next.Account) || !string.IsNullOrWhiteSpace(next.Client)))
            SetFeedback(error, "Check connection details", true);
        else SetFeedback("Choose Save connection or Test connection to apply these details.", "Unsaved changes");
        UpdateConnectionActions(); UpdateCredentialFeedback();
    }

    private void SetFeedback(string message, string title = "Status", bool failed = false)
    {
        _connectionHeading = title; _connectionFailed = failed; _feedback.Text = message;
        UpdateConnectionFeedback();
        // Recovery can change the scroll viewport; refresh cached auto rows and their following section.
        if (_navigatorCards != null) Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            _navigatorCards?.InvalidateMeasure(); _navigatorRoot?.InvalidateArrange();
        }, Avalonia.Threading.DispatcherPriority.Background);
    }

    private void UpdateConnectionFeedback()
    {
        if (_navigatorFeedbackTitle == null) return;
        _navigatorFeedbackTitle.Text = _connectionHeading;
        _navigatorFeedbackTitle.Foreground = _connectionFailed ? Error : Teal;
        _navigatorFeedbackCard!.Background = Brush.Parse(_connectionFailed ? "#11913F32" : "#112D7169");
        _navigatorFeedbackCard.BorderBrush = Brush.Parse(_connectionFailed ? "#55913F32" : "#552D7169");
    }

    private void UpdateCredentialFeedback()
    {
        if (_navigatorCredentialTitle == null) return;
        string account = _account.Text?.Trim().ToLowerInvariant() ?? "";
        bool known = account.Length > 0 && account == _credentialAccount;
        bool session = known && _credentialStatus.Contains("session-only", StringComparison.OrdinalIgnoreCase);
        bool storageKnown = !string.IsNullOrEmpty(_credentialStatus);
        _navigatorCredentialTitle.Text = known ? _credentialMissing ? "No token available" : session ? "Token kept for this session" : storageKnown ? "Token saved securely" : "Token available" : "Token stored separately";
        _navigatorCredentialTitle.Foreground = session ? Amber : known && !_credentialMissing ? Teal : Muted;
        _navigatorCredentialDetail!.Text = known ? _credentialMissing ? "Paste the API token for this User ID."
            : session ? "Enter the token again after restarting Stopwatch." : storageKnown ? _credentialStatus : "Storage protection has not been verified for this account."
            : "Saved tokens stay hidden. Test connection checks this account’s token.";
        _token.Watermark = known && !_credentialMissing ? "Paste a token to replace it" : "Paste your API token";
    }

    private void UpdateConnectionActions()
    {
        bool valid = HabiticaApiClient.ConfigurationError(_account.Text?.Trim(), _client.Text?.Trim()) == null;
        bool sameAccount = _account.Text?.Trim() == _settings?.HabiticaUserId && _client.Text?.Trim() == _settings?.HabiticaClientId;
        double wait = sameAccount && _lastSyncStatus?.NextRequestUtc is DateTime next ? Math.Ceiling((next.ToUniversalTime() - DateTime.UtcNow).TotalSeconds) : 0;
        _saveConnection.IsEnabled = valid && !_testing;
        _test.IsEnabled = valid && !_testing && wait <= 0;
        _test.Content = _testing ? "Testing…" : wait > 0 ? $"Try again in {wait:0}s" : "Test connection";
        _enabled.IsEnabled = !_testing && (_enabled.IsChecked == true || valid);
        _syncExisting.IsEnabled = !_testing && !_queueing && _lastSyncStatus?.ImportProgress?.IsQueuing != true;
        if (_navigatorReveal != null)
        {
            _navigatorReveal.IsEnabled = !string.IsNullOrEmpty(_token.Text);
            if (!_navigatorReveal.IsEnabled) SetTokenVisible(false);
        }
    }

    private void UpdateNavigatorStatus(HabiticaSyncStatus status)
    {
        _lastSyncStatus = status; UpdateConnectionActions(); UpdateCredentialFeedback();
        if (_navigatorSyncTitle == null) return;
        bool active = _settings?.HabiticaEnabled == true && status.IsOwner;
        _navigatorSyncTitle.Text = active ? "Sync enabled" : _settings?.HabiticaEnabled == true ? "Sync needs attention" : "Sync paused";
        _navigatorSyncTitle.Foreground = active ? Teal : Amber;
        _navigatorSyncDetail!.Text = active ? status.Message : _settings?.HabiticaEnabled == true ? status.Message : "New changes wait locally until you enable sync again.";
        _navigatorPending!.Text = status.PendingCount.ToString();
        _navigatorLastSync!.Text = status.LastSuccessUtc?.ToLocalTime().ToString("g") ?? "Never";
    }

    private void UpdateRefreshLoop()
    {
        if (IsEffectivelyVisible) { RefreshStatus(); _refresh.Start(); }
        else { _refresh.Stop(); HideConnectionDraft(); }
    }
}
