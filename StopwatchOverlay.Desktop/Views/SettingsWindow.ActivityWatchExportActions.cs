using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Media;
using StopwatchOverlay.ActivityWatch;

namespace StopwatchOverlay.Desktop.Views;

public partial class SettingsWindow
{
    private CancellationTokenSource? _activityWatchExportOperation;
    private (string Folder, string File, string Minimum, string Address, bool Titles, bool Web)? _activityWatchExportOptions;
    private string _activityWatchExportTitle = "Not synced in this window", _activityWatchExportMessage = "Use Sync today to update the report.",
        _activityWatchExportTone = "warning", _activityWatchExportTotals = "", _activityWatchExportStamp = "";

    private void RefreshActivityWatchExport()
    {
        if (_closed) return;
        var options = (_settings.ObsidianVaultFolder?.Trim() ?? "", ActivityWatchFileNameTextBox.Text?.Trim() ?? "",
            ActivityWatchMinDurationTextBox.Text?.Trim() ?? "", ActivityWatchServerUrlTextBox.Text?.Trim() ?? "",
            ActivityWatchIncludeTitlesCheck.IsChecked == true, ActivityWatchIncludeWebCheck.IsChecked == true);
        if (_activityWatchExportOptions != options)
        {
            _activityWatchExportOptions = options;
            CancelActivityWatchExport();
            SetActivityWatchExportState(options.Item1.Length == 0 ? "Choose an export folder" : "Not synced in this window",
                options.Item1.Length == 0 ? "Select a folder in Obsidian / Markdown settings, then return here." : "Use Sync today to update the report.", "warning");
        }
        bool busy = _activityWatchExportOperation != null;
        bool enabled = ActivityWatchEnabledCheck.IsChecked == true;
        bool fileValid = ActivityWatchSync.TryNormalizeExportFileName(options.Item2, out _);
        bool minValid = int.TryParse(options.Item3, out int seconds) && seconds is >= 1 and <= 3600;
        bool intervalValid = int.TryParse(ActivityWatchPeriodicSyncIntervalTextBox.Text, out int minutes) && minutes is >= 1 and <= 1440;
        bool addressValid = TryActivityWatchAddress(out _, out _);
        SyncActivityWatchNowButton.IsEnabled = options.Item1.Length > 0 && fileValid && minValid && addressValid && !busy;
        foreach (Control control in new Control[] { ActivityWatchFileNameTextBox, ActivityWatchMinDurationTextBox,
            ActivityWatchIncludeTitlesCheck, ActivityWatchIncludeWebCheck }) control.IsEnabled = !busy;
        ActivityWatchSyncOnStopwatchCheck.IsEnabled = ActivityWatchPeriodicSyncCheck.IsEnabled = enabled && !busy;
        ActivityWatchPeriodicSyncIntervalTextBox.IsEnabled = enabled && ActivityWatchPeriodicSyncCheck.IsChecked == true && !busy;
        if (_activityWatchExportCards == null) return;
        SyncActivityWatchNowButton.Content = busy ? "Syncing…" : "Sync today";
        _activityWatchChangeFolder!.IsEnabled = !busy;
        _activityWatchChangeFolder.Content = options.Item1.Length == 0 ? "Choose folder" : "Change folder";
        _activityWatchFolder!.Text = options.Item1.Length == 0 ? "No export folder selected" : options.Item1;
        ToolTip.SetTip(_activityWatchFolder, options.Item1);
        _activityWatchFolder.Foreground = options.Item1.Length == 0 ? Brush.Parse("#865C15") : SettingsInk;
        _activityWatchAutoState!.Text = enabled ? "Integration on · automatic sync preferences" : "Automatic sync paused · integration is off";
        _activityWatchAutoState.Foreground = Brush.Parse(enabled ? "#2D7169" : "#865C15");
        ExportHint(_activityWatchIntervalHint!, intervalValid, "1–1,440 minutes · while Stopwatch is open", "Enter a whole number from 1 to 1,440 minutes.");
        ExportHint(_activityWatchMinimumHint!, minValid, "Small totals are omitted; timeline filtering also applies.", "Enter a whole number from 1 to 3,600 seconds.");
        ExportHint(_activityWatchFileHint!, fileValid && addressValid, "Blank uses ActivityWatch Log.md; .md is added if needed.",
            !fileValid ? "Use a file name without folders or reserved characters." : "Check the server address in Connection & status.");
        PaintActivityWatchExportState();
    }

