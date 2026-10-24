using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using StopwatchOverlay.Desktop.Platform;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Habitica;

namespace StopwatchOverlay.Desktop.Views;

public partial class NoteEntryWindow : Window
{
    private readonly NoteType _noteType;
    private readonly AppSettings _settings;
    private NoteEntry? _originalEntry;
    private readonly string _openedVault;
    private readonly string? _openedSubfolder;
    private readonly string _openedHabiticaAccount;
    private readonly string _openedHabiticaClient;
    private readonly bool _openedHabiticaEnabled;
    private readonly bool _isEditing;
    private bool _saving;
    private bool _closed;
    private readonly HabiticaSubtaskEditor _subtaskEditor = new();
    private bool _savedWithWarning;

    public NoteEntryWindow() : this(NoteType.Note, new AppSettings())
    {
    }

    public NoteEntryWindow(NoteType noteType, AppSettings settings, NoteEntry? editingEntry = null)
    {
        InitializeComponent();
        OverlayScreenConstraint.Attach(this);
        _noteType = noteType;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _originalEntry = editingEntry;
        _isEditing = editingEntry != null;
        _openedVault = _settings.ObsidianVaultFolder;
        _openedSubfolder = _settings.NotesSubfolder;
        _openedHabiticaAccount = _settings.HabiticaUserId;
        _openedHabiticaClient = _settings.HabiticaClientId;
        _openedHabiticaEnabled = _settings.HabiticaEnabled;
        ConfigureMode(noteType);
        TodoDetailsPanel.IsVisible = noteType is NoteType.Todo or NoteType.Reminder;
        TodoPriorityRow.IsVisible = noteType == NoteType.Todo;
        SubtaskEditorHost.Content = _subtaskEditor;
        SubtaskEditorHost.IsVisible = noteType == NoteType.Todo;
        TodoPriorityCombo.ItemsSource = new[] { "Urgent", "High", "Normal", "Low" };
        TodoPriorityCombo.SelectedIndex = 2;
        TodoDueDate.SelectedDate = DateTimeOffset.Now.Date;
        TodoDueTime.SelectedTime = new TimeSpan(DateTime.Now.AddHours(1).Hour, DateTime.Now.Minute, 0);
        DueEnabledToggle.IsCheckedChanged += (_, _) =>
        {
            DueFields.IsVisible = DueEnabledToggle.IsChecked == true;
            Height = noteType == NoteType.Todo ? (DueFields.IsVisible ? 650 : 480) : (DueFields.IsVisible ? 480 : 330);
            MinHeight = noteType == NoteType.Todo ? 440 : 300;
        };
        if (noteType == NoteType.Todo) { Height = 480; MinHeight = 440; }
        if (noteType == NoteType.Reminder) { Height = 330; MinHeight = 300; }
        if (editingEntry != null)
        {
            if (noteType is not (NoteType.Todo or NoteType.Reminder) || editingEntry.Type != noteType) throw new ArgumentException("Only matching tasks can be edited.");
            HeadingText.Text = Title = noteType == NoteType.Todo ? "Edit Todo" : "Edit Reminder";
            SaveButton.Content = "Save changes";
            NoteInputBox.Text = editingEntry.Text;
            var organization = TodoOrganizationStore.Load(_settings.ObsidianVaultFolder, _settings.NotesSubfolder);
            TodoPriorityCombo.SelectedIndex = organization.GetPriority(editingEntry) - 1;
            var details = HabiticaIntegration.Current.GetDetails(_settings, editingEntry);
            _subtaskEditor.SetSubtasks(details.Children);
            if ((noteType == NoteType.Todo ? organization.GetDueAt(editingEntry) : details.DueAt) is DateTime due)
            {
                TodoDueDate.SelectedDate = new DateTimeOffset(due);
                TodoDueTime.SelectedTime = due.TimeOfDay;
                DueEnabledToggle.IsChecked = true;
            }
        }
        Closed += (_, _) => _closed = true;
        // Handle shortcuts before the multiline TextBox consumes Enter.
        AddHandler(KeyDownEvent, OnEditorKeyDown, RoutingStrategies.Tunnel);
        Opened += (_, _) => NoteInputBox.Focus();
    }

    private void ConfigureMode(NoteType noteType)
    {
        switch (noteType)
        {
            case NoteType.Todo:
                BadgeIcon.Text = "☑";
                if (Application.Current?.TryFindResource("SuccessBrush", out var succ) == true && succ is IBrush succBrush)
                    BadgeIcon.Foreground = succBrush;
                HeadingText.Text = "Add Todo";
                Title = "Add Todo";
                NoteInputBox.Watermark = "What needs to be done?";
                break;

            case NoteType.Reminder:
                BadgeIcon.Text = "⏰";
                if (Application.Current?.TryFindResource("DangerTextBrush", out var dang) == true && dang is IBrush dangBrush)
                    BadgeIcon.Foreground = dangBrush;
                HeadingText.Text = "Add Reminder";
                Title = "Add Reminder";
                NoteInputBox.Watermark = "What should I remind you about?";
                break;

            case NoteType.Note:
            default:
                BadgeIcon.Text = "📝";
                if (Application.Current?.TryFindResource("AccentBrush", out var acc) == true && acc is IBrush accBrush)
                    BadgeIcon.Foreground = accBrush;
                HeadingText.Text = "Quick Note";
                Title = "Quick Note";
                NoteInputBox.Watermark = "Write a quick note...";
                break;
        }
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnClearDueClicked(object? sender, RoutedEventArgs e) => DueEnabledToggle.IsChecked = false;

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        await SaveAndCloseAsync();
    }

