using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using StopwatchOverlay.ActivityWatch;
using StopwatchOverlay.Mood;
using StopwatchOverlay.PeriodicReview;

namespace StopwatchOverlay.Desktop.Views;

public partial class ControllerWindow
{
    private readonly Dictionary<Guid, bool> _overlayVisibilityOverrides = new();
    private Window? _timerCommandDialog;
    private ActiveClocksShelfWindow? _activeClocksShelf;
    private TimerEditUndo? _lastTimerEdit;
    private bool _shortcutSyncRunning;
    private string? _shortcutFeedback;
    private DateTime _shortcutFeedbackUntil;

    private sealed record TimerEditUndo(
        TimerSession Session, string Name, string Category, double ScorePerHour, int Mode,
        TimeSpan Elapsed, TimeSpan CountdownRemaining, bool IsRunning,
        IReadOnlyList<WorkIntervalDocumentEntry> Intervals);

    private void ShowControllerFromShortcut()
    {
        Show();
        if (WindowState == WindowState.Minimized) WindowState = WindowState.Normal;
        Activate();
    }

    private NoteCommandAction ResolveNoteCommand(uint key)
    {
        uint digit = key is >= 0x60 and <= 0x69 ? key - 0x60 + 0x30 : key;
        var keys = _settings.NoteCommandModeKeys;
        foreach (var (action, boundKey) in keys)
            if (boundKey == key || boundKey == digit) return action;

        // Preserve the original 1–4 and letter pairs for default and legacy bindings.
        var alias = digit switch
        {
            0x31 or 0x54 => (NoteCommandAction.AddTodo, 0x31u, 0x54u),
            0x32 or 0x4E => (NoteCommandAction.AddNote, 0x32u, 0x4Eu),
            0x33 or 0x52 => (NoteCommandAction.AddReminder, 0x33u, 0x52u),
            0x34 or 0x56 => (NoteCommandAction.ViewNotes, 0x34u, 0x56u),
            _ => default
        };
        return alias.Item1 != 0 && keys.TryGetValue(alias.Item1, out uint binding)
            && (binding == alias.Item2 || binding == alias.Item3) ? alias.Item1 : 0;
    }

    private async Task ShowTimerCommandDialog(Window dialog)
    {
        _timerCommandDialog = dialog;
        try
        {
            if (IsVisible && WindowState != WindowState.Minimized)
            {
                await dialog.ShowDialog(this);
            }
            else
            {
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                dialog.Closed += (_, _) => closed.TrySetResult();
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                dialog.Topmost = true;
                dialog.Show();
                await closed.Task;
            }
        }
        finally { _timerCommandDialog = null; }
    }

    private async void OpenTimerPicker()
    {
        if (_timerCommandDialog != null || _timerManager.Count == 0) return;
        var picker = new TimerPickerWindow(_timerManager.Sessions, _timerManager.Active, _settings);
        await ShowTimerCommandDialog(picker);
        if (!_isExiting && _timerManager.Activate(picker.SelectedTimer))
        {
            RefreshTimerRail();
            UpdateActiveTimerDisplay();
            UpdateOverlayStates();
        }
    }

    private void ToggleActiveClocksShelf()
    {
        if (_activeClocksShelf?.IsVisible == true)
        {
            _activeClocksShelf.Hide();
            return;
        }

        if (_activeClocksShelf == null)
        {
            _activeClocksShelf = new ActiveClocksShelfWindow(
                session =>
                {
                    if (!_timerManager.Activate(session)) return;
                    RefreshTimerRail();
                    UpdateActiveTimerDisplay();
                    UpdateOverlayStates();
                },
                session =>
                {
                    if (session.IsRunning) ToggleSessionStartPause(session);
                });
            _activeClocksShelf.Closed += (_, _) => _activeClocksShelf = null;
        }
        UpdateActiveClocksShelf();
        _activeClocksShelf.Show();
    }

