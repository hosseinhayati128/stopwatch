using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using StopwatchOverlay.Habitica;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Connection drafts and secrets stay separate from serialized settings.</summary>
public sealed partial class HabiticaSettingsPanel : Border
{
    protected override Type StyleKeyOverride => typeof(Border);
    private readonly TextBox _account = new() { Watermark = "Habitica User ID" };
    private readonly TextBox _client = new() { Watermark = "Author UUID-StopwatchOverlay" };
    private readonly TextBox _token = new() { PasswordChar = '●', Watermark = "Enter a token to replace the stored token" };
    private readonly CheckBox _enabled = new() { Content = "Enable Habitica sync on this computer" };
    private readonly TextBlock _status = Label("");
    private readonly TextBlock _credentials = Label("");
    private readonly TextBlock _feedback = Label("");
    private readonly TextBlock _confirmation = Label("");
    private readonly StackPanel _confirmPanel = new() { Name = "HabiticaImportConfirmation", Spacing = 8, IsVisible = false };
    private readonly StackPanel _importProgress = new() { Name = "HabiticaImportProgressPanel", Spacing = 6, Margin = new Thickness(0, 4, 0, 8), IsVisible = false };
    private readonly TextBlock _importProgressText = Label("");
    private readonly TextBlock _importProgressDetail = Label("");
    private readonly ProgressBar _importProgressBar = new() { Name = "HabiticaImportProgressBar", Height = 5, Minimum = 0, Maximum = 1 };
    private readonly HabiticaIntegration _integration;
    private readonly Button _syncExisting, _transferVault, _restoreMissing, _confirmButton, _cancelConfirmation;
    private Button? _confirmationSource;
    private bool _queueing;
    private string _importScope = "";
    private string? _importError;
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Button _test, _saveConnection, _pasteToken;
    private AppSettings? _settings;
    private Action? _save;
    private Action? _confirmed;
    private CancellationTokenSource? _testCancellation;
    private bool _loading;
    private bool _pastingToken;
    private readonly Func<Task<string?>> _readTokenClipboard;

    public HabiticaSettingsPanel() : this(null) { }

