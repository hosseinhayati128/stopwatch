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

    public NoteEntryWindow() : this(NoteType.Note, new AppSettings())
    {
    }

    public NoteEntryWindow(NoteType noteType, AppSettings settings)
    {
        InitializeComponent();
        _noteType = noteType;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        ConfigureMode(noteType);
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

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        await SaveAndCloseAsync();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
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
        string text = NoteInputBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            NoteInputBox.Focus();
            return;
        }

        string vaultFolder = _settings.ObsidianVaultFolder;
        bool hasVault = !string.IsNullOrWhiteSpace(vaultFolder) && Directory.Exists(vaultFolder);

        if (!hasVault && !_settings.TelegramEnabled)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "Select Obsidian Vault Folder",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                string localPath = folders[0].Path.LocalPath;
                if (!string.IsNullOrWhiteSpace(localPath))
                {
                    vaultFolder = localPath;
                    _settings.ObsidianVaultFolder = vaultFolder;
                    SettingsStore.Save(_settings);
                    hasVault = true;
                }
            }

            if (!hasVault)
            {
                return;
            }
        }

        if (hasVault)
        {
            var result = ObsidianNotesSync.AppendEntry(
                vaultFolder,
                _noteType,
                text,
                _settings.NotesSubfolder);

            if (!result.Success && !_settings.TelegramEnabled)
            {
                return;
            }
        }

        if (_settings.TelegramEnabled)
        {
            TelegramNotesSync.DispatchNoteInBackground(_settings, _noteType, text, DateTime.Now);
        }

        Close();
    }
}
