using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using StopwatchOverlay.Internet;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private readonly Func<IReadOnlyList<InternetCheckResult>> _internetReadHistory = InternetHistoryStore.GetAll;
    private readonly Func<AppSettings, InternetSyncResult> _internetSyncReport = settings => InternetLogSync.SyncLog(settings);
    private readonly Func<AppSettings, InternetClearResult> _internetClearHistory = InternetLogSync.ClearHistory;
    private IReadOnlyList<InternetCheckResult>? _internetHistory;
    private string? _internetHistoryError;
    private bool _internetExportBusy;
    private int _internetExportGeneration, _internetHistoryGeneration;
    private AppSettings? _internetClearRequest;
    private (string Folder, string File, int Days)? _internetExportOptions;
    private string _internetExportTitle = "Not synced in this window", _internetExportMessage = "Sync saved checks to the selected report.",
        _internetExportTone = "warning", _internetExportTime = "";
    private bool InternetHistoryVisible => CurrentCategory == "Internet" && (_internetPage == null || _internetShowingHistory);

    internal SettingsWindow(AppSettings settings, Func<IReadOnlyList<InternetCheckResult>> read,
        Func<AppSettings, InternetSyncResult> sync, Func<AppSettings, InternetClearResult> clear) : this(settings)
    { _internetReadHistory = read; _internetSyncReport = sync; _internetClearHistory = clear; }

    private AppSettings InternetExportSnapshot() => new()
    {
        ObsidianVaultFolder = _settings.ObsidianVaultFolder,
        InternetLogFileName = InternetFileNameTextBox.Text?.Trim() ?? "",
        InternetLogRetentionDays = _settings.InternetLogRetentionDays
    };

    private void CommitInternetFile()
    {
        if (_loading || _committing || _closed || _internetExportBusy) return;
        string raw = InternetFileNameTextBox.Text?.Trim() ?? "";
        if (InternetLogSync.TryNormalizeFileName(raw, out _) && raw != _settings.InternetLogFileName)
        { _settings.InternetLogFileName = raw; CommitControls(SettingsChangeKind.InternetMonitor); }
        RefreshInternetExport();
    }

    private async Task LoadInternetHistoryAsync()
    {
        if (_closed || _loading || !InternetHistoryVisible || _internetExportBusy) return;
        int version = ++_internetHistoryGeneration;
        try
        {
            var history = await Task.Run(_internetReadHistory);
            if (_closed || version != _internetHistoryGeneration || !InternetHistoryVisible) return;
            _internetHistory = history; _internetHistoryError = null;
        }
        catch (Exception ex) { if (_closed || version != _internetHistoryGeneration) return; _internetHistoryError = ex.Message; }
        RefreshInternetExport();
    }

    private void RefreshInternetExport()
    {
        if (_closed) return;
        var snapshot = InternetExportSnapshot();
        var options = (snapshot.ObsidianVaultFolder?.Trim() ?? "", snapshot.InternetLogFileName, snapshot.InternetLogRetentionDays);
        if (_internetExportOptions != options)
        {
            _internetExportOptions = options; _internetClearRequest = null; _internetExportGeneration++;
            SetInternetExportState(_internetExportBusy ? "Operation in progress" : options.Item1.Length == 0 ? "Choose an export folder" : "Not synced in this window",
                _internetExportBusy ? "The started operation is finishing for its original destination." : options.Item1.Length == 0 ? "Set your vault folder in Obsidian / Markdown settings." : "Sync saved checks to the selected report.", "warning");
        }
        bool fileValid = InternetLogSync.TryNormalizeFileName(snapshot.InternetLogFileName, out _);
        bool targetValid = InternetLogSync.TryGetTargetPath(snapshot, out _, out string targetError);
        bool confirming = _internetClearRequest != null, idle = !_internetExportBusy && !confirming;
        SyncInternetLogButton.IsEnabled = idle && targetValid;
        ClearInternetLogButton.IsEnabled = idle && fileValid && (options.Item1.Length == 0 || targetValid) && _internetHistoryError == null && _internetHistory?.Count > 0;
        ClearInternetLogButton.IsVisible = !confirming;
        InternetFileNameTextBox.IsEnabled = InternetRetentionCombo.IsEnabled = idle;
        if (_internetClearBox != null)
        {
            _internetClearBox.IsVisible = confirming;
            if (confirming)
            {
                InternetLogSync.TryGetTargetPath(_internetClearRequest!, out string target, out _);
                _internetClearDetail!.Text = string.IsNullOrWhiteSpace(_internetClearRequest!.ObsidianVaultFolder)
                    ? $"Remove {_internetHistory?.Count ?? 0} saved checks locally. No export folder is selected, so existing Markdown copies cannot be updated."
                    : $"Remove {_internetHistory?.Count ?? 0} saved checks locally and empty “{target}”. Other files stay unchanged.";
            }
        }
        if (_internetExportCards == null) return;
        SyncInternetLogButton.Content = _internetExportBusy ? "Working…" : "Sync now";
        _internetFolderButton!.IsEnabled = idle; _internetFolderButton.Content = options.Item1.Length == 0 ? "Choose folder" : "Folder settings";
        _internetFolder!.Text = options.Item1.Length == 0 ? "No export folder selected" : options.Item1;
        _internetFolder.Foreground = options.Item1.Length == 0 ? Brush.Parse("#865C15") : SettingsInk;
        _internetFolder.FlowDirection = options.Item1.Any(c => c is >= '\u0590' and <= '\u08ff') ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        _internetFileHint!.Text = !fileValid ? "Use a file name without folders or reserved characters." : !targetValid && options.Item1.Length > 0 ? targetError : "Blank uses Internet Log.md; .md is added if needed.";
        _internetFileHint.Foreground = fileValid && (targetValid || options.Item1.Length == 0) ? SettingsMuted : Brush.Parse("#913F32");
        _internetRetentionHint!.Text = snapshot.InternetLogRetentionDays == 0 ? "No automatic removal. You can clear history below." : "Older checks are removed when the report is synced. This also changes local history.";
        int count = _internetHistory?.Count ?? 0;
        _internetHistoryCount!.Text = _internetHistoryError != null ? "History unavailable" : _internetHistory == null ? "Loading history…" : count == 0 ? "No saved checks" : $"{count} saved check{(count == 1 ? "" : "s")}";
        _internetHistoryDates!.Text = _internetHistoryError ?? (_internetHistory == null ? "Reading local history." : count == 0 ? "New automatic checks will appear here."
            : $"{_internetHistory.Min(c => c.Timestamp):MMM d}–{_internetHistory.Max(c => c.Timestamp):MMM d} · latest {_internetHistory.Max(c => c.Timestamp):MMM d, HH:mm}");
        PaintInternetExportState();
    }

    private void SetInternetExportState(string title, string message, string tone = "normal", string stamp = "")
    {
        if (_closed) return;
        _internetExportTitle = title; _internetExportMessage = message; _internetExportTone = tone; _internetExportTime = stamp;
        InternetStatusText.Text = title + " · " + message;
        InternetStatusText.Foreground = tone == "error" ? Brushes.OrangeRed : tone == "warning" ? Brushes.Goldenrod : Brushes.ForestGreen;
        PaintInternetExportState();
    }

    private void PaintInternetExportState()
    {
        if (_internetExportCards == null || _closed) return;
        string color = _internetExportTone == "error" ? "913F32" : _internetExportTone == "warning" ? "865C15" : "2D7169";
        _internetExportHeading!.Text = _internetExportTitle; _internetExportHeading.Foreground = Brush.Parse("#" + color);
        _internetExportDetail!.Text = _internetExportMessage; _internetExportStamp!.Text = _internetExportTime; _internetExportStamp.IsVisible = _internetExportTime.Length > 0;
        _internetExportBox!.Background = Brush.Parse("#11" + color); _internetExportBox.BorderBrush = Brush.Parse("#55" + color);
        _internetExportProgress!.IsVisible = _internetExportBusy;
    }

    private void DismissInternetExport()
    {
        _internetClearRequest = null; _internetExportGeneration++; _internetHistoryGeneration++;
        if (_internetExportBusy) SetInternetExportState("Operation in progress", "The started operation is finishing for its original destination.", "warning");
        RefreshInternetExport();
    }

    private void BeginInternetClear()
    {
        if (_closed || _internetExportBusy || !InternetHistoryVisible) return;
        CommitInternetFile(); RefreshInternetExport(); if (!ClearInternetLogButton.IsEnabled) return;
        _internetClearRequest = InternetExportSnapshot();
        if (_internetExportCards == null)
        { CreateInternetClearConfirmation(); InternetExportPanel.Children.Add(_internetClearBox!); }
        RefreshInternetExport(); _internetClearBox!.BringIntoView(); _internetClearCancel!.Focus();
    }

    private async Task RunInternetExportAsync(bool clear)
    {
        if (_closed || _internetExportBusy || !InternetHistoryVisible) return;
        AppSettings snapshot;
        if (clear)
        {
            if (_internetClearRequest == null) return;
            snapshot = _internetClearRequest;
        }
        else
        {
            CommitInternetFile(); RefreshInternetExport(); if (!SyncInternetLogButton.IsEnabled) return;
            snapshot = InternetExportSnapshot();
        }
        _internetClearRequest = null; _internetExportBusy = true; _internetHistoryGeneration++;
        int generation = _internetExportGeneration;
        bool Current() => !_closed && generation == _internetExportGeneration && InternetHistoryVisible;
        SetInternetExportState(clear ? "Clearing history…" : "Syncing saved checks…", clear ? "Emptying the selected report and local history." : "Updating the Markdown report.");
        RefreshInternetExport();
        try
        {
            string stamp = "";
            if (clear)
            {
                var result = await Task.Run(() => _internetClearHistory(snapshot));
                if (!Current()) return;
                if (result.Success) stamp = DateTime.Now.ToString("MMM d · HH:mm:ss");
                SetInternetExportState(result.Success ? "History cleared" : result.ReportCleared ? "Report emptied · local history kept" : "History was not cleared",
                    result.Message ?? "Check the destination and try clearing again.", result.Success ? "normal" : "error", stamp);
            }
            else
            {
                var result = await Task.Run(() => _internetSyncReport(snapshot));
                if (!Current()) return;
                bool empty = result.CheckCount == 0;
                SetInternetExportState(!result.Success ? "Couldn’t update the report" : empty ? "Report has no saved checks" : "Report updated",
                    result.Message ?? "Check the destination folder and try again.", !result.Success ? "error" : empty ? "warning" : "normal",
                    result.Success ? $"{DateTime.Now:MMM d · HH:mm:ss} · {Path.GetFileName(result.TargetFilePath)}" : "");
            }
        }
        catch (Exception ex) { if (Current()) SetInternetExportState("Operation failed", ex.Message, "error"); }
        finally
        {
            _internetExportBusy = false;
            if (!_closed)
            {
                if (!Current()) SetInternetExportState("Operation finished", "The earlier operation has finished. Its result belongs to the previous page or destination.", "warning");
                RefreshInternetExport(); await LoadInternetHistoryAsync();
            }
        }
    }
}