    public HabiticaSettingsPanel(Func<Task<string?>>? readTokenClipboard, HabiticaIntegration? integration = null)
    {
        _integration = integration ?? HabiticaIntegration.Current;
        _readTokenClipboard = readTokenClipboard ?? (() => TopLevel.GetTopLevel(this)?.Clipboard?.GetTextAsync() ?? Task.FromResult<string?>(null));
        _token.PastingFromClipboard += async (_, args) =>
        {
            if (args.Handled) return;
            args.Handled = true;
            await PasteTokenAsync();
        };
        // Fluent TextBox does not supply an editing context menu automatically.
        var pasteToken = new MenuItem { Header = "Paste", InputGesture = TextBox.PasteGesture };
        pasteToken.Click += (_, _) => _token.Paste();
        _token.ContextMenu = new ContextMenu { Items = { pasteToken } };
        _pasteToken = ActionButton("Paste", () => _token.Paste());
        _pasteToken.Name = "PasteHabiticaTokenButton";
        _pasteToken.Focusable = false; // preserve the token field's selection when replacing text
        Avalonia.Automation.AutomationProperties.SetName(_pasteToken, "Paste Habitica API token");
        _token.InnerRightContent = _pasteToken;
        Classes.Add("settingsCard");
        var content = new StackPanel { Spacing = 10 };
        var title = Label("Habitica");
        title.Classes.Clear();
        title.Classes.Add("sectionHeader");
        content.Children.Add(title);
        content.Children.Add(Label("Windows and Ubuntu can sync at the same time using separate local vaults and the same Habitica account. Each computer sends only its own items; local lists are not mirrored. No handoff is needed."));
        content.Children.Add(Label("Send todos, reminders and explicit checklist items from Stopwatch to Habitica. Standalone notes stay local. Sync runs while Stopwatch is open, including in the tray."));
        AddField(content, "User ID", _account);
        AddField(content, "Public X-Client identifier", _client);
        content.Children.Add(Label("Use an author-owned Habitica UUID followed by -StopwatchOverlay. This public identifier is not your API token."));
        AddField(content, "API token", _token);
        content.Children.Add(Label("This field clears after saving. Leaving it blank keeps any previously saved token; it does not delete it."));
        content.Children.Add(Label("Tokens use the operating system's protected secret store when available. Otherwise they last for this session only and must be entered again after restart."));
        content.Children.Add(_credentials);
        _saveConnection = ActionButton("Save connection", () => Run(SaveConnection));
        content.Children.Add(_saveConnection);
        content.Children.Add(_enabled);
        _enabled.IsCheckedChanged += (_, _) =>
        {
            if (_loading || _settings == null) return;
            Run(() =>
            {
                bool enable = _enabled.IsChecked == true;
                if (enable)
                {
                    SaveConnection();
                    if (!_integration.EnableHere(_settings))
                        throw new InvalidOperationException("This vault contains Habitica links owned by another installation. Separate local vaults can sync together. If you copied this linked vault, use the advanced move option after stopping its old copy.");
                }
                else _integration.Disable(_settings);
                _save?.Invoke();
            });
            RefreshStatus();
        };
        _test = ActionButton("Test connection", async () => await TestAsync());
        content.Children.Add(_test);
        _syncExisting = ActionButton("Sync existing items…", () => Run(() =>
        {
            if (_settings == null || _queueing) return;
            SaveConnection();
            if (!_settings.HabiticaEnabled || !_integration.GetStatus(_settings).IsOwner)
                throw new InvalidOperationException("Enable Habitica sync on this computer before queuing existing items.");
            int count = _recoveryRoot != null ? _recoveryExistingCount ?? 0 : _integration.CountExisting(_settings);
            if (count == 0) { SetFeedback("No existing items to sync."); return; }
            Confirm($"Sync {count} existing local todos and reminders to this Habitica account? Already linked items will be updated; existing Habitica tasks are not imported or matched by title.",
                () => { _ = QueueExistingAsync(); }, _syncExisting!, "Confirm sync");
        }));
        _syncExisting.Name = "SyncExistingHabiticaButton";
        content.Children.Add(_syncExisting);
        content.Children.Add(_confirmPanel);
        _importProgressText.Name = "HabiticaImportProgressText";
        _importProgressText.FontWeight = FontWeight.SemiBold;
        _importProgressDetail.Name = "HabiticaImportProgressDetail";
        _importProgressDetail.FontSize = 12;
        _importProgress.Children.Add(_importProgressText);
        _importProgress.Children.Add(_importProgressBar);
        _importProgress.Children.Add(_importProgressDetail);
        content.Children.Add(_importProgress);
        var copiedVaultOptions = new Expander { Name = "HabiticaCopiedVaultOptions", Header = "Copied or shared vault (advanced)", IsExpanded = false };
        var copiedVaultContent = new StackPanel { Spacing = 8 };
        copiedVaultContent.Children.Add(Label("Use this only when moving an existing linked vault to another computer. Independent local vaults do not need a transfer. Do not run two writers against the same copied/shared Habitica metadata."));
        _transferVault = ActionButton("Move this linked vault here…", () => Run(() =>
        {
            SaveConnection();
            Confirm("Close Stopwatch or disable sync on the old copy first, and finish copying the vault. This transfers sync ownership; it does not copy files. Separate local vaults are unaffected.", () =>
            {
                if (_settings == null) return;
                if (!_integration.EnableHere(_settings, takeOver: true))
                    throw new InvalidOperationException("Unable to move this linked vault to this installation.");
                _save?.Invoke();
                SetFeedback("This linked vault now syncs from this installation. Separate local vaults are unaffected.");
            }, _transferVault!, "Confirm transfer");
        }));
        _transferVault.Name = "TransferHabiticaVaultButton";
        copiedVaultContent.Children.Add(_transferVault);
        copiedVaultOptions.Content = copiedVaultContent;
        content.Children.Add(copiedVaultOptions);
        _restoreMissing = ActionButton("Recreate missing linked tasks…", () => Run(() =>
        {
            if (_settings == null) return;
            int count = _integration.CountMissing(_settings);
            if (count == 0) { SetFeedback("No missing linked tasks need recreation."); return; }
            Confirm($"Recreate {count} previously linked tasks that Habitica reported missing? Their current local text and checklist will be used. Review Habitica first to avoid recreating deliberately deleted tasks.", () =>
            {
                _integration.RecreateMissing(_settings);
                SetFeedback("Missing tasks queued for explicit recreation.");
            }, _restoreMissing!, $"Recreate {count} tasks");
        }));
        _restoreMissing.Name = "RestoreMissingHabiticaButton";
        content.Children.Add(_restoreMissing);
        _confirmPanel.Children.Add(_confirmation);
        var confirmationActions = new WrapPanel();
        _confirmButton = ActionButton("Confirm sync", () => Run(() =>
        {
            var action = _confirmed;
            ClearConfirmation();
            action?.Invoke();
        }));
        _confirmButton.Name = "ConfirmHabiticaActionButton";
        _confirmButton.Margin = new(0, 0, 8, 4);
        confirmationActions.Children.Add(_confirmButton);
        _cancelConfirmation = ActionButton("Cancel", () => { var source = _confirmationSource; ClearConfirmation(); source?.Focus(); });
        _cancelConfirmation.Name = "CancelHabiticaActionButton";
        confirmationActions.Children.Add(_cancelConfirmation);
        _confirmPanel.Children.Add(confirmationActions);
        content.Children.Add(_feedback);
        content.Children.Add(_status);
        Child = content;
        _account.TextChanged += (_, _) => ConnectionDraftChanged();
        _client.TextChanged += (_, _) => ConnectionDraftChanged();
        _token.TextChanged += (_, _) => ConnectionDraftChanged();
        _refresh.Tick += (_, _) => { if (IsEffectivelyVisible) RefreshStatus(); };
        AttachedToVisualTree += (_, _) => UpdateRefreshLoop();
        PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty) UpdateRefreshLoop(); };
        DetachedFromVisualTree += (_, _) =>
        {
            _refresh.Stop();
            HideConnectionDraft();
            _lastDraft = (_account.Text ?? "", _client.Text ?? "", "");
            _token.Text = "";
            ClearConfirmation();
        };
    }

    public void Bind(AppSettings settings, Action saveSettings)
    {
        _settings = settings;
        _save = saveSettings;
        Reload();
    }

    public void Reload()
    {
        if (_settings == null) return;
        bool changed = _account.Text != _settings.HabiticaUserId || _client.Text != _settings.HabiticaClientId;
        _loading = true;
        _account.Text = _settings.HabiticaUserId; _client.Text = _settings.HabiticaClientId;
        _lastDraft = Draft(); _loading = false;
        if (changed) { HideConnectionDraft(); SetFeedback("Check the connection for this account.", "Ready to test"); }
        else if (string.IsNullOrEmpty(_feedback.Text)) SetFeedback("Enter your User ID, public client identifier, and API token.", "Connect your account");
        RefreshStatus();
    }

    private async Task PasteTokenAsync()
    {
        if (_pastingToken || _token.IsReadOnly || !_token.IsEnabled) return;
        _pastingToken = true;
        var owner = TopLevel.GetTopLevel(this);
        string? previous = _token.Text, account = _account.Text, client = _client.Text;
        int start = _token.SelectionStart, end = _token.SelectionEnd;
        try
        {
            // Single-line TextBox normally drops everything after the first line break,
            // including a token copied with a leading blank line. Normalize before insertion.
            string? text = (await _readTokenClipboard().WaitAsync(TimeSpan.FromSeconds(3)))?.Trim();
            if (owner != TopLevel.GetTopLevel(this) || !IsVisible || previous != _token.Text || account != _account.Text || client != _client.Text) return;
            if (string.IsNullOrEmpty(text)) { SetFeedback("The clipboard contains no text API token. Copy the token again, then paste.", "Paste needs attention", true); return; }
            if (text.Any(char.IsControl)) { SetFeedback("Copy just the API token, without other lines of text.", "Paste needs attention", true); return; }
            _token.SelectionStart = start; _token.SelectionEnd = end;
            _token.SelectedText = text;
            SetFeedback("API token pasted. Choose Save connection to store it.", "Unsaved changes");
        }
        catch
        {
            // Do not include clipboard contents or exception messages in feedback or logs.
            if (owner == TopLevel.GetTopLevel(this) && IsVisible)
                SetFeedback("Could not read the clipboard. Copy the API token again and retry Paste.", "Paste needs attention", true);
        }
        finally { _pastingToken = false; }
    }

    private void SaveConnection()
    {
        if (_settings == null) return;
        string account = _account.Text?.Trim() ?? "", client = _client.Text?.Trim() ?? "";
        string? error = HabiticaApiClient.ConfigurationError(account, client);
        if (error != null) throw new InvalidOperationException(error);
        string? token = _token.Text?.Trim(); bool replacing = !string.IsNullOrWhiteSpace(token);
        // Write the replacement before committing the account, so storage failures leave the old connection intact.
        if (replacing) _integration.SetToken(account, token!);
        _savingConnection = true;
        try
        {
            if (_settings.HabiticaUserId != account || _settings.HabiticaClientId != client)
            { _testCancellation?.Cancel(); _integration.CancelPendingRequests(); _integration.Disable(_settings); }
            _settings.HabiticaUserId = account; _settings.HabiticaClientId = client;
            if (replacing)
            {
                _credentialAccount = account.ToLowerInvariant(); _credentialMissing = false; _credentialStatus = _integration.CredentialStatus;
                _token.Text = "";
            }
            SetTokenVisible(false); _lastDraft = Draft();
            _integration.Configure(_settings); _save?.Invoke();
            SetFeedback(replacing ? "Connection settings saved. " + _integration.CredentialStatus : "Connection settings saved. Any previously stored token was kept.", "Connection saved");
        }
        finally { _savingConnection = false; }
    }

    private async Task TestAsync()
    {
        if (_settings == null || _testing || !_test.IsEnabled) return;
        _testing = true;
        CancellationTokenSource? cancellation = null;
        string scope = "";
        (string Account, string Client, string Token) draft = default;
        bool Current() => cancellation != null && !cancellation.IsCancellationRequested && IsEffectivelyVisible &&
            _testCancellation == cancellation && _settings != null && ImportScope(_settings) == scope && Draft() == draft;
        try
        {
            SaveConnection();
            cancellation = new CancellationTokenSource(); _testCancellation = cancellation;
            scope = ImportScope(_settings); draft = Draft();
            var snapshot = new AppSettings { HabiticaUserId = _settings.HabiticaUserId, HabiticaClientId = _settings.HabiticaClientId,
                HabiticaEnabled = _settings.HabiticaEnabled, ObsidianVaultFolder = _settings.ObsidianVaultFolder, NotesSubfolder = _settings.NotesSubfolder };
            SetFeedback("Checking your Habitica account…", "Testing connection"); UpdateConnectionActions();
            var result = await _integration.TestConnectionAsync(snapshot, cancellation.Token);
            if (!Current()) return;
            if (result.Error is not (HabiticaErrorKind.Conflict or HabiticaErrorKind.RateLimited or HabiticaErrorKind.Storage))
            {
                string account = snapshot.HabiticaUserId.ToLowerInvariant();
                // Cached secret reads do not identify their protection; retain only this account's save evidence.
                if (_credentialAccount != account) _credentialStatus = "";
                _credentialAccount = account; _credentialMissing = result.Error == HabiticaErrorKind.Configuration;
            }
            SetFeedback(ConnectionTestMessage(result), result.Success ? "Last test succeeded" : result.Error == HabiticaErrorKind.RateLimited ? "Waiting to retry" : "Connection needs attention", !result.Success);
        }
        catch (OperationCanceledException) { }
        catch (InvalidOperationException ex) { if (cancellation == null || Current()) SetFeedback(ex.Message, "Check connection details", true); }
        catch { if (cancellation == null || Current()) SetFeedback("Connection test failed. Check configuration and network connectivity.", "Connection needs attention", true); }
        finally
        {
            if (_testCancellation == cancellation) _testCancellation = null;
            cancellation?.Dispose(); _testing = false;
            if (IsEffectivelyVisible) RefreshStatus();
        }
    }

    private string ConnectionTestMessage(HabiticaApiResult<string> result)
    {
        if (result.Success) return "Connection succeeded.";
        string status = result.HttpStatus is int code ? $" (HTTP {code})" : "";
        return result.Error switch
        {
            HabiticaErrorKind.Configuration => HabiticaApiClient.ConfigurationError(_settings?.HabiticaUserId, _settings?.HabiticaClientId)
                ?? "No API token is available for this User ID. Paste the token and choose Save connection. A session-only token must be entered again after restarting.",
            HabiticaErrorKind.Authentication => "Habitica rejected the User ID or API token" + status + ". Check that both belong to the same account.",
            HabiticaErrorKind.Network => "Could not reach Habitica. Check internet access, DNS, proxy or VPN settings. Your saved token was kept.",
            HabiticaErrorKind.Server => "Habitica returned a server error" + status + ". Try again later; your saved token was kept.",
            HabiticaErrorKind.InvalidResponse => "Habitica returned an unexpected response" + status + ". A proxy or network filter may have interrupted the request. Your saved token was kept.",
            HabiticaErrorKind.Storage => "The notes folder or Habitica sync state could not be read or saved. Check the folder and its permissions.",
            HabiticaErrorKind.RateLimited => "Please wait for the saved request delay before testing again (at least 30 seconds between requests; longer after a Habitica rate limit).",
            HabiticaErrorKind.Conflict => "A sync request is running. Wait for it to finish before testing again.",
            _ => "Habitica rejected the connection request" + status + ". Check the public client identifier and connection settings."
        };
    }

    private void RefreshStatus()
    {
        if (_settings == null) return;
        try
        {
            var status = _integration.GetStatus(_settings);
            _status.Text = $"{status.Message}\nPending changes: {status.PendingCount}\nLast successful sync: {(status.LastSuccessUtc?.ToLocalTime().ToString("g") ?? "Never")}";
            _credentials.Text = _integration.CredentialStatus;
            UpdateImportProgress(status);
            _loading = true;
            _enabled.IsChecked = _settings.HabiticaEnabled && status.IsOwner;
            UpdateNavigatorStatus(status);
        }
        catch { _status.Text = "Sync status unavailable. Check the vault location and access permissions."; }
        finally { _loading = false; }
    }

    private static string ImportScope(AppSettings settings) => $"{settings.HabiticaUserId}|{settings.HabiticaClientId}|{settings.ObsidianVaultFolder}|{settings.NotesSubfolder}";

    private async Task QueueExistingAsync()
    {
        if (_settings == null || _queueing) return;
        var source = _settings;
        var snapshot = new AppSettings { HabiticaEnabled = source.HabiticaEnabled,
            HabiticaUserId = source.HabiticaUserId, HabiticaClientId = source.HabiticaClientId,
            ObsidianVaultFolder = source.ObsidianVaultFolder, NotesSubfolder = source.NotesSubfolder };
        _importScope = ImportScope(snapshot);
        _importError = null;
        _queueing = true;
        _syncExisting.IsEnabled = false;
        UpdateImportProgress(_integration.GetStatus(snapshot));
        Reveal(_importProgress);
        try
        {
            await Task.Run(() => _integration.ImportExisting(snapshot));
            if (_settings != null && ImportScope(_settings) == _importScope)
                SetFeedback("Existing items queued. Progress counts items confirmed by Habitica.");
        }
        catch (InvalidOperationException ex)
        {
            if (_settings != null && ImportScope(_settings) == _importScope) _importError = ex.Message;
        }
        catch
        {
            if (_settings != null && ImportScope(_settings) == _importScope)
                _importError = "Could not queue all items. Check the notes folder and try again; queued items are kept.";
        }
        finally { _queueing = false; _syncExisting.IsEnabled = true; RefreshStatus(); }
    }

    private void UpdateImportProgress(HabiticaSyncStatus status)
    {
        bool sameScope = _settings != null && ImportScope(_settings) == _importScope;
        bool preparing = (_queueing && sameScope) || status.ImportProgress?.IsQueuing == true;
        string? error = sameScope ? _importError : null;
        var progress = status.ImportProgress;
        _importProgress.IsVisible = preparing || progress != null || error != null;
        if (!_importProgress.IsVisible) return;
        _importProgressBar.IsVisible = preparing || progress is { Total: > 0 };
        if (!_queueing) _syncExisting.IsEnabled = progress?.IsQueuing != true;
        _importProgressBar.IsIndeterminate = preparing;
        _importProgressBar.Maximum = Math.Max(1, progress?.Total ?? 1);
        _importProgressBar.Value = progress?.Completed ?? 0;
        _importProgressText.Text = preparing ? "Preparing existing items…" : progress == null ? "Could not start existing-item sync"
            : progress.Total == 0 ? "No existing todos or reminders to sync" : $"{progress.Completed} / {progress.Total} items synced";
        if (preparing) _importProgressDetail.Text = "Queuing your local todos and reminders. You can keep using Stopwatch.";
        else if (error != null || progress?.Error != null) _importProgressDetail.Text = error ?? progress!.Error;
        else if (progress?.Total == 0) _importProgressDetail.Text = "New local tasks will sync automatically while Habitica sync is enabled.";
        else if (_settings?.HabiticaEnabled != true) _importProgressDetail.Text = "Paused — enable Habitica sync on this computer to continue. Queued items are kept.";
        else if (!status.IsOwner) _importProgressDetail.Text = "Sync is waiting for this vault to be enabled on this computer.";
        else if (progress != null && progress.Completed >= progress.Total) _importProgressDetail.Text = "Sync complete. All items in this batch were confirmed by Habitica.";
        else
        {
            string waiting = progress == null ? "" : $"{progress.Queued} waiting" + (progress.Blocked > 0 ? $" · {progress.Blocked} need attention. {status.Message}" : "");
            if (progress?.Queued > 0 && status.IsRequestRunning) _importProgressDetail.Text = waiting + " · Syncing with Habitica…";
            else if (progress?.Queued > 0 && status.NextRequestUtc is DateTime next && next > DateTime.UtcNow)
                _importProgressDetail.Text = waiting + $" · Next request in {Math.Max(1, (int)Math.Ceiling((next.ToUniversalTime() - DateTime.UtcNow).TotalSeconds))}s. Habitica requests are spaced apart.";
            else _importProgressDetail.Text = waiting + (progress?.Queued == 0 && progress.Blocked > 0 ? "" : " · Waiting for the next sync check.");
        }
        // Connection failures (for example a session-only token lost on restart) may
        // pause the whole worker without blocking an individual task target.
        if (!preparing && error == null && progress?.Error == null && _settings?.HabiticaEnabled == true &&
            status.IsOwner && progress is { Total: > 0 } && progress.Completed < progress.Total &&
            progress.Blocked == 0 && !string.IsNullOrWhiteSpace(status.Message))
            _importProgressDetail.Text += "\n" + status.Message;
        Avalonia.Automation.AutomationProperties.SetName(_importProgressBar, _importProgressText.Text);
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (InvalidOperationException ex) { SetFeedback(ex.Message, "Action needs attention", true); Reveal(_feedback); }
        catch { SetFeedback("Unable to save Habitica changes. Check configuration and storage access.", "Action needs attention", true); Reveal(_feedback); }
        RefreshStatus();
    }

    private void Confirm(string message, Action action, Button source, string caption)
    {
        string Scope() => _settings == null ? "" : $"{_settings.HabiticaUserId}|{_settings.HabiticaClientId}|{_settings.ObsidianVaultFolder}|{_settings.NotesSubfolder}|{_settings.HabiticaEnabled}";
        string scope = Scope();
        ClearConfirmation();
        _confirmationSource = source; _confirmButton.Content = caption;
        _confirmation.Text = message;
        _confirmed = () =>
        {
            if (scope != Scope()) throw new InvalidOperationException("The Habitica account or notes folder changed. Review the action again before confirming.");
            action();
        };
        _confirmPanel.IsVisible = true;
        PositionRecoveryConfirmation(source);
        Reveal(_confirmPanel);
        Dispatcher.UIThread.Post(() => { if (_confirmPanel.IsEffectivelyVisible) _cancelConfirmation.Focus(); }, DispatcherPriority.Background);
    }

    private void Reveal(Control control) => Dispatcher.UIThread.Post(() =>
    {
        if (control == _feedback && _recoveryRoot?.IsVisible == true && _recoveryFeedback != null) control = _recoveryFeedback;
        if (!IsEffectivelyVisible || !control.IsVisible) return;
        control.UpdateLayout();
        control.BringIntoView();
    }, DispatcherPriority.Background);

    private void ClearConfirmation()
    {
        _confirmed = null; _confirmPanel.IsVisible = false;
        if (_confirmationSource != null) _confirmationSource.IsVisible = true;
        _confirmationSource = null;
        if (_recoveryConfirmationBox != null) _recoveryConfirmationBox.IsVisible = false;
    }
    private static TextBlock Label(string text)
    {
        var label = new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap };
        label.Classes.Add("subheading");
        return label;
    }
    private static void AddField(StackPanel panel, string title, Control field)
    {
        var label = Label(title);
        label.Classes.Clear();
        label.Classes.Add("sectionHeader");
        panel.Children.Add(label);
        panel.Children.Add(field);
    }
    private static Button ActionButton(string title, Action action)
    {
        var button = new Button { Content = title };
        button.Classes.Add("modern");
        button.Click += (_, _) => action();
        return button;
    }
}
