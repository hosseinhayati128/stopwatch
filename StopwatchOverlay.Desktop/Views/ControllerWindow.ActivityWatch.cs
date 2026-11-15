using StopwatchOverlay.ActivityWatch;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    private ActivityWatchSyncService? _activityWatchSyncService;

    private void StartActivityWatchSync()
    {
        if (!_previewMode && _activityWatchSyncService == null)
            AttachActivityWatchSync(new ActivityWatchSyncService(() => _settings));
    }

    private void AttachActivityWatchSync(ActivityWatchSyncService service)
    {
        StopActivityWatchSync();
        _activityWatchSyncService = service;
        ObsidianLogSync.ExportCompleted += OnObsidianHistoryExported;
    }

    private void OnObsidianHistoryExported(AppSettings settings)
    {
        if (!_isExiting && ReferenceEquals(settings, _settings)) _activityWatchSyncService?.SyncAfterStopwatchExport();
    }

    private void StopActivityWatchSync()
    {
        ObsidianLogSync.ExportCompleted -= OnObsidianHistoryExported;
        _activityWatchSyncService?.Dispose();
        _activityWatchSyncService = null;
    }
}
