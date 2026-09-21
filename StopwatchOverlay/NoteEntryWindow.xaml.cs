using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace StopwatchOverlay;

public partial class NoteEntryWindow : Window
{
    private readonly NoteType _noteType;
    private readonly AppSettings _settings;

    public NoteEntryWindow(NoteType noteType, AppSettings settings)
    {
        InitializeComponent();
        _noteType = noteType;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        string theme = OverlayThemeManager.Apply(this, _settings.OverlayTheme, _settings.ThemeMode);
        bool pirate = theme == OverlayThemeCatalog.Pirate;
        Themes.PirateVisual.SetEnabled(this, pirate);
        if (pirate) { Width = 620; Height = 620; MinHeight = 360; }
        ConfigureMode(noteType);
    }

    private void ConfigureMode(NoteType noteType)
    {
        switch (noteType)
        {
            case NoteType.Todo:
                BadgeIcon.Text = "☑";
                BadgeIcon.SetResourceReference(TextBlock.ForegroundProperty, "SuccessBrush");
                HeadingText.Text = "Add Todo";
                Title = "Add Todo";
                SetPlaceholder("What needs to be done?");
                break;

            case NoteType.Reminder:
                BadgeIcon.Text = "⏰";
                BadgeIcon.SetResourceReference(TextBlock.ForegroundProperty, "DangerTextBrush");
                HeadingText.Text = "Add Reminder";
                Title = "Add Reminder";
                SetPlaceholder("What should I remind you about?");
                break;

            case NoteType.Note:
            default:
                BadgeIcon.Text = "📝";
                BadgeIcon.SetResourceReference(TextBlock.ForegroundProperty, "AccentBrush");
                HeadingText.Text = "Quick Note";
                Title = "Quick Note";
                SetPlaceholder("Write a quick note...");
                break;
        }
    }

    private void SetPlaceholder(string placeholder)
    {
        NoteInputBox.Tag = placeholder;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        NoteInputBox.Focus();
    }

    private void NoteFrame_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!Themes.PirateVisual.GetEnabled(this)) return;
        // Keep the editor usable when window dimensions or application scale change.
        bool compact = NoteFrame.ActualHeight < 450 || NoteFrame.ActualWidth < 460;
        EntryHeader.Margin = compact ? new Thickness(0, 6, 0, 10) : new Thickness(0, 36, 0, 14);
        foreach (Button button in new[] { CancelButton, SaveButton })
        {
            button.MinHeight = compact ? 48 : 62;
            button.Padding = compact ? new Thickness(28, 6, 28, 6) : new Thickness(40, 8, 40, 8);
        }
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            try { DragMove(); } catch { }
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
            return;
        }

        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            e.Handled = true;
            SaveAndClose();
        }
    }

    private void NoteInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Control) != 0)
        {
            e.Handled = true;
            SaveAndClose();
        }
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        SaveAndClose();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void SaveAndClose()
    {
        string text = NoteInputBox.Text?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(text))
        {
            NoteInputBox.Focus();
            return;
        }

        string vaultFolder = _settings.ObsidianVaultFolder;
        if (string.IsNullOrWhiteSpace(vaultFolder) || !Directory.Exists(vaultFolder))
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select Obsidian Vault Folder",
                Multiselect = false
            };

            if (dialog.ShowDialog(this) == true && !string.IsNullOrWhiteSpace(dialog.FolderName))
            {
                vaultFolder = dialog.FolderName;
                _settings.ObsidianVaultFolder = vaultFolder;
                SettingsStore.Save(_settings);
            }
            else
            {
                MessageBox.Show(
                    this,
                    "An Obsidian vault must be selected to save notes.",
                    "Vault Not Selected",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }
        }

        var result = ObsidianNotesSync.AppendEntry(
            vaultFolder,
            _noteType,
            text,
            _settings.NotesSubfolder);

        if (result.Success)
        {
            Close();
        }
        else
        {
            MessageBox.Show(
                this,
                result.Message ?? "Failed to save note.",
                "Error Saving Note",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }
}
