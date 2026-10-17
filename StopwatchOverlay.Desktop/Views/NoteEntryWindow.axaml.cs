using System;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace StopwatchOverlay.Desktop.Views;

public partial class NoteEntryWindow : Window
{
    private readonly NoteType _noteType;
    private readonly AppSettings _settings;
    private NoteEntry? _originalEntry;
    private readonly string _openedVault;
    private readonly string? _openedSubfolder;
    private readonly bool _isEditing;
    private bool _saving;
    private bool _closed;

    public NoteEntryWindow() : this(NoteType.Note, new AppSettings())
    {
    }

    public NoteEntryWindow(NoteType noteType, AppSettings settings, NoteEntry? editingEntry = null)
    {
        InitializeComponent();
        _noteType = noteType;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _originalEntry = editingEntry;
        _isEditing = editingEntry != null;
        _openedVault = _settings.ObsidianVaultFolder;
        _openedSubfolder = _settings.NotesSubfolder;
        ConfigureMode(noteType);
        TodoDetailsPanel.IsVisible = noteType == NoteType.Todo;
        TodoPriorityCombo.ItemsSource = new[] { "Urgent", "High", "Normal", "Low" };
        TodoPriorityCombo.SelectedIndex = 2;
        TodoDueDate.SelectedDate = DateTimeOffset.Now.Date;
        TodoDueTime.SelectedTime = new TimeSpan(DateTime.Now.AddHours(1).Hour, DateTime.Now.Minute, 0);
        DueEnabledToggle.IsCheckedChanged += (_, _) =>
        {
            DueFields.IsVisible = DueEnabledToggle.IsChecked == true;
            Height = noteType == NoteType.Todo ? (DueFields.IsVisible ? 530 : 350) : 270;
            MinHeight = noteType == NoteType.Todo ? (DueFields.IsVisible ? 480 : 320) : 220;
        };
        if (noteType == NoteType.Todo) { Height = 350; MinHeight = 320; }
        if (editingEntry != null)
        {
            if (noteType != NoteType.Todo || editingEntry.Type != NoteType.Todo) throw new ArgumentException("Only todos can be edited.");
            HeadingText.Text = Title = "Edit Todo";
            SaveButton.Content = "Save changes";
            NoteInputBox.Text = editingEntry.Text;
            var organization = TodoOrganizationStore.Load(_settings.ObsidianVaultFolder, _settings.NotesSubfolder);
            TodoPriorityCombo.SelectedIndex = organization.GetPriority(editingEntry) - 1;
            if (organization.GetDueAt(editingEntry) is DateTime due)
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
        string text = NoteInputBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text)) { NoteInputBox.Focus(); return; }
        DateTime? dueAt = null;
        if (_noteType == NoteType.Todo && DueEnabledToggle.IsChecked == true)
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
            { ShowSaveError("The notes folder changed. Close this editor and open the todo again."); return; }
            DateTime now = DateTime.Now;
            DateTime entryTimestamp = _originalEntry?.Timestamp ?? new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            string vaultFolder = _settings.ObsidianVaultFolder;
            bool hasVault = !string.IsNullOrWhiteSpace(vaultFolder) && Directory.Exists(vaultFolder);
            if (!hasVault && (!_settings.TelegramEnabled || (_noteType == NoteType.Todo && (dueAt.HasValue || priority != 3))))
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
            if (hasVault)
            {
                if (_originalEntry == null)
                {
                    var result = ObsidianNotesSync.AppendEntry(vaultFolder, _noteType, text, _settings.NotesSubfolder, entryTimestamp);
                    if (!result.Success) { ShowSaveError(result.Message ?? "Could not save the note."); return; }
                    if (_noteType == NoteType.Todo) _originalEntry = new(NoteType.Todo, entryTimestamp, text.Replace("\r\n", "\n").Replace('\r', '\n'));
                }
                if (_noteType == NoteType.Todo)
                {
                    var result = TodoOrganizationStore.SaveTodo(vaultFolder, _originalEntry!, text, priority, dueAt, _settings.NotesSubfolder);
                    if (!result.Success) { ShowSaveError(result.Message ?? "Could not save todo changes."); return; }
                }
            }
            if (_settings.TelegramEnabled && !wasEditing)
                TelegramNotesSync.DispatchNoteInBackground(_settings, _noteType, text, entryTimestamp);
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