    private void UpdateActiveClocksShelf()
    {
        if (_activeClocksShelf == null) return;
        _activeClocksShelf.ApplyTheme(_settings.ThemeMode);
        _activeClocksShelf.UpdateClocks(_timerManager.Sessions, _timerManager.Active, FormatDisplayTime);
        var active = _timerManager.Active;
        var anchor = active?.IsSeparated == true ? _overlays.GetValueOrDefault(active.Id) : _herdOverlay;
        if (anchor?.IsVisible == true)
        {
            _activeClocksShelf.RepositionUnder(anchor);
        }
    }

    private bool IsTimerOverlayVisible(TimerSession session) =>
        _overlayVisibilityOverrides.GetValueOrDefault(session.Id, _overlaysVisible);

    private void ApplyTimerOverlayVisibility(OverlayWindow overlay, TimerSession session)
    {
        bool visible = IsTimerOverlayVisible(session);
        if (visible == overlay.IsVisible) return;
        if (visible) overlay.Show();
        else overlay.Hide();
    }

    private void ToggleActiveTimerOverlay()
    {
        if (_timerManager.Active is not { } session) return;
        _overlayVisibilityOverrides[session.Id] = !IsTimerOverlayVisible(session);
        UpdateOverlayStates();
    }

    private void ShowActiveTimerOverlay()
    {
        if (_timerManager.Active is not { } session) return;
        _overlayVisibilityOverrides[session.Id] = true;
        UpdateOverlayStates();
    }

    private TimerNameWindow CreateProjectChooser(TimerSession? session) => new(
        session?.Name ?? "",
        _projectHistory.ProjectNames,
        isCreatingTimer: session == null,
        categories: _settings.GetNormalizedProjectCategories(),
        currentCategory: session?.Category ?? "Work",
        getProjectCategory: _projectHistory.GetProjectCategory,
        currentScorePerHour: session?.ScorePerHour ?? 1,
        getProjectScore: _projectHistory.GetProjectScorePerHour,
        theme: _settings.ThemeMode);

    private void ApplyProjectChoice(TimerSession session, TimerNameWindow dialog)
    {
        session.Name = dialog.TimerName;
        session.Category = dialog.Category;
        session.ScorePerHour = dialog.ScorePerHour;
        if (!string.IsNullOrWhiteSpace(session.Name))
        {
            session.Name = _projectHistory.RegisterProject(session.Name, dialog.Category, dialog.ScorePerHour);
            _projectTimeStore.Save(_projectHistory);
        }
        if (_settings.AddProjectCategory(dialog.Category)) SettingsStore.Save(_settings);
    }

    private void SaveTimerEditUndo(TimerSession session) => _lastTimerEdit = new(
        session, session.Name, session.Category, session.ScorePerHour, session.Mode,
        session.Elapsed, session.CountdownRemaining, session.IsRunning,
        _projectHistory.CaptureTimerIntervalsSnapshot(session.Id));

    private void UndoTimerEdit()
    {
        if (_lastTimerEdit is not { } undo || !_timerManager.Activate(undo.Session))
        {
            ShowShortcutFeedback("No timer edit to undo.");
            return;
        }

        var session = undo.Session;
        _lastTimerEdit = null;
        session.Name = undo.Name;
        session.Category = undo.Category;
        session.ScorePerHour = undo.ScorePerHour;
        session.Mode = undo.Mode;
        session.CountdownRemaining = undo.CountdownRemaining;
        session.IsRunning = undo.IsRunning;
        session.RestoreElapsed(undo.Elapsed, undo.IsRunning);
        session.LastCountdownUpdateUtc = undo.IsRunning ? DateTime.UtcNow : default;
        _projectHistory.RestoreTimerIntervalsSnapshot(session.Id, undo.Intervals);
        _projectTimeStore.Save(_projectHistory);
        _dashboardWindow?.RefreshData();
        RefreshTimerRail();
        UpdateActiveTimerDisplay();
        UpdateOverlayStates();
        ShowShortcutFeedback($"Restored {session.DisplayName}.");
    }

    private ProjectRecordMutationResult AddProjectRecord(string name, DateTime startUtc, DateTime endUtc)
    {
        var record = _projectHistory.AddManualInterval(name, startUtc, endUtc);
        _projectTimeStore.Save(_projectHistory);
        return new ProjectRecordMutationResult(ProjectRecordMutationStatus.Success, record);
    }