    private void OnEditorKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
        else if (e.Key == Key.Enter && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            e.Handled = true;
            _ = SaveAndCloseAsync();
        }
    }

    private async Task SaveAndCloseAsync()
    {
        if (_saving || _closed) return;
        if (_savedWithWarning) { Close(); return; }
        string text = NoteInputBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text)) { NoteInputBox.Focus(); return; }
        if (_noteType != NoteType.Note) text = text.Replace("\r\n", "\n").Replace('\r', '\n');
        if (_noteType == NoteType.Reminder) text = text.Replace('\n', ' ');
        var children = _noteType == NoteType.Todo ? _subtaskEditor.GetSubtasks() : null;
        if (children?.Any(child => string.IsNullOrWhiteSpace(child.Text)) == true)
        { ShowSaveError("Enter text for each subtask or remove empty subtasks."); return; }
        DateTime? dueAt = null;
        if (_noteType is NoteType.Todo or NoteType.Reminder && DueEnabledToggle.IsChecked == true)
        {
            if (TodoDueDate.SelectedDate == null || TodoDueTime.SelectedTime == null)
            { ShowSaveError("Choose both a due date and a time, or clear the due date."); return; }
            dueAt = DateTime.SpecifyKind(TodoDueDate.SelectedDate.Value.Date + TodoDueTime.SelectedTime.Value, DateTimeKind.Unspecified);
            if (TimeZoneInfo.Local.IsInvalidTime(dueAt.Value))
            { ShowSaveError("That local time does not exist because the clocks change. Choose another time."); return; }
        }
        int priority = TodoPriorityCombo.SelectedIndex + 1;
        _saving = true;
        SaveButton.IsEnabled = false;
        SaveError.IsVisible = false;
        try
        {
            if (_isEditing && (_openedVault != _settings.ObsidianVaultFolder || _openedSubfolder != _settings.NotesSubfolder))
            { ShowSaveError("The notes folder changed. Close this editor and open the task again."); return; }
            if (_isEditing && (_openedHabiticaAccount != _settings.HabiticaUserId || _openedHabiticaClient != _settings.HabiticaClientId || _openedHabiticaEnabled != _settings.HabiticaEnabled))
            { ShowSaveError("Habitica settings changed. Close this editor and open the task again."); return; }
            DateTime now = DateTime.Now;
            DateTime entryTimestamp = _originalEntry?.Timestamp ?? new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            string vaultFolder = _settings.ObsidianVaultFolder;
            bool hasVault = !string.IsNullOrWhiteSpace(vaultFolder) && Directory.Exists(vaultFolder);
            if (!hasVault && (_isEditing || !_settings.TelegramEnabled || dueAt.HasValue || children?.Count > 0 || (_noteType != NoteType.Note && _settings.HabiticaEnabled) || (_noteType == NoteType.Todo && priority != 3)))
            {
                var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                { Title = "Select Obsidian Vault Folder", AllowMultiple = false });
                if (_closed) return;
                if (folders.Count > 0)
                {
                    string localPath = folders[0].Path.LocalPath;
                    if (!string.IsNullOrWhiteSpace(localPath))
                    { vaultFolder = localPath; _settings.ObsidianVaultFolder = vaultFolder; SettingsStore.Save(_settings); hasVault = true; }
                }
                if (!hasVault) return;
            }
            bool wasEditing = _isEditing;
            string? syncWarning = null;
            if (hasVault)
            {
                var before = _originalEntry;
                var after = new NoteEntry(_noteType, entryTimestamp, text, before?.IsCompleted ?? false);
                var mutation = HabiticaIntegration.Current.Apply(_settings, before, after, dueAt, children, false, () =>
                {
                    if (before == null)
                    {
                        var appended = ObsidianNotesSync.AppendEntry(vaultFolder, _noteType, text, _settings.NotesSubfolder, entryTimestamp);
                        if (!appended.Success) return appended;
                        _originalEntry = after; // Keep retries from appending a second local copy.
                    }
                    if (_noteType == NoteType.Todo)
                        return TodoOrganizationStore.SaveTodo(vaultFolder, before ?? after, text, priority, dueAt, _settings.NotesSubfolder);
                    if (before != null)
                        return ObsidianNotesSync.UpdateEntryText(vaultFolder, before, text, _settings.NotesSubfolder);
                    return new NoteSyncResult(true, "");
                });
                if (!mutation.LocalResult.Success) { ShowSaveError(mutation.LocalResult.Message ?? "Could not save the task."); return; }
                _originalEntry = after;
                syncWarning = mutation.SyncWarning;
            }
            if (_settings.TelegramEnabled && !wasEditing)
                TelegramNotesSync.DispatchNoteInBackground(_settings, _noteType, text, entryTimestamp);
            if (syncWarning != null)
            {
                _savedWithWarning = true;
                NoteInputBox.IsEnabled = TodoDetailsPanel.IsEnabled = false;
                SaveButton.Content = "Close";
                ShowSaveError(syncWarning);
                return;
            }
            Close();
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "NoteEntryWindow.Save");
            ShowSaveError("Could not save your changes. Check the notes folder and try again.");
        }
        finally { _saving = false; SaveButton.IsEnabled = true; }
    }

    private void ShowSaveError(string message)
    {
        SaveError.Text = message;
        SaveError.IsVisible = true;
    }
}
