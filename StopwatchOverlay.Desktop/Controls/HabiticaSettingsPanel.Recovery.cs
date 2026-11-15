using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.VisualTree;
using StopwatchOverlay.Habitica;
using StopwatchOverlay.Desktop.Views;

namespace StopwatchOverlay.Desktop.Controls;

public sealed partial class HabiticaSettingsPanel
{
    private StackPanel? _recoveryRoot, _recoveryBulkBody, _recoveryMissingBody, _recoveryTransferBody;
    private Grid? _recoveryCards;
    private Button? _recoveryTab, _connectionTab, _recoveryContext;
    private TextBlock? _recoveryOverview, _recoveryReady, _recoveryDetail, _recoveryMissing, _recoveryFeedback;
    private Border? _recoveryConfirmationBox, _recoveryFeedbackBox;
    private bool? _recoveryStacked;
    private bool _recoveryCounting;
    private int? _recoveryExistingCount;
    private int _recoveryScanVersion, _recoveryMissingCount;
    private string _recoveryScope = "", _recoveryCountError = "";

    private StackPanel BuildRecoveryPage()
    {
        foreach (var button in new[] { _syncExisting, _transferVault, _restoreMissing, _confirmButton, _cancelConfirmation })
        { NavigatorFormStyles.StyleButton(button, button == _syncExisting || button == _confirmButton); button.Classes.Add("habiticaAction"); button.MinHeight = 36; button.Padding = new(12, 7); }
        _syncExisting.Content = "Review existing items"; _restoreMissing.Content = "Review missing tasks"; _transferVault.Content = "Review vault transfer";
        _confirmation.Classes.Add("habiticaDetail"); _importProgressText.Classes.Add("habiticaDetail"); _importProgressDetail.Classes.Add("habiticaDetail");
        _importProgressBar.Height = 7; _importProgressBar.Foreground = Teal;
        _importProgressText.FontFamily = ClockPickerPresentation.Body; _importProgressText.FontSize = 17;
        _confirmation.FontSize = 13;
        _recoveryOverview = Text("", 13, Teal); _recoveryReady = Text("Checking local items…", 17, Ink);
        _recoveryDetail = Text("Todos and reminders in this vault can join your Habitica sync. Already linked items will be updated.", 13);
        _recoveryMissing = Text("", 14, Teal);
        _recoveryContext = ActionButton("Connection & status", () => SelectRecoveryTab(false));
        _recoveryContext.Name = "HabiticaRecoveryContext"; NavigatorFormStyles.StyleButton(_recoveryContext);
        _recoveryConfirmationBox = new Border { IsVisible = false, Padding = new(12), CornerRadius = new(5), BorderThickness = new(1),
            BorderBrush = Brush.Parse("#809A7B48"), Background = Brush.Parse("#50E8BE70"), Child = new StackPanel { Children = { _confirmPanel } } };
        var bulk = Card("Sync existing items", _recoveryReady, _recoveryDetail, _importProgress, _syncExisting, _recoveryContext,
            _recoveryConfirmationBox, Text("New local tasks sync automatically while Habitica sync is enabled.", 12));
        bulk.Name = "HabiticaRecoveryBulk"; _recoveryBulkBody = (StackPanel)bulk.Child!;
        var missing = Card("Restore missing tasks", _recoveryMissing,
            Text("Recreate linked tasks that Habitica reported missing, using their local text and checklist.", 13),
            Text("Nothing is recreated automatically.", 12), _restoreMissing);
        missing.Name = "HabiticaRecoveryRestore"; _recoveryMissingBody = (StackPanel)missing.Child!;
        var transfer = Card("Use a copied vault here", Text("Only for the same copied or shared vault", 14, Ink),
            Text("Transfer its Habitica sync ownership to this computer.", 13),
            Text("Separate Windows and Ubuntu vaults sync independently and do not need a transfer.", 12), _transferVault);
        transfer.Name = "HabiticaRecoveryTransfer"; _recoveryTransferBody = (StackPanel)transfer.Child!;
        _recoveryCards = new Grid { Children = { missing, transfer } };
        _recoveryFeedback = Text("", 13); _recoveryFeedback.Name = "HabiticaRecoveryFeedback";
        _recoveryFeedbackBox = new Border { IsVisible = false, Padding = new(12), CornerRadius = new(5), BorderThickness = new(1), Child = _recoveryFeedback };
        _recoveryRoot = new StackPanel { IsVisible = false, Spacing = 14, Children = {
            _recoveryOverview, _recoveryFeedbackBox, bulk, Text("RECOVERY TOOLS", 11), _recoveryCards } };
        return _recoveryRoot;
    }

