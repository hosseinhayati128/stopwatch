using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using StopwatchOverlay.Desktop.Platform;
using StopwatchOverlay.Habitica;

namespace StopwatchOverlay.Desktop.Views;

public partial class NotesViewerWindow : Window
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _autoCloseTimer;
    private int _countdownSeconds = 30;
    private List<NoteEntry> _allEntries = new();
    private TodoOrganization? _organization;
    private NoteType? _currentFilter = null;
    private string? _loadMessage;
    private string _loadTitle = "Could not load notes";
    private ConfirmationDialogWindow? _deleteReminderDialog;
    private (string Vault, string Folder, string Account, string Client, bool Enabled)? _loadedNotesContext;

    public NotesViewerWindow() : this(new AppSettings())
    {
    }

    public NotesViewerWindow(AppSettings settings)
    {
        InitializeComponent();
        WindowsTopmostGuard.Attach(this);
        OverlayScreenConstraint.Attach(this);
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));

        _autoCloseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _autoCloseTimer.Tick += AutoCloseTimer_Tick;

        ConfigureSearch();
        Opened += OnWindowOpened;
        ApplyViewerAppearance();
    }

    private void OnWindowOpened(object? sender, EventArgs e)
    {
        LoadNotes();
        _autoCloseTimer.Start();
        Focus();
    }

    private void AutoCloseTimer_Tick(object? sender, EventArgs e)
    {
        _countdownSeconds--;
        if (_countdownSeconds <= 0)
        {
            _autoCloseTimer.Stop();
            Close();
            return;
        }

        CountdownText.Text = _viewerSummary!=null?$"Auto-close · {_countdownSeconds}s":$"Auto-closing in {_countdownSeconds}s";
    }

    private void LoadNotes()
    {
        _allEntries.Clear();_organization=null;_loadMessage=null;
        _todoDisplayText.Clear();
        _loadedNotesContext = (_settings.ObsidianVaultFolder, _settings.NotesSubfolder, _settings.HabiticaUserId, _settings.HabiticaClientId, _settings.HabiticaEnabled);
        string vaultFolder = _settings.ObsidianVaultFolder;
        if (string.IsNullOrWhiteSpace(vaultFolder) || !Directory.Exists(vaultFolder))
        {
            VaultLocationText.Text = "Vault: Not configured";
            _loadTitle="Choose a notes folder";
            _loadMessage="Select an Obsidian vault in Settings to view your notes, todos, and reminders.";
            RenderEntries();
            return;
        }

        VaultLocationText.Text = $"Vault: {Path.GetFileName(Path.TrimEndingDirectorySeparator(vaultFolder))} / {_settings.NotesSubfolder}";
        ToolTip.SetTip(VaultLocationText,Path.Combine(vaultFolder,_settings.NotesSubfolder));

        try
        {
            _allEntries = ObsidianNotesSync.LoadAllNotes(vaultFolder, _settings.NotesSubfolder).ToList();
            _organization = TodoOrganizationStore.Load(vaultFolder, _settings.NotesSubfolder);
            RenderEntries();
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "NotesViewerWindow.LoadNotes");
            _loadTitle="Could not load notes";_loadMessage=$"Error reading notes: {ex.Message}";
            RenderEntries();
        }
    }

    private void RenderEntries()
    {
        ItemsContainer.Children.Clear();

        string query = SearchField.Text?.Trim() ?? "";
        var matches = query.Length == 0 ? _allEntries : _allEntries
            .Where(e => DisplayText(e).Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        var filtered = _currentFilter.HasValue ? matches.Where(e => e.Type == _currentFilter.Value).ToList() : matches;
        UpdateViewerSummary(filtered.Count, matches, query.Length != 0 && _loadMessage == null);
        if(_loadMessage!=null)
        {
            if(_viewerSummary!=null)ShowViewerEmpty(_loadTitle,_loadMessage);
            else {EmptyMessageText.Text=_loadMessage;EmptyMessageText.IsVisible=true;ItemsContainer.Children.Add(EmptyMessageText);}
            return;
        }

        if (filtered.Count == 0)
        {
            if (query.Length > 0)
            {
                if (_viewerSummary != null) ShowViewerEmpty("No matches", "Try another word or clear the search.");
                else
                {
                    EmptyMessageText.Text = "No matches. Try another word or clear the search.";
                    EmptyMessageText.IsVisible = true;
                    ItemsContainer.Children.Add(EmptyMessageText);
                }
                return;
            }
            if(_viewerSummary!=null)
            {
                string kind=_currentFilter switch{NoteType.Todo=>"todos",NoteType.Reminder=>"reminders",NoteType.Note=>"quick notes",_=>"notes, todos or reminders"};
                ShowViewerEmpty(_currentFilter==null?"Nothing here yet":$"No {kind} yet",$"Your {kind} will appear here once you add one.");
                return;
            }
            EmptyMessageText.Text = _currentFilter switch
            {
                NoteType.Todo => "No todos found.",
                NoteType.Reminder => "No reminders found.",
                NoteType.Note => "No quick notes found.",
                _ => "No notes, todos, or reminders found in vault."
            };
            EmptyMessageText.IsVisible = true;
            ItemsContainer.Children.Add(EmptyMessageText);
            return;
        }

        EmptyMessageText.IsVisible = false;

        var grouped = filtered
            .GroupBy(e => e.Timestamp.Date)
            .OrderByDescending(g => g.Key);

        DateTime today = DateTime.Today;
        DateTime yesterday = today.AddDays(-1);

        foreach (var group in grouped)
        {
            string dateLabel;
            if (group.Key == today)
                dateLabel = $"Today · {group.Key:yyyy-MM-dd}";
            else if (group.Key == yesterday)
                dateLabel = $"Yesterday · {group.Key:yyyy-MM-dd}";
            else
                dateLabel = group.Key.ToString("yyyy-MM-dd");

            // Date Header
            var dateHeaderBorder = new Border
            {
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 8, 0, 6),
                CornerRadius = new CornerRadius(4)
            };
            if (Application.Current?.TryFindResource("SurfaceRaisedBrush", out var srb) == true && srb is IBrush srBrush)
                dateHeaderBorder.Background = srBrush;
            if (Application.Current?.TryFindResource("BorderBrush", out var bb) == true && bb is IBrush bBrush)
                dateHeaderBorder.BorderBrush = bBrush;

            var dateHeaderText = new TextBlock
            {
                Text = dateLabel,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold
            };
            if (Application.Current?.TryFindResource("AccentBrush", out var ab) == true && ab is IBrush aBrush)
                dateHeaderText.Foreground = aBrush;

            dateHeaderBorder.Child = dateHeaderText;
            if(_viewerSummary!=null)
            {
                dateHeaderText.Text=(group.Key==today?$"Today · {group.Key:dd MMM}":group.Key==yesterday?$"Yesterday · {group.Key:dd MMM}":group.Key.ToString("dd MMM yyyy")).ToUpperInvariant();
                dateHeaderText.FontFamily=Controls.ClockPickerPresentation.Body;dateHeaderText.FontSize=11;dateHeaderText.Foreground=Controls.ClockPickerPresentation.Muted;
                dateHeaderBorder.Background=Brushes.Transparent;dateHeaderBorder.BorderThickness=new(0);dateHeaderBorder.Padding=new(0);dateHeaderBorder.Margin=new(2,3,0,8);
            }
            ItemsContainer.Children.Add(dateHeaderBorder);

            // Entries in this date
            foreach (var entry in group.OrderByDescending(e => e.Timestamp))
            {
                var entryBorder = CreateEntryCard(entry, query);
                ItemsContainer.Children.Add(entryBorder);
            }
        }
    }

    private Border CreateEntryCard(NoteEntry entry, string query)
    {
        var border = new Border
        {
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(10, 7, 10, 7),
            Margin = new Thickness(0, 0, 0, 5)
        };
        if (Application.Current?.TryFindResource("SurfaceRaisedBrush", out var srb) == true && srb is IBrush srBrush)
            border.Background = srBrush;
        if (Application.Current?.TryFindResource("BorderSoftBrush", out var bsb) == true && bsb is IBrush bsBrush)
            border.BorderBrush = bsBrush;

        var grid = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*")
        };

        // Badge / Icon
        string icon;
        string iconBrushKey;
        switch (entry.Type)
        {
            case NoteType.Todo:
                icon = entry.IsCompleted ? "☑" : "☐";
                iconBrushKey = entry.IsCompleted ? "SecondaryTextBrush" : "SuccessBrush";
                break;
            case NoteType.Reminder:
                icon = "⏰";
                iconBrushKey = "DangerTextBrush";
                break;
            case NoteType.Note:
            default:
                icon = "📝";
                iconBrushKey = "AccentBrush";
                break;
        }

        var iconText = new TextBlock
        {
            Text = icon,
            FontSize = 13,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 0, 8, 0)
        };
        if (Application.Current?.TryFindResource(iconBrushKey, out var ib) == true && ib is IBrush iBrush)
            iconText.Foreground = iBrush;
        Grid.SetColumn(iconText, 0);
        if (entry.Type is NoteType.Todo or NoteType.Reminder)
        {
            var completed = new CheckBox { IsChecked = entry.IsCompleted, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, -5, 7, 0) };
            Avalonia.Automation.AutomationProperties.SetName(completed, "Complete " + entry.Text);
            completed.IsCheckedChanged += (_, _) =>
            {
                bool value = completed.IsChecked == true;
                var details = HabiticaIntegration.Current.GetDetails(_settings, entry);
                var mutation = HabiticaIntegration.Current.Apply(_settings, entry, entry with { IsCompleted = value },
                    entry.Type == NoteType.Todo ? _organization?.GetDueAt(entry) : details.DueAt, null, false,
                    () => ObsidianNotesSync.SetEntryCompleted(_settings.ObsidianVaultFolder, entry, value, _settings.NotesSubfolder));
                LoadNotes();
                ShowMutationStatus(mutation);
                if (mutation.LocalResult.Success && entry.Type == NoteType.Todo && _settings.TelegramEnabled)
                    _ = TelegramNotesSync.SyncTodoCompletionsAsync(_settings);
            };
            Grid.SetColumn(completed, 0); grid.Children.Add(completed);
        }
        else grid.Children.Add(iconText);

        // Timestamp
        var timeText = new TextBlock
        {
            Text = entry.Timestamp.ToString("HH:mm"),
            FontSize = 11,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(0, 1, 10, 0)
        };
        if (Application.Current?.TryFindResource("SecondaryTextBrush", out var stb) == true && stb is IBrush stBrush)
            timeText.Foreground = stBrush;
        Grid.SetColumn(timeText, 1);
        grid.Children.Add(timeText);

        // Content
        var contentText = entry.Type == NoteType.Todo
            ? Controls.TodoRichText.Create(entry.Text, entry.IsCompleted)
            : new TextBlock { Text = entry.Text, TextWrapping = TextWrapping.Wrap };
        string display = DisplayText(entry);
        Controls.AutoTextDirection.Apply(contentText, display);
        contentText.FontSize = 13;
        contentText.VerticalAlignment = VerticalAlignment.Top;
        string textBrushKey = entry.IsCompleted ? "SecondaryTextBrush" : "PrimaryTextBrush";
        if (Application.Current?.TryFindResource(textBrushKey, out var tb) == true && tb is IBrush tBrush)
            contentText.Foreground = tBrush;

        if (entry.IsCompleted)
        {
            contentText.TextDecorations = TextDecorations.Strikethrough;
        }
        if (query.Length > 0)
            HighlightMatches(contentText, display, query);

        void ToggleProgress()
        {
            CrashLogger.RecordUiAction("Toggle note viewer todo progress");
            var organization = TodoOrganizationStore.Load(_settings.ObsidianVaultFolder, _settings.NotesSubfolder);
            var result = TodoOrganizationStore.SetInProgress(_settings.ObsidianVaultFolder, entry, !organization.IsInProgress(entry), _settings.NotesSubfolder);
            if (!result.Success) { ToolTip.SetTip(border, result.Message ?? "Could not save In progress."); return; }
            LoadNotes();
        }
        if (entry.Type == NoteType.Todo && !entry.IsCompleted)
        {
            ToolTip.SetTip(contentText, "Double-click to toggle In progress");
            contentText.AddHandler(PointerPressedEvent, (_, e) =>
            {
                if (e.Handled || e.ClickCount != 2 || !e.GetCurrentPoint(contentText).Properties.IsLeftButtonPressed) return;
                e.Handled = true; ToggleProgress();
            }, RoutingStrategies.Bubble);
        }
        if (entry.Type == NoteType.Todo)
        {
            var content = new Grid { ColumnDefinitions = new ColumnDefinitions("*,20") };
            content.Children.Add(contentText);
            if (_organization?.IsInProgress(entry) == true)
            {
                var progressIcon = Controls.TodoProgressBadge.Create(ToggleProgress);
                progressIcon.Margin = new Thickness(0, 1, 0, 0);
                Grid.SetColumn(progressIcon, 1); content.Children.Add(progressIcon);
            }
            Grid.SetColumn(content, 2); grid.Children.Add(content);
        }
        else
        {
            Grid.SetColumn(contentText, 2); grid.Children.Add(contentText);
        }

        border.Child = grid;
        DateTime? reminderDue=null;
        if (entry.Type is NoteType.Todo or NoteType.Reminder)
        {
            var menu = new ContextMenu();
            var edit = new MenuItem { Header = entry.Type == NoteType.Reminder ? "Edit reminder…" : "Edit todo…" };
            edit.Click += async (_, _) =>
            {
                menu.Close(); PauseAutoClose();
                var editor = new NoteEntryWindow(entry.Type, _settings, entry);
                await editor.ShowDialog(this);
                LoadNotes();
            };
            menu.Items.Add(edit);
            if (entry.Type == NoteType.Reminder)
            {
                var delete = new MenuItem { Header = "Delete reminder…" };
                delete.Click += async (_, _) => { menu.Close(); await DeleteReminderAsync(entry); };
                menu.Items.Add(delete);
                reminderDue = HabiticaIntegration.Current.GetDetails(_settings, entry).DueAt;
                if (reminderDue.HasValue) ToolTip.SetTip(border, "Due " + reminderDue.Value.ToString("g") + " · Right-click to edit or delete");
                else ToolTip.SetTip(border, "Right-click to edit or delete");
            }
            menu.Opened += (_, _) => PauseAutoClose();
            border.ContextMenu = menu;
        }
        if(_viewerSummary!=null)StyleViewerRow(border,entry,contentText,timeText,reminderDue);
        return border;
    }

    private void ShowMutationStatus(HabiticaMutationResult result)
    {
        string? message = !result.LocalResult.Success ? result.LocalResult.Message ?? "Could not change the task." : result.SyncWarning;
        if(_viewerStatus!=null)
        {
            _viewerStatus.Text=message;_viewerStatusScroll!.IsVisible=message!=null;
            if(message!=null)PauseAutoClose();
            return;
        }
        if (message == null) return;
        _autoCloseTimer.Stop();
        CountdownText.Text = message;
        CountdownText.TextWrapping = TextWrapping.Wrap;
    }

    private async Task DeleteReminderAsync(NoteEntry entry)
    {
        if (!IsVisible || _deleteReminderDialog != null) return;
        PauseAutoClose();
        string vault = _settings.ObsidianVaultFolder, folder = _settings.NotesSubfolder;
        string account = _settings.HabiticaUserId, client = _settings.HabiticaClientId;
        bool enabled = _settings.HabiticaEnabled;
        if (_loadedNotesContext != (vault, folder, account, client, enabled))
        {
            LoadNotes();
            ShowMutationStatus(new(new(false, "", "The notes folder or Habitica settings changed. Select the reminder again.")));
            return;
        }
        var dialog = new ConfirmationDialogWindow("Delete reminder", "Delete this reminder?",
            entry.Text + "\n\nRemoves the local reminder and its linked Habitica task. Existing Telegram reminders remain unchanged.", "Delete", destructive: true);
        dialog.ConfigureDeleteReminder(entry.Text, HabiticaIntegration.Current.GetDetails(_settings, entry).DueAt, _settings);
        _deleteReminderDialog = dialog;
        try
        {
            if (!await dialog.ShowDialog<bool>(this) || !IsVisible) return;
            if (vault != _settings.ObsidianVaultFolder || folder != _settings.NotesSubfolder || account != _settings.HabiticaUserId || client != _settings.HabiticaClientId || enabled != _settings.HabiticaEnabled)
            { ShowMutationStatus(new(new(false, "", "The notes folder or Habitica settings changed. Refresh and try again."))); return; }
            var details = HabiticaIntegration.Current.GetDetails(_settings, entry);
            var mutation = HabiticaIntegration.Current.Apply(_settings, entry, entry, details.DueAt, null, true,
                () => ObsidianNotesSync.DeleteEntry(vault, entry, folder));
            LoadNotes(); ShowMutationStatus(mutation);
        }
        finally { if (ReferenceEquals(_deleteReminderDialog, dialog)) _deleteReminderDialog = null; }
    }

    private void OnFilterChanged(object? sender, RoutedEventArgs e)
    {
        if (FilterTodosRadio?.IsChecked == true)
            _currentFilter = NoteType.Todo;
        else if (FilterNotesRadio?.IsChecked == true)
            _currentFilter = NoteType.Note;
        else if (FilterRemindersRadio?.IsChecked == true)
            _currentFilter = NoteType.Reminder;
        else
            _currentFilter = null;

        if (IsLoaded)
        {
            RenderEntries();
        }
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (!e.Handled && e.Key is (Key.Enter or Key.Escape))
        {
            e.Handled = true;
            _autoCloseTimer.Stop();
            Close();
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        _autoCloseTimer.Stop();
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _autoCloseTimer.Stop();
        _deleteReminderDialog?.Close(false);
        base.OnClosed(e);
    }
}
