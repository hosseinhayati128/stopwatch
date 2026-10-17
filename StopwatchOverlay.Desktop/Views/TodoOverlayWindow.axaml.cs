using System;
using System.IO;
using System.Collections.Generic;
using Avalonia.Platform;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;

namespace StopwatchOverlay.Desktop.Views;

public partial class TodoOverlayWindow : Window
{
    private readonly AppSettings _settings;
    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _positionTimer;
    private string? _snapshot;
    private bool _syncRunning;
    private bool _editing;
    private bool _deleteInProgress;
    private ConfirmationDialogWindow? _deleteDialog;
    private bool _closed;
    private DateTime _nextSyncUtc;
    private NoteEntry? _linkEntry;
    private string? _syncError;
    private Point _pressPoint;
    private PointerPressedEventArgs? _dragPress;
    private bool _dragMoved;
    private bool _positionReady;
    private PixelPoint _anchor;

    public bool IsExpanded { get; private set; }
    public event Action? AddTodoRequested;

    public TodoOverlayWindow() : this(new AppSettings()) { }

    public TodoOverlayWindow(AppSettings settings)
    {
        InitializeComponent();
        _settings = settings;
        PropertyChanged += (_, change) =>
        {
            if (change.Property != IsVisibleProperty || !IsVisible) return;
            // X11 window managers clear ABOVE when the window is unmapped. Avalonia
            // retains Topmost=true, so force a fresh native request after each show.
            Dispatcher.UIThread.Post(() =>
            {
                if (_closed || !IsVisible) return;
                Topmost = false;
                Topmost = true;
            }, DispatcherPriority.Background);
        };
        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _refreshTimer.Tick += async (_, _) => { ReloadTodos(); await SynchronizeTelegramAsync(); };
        ShowCompletedToggle.IsChecked = _settings.TodoOverlayShowCompleted;
        ShowCompletedToggle.IsCheckedChanged += (_, _) =>
        {
            _settings.TodoOverlayShowCompleted = ShowCompletedToggle.IsChecked == true;
            if (!SettingsStore.Save(_settings)) ShowError("Could not save your completed-task preference.");
            _snapshot = null;
            ReloadTodos();
        };
        _positionTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        _positionTimer.Tick += (_, _) => RememberDraggedPosition();
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Escape && IsExpanded) { e.Handled = true; ToggleExpanded(); }
        }, RoutingStrategies.Tunnel);
        // Observe the press before Button consumes it, without stealing a normal click.
        var header = this.FindControl<Grid>("OverlayHeader")!;
        header.AddHandler(PointerPressedEvent, HeaderPointerPressed, RoutingStrategies.Tunnel);
        header.AddHandler(PointerMovedEvent, HeaderPointerMoved, RoutingStrategies.Tunnel);
        header.AddHandler(PointerReleasedEvent, HeaderPointerReleased, RoutingStrategies.Tunnel);
        Opened += (_, _) =>
        {
            var area = (Screens.ScreenFromVisual(this) ?? Screens.Primary)?.WorkingArea;
            _anchor = _settings.TodoOverlayHasPosition
                ? new PixelPoint(_settings.TodoOverlayLeft, _settings.TodoOverlayTop)
                : new PixelPoint((area?.Right ?? 1200) - 112, (area?.Y ?? 0) + 160);
            _positionReady = true;
            KeepOnScreen();
            _refreshTimer.Start();
            ReloadTodos();
            _ = SynchronizeTelegramAsync();
        };
        PositionChanged += (_, _) =>
        {
            if (_positionReady && _dragMoved)
            {
                _positionTimer.Stop();
                _positionTimer.Start();
            }
        };
        Closed += (_, _) => { _closed = true; if (_deleteDialog?.IsVisible == true) _deleteDialog.Close(false); _refreshTimer.Stop(); _positionTimer.Stop(); };
        ApplySettings();
    }

    public void ApplySettings()
    {
        bool pirate = AppThemeCatalog.Normalize(_settings.ThemeMode) == AppThemeCatalog.Pirate;
        Classes.Set("pirate", pirate);
        SkullArtwork.IsVisible = pirate;
        PlainLauncher.IsVisible = !pirate;
        ShortcutHint.Text = $"{(_settings.NoteLeaderShortcut ?? AppSettings.DefaultNoteLeaderShortcut()).Format()} → Space · Esc to collapse";
        _snapshot = null;
        ShowCompletedToggle.IsChecked = _settings.TodoOverlayShowCompleted;
        _nextSyncUtc = DateTime.MinValue;
        ReloadTodos();
    }

    public void ToggleExpanded()
    {
        // A click immediately after dragging must use the current monitor, not the old debounced anchor.
        if (_positionTimer.IsEnabled) RememberDraggedPosition();
        IsExpanded = !IsExpanded;
        _dragMoved = false;
        Width = IsExpanded ? 360 : 88;
        Height = IsExpanded ? 430 : 88;
        TodoPanel.IsVisible = ExpandedHeading.IsVisible = HeadingSurface.IsVisible = IsExpanded;
        AutomationProperties.SetName(LauncherButton, IsExpanded ? "Collapse todos" : "Expand todos");
        ToolTip.SetTip(LauncherButton, IsExpanded ? "Click to collapse todos · Drag to move" : "Click to expand todos · Drag to move");
        if (IsExpanded)
        {
            ReloadTodos();
            _refreshTimer.Start();
            Activate();
            Focus();
        }
        if (!IsExpanded)
        {
            CloseLinkEditor();
            _editing = false;
            EditTodosButton.Content = "Edit";
            _snapshot = null;
        }
        KeepOnScreen();
    }

    public void ReloadTodos()
    {
        try
        {
            bool configured = !string.IsNullOrWhiteSpace(_settings.ObsidianVaultFolder) && Directory.Exists(_settings.ObsidianVaultFolder);
            var entries = configured
                ? ObsidianNotesSync.LoadAllNotes(_settings.ObsidianVaultFolder, _settings.NotesSubfolder)
                    .Where(entry => entry.Type == NoteType.Todo).OrderBy(entry => entry.IsCompleted).ThenByDescending(entry => entry.Timestamp).ToArray()
                : Array.Empty<NoteEntry>();
            var tracked = _settings.TelegramEnabled
                ? TelegramOutboxStore.GetTrackedTodos(TelegramNotesSync.GetBotIdentity(_settings), TelegramNotesSync.GetTodoVaultKey(_settings))
                : Array.Empty<TelegramSentNoteRecord>();
            bool Linked(NoteEntry entry) => tracked.Count(record => TelegramNotesSync.MatchesTodo(record, entry)) == 1;
            TelegramSyncStatus.IsVisible = _settings.TelegramEnabled && _linkEntry == null;
            TelegramSyncStatus.Text = _syncError ?? (_syncRunning ? "Updating Telegram…" :
                entries.Any(entry => !Linked(entry)) ? "Use Edit → Link to connect older Telegram messages." : "Completion sync is up to date.");
            var visible = entries.Where(entry => _settings.TodoOverlayShowCompleted || !entry.IsCompleted).ToArray();
            string snapshot = configured + "|" + _editing + "|" + _deleteInProgress + "|" + _settings.TodoOverlayShowCompleted + "|" + _settings.TelegramEnabled +
                string.Join("\n", entries.Select(entry => $"{entry.Timestamp:O}|{entry.IsCompleted}|{Linked(entry)}|{entry.Text}"));
            if (_snapshot == snapshot) return;
            _snapshot = snapshot;
            TodoRows.Children.Clear();
            int remaining = entries.Count(entry => !entry.IsCompleted);
            TodoCount.Text = remaining == 1 ? "1 task remaining" : $"{remaining} tasks remaining";
            EmptyMessage.IsVisible = visible.Length == 0;
            EmptyMessage.Text = !configured ? "Choose a notes folder in Settings to see your todos here."
                : entries.Length == 0 ? "No todos yet. Add your first task below." : "All done. Turn on Show completed to see finished tasks.";
            foreach (var entry in visible)
            {
                var text = Controls.TodoRichText.Create(entry.Text, entry.IsCompleted);
                text.MaxWidth = 240;
                text.Margin = new Thickness(7, 0, 0, 0);
                var row = new CheckBox { Content = text, IsChecked = entry.IsCompleted, IsEnabled = !_deleteInProgress };
                row.Classes.Add("todoRow");
                AutomationProperties.SetName(row, entry.Text);
                ToolTip.SetTip(row, entry.Timestamp.ToString("g"));
                row.IsCheckedChanged += (_, _) =>
                {
                    var result = ObsidianNotesSync.SetTodoCompleted(_settings.ObsidianVaultFolder, entry, row.IsChecked == true, _settings.NotesSubfolder);
                    ErrorMessage.IsVisible = !result.Success;
                    if (!result.Success) ShowError(result.Message ?? "Could not save this todo. Try again.");
                    _snapshot = null;
                    Dispatcher.UIThread.Post(() => { ReloadTodos(); _nextSyncUtc = DateTime.MinValue; _ = SynchronizeTelegramAsync(); });
                };
                var container = new Grid { RowDefinitions = new RowDefinitions("Auto,Auto") };
                var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 2, 0, 10), IsVisible = _editing, IsEnabled = !_deleteInProgress };
                if (_settings.TelegramEnabled)
                {
                    var linkButton = new Button { Content = Linked(entry) ? "Relink" : "Link", FontSize = 12, Height = 28, Padding = new Thickness(10, 0) };
                    linkButton.Classes.Add("modern");
                    ToolTip.SetTip(linkButton, "Link or change this todo's existing Telegram message");
                    AutomationProperties.SetName(linkButton, "Link Telegram message for " + entry.Text);
                    linkButton.Click += (_, _) => OpenLinkEditor(entry);
                    actions.Children.Add(linkButton);
                }
                var deleteButton = new Button { Content = "Delete", FontSize = 12, Height = 28, Padding = new Thickness(10, 0) };
                deleteButton.Classes.Add("danger");
                AutomationProperties.SetName(deleteButton, "Delete todo " + entry.Text);
                deleteButton.Click += async (_, _) => await DeleteTodoAsync(entry);
                actions.Children.Add(deleteButton);
                Grid.SetRow(actions, 1);
                container.Children.Add(actions);
                container.Children.Add(row);
                TodoRows.Children.Add(container);
            }
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "TodoOverlayWindow.ReloadTodos");
            ShowError("Could not read your todos. Check the notes folder in Settings.");
        }
    }

    private void EditTodosClicked(object? sender, RoutedEventArgs e)
    {
        if (_deleteInProgress) return;
        CloseLinkEditor();
        _editing = !_editing;
        EditTodosButton.Content = _editing ? "Done" : "Edit";
        AutomationProperties.SetName(EditTodosButton, _editing ? "Finish editing todo actions" : "Edit todo actions");
        _snapshot = null;
        ReloadTodos();
    }

    private async Task DeleteTodoAsync(NoteEntry entry)
    {
        if (_closed || _deleteInProgress) return;
        _deleteInProgress = true;
        EditTodosButton.IsEnabled = false;
        try
        {
            // Freeze the account and folder before presenting the confirmation.
            var settings = new AppSettings {
                TelegramEnabled = _settings.TelegramEnabled, TelegramBotToken = _settings.TelegramBotToken,
                TelegramChatId = _settings.TelegramChatId, TelegramTodosTopicId = _settings.TelegramTodosTopicId,
                ObsidianVaultFolder = _settings.ObsidianVaultFolder, NotesSubfolder = _settings.NotesSubfolder
            };
            bool linked = TelegramNotesSync.HasTodoMessageLink(settings, entry);
            string effects = linked
                ? "This removes the task from your Obsidian todo file and deletes its linked Telegram message."
                : "This removes the task from your Obsidian todo file and cancels its unsent Telegram copy. There is no linked Telegram message to delete.";
            _deleteDialog = new ConfirmationDialogWindow("Delete todo", "Delete this todo?",
                entry.Text + "\n\n" + effects + "\n\nThis cannot be undone.", "Delete", destructive: true) { Topmost = Topmost };
            bool confirmed = await _deleteDialog.ShowDialog<bool>(this);
            _deleteDialog = null;
            if (!confirmed || _closed) return;
            if (settings.TelegramBotToken != _settings.TelegramBotToken ||
                TelegramNotesSync.GetTodoVaultKey(settings) != TelegramNotesSync.GetTodoVaultKey(_settings))
            {
                ShowError("Your Telegram account or notes folder changed. Review the todo and choose Delete again.");
                return;
            }
            _snapshot = null;
            ReloadTodos();
            EditTodosButton.Content = "Deleting…";
            var result = await Task.Run(() => TelegramNotesSync.DeleteTodoEverywhereAsync(settings, entry));
            if (_closed) return;
            ErrorMessage.IsVisible = !result.Success;
            if (!result.Success) ShowError(result.ErrorMessage ?? "Could not finish deleting the todo. Try again.");
            _nextSyncUtc = DateTime.MinValue;
        }
        catch (Exception)
        {
            if (!_closed) ShowError("Deletion could not finish. Refresh and try again.");
        }
        finally
        {
            _deleteDialog = null;
            _deleteInProgress = false;
            if (!_closed)
            {
                EditTodosButton.IsEnabled = true;
                EditTodosButton.Content = _editing ? "Done" : "Edit";
                _snapshot = null;
                ReloadTodos();
            }
        }
    }

    private async Task SynchronizeTelegramAsync()
    {
        if (_closed || _syncRunning || _deleteInProgress || !_settings.TelegramEnabled || DateTime.UtcNow < _nextSyncUtc) return;
        _syncRunning = true;
        // Keep one coherent account/destination while an HTTP request is in flight.
        var settings = new AppSettings {
            TelegramEnabled = _settings.TelegramEnabled, TelegramBotToken = _settings.TelegramBotToken,
            TelegramChatId = _settings.TelegramChatId, TelegramTodosTopicId = _settings.TelegramTodosTopicId,
            TelegramNotesTopicId = _settings.TelegramNotesTopicId, TelegramRemindersTopicId = _settings.TelegramRemindersTopicId,
            ObsidianVaultFolder = _settings.ObsidianVaultFolder, NotesSubfolder = _settings.NotesSubfolder
        };
        try
        {
            var result = await Task.Run(() => TelegramNotesSync.SyncTodoCompletionsAsync(settings));
            if (_closed) return;
            _syncError = result.Success ? null : result.ErrorMessage;
            _nextSyncUtc = DateTime.UtcNow.AddSeconds(result.Success ? 2 : 15);
            ReloadTodos();
        }
        catch (Exception)
        {
            _syncError = "Telegram sync will retry automatically.";
            _nextSyncUtc = DateTime.UtcNow.AddSeconds(15);
            if (!_closed) ReloadTodos();
        }
        finally { _syncRunning = false; if (!_closed) ReloadTodos(); }
    }

    private void OpenLinkEditor(NoteEntry entry)
    {
        _linkEntry = entry;
        LinkTaskText.Text = "Link Telegram message for: " + entry.Text;
        TelegramMessageLink.Text = "";
        TodoContentGrid.RowDefinitions = new RowDefinitions("Auto,0,*,Auto,Auto,Auto");
        TodoScrollViewer.IsVisible = TodoOptionsRow.IsVisible = AddTodoButton.IsVisible = ShortcutHint.IsVisible = false;
        TelegramSyncStatus.IsVisible = false;
        TelegramLinkPanel.IsVisible = true;
        TelegramMessageLink.Focus();
    }
    private void ConfirmLinkClicked(object? sender, RoutedEventArgs e)
    {
        if (_linkEntry == null) return;
        var result = TelegramNotesSync.LinkTodoMessage(_settings, _linkEntry, TelegramMessageLink.Text ?? "");
        ErrorMessage.IsVisible = !result.Success;
        if (!result.Success) { ShowError(result.ErrorMessage ?? "Could not save this message link."); return; }
        CloseLinkEditor();
        _nextSyncUtc = DateTime.MinValue;
        _snapshot = null;
        ReloadTodos();
        _ = SynchronizeTelegramAsync();
    }
    private void CloseLinkEditor()
    {
        TelegramLinkPanel.IsVisible = false;
        _linkEntry = null;
        TodoContentGrid.RowDefinitions = new RowDefinitions("Auto,*,Auto,Auto,Auto,Auto");
        TodoScrollViewer.IsVisible = TodoOptionsRow.IsVisible = AddTodoButton.IsVisible = ShortcutHint.IsVisible = true;
        TelegramSyncStatus.IsVisible = _settings.TelegramEnabled;
        if (IsExpanded) Focus();
    }
    private void CancelLinkClicked(object? sender, RoutedEventArgs e) => CloseLinkEditor();

    private void ShowError(string message) { ErrorMessage.Text = message; ErrorMessage.IsVisible = true; }
    private void ToggleClicked(object? sender, RoutedEventArgs e) { if (!_dragMoved) ToggleExpanded(); }
    private void AddClicked(object? sender, RoutedEventArgs e) => AddTodoRequested?.Invoke();
    private void HeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pressPoint = e.GetPosition(this);
        _dragPress = e;
        _dragMoved = false;
    }
    private void HeaderPointerMoved(object? sender, PointerEventArgs e)
    {
        var delta = e.GetPosition(this) - _pressPoint;
        if (_dragMoved || _dragPress == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed || delta.X * delta.X + delta.Y * delta.Y < 36) return;
        _dragMoved = true;
        BeginMoveDrag(_dragPress);
        e.Handled = true;
    }
    private void HeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragMoved) return;
        _positionTimer.Stop();
        _positionTimer.Start();
        e.Handled = true;
    }
    private void RememberDraggedPosition()
    {
        _positionTimer.Stop();
        _anchor = Position;
        _settings.TodoOverlayHasPosition = true;
        _settings.TodoOverlayLeft = Position.X;
        _settings.TodoOverlayTop = Position.Y;
        if (!SettingsStore.Save(_settings)) ShowError("Could not remember the overlay position.");
    }

    private void KeepOnScreen()
    {
        if (_positionReady) Position = ClampToScreens(_anchor, new Size(Width, Height), Screens.All);
    }

    internal static PixelPoint ClampToScreens(PixelPoint anchor, Size logicalSize, IReadOnlyList<Screen> screens)
    {
        // A top-edge launcher may overlap the system panel: choose by full monitor bounds first.
        var screen = screens.FirstOrDefault(screen => anchor.X >= screen.Bounds.X && anchor.X < screen.Bounds.Right &&
            anchor.Y >= screen.Bounds.Y && anchor.Y < screen.Bounds.Bottom) ??
            screens.OrderBy(screen => DistanceToScreen(anchor, screen.Bounds)).FirstOrDefault();
        if (screen == null) return anchor;
        var area = screen.WorkingArea;
        int width = (int)Math.Ceiling(logicalSize.Width * screen.Scaling);
        int height = (int)Math.Ceiling(logicalSize.Height * screen.Scaling);
        return new PixelPoint(Math.Clamp(anchor.X, area.X, Math.Max(area.X, area.Right - width)),
                              Math.Clamp(anchor.Y, area.Y, Math.Max(area.Y, area.Bottom - height)));
    }

    private static double DistanceToScreen(PixelPoint anchor, PixelRect bounds)
    {
        double dx = (double)anchor.X - Math.Clamp(anchor.X, bounds.X, bounds.Right);
        double dy = (double)anchor.Y - Math.Clamp(anchor.Y, bounds.Y, bounds.Bottom);
        return dx * dx + dy * dy;
    }
}