    private WrapPanel BuildRecoveryTabs()
    {
        _connectionTab = ActionButton("Connection & status", () => SelectRecoveryTab(false)); _connectionTab.Name = "HabiticaConnectionTab";
        _recoveryTab = ActionButton("Sync & recovery", () => SelectRecoveryTab(true)); _recoveryTab.Name = "HabiticaRecoveryTab";
        NavigatorFormStyles.StyleButton(_connectionTab, true); NavigatorFormStyles.StyleButton(_recoveryTab);
        foreach (var tab in new[] { _connectionTab, _recoveryTab }) { tab.MinHeight = 36; tab.Padding = new(12, 7); tab.FontSize = 13; }
        _connectionTab.Margin = new(0, 0, 8, 4); _recoveryTab.Margin = new(0, 0, 0, 4);
        return new WrapPanel { Children = { _connectionTab, _recoveryTab } };
    }

    private void SelectRecoveryTab(bool recovery)
    {
        if (_recoveryRoot == null || _navigatorCards == null) return;
        HideConnectionDraft(); ClearConfirmation();
        _navigatorCards.IsVisible = !recovery; _recoveryRoot.IsVisible = recovery;
        _connectionTab!.Classes.Set("editorPrimary", !recovery); _recoveryTab!.Classes.Set("editorPrimary", recovery);
        if (_navigatorRoot?.Children[0] is StackPanel heading && heading.Children[1] is TextBlock subtitle)
            subtitle.Text = recovery ? "Bring existing tasks across and manage linked-task recovery." : "Connect your account and follow sync on this computer.";
        if (this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault() is { } scroll) scroll.Offset = default;
        if (recovery) _ = RefreshRecoveryInventoryAsync();
        RefreshStatus();
    }

    private async Task RefreshRecoveryInventoryAsync()
    {
        if (_settings == null || _recoveryRoot == null) return;
        int version = ++_recoveryScanVersion;
        var snapshot = new AppSettings { HabiticaUserId = _settings.HabiticaUserId, HabiticaClientId = _settings.HabiticaClientId,
            ObsidianVaultFolder = _settings.ObsidianVaultFolder, NotesSubfolder = _settings.NotesSubfolder };
        _recoveryScope = ImportScope(snapshot); _recoveryCounting = true; _recoveryExistingCount = null; _recoveryCountError = "";
        UpdateRecoveryActions();
        int? count = null; string error = "";
        try { count = await Task.Run(() => _integration.CountExisting(snapshot)); }
        catch { error = "Could not read local items. Check the vault and notes folder, then reopen Sync & recovery to retry."; }
        if (version != _recoveryScanVersion || _recoveryRoot == null) return;
        _recoveryCounting = false;
        if (_settings == null || ImportScope(_settings) != ImportScope(snapshot)) { RefreshStatus(); return; }
        _recoveryExistingCount = count; _recoveryCountError = error;
        RefreshStatus();
    }

    private bool RecoveryDraftSaved() => _settings != null &&
        _account.Text?.Trim() == _settings.HabiticaUserId && _client.Text?.Trim() == _settings.HabiticaClientId && string.IsNullOrWhiteSpace(_token.Text);

    private bool CanReviewExisting(HabiticaSyncStatus? status)
    {
        var batch = status?.ImportProgress;
        bool failed = _importError != null && _settings != null && ImportScope(_settings) == _importScope;
        return !_queueing && batch?.IsQueuing != true &&
            (batch == null || batch.Completed >= batch.Total || batch.Error != null || failed);
    }

    private void UpdateRecoveryActions()
    {
        if (_recoveryRoot == null) return;
        bool valid = _settings != null && HabiticaApiClient.ConfigurationError(_settings.HabiticaUserId, _settings.HabiticaClientId) == null;
        bool ready = valid && RecoveryDraftSaved() && !_testing && !_queueing && _lastSyncStatus?.ImportProgress?.IsQueuing != true;
        bool active = _settings?.HabiticaEnabled == true && _lastSyncStatus?.IsOwner == true;
        _syncExisting.IsEnabled = ready && active && !_recoveryCounting && _recoveryExistingCount > 0 && CanReviewExisting(_lastSyncStatus);
        _restoreMissing.IsEnabled = ready && active && _recoveryMissingCount > 0;
        _transferVault.IsEnabled = ready;
    }

