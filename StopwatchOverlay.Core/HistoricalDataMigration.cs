using System;
using System.IO;

namespace StopwatchOverlay;

public sealed record HistoricalDataMigrationResult(
    bool Success,
    string HistoryBackupPath,
    string? LogBackupPath,
    int PrunedIntervalsCount,
    int RemainingIntervalsCount,
    int ResyncedLogRecordCount,
    string Message);

public static class HistoricalDataMigration
{
    public static HistoricalDataMigrationResult Run(
        string? historyFilePath = null,
        string? settingsFilePath = null)
    {
        string historyPath = historyFilePath ?? ProjectTimeStore.ProjectHistoryPath;
        if (!File.Exists(historyPath))
        {
            return new HistoricalDataMigrationResult(
                false, "", null, 0, 0, 0, $"History file not found at: {historyPath}");
        }

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string historyBackupPath = $"{historyPath}.bak.{timestamp}";
        File.Copy(historyPath, historyBackupPath, overwrite: false);

        AppSettings settings = settingsFilePath != null && File.Exists(settingsFilePath)
            ? SettingsStore.Load(settingsFilePath)
            : SettingsStore.Load();

        string? logBackupPath = null;
        if (!string.IsNullOrWhiteSpace(settings.ObsidianVaultFolder))
        {
            string exportFileName = !string.IsNullOrWhiteSpace(settings.ObsidianExportFileName)
                ? settings.ObsidianExportFileName
                : "Stopwatch Log.md";
            string logPath = Path.Combine(settings.ObsidianVaultFolder, exportFileName);
            if (File.Exists(logPath))
            {
                logBackupPath = $"{logPath}.bak.{timestamp}";
                File.Copy(logPath, logBackupPath, overwrite: false);
            }
        }

        var store = new ProjectTimeStore(historyPath);
        if (!store.TryLoad(out var history) || history == null)
        {
            return new HistoricalDataMigrationResult(
                false, historyBackupPath, logBackupPath, 0, 0, 0, "Failed to parse and load project-history.json");
        }

        int prunedCount = history.LastDeduplicationPrunedCount;
        bool saved = store.Save(history);
        if (!saved)
        {
            return new HistoricalDataMigrationResult(
                false, historyBackupPath, logBackupPath, prunedCount, 0, 0, "Failed to save sanitized history to disk");
        }

        var view = history.CreateView(DateTime.UtcNow);
        int remainingCount = view.Intervals.Count;

        var syncResult = ObsidianLogSync.SyncHistory(view, settings);

        return new HistoricalDataMigrationResult(
            true,
            historyBackupPath,
            logBackupPath,
            prunedCount,
            remainingCount,
            syncResult.RecordCount,
            $"Migration successful. Pruned {prunedCount} duplicate/micro-intervals. Remaining: {remainingCount}. Resynced {syncResult.RecordCount} log rows.");
    }
}