    private async void OpenAddRecord()
    {
        if (_timerCommandDialog != null) return;
        var view = _projectHistory.CreateView(DateTime.UtcNow);
        string? projectKey = view.Projects.FirstOrDefault(project =>
            string.Equals(project.Name, _timerManager.Active?.Name, StringComparison.OrdinalIgnoreCase))?.Key;
        var editor = new ProjectRecordEditorWindow(view.Projects, projectKey, commit: AddProjectRecord, theme: _settings.ThemeMode);
        await ShowTimerCommandDialog(editor);
        if (editor.SavedRecord != null) _dashboardWindow?.RefreshData();
    }

    private async void OpenPeriodicReview()
    {
        if (_timerCommandDialog != null) return;
        DateTime now = DateTime.UtcNow;
        DateTime start = _settings.LastPeriodicReviewCompletedUtc is DateTime last && last < now && now - last <= TimeSpan.FromDays(1)
            ? last : now.AddMinutes(-Math.Clamp(_settings.PeriodicReviewIntervalMinutes, 1, 1440));
        var review = new PeriodicReviewWindow(
            start, now, _settings, _projectHistory.CreateView(now), _timerManager.Sessions,
            SavePeriodicReview,
            name =>
            {
                string canonical = _projectHistory.RegisterProject(name);
                _projectTimeStore.Save(_projectHistory);
                return canonical;
            },
            mood => MoodLogSync.AppendMood(mood, _settings));
        await ShowTimerCommandDialog(review);
    }

    private void SavePeriodicReview(List<PeriodicReviewStopwatchSlot> slots)
    {
        foreach (var slot in slots.Where(slot => slot.HasChanged))
        {
            if (slot.ExistingIntervalId is Guid id)
            {
                if (string.IsNullOrWhiteSpace(slot.SelectedProjectName)) _projectHistory.DeleteClosedInterval(id);
                else _projectHistory.UpdateClosedInterval(id, slot.SelectedProjectName, slot.StartUtc, slot.EndUtc);
            }
            else if (!string.IsNullOrWhiteSpace(slot.SelectedProjectName))
            {
                _projectHistory.AddManualInterval(slot.SelectedProjectName, slot.StartUtc, slot.EndUtc);
            }
        }
        _projectHistory.DeduplicateIntervals();
        _projectTimeStore.Save(_projectHistory);
        _settings.LastPeriodicReviewCompletedUtc = DateTime.UtcNow;
        SettingsStore.Save(_settings);
        ObsidianLogSync.SyncHistory(_projectHistory.CreateView(DateTime.UtcNow), _settings);
        _dashboardWindow?.RefreshData();
    }

    private async void SyncActivityWatchFromShortcut()
    {
        if (_shortcutSyncRunning) return;
        if (!_settings.ActivityWatchEnabled)
        {
            ShowShortcutFeedback("ActivityWatch is disabled in Settings.");
            return;
        }
        if (string.IsNullOrWhiteSpace(_settings.ObsidianVaultFolder))
        {
            ShowShortcutFeedback("Set an Obsidian vault folder before syncing.");
            return;
        }

        _shortcutSyncRunning = true;
        ShowShortcutFeedback("Syncing ActivityWatch…");
        try
        {
            var result = await ActivityWatchSync.SyncAsync(_settings);
            ShowShortcutFeedback(result.Success
                ? $"ActivityWatch synced {result.AppCount} apps and {result.WebCount} web domains."
                : result.Message ?? "ActivityWatch sync failed.");
        }
        catch (Exception exception)
        {
            CrashLogger.LogRecoverable(exception, "Shortcut.ActivityWatchSync");
            ShowShortcutFeedback("ActivityWatch sync failed.");
        }
        finally { _shortcutSyncRunning = false; }
    }

    private void ShowShortcutFeedback(string message)
    {
        _shortcutFeedback = message;
        _shortcutFeedbackUntil = DateTime.UtcNow.AddSeconds(5);
        UpdateActiveTimerDisplay();
    }
}