    private void UpdateRecoveryStatus(HabiticaSyncStatus status)
    {
        if (_recoveryRoot == null || _settings == null) return;
        if (_recoveryScope != ImportScope(_settings))
        {
            ClearConfirmation(); _recoveryExistingCount = null;
            if (_recoveryRoot.IsVisible) _ = RefreshRecoveryInventoryAsync();
        }
        bool valid = HabiticaApiClient.ConfigurationError(_settings.HabiticaUserId, _settings.HabiticaClientId) == null;
        bool saved = RecoveryDraftSaved(); bool active = _settings.HabiticaEnabled && status.IsOwner;
        _recoveryOverview!.Text = !valid ? "Set up your connection before syncing this vault." : !saved ? "Unsaved connection changes · Save them in Connection & status first."
            : !_settings.HabiticaEnabled ? "Sync paused on this computer · Pending changes are saved"
            : !status.IsOwner ? "This vault is not enabled on this computer · Review connection or copied-vault transfer."
            : "Sync enabled on this computer · Your local vault";
        _recoveryOverview.Foreground = active && valid && saved ? Teal : Amber;
        _recoveryContext!.IsVisible = !active || !valid || !saved;
        _syncExisting.IsVisible = CanReviewExisting(status) && _confirmationSource != _syncExisting;
        _recoveryReady!.IsVisible = _recoveryDetail!.IsVisible = !_importProgress.IsVisible;
        _recoveryReady.Text = _recoveryCounting ? "Checking local items…" : _recoveryCountError.Length > 0 ? "Local items unavailable"
            : _recoveryExistingCount is > 0 ? $"{_recoveryExistingCount} local items available" : "No existing items to sync";
        _recoveryDetail.Text = _recoveryCountError.Length > 0 ? _recoveryCountError : _recoveryExistingCount == 0
            ? "Add a todo or reminder to your local vault. Standalone notes stay local."
            : "Todos and reminders in this vault can join your Habitica sync. Already linked items will be updated.";
        bool attention = status.ImportProgress is { Blocked: > 0 } || status.ImportProgress?.Error != null || _importError != null;
        _importProgressBar.Foreground = !active || attention ? Amber : Teal;
        _importProgressText.Foreground = attention ? Amber : Ink;
        try { _recoveryMissingCount = _integration.CountMissing(_settings); }
        catch { _recoveryMissingCount = 0; _recoveryMissing!.Text = "Missing-task status unavailable"; UpdateRecoveryActions(); return; }
        _recoveryMissing!.Text = _recoveryMissingCount == 0 ? "No missing linked tasks" : $"{_recoveryMissingCount} linked tasks need attention";
        _recoveryMissing.Foreground = _recoveryMissingCount == 0 ? Teal : Amber;
        UpdateRecoveryActions(); UpdateRecoveryFeedback();
    }

    private void UpdateRecoveryFeedback()
    {
        if (_recoveryFeedbackBox == null) return;
        _recoveryFeedbackBox.IsVisible = _connectionFailed || _connectionHeading == "Status";
        _recoveryFeedback!.Text = _feedback.Text; _recoveryFeedback.Foreground = _connectionFailed ? Error : Teal;
        _recoveryFeedbackBox.Background = Brush.Parse(_connectionFailed ? "#11913F32" : "#112D7169");
        _recoveryFeedbackBox.BorderBrush = Brush.Parse(_connectionFailed ? "#55913F32" : "#552D7169");
    }

    private void PositionRecoveryConfirmation(Button source)
    {
        if (_recoveryConfirmationBox == null) return;
        var body = source == _syncExisting ? _recoveryBulkBody : source == _restoreMissing ? _recoveryMissingBody : _recoveryTransferBody;
        ((Panel)_recoveryConfirmationBox.Parent!).Children.Remove(_recoveryConfirmationBox);
        body!.Children.Insert(body.Children.IndexOf(source) + 1, _recoveryConfirmationBox);
        _recoveryConfirmationBox.IsVisible = true; source.IsVisible = false;
    }

    private void ResizeRecoveryLayout(double width)
    {
        if (_recoveryCards == null || width <= 0) return;
        bool stacked = width < 760 * (_settings?.Typography.Resolve("Settings").SizePercent ?? 100) / 100;
        if (_recoveryStacked == stacked) return; _recoveryStacked = stacked;
        _recoveryCards.ColumnDefinitions = new(stacked ? "*" : "*,14,*"); _recoveryCards.RowDefinitions = new(stacked ? "Auto,14,Auto" : "Auto");
        Grid.SetColumn(_recoveryCards.Children[1], stacked ? 0 : 2); Grid.SetRow(_recoveryCards.Children[1], stacked ? 2 : 0);
    }

    private void ResetRecoveryLayout()
    {
        ++_recoveryScanVersion; _recoveryCounting = false; _recoveryScope = ""; _recoveryExistingCount = null;
        _recoveryRoot = _recoveryBulkBody = _recoveryMissingBody = _recoveryTransferBody = null;
        _recoveryCards = null; _recoveryStacked = null; _recoveryTab = _connectionTab = _recoveryContext = null;
        _recoveryOverview = _recoveryReady = _recoveryDetail = _recoveryMissing = _recoveryFeedback = null;
        _recoveryConfirmationBox = _recoveryFeedbackBox = null;
        _syncExisting.Content = "Sync existing items…"; _restoreMissing.Content = "Recreate missing linked tasks…"; _transferVault.Content = "Move this linked vault here…";
        _restoreMissing.IsEnabled = _transferVault.IsEnabled = true; _syncExisting.IsVisible = true;
        foreach (var text in new[] { _confirmation, _importProgressText, _importProgressDetail }) text.Classes.Remove("habiticaDetail");
        _importProgressText.ClearValue(TextBlock.ForegroundProperty); _importProgressBar.ClearValue(RangeBase.ForegroundProperty);
        _importProgressText.ClearValue(TextBlock.FontFamilyProperty); _importProgressText.ClearValue(TextBlock.FontSizeProperty); _confirmation.ClearValue(TextBlock.FontSizeProperty);
        _importProgressBar.Height = 5;
    }
}