    private static void ExportHint(TextBlock label, bool valid, string normal, string invalid)
    { label.Text = valid ? normal : invalid; label.Foreground = valid ? SettingsMuted : Brush.Parse("#913F32"); }

    private void SetActivityWatchExportState(string title, string message, string tone = "normal", string totals = "", string stamp = "")
    {
        if (_closed) return;
        _activityWatchExportTitle = title; _activityWatchExportMessage = message; _activityWatchExportTone = tone;
        _activityWatchExportTotals = totals; _activityWatchExportStamp = stamp;
        ActivityWatchStatusText.Text = title + " · " + message;
        ActivityWatchStatusText.Foreground = tone == "error" ? Brushes.OrangeRed : tone == "warning" ? Brushes.Goldenrod : Brushes.ForestGreen;
        PaintActivityWatchExportState();
    }

    private void PaintActivityWatchExportState()
    {
        if (_closed || _activityWatchExportCards == null) return;
        string color = _activityWatchExportTone == "error" ? "913F32" : _activityWatchExportTone == "warning" ? "865C15" : "2D7169";
        _activityWatchExportHeading!.Text = _activityWatchExportTitle; _activityWatchExportHeading.Foreground = Brush.Parse("#" + color);
        _activityWatchExportDetail!.Text = _activityWatchExportMessage;
        _activityWatchExportCounts!.Text = _activityWatchExportTotals; _activityWatchExportCounts.IsVisible = _activityWatchExportTotals.Length > 0;
        _activityWatchExportTime!.Text = _activityWatchExportStamp; _activityWatchExportTime.IsVisible = _activityWatchExportStamp.Length > 0;
        _activityWatchExportBox!.Background = Brush.Parse("#11" + color); _activityWatchExportBox.BorderBrush = Brush.Parse("#55" + color);
        _activityWatchExportProgress!.IsVisible = _activityWatchExportOperation != null;
    }

    private void CancelActivityWatchExport()
    {
        var operation = _activityWatchExportOperation;
        if (operation == null) return;
        _activityWatchExportOperation = null; operation.Cancel();
        if (!_closed)
        {
            SetActivityWatchExportState("Sync canceled", "Return to Sync today when you’re ready.", "warning");
            RefreshActivityWatchExport();
        }
    }

    private async Task RunActivityWatchExportAsync()
    {
        if (_closed || _activityWatchExportOperation != null) return;
        RefreshActivityWatchExport();
        if (!SyncActivityWatchNowButton.IsEnabled) return;
        CommitControls(SettingsChangeKind.ActivityWatch);
        using var operation = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        _activityWatchExportOperation = operation;
        bool Current() => !_closed && ReferenceEquals(_activityWatchExportOperation, operation);
        SetActivityWatchExportState("Syncing today’s activity…", "Reading activity and writing the Markdown report.");
        RefreshActivityWatchExport();
        try
        {
            var result = await ActivityWatchSync.SyncAsync(_settings, ct: operation.Token, httpClient: _activityWatchHttpClient);
            if (!Current()) return;
            if (!result.Success) SetActivityWatchExportState("Couldn’t update the report", result.Message ?? "Check ActivityWatch and the destination folder, then try again.", "error");
            else
            {
                bool empty = result.AppCount == 0 && result.WebCount == 0;
                SetActivityWatchExportState(empty ? "Report contains no entries" : "Today’s report updated",
                    empty ? "No app or domain totals met these filters. If you expected activity, check ActivityWatch and its watchers." : "Saved today’s section to the selected file.",
                    empty ? "warning" : "normal", $"{result.AppCount} app{(result.AppCount == 1 ? "" : "s")} · {result.WebCount} web domain{(result.WebCount == 1 ? "" : "s")}",
                    $"Today · {DateTime.Now:HH:mm:ss} · {Path.GetFileName(result.TargetFilePath)}");
            }
        }
        catch (Exception ex) { if (Current()) SetActivityWatchExportState("Couldn’t update the report", ex.Message, "error"); }
        finally
        {
            if (Current()) { _activityWatchExportOperation = null; RefreshActivityWatchExport(); }
        }
    }
}
