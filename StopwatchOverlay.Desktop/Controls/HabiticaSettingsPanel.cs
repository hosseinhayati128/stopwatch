using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using StopwatchOverlay.Habitica;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>Connection drafts and secrets stay separate from serialized settings.</summary>
public sealed class HabiticaSettingsPanel : Border
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
    private readonly StackPanel _confirmPanel = new() { Spacing = 8, IsVisible = false };
    private readonly DispatcherTimer _refresh = new() { Interval = TimeSpan.FromSeconds(2) };
    private readonly Button _test;
    private AppSettings? _settings;
    private Action? _save;
    private Action? _confirmed;
    private CancellationTokenSource? _testCancellation;
    private bool _loading;
    private bool _pastingToken;
    private readonly Func<Task<string?>> _readTokenClipboard;

    public HabiticaSettingsPanel() : this(null) { }

    public HabiticaSettingsPanel(Func<Task<string?>>? readTokenClipboard)
    {
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
        var pasteButton = ActionButton("Paste", () => _token.Paste());
        pasteButton.Name = "PasteHabiticaTokenButton";
        pasteButton.Focusable = false; // preserve the token field's selection when replacing text
        Avalonia.Automation.AutomationProperties.SetName(pasteButton, "Paste Habitica API token");
        _token.InnerRightContent = pasteButton;
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
        content.Children.Add(ActionButton("Save connection", () => Run(SaveConnection)));
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
                    if (!HabiticaIntegration.Current.EnableHere(_settings))
                        throw new InvalidOperationException("This vault contains Habitica links owned by another installation. Separate local vaults can sync together. If you copied this linked vault, use the advanced move option after stopping its old copy.");
                }
                else HabiticaIntegration.Current.Disable(_settings);
                _save?.Invoke();
            });
            RefreshStatus();
        };
        _test = ActionButton("Test connection", async () => await TestAsync());
        content.Children.Add(_test);
        content.Children.Add(ActionButton("Sync existing items…", () => Run(() =>
        {
            if (_settings == null) return;
            SaveConnection();
            int count = HabiticaIntegration.Current.CountExisting(_settings);
            Confirm($"Queue {count} existing local todos and reminders for this account? Existing Habitica tasks are not imported or matched by title.", () =>
            {
                HabiticaIntegration.Current.ImportExisting(_settings);
                _feedback.Text = "Existing items queued. Delivery follows Habitica's request limits.";
            });
        })));
        var copiedVaultOptions = new Expander { Name = "HabiticaCopiedVaultOptions", Header = "Copied or shared vault (advanced)", IsExpanded = false };
        var copiedVaultContent = new StackPanel { Spacing = 8 };
        copiedVaultContent.Children.Add(Label("Use this only when moving an existing linked vault to another computer. Independent local vaults do not need a transfer. Do not run two writers against the same copied/shared Habitica metadata."));
        copiedVaultContent.Children.Add(ActionButton("Move this linked vault here…", () => Run(() =>
        {
            SaveConnection();
            Confirm("For this copied/shared vault only: disable sync or close its old Stopwatch installation and finish copying the vault before moving these links here. Other computers with separate local vaults may keep syncing.", () =>
            {
                if (_settings == null) return;
                if (!HabiticaIntegration.Current.EnableHere(_settings, takeOver: true))
                    throw new InvalidOperationException("Unable to move this linked vault to this installation.");
                _save?.Invoke();
                _feedback.Text = "This linked vault now syncs from this installation. Separate local vaults are unaffected.";
            });
        })));
        copiedVaultOptions.Content = copiedVaultContent;
        content.Children.Add(copiedVaultOptions);
        content.Children.Add(ActionButton("Recreate missing linked tasks…", () => Run(() =>
        {
            if (_settings == null) return;
            int count = HabiticaIntegration.Current.CountMissing(_settings);
            if (count == 0) { _feedback.Text = "No missing linked tasks need recreation."; return; }
            Confirm($"Recreate {count} previously linked tasks that Habitica reported missing? Their current local text and checklist will be used. Review Habitica first to avoid recreating deliberately deleted tasks.", () =>
            {
                HabiticaIntegration.Current.RecreateMissing(_settings);
                _feedback.Text = "Missing tasks queued for explicit recreation.";
            });
        })));
        _confirmPanel.Children.Add(_confirmation);
        _confirmPanel.Children.Add(ActionButton("Confirm", () => Run(() =>
        {
            var action = _confirmed;
            ClearConfirmation();
            action?.Invoke();
        })));
        _confirmPanel.Children.Add(ActionButton("Cancel", ClearConfirmation));
        content.Children.Add(_confirmPanel);
        content.Children.Add(_feedback);
        content.Children.Add(_status);
        Child = content;
        _account.TextChanged += (_, _) => ClearConfirmation();
        _client.TextChanged += (_, _) => ClearConfirmation();
        _token.TextChanged += (_, _) => ClearConfirmation();
        _refresh.Tick += (_, _) => RefreshStatus();
        AttachedToVisualTree += (_, _) => { _refresh.Start(); RefreshStatus(); };
        DetachedFromVisualTree += (_, _) =>
        {
            _refresh.Stop();
            _testCancellation?.Cancel();
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
        _account.Text = _settings.HabiticaUserId;
        _client.Text = _settings.HabiticaClientId;
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
            if (string.IsNullOrEmpty(text)) { _feedback.Text = "The clipboard contains no text API token. Copy the token again, then paste."; return; }
            if (text.Any(char.IsControl)) { _feedback.Text = "Copy just the API token, without other lines of text."; return; }
            _token.SelectionStart = start; _token.SelectionEnd = end;
            _token.SelectedText = text;
            _feedback.Text = "API token pasted. Choose Save connection to store it.";
        }
        catch
        {
            // Do not include clipboard contents or exception messages in feedback or logs.
            if (owner == TopLevel.GetTopLevel(this) && IsVisible)
                _feedback.Text = "Could not read the clipboard. Copy the API token again and retry Paste.";
        }
        finally { _pastingToken = false; }
    }

    private void SaveConnection()
    {
        if (_settings == null) return;
        string account = _account.Text?.Trim() ?? "";
        string client = _client.Text?.Trim() ?? "";
        string? configurationError = HabiticaApiClient.ConfigurationError(account, client);
        if (configurationError != null) throw new InvalidOperationException(configurationError);
        var integration = HabiticaIntegration.Current;
        if (_settings.HabiticaUserId != account || _settings.HabiticaClientId != client)
        {
            _testCancellation?.Cancel();
            integration.CancelPendingRequests();
            integration.Disable(_settings);
        }
        _settings.HabiticaUserId = account;
        _settings.HabiticaClientId = client;
        string? tokenDraft = _token.Text;
        bool replacingToken = !string.IsNullOrWhiteSpace(tokenDraft);
        if (replacingToken)
        {
            if (!Guid.TryParse(account, out _))
                throw new InvalidOperationException("Enter a valid Habitica User ID before saving a token.");
            integration.SetToken(account, tokenDraft!.Trim());
            _token.Text = "";
            _token.Watermark = integration.CredentialStatus.Contains("session-only", StringComparison.OrdinalIgnoreCase)
                ? "Token kept for this session — blank keeps it"
                : "Token saved securely — blank keeps it";
        }
        integration.Configure(_settings);
        _save?.Invoke();
        _feedback.Text = replacingToken ? "Connection settings saved. " + integration.CredentialStatus
            : "Connection settings saved. Any previously stored token was kept.";
    }

    private async Task TestAsync()
    {
        if (_settings == null) return;
        _test.IsEnabled = false;
        try
        {
            SaveConnection();
            _testCancellation?.Cancel();
            using var cancellation = new CancellationTokenSource();
            _testCancellation = cancellation;
            _feedback.Text = "Testing connection…";
            var result = await HabiticaIntegration.Current.TestConnectionAsync(_settings, cancellation.Token);
            if (!cancellation.IsCancellationRequested)
                _feedback.Text = ConnectionTestMessage(result);
        }
        catch (OperationCanceledException) { _feedback.Text = "Connection test canceled."; }
        catch (InvalidOperationException ex) { _feedback.Text = ex.Message; }
        catch { _feedback.Text = "Connection test failed. Check configuration and network connectivity."; }
        finally { _testCancellation = null; _test.IsEnabled = true; RefreshStatus(); }
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
            var status = HabiticaIntegration.Current.GetStatus(_settings);
            _status.Text = $"{status.Message}\nPending changes: {status.PendingCount}\nLast successful sync: {(status.LastSuccessUtc?.ToLocalTime().ToString("g") ?? "Never")}";
            _credentials.Text = HabiticaIntegration.Current.CredentialStatus;
            _loading = true;
            _enabled.IsChecked = _settings.HabiticaEnabled && status.IsOwner;
        }
        catch { _status.Text = "Sync status unavailable. Check the vault location and access permissions."; }
        finally { _loading = false; }
    }

    private void Run(Action action)
    {
        try { action(); }
        catch (InvalidOperationException ex) { _feedback.Text = ex.Message; }
        catch { _feedback.Text = "Unable to save Habitica changes. Check configuration and storage access."; }
        RefreshStatus();
    }

    private void Confirm(string message, Action action)
    {
        string Scope() => _settings == null ? "" : $"{_settings.HabiticaUserId}|{_settings.HabiticaClientId}|{_settings.ObsidianVaultFolder}|{_settings.NotesSubfolder}|{_settings.HabiticaEnabled}";
        string scope = Scope();
        _confirmation.Text = message;
        _confirmed = () =>
        {
            if (scope != Scope()) throw new InvalidOperationException("The Habitica account or notes folder changed. Review the action again before confirming.");
            action();
        };
        _confirmPanel.IsVisible = true;
    }

    private void ClearConfirmation() { _confirmed = null; _confirmPanel.IsVisible = false; }
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
