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
using ShapePath = Avalonia.Controls.Shapes.Path;
using StopwatchOverlay.Desktop.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StopwatchOverlay.Desktop.Platform;
using StopwatchOverlay.Habitica;

namespace StopwatchOverlay.Desktop.Views;

public partial class TodoOverlayWindow : Window
{
    private readonly AppSettings _settings;
    private bool _loadingOrganization;
    private TodoOrganization? _organization;
    private NoteEntry? _draggedEntry;
    private Grid? _draggedRow;
    private Grid? _dropRow;
    private NoteEntry? _dropEntry;
    private Point _dragOrigin;
    private bool _reordering;
    private bool _dropAfter;
    private IPointer? _dragPointer;

    private readonly DispatcherTimer _refreshTimer;
    private readonly DispatcherTimer _positionTimer;
    private string? _snapshot;
    private bool _syncRunning;
    private ContextMenu? _openTaskMenu;
    private bool _deleteInProgress;
    private ConfirmationDialogWindow? _deleteDialog;
    private NoteEntryWindow? _taskEditor;
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
        if (OperatingSystem.IsWindows()) SizeChanged += (_, _) => UpdateExpandedVisibility();
        WindowsTopmostGuard.Attach(this);
        OverlayScreenConstraint.Attach(this);
        _settings = settings;
        TodoSortCombo.ItemsSource = new[] { "Priority", "Manual" };
        TodoSortCombo.SelectionChanged += (_, _) =>
        {
            if (_loadingOrganization || TodoSortCombo.SelectedItem is not string mode) return;
            SaveOrganization(TodoOrganizationStore.SetSortMode(_settings.ObsidianVaultFolder, mode, _settings.NotesSubfolder));
            TodoScrollViewer.Offset = default;
        };
        Deactivated += (_, _) => CancelInteractions();
        TodoScrollViewer.ScrollChanged += (_, e) =>
        {
            if (e.OffsetDelta == default) return;
            foreach (var row in TodoRows.GetVisualDescendants().OfType<HoldToCompleteCheckBox>()) row.CancelHold();
        };
        TodoScrollViewer.AddHandler(PointerWheelChangedEvent, (_, _) =>
        {
            foreach (var row in TodoRows.GetVisualDescendants().OfType<HoldToCompleteCheckBox>()) row.CancelHold();
        }, RoutingStrategies.Tunnel);
        PropertyChanged += (_, change) =>
        {
            if (change.Property != IsVisibleProperty) return;
            if (!IsVisible) { _openTaskMenu?.Close(); CancelInteractions(); return; }
            // X11 window managers clear ABOVE when the window is unmapped. Avalonia
            // retains Topmost=true, so force a fresh native request after each show.
            Dispatcher.UIThread.Post(() =>
            {
                if (_closed || !IsVisible) return;
                if (!OperatingSystem.IsWindows())
                {
                    Topmost = false;
                    Topmost = true;
                }
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
        Closed += (_, _) => { _closed = true; _openTaskMenu?.Close(); if (_taskEditor?.IsVisible == true) _taskEditor.Close(); if (_deleteDialog?.IsVisible == true) _deleteDialog.Close(false); _refreshTimer.Stop(); _positionTimer.Stop(); };
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
        Width = IsExpanded ? 400 : 88;
        Height = IsExpanded ? 500 : 88;
        UpdateExpandedVisibility();
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
            CancelInteractions();
            CloseLinkEditor();
            _openTaskMenu?.Close();
            _snapshot = null;
        }
        KeepOnScreen();
    }

    private void UpdateExpandedVisibility()
    {
        // Windows delivers its native resize asynchronously. Do not measure the
        // expanded controls inside the launcher's 88px client area while it resizes.
        bool visible = IsExpanded && (!OperatingSystem.IsWindows() ||
            (ClientSize.Width >= Width - 1 && Bounds.Width >= Width - 1));
        TodoPanel.IsVisible = ExpandedHeading.IsVisible = HeadingSurface.IsVisible = visible;
    }

    public void ReloadTodos()
    {
        try
        {
            if (_openTaskMenu?.IsOpen == true || _draggedEntry != null || TodoRows.GetVisualDescendants().OfType<HoldToCompleteCheckBox>().Any(row => row.IsHolding)) return;
            bool configured = !string.IsNullOrWhiteSpace(_settings.ObsidianVaultFolder) && Directory.Exists(_settings.ObsidianVaultFolder);
            var entries = configured
                ? ObsidianNotesSync.LoadAllNotes(_settings.ObsidianVaultFolder, _settings.NotesSubfolder)
                    .Where(entry => entry.Type == NoteType.Todo).ToArray()
                : Array.Empty<NoteEntry>();
            _organization = configured ? TodoOrganizationStore.Load(_settings.ObsidianVaultFolder, _settings.NotesSubfolder) : null;
            entries = _organization?.Arrange(entries) ?? entries;
            _loadingOrganization = true;
            try { TodoSortCombo.IsEnabled = configured && !_deleteInProgress; TodoSortCombo.SelectedItem = _organization?.SortMode ?? "Priority"; }
            finally { _loadingOrganization = false; }
            var tracked = _settings.TelegramEnabled
                ? TelegramOutboxStore.GetTrackedTodos(TelegramNotesSync.GetBotIdentity(_settings), TelegramNotesSync.GetTodoVaultKey(_settings))
                : Array.Empty<TelegramSentNoteRecord>();
            bool Linked(NoteEntry entry) => tracked.Count(record => TelegramNotesSync.MatchesTodo(record, entry)) == 1;
            TelegramSyncStatus.IsVisible = _settings.TelegramEnabled && _linkEntry == null;
            TelegramSyncStatus.Text = _syncError ?? (_syncRunning ? "Updating Telegram…" :
                entries.Any(entry => !Linked(entry)) ? "Right-click a task → Link to connect older Telegram messages." : "Completion sync is up to date.");
            var visible = entries.Where(entry => _settings.TodoOverlayShowCompleted || !entry.IsCompleted).ToArray();
            var taskDetails = new Dictionary<NoteEntry, HabiticaTaskDetails>();
            foreach (var entry in entries) taskDetails[entry] = HabiticaIntegration.Current.GetDetails(_settings, entry);
            string snapshot = _settings.ObsidianVaultFolder + "|" + _settings.NotesSubfolder + "|" + DetailAccount() + "|" + (_organization?.SortMode ?? "Priority") + "|" + configured + "|" + _deleteInProgress + "|" + _settings.TodoOverlayShowCompleted + "|" + _settings.TelegramEnabled +
                string.Join("\n", entries.Select(entry => $"{entry.Timestamp:O}|{entry.IsCompleted}|{Linked(entry)}|{_organization?.GetPriority(entry) ?? 3}|{_organization?.IsInProgress(entry)}|{_organization?.GetDueAt(entry):O}|{TodoDeadlines.GetState(entry, _organization?.GetDueAt(entry), DateTime.Now)}|{entry.Text}"));
            snapshot += System.Text.Json.JsonSerializer.Serialize(taskDetails.Values.Select(details => details.Children));
            if (_snapshot == snapshot) return;
            _snapshot = snapshot;
            var scrollOffset = TodoScrollViewer.Offset;
            TodoRows.Children.Clear();
            int remaining = entries.Count(entry => !entry.IsCompleted);
            TodoCount.Text = remaining == 1 ? "1 task remaining" : $"{remaining} tasks remaining";
            EmptyMessage.IsVisible = visible.Length == 0;
            EmptyMessage.Text = !configured ? "Choose a notes folder in Settings to see your todos here."
                : entries.Length == 0 ? "No todos yet. Add your first task below." : "All done. Turn on Show completed to see finished tasks.";
            foreach (var entry in visible)
            {
                var text = Controls.TodoRichText.Create(TodoDetails.Parse(entry.Text).Body, entry.IsCompleted);
                text.MaxWidth = _organization?.SortMode == "Manual" ? 210 : 230;
                text.Margin = new Thickness(7, 0, 0, 0);
                var row = new HoldToCompleteCheckBox { Content = text, IsChecked = entry.IsCompleted, IsEnabled = !_deleteInProgress };
                row.Classes.Add("todoRow");
                AutomationProperties.SetName(row, (_organization?.IsInProgress(entry) == true ? "In progress: " : "") + entry.Text);
                row.ProgressToggleRequested += (_, _) => ToggleProgress(entry);
                ToolTip.SetTip(row, "Hold for 1 second to " + (entry.IsCompleted ? "mark incomplete" : "complete") + (entry.IsCompleted ? " · " : " · Double-click to toggle In progress · ") + entry.Timestamp.ToString("g"));
                row.IsCheckedChanged += (_, _) =>
                {
                    bool completed = row.IsChecked == true;
                    var mutation = HabiticaIntegration.Current.Apply(_settings, entry, entry with { IsCompleted = completed },
                        _organization?.GetDueAt(entry), null, false,
                        () => ObsidianNotesSync.SetTodoCompleted(_settings.ObsidianVaultFolder, entry, completed, _settings.NotesSubfolder));
                    ErrorMessage.IsVisible = !mutation.LocalResult.Success || mutation.SyncWarning != null;
                    if (!mutation.LocalResult.Success) ShowError(mutation.LocalResult.Message ?? "Could not save this todo. Try again.");
                    else if (mutation.SyncWarning != null) ShowError(mutation.SyncWarning);
                    _snapshot = null;
                    Dispatcher.UIThread.Post(() => { ReloadTodos(); _nextSyncUtc = DateTime.MinValue; _ = SynchronizeTelegramAsync(); });
                };
                var container = new Grid { Tag = entry, RowDefinitions = new RowDefinitions("Auto,Auto,Auto,Auto,Auto"), ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,20"), Margin = new Thickness(0, 0, 0, 6) };
                DateTime? dueAt = _organization?.GetDueAt(entry);
                var dueState = TodoDeadlines.GetState(entry, dueAt, DateTime.Now);
                if (dueState is TodoDueState.Soon or TodoDueState.Overdue)
                {
                    bool dark = AppThemeCatalog.Normalize(_settings.ThemeMode) == AppThemeCatalog.Midnight;
                    bool overdue = dueState == TodoDueState.Overdue;
                    var deadlineSurface = new Border { Name = "DeadlineSurface", CornerRadius = new CornerRadius(5),
                        Background = new SolidColorBrush(Color.Parse(overdue ? (dark ? "#30F08080" : "#20C42635") : (dark ? "#32D5972C" : "#25B87816"))),
                        BorderBrush = new SolidColorBrush(Color.Parse(overdue ? (dark ? "#806E4343" : "#607E3030") : (dark ? "#807D622A" : "#60865F24"))),
                        BorderThickness = new Thickness(1), Margin = new Thickness(-4, 0, -4, 0), IsHitTestVisible = false };
                    Grid.SetRowSpan(deadlineSurface, 5); Grid.SetColumnSpan(deadlineSurface, 4);
                    container.Children.Add(deadlineSurface);
                }
                if (dueAt.HasValue)
                {
                    var dueLabel = new TextBlock { Name = "TodoDueLabel", Text = TodoDeadlines.Label(dueAt.Value, dueState), FontSize = 11,
                        TextWrapping = TextWrapping.Wrap, Margin = new Thickness(7, 0, 0, 7) };
                    if (Application.Current?.TryFindResource(dueState == TodoDueState.Overdue ? "DangerTextBrush" : "SecondaryTextBrush", out var dueBrush) == true && dueBrush is IBrush brush) dueLabel.Foreground = brush;
                    Grid.SetRow(dueLabel, 2); Grid.SetColumn(dueLabel, 2); container.Children.Add(dueLabel);
                    ToolTip.SetTip(row, "Hold for 1 second to " + (entry.IsCompleted ? "mark incomplete" : "complete") + (entry.IsCompleted ? " · " : " · Double-click to toggle In progress · ") + dueLabel.Text);
                }
                if (_organization?.IsInProgress(entry) == true)
                {
                    var statusBadge = TodoProgressBadge.Create(() => ToggleProgress(entry));
                    statusBadge.Margin = new Thickness(0, 9, 0, 0);
                    Grid.SetRow(statusBadge, 0); Grid.SetColumn(statusBadge, 3);
                    container.Children.Add(statusBadge);
                }
                var badge = CreatePriorityBadge(entry);
                Grid.SetColumn(badge, 0);
                container.Children.Add(badge);
                if (_organization?.SortMode == "Manual")
                {
                    var handle = CreateDragHandle(entry, container);
                    Grid.SetColumn(handle, 1);
                    container.Children.Add(handle);
                }
                Grid.SetColumn(row, 2);
                var progress = new ProgressBar { Minimum = 0, Maximum = 1, Height = 3, Margin = new Thickness(7, 0, 0, 0), IsHitTestVisible = false, Background = Brushes.Transparent };
                progress.Bind(ProgressBar.ValueProperty, row.GetObservable(HoldToCompleteCheckBox.HoldProgressProperty));
                progress.Foreground = PriorityBrush(_organization?.GetPriority(entry) ?? 3);
                Grid.SetColumn(progress, 2); Grid.SetRow(progress, 1);
                container.Children.Add(progress);
                container.ContextMenu = CreateTaskContextMenu(entry, Linked(entry));
                row.ContextMenu = CreateTaskContextMenu(entry, Linked(entry));
                text.ContextMenu = CreateTaskContextMenu(entry, Linked(entry), text.ContextMenu);
                container.Children.Add(row);
                AddDetailControls(container, entry, TodoDetails.Parse(entry.Text), taskDetails[entry].Children);
                TodoRows.Children.Add(container);
            }
            TodoScrollViewer.Offset = scrollOffset;
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "TodoOverlayWindow.ReloadTodos");
            ShowError("Could not read your todos. Check the notes folder in Settings.");
        }
    }

    private static readonly string[] PriorityTitles = Enumerable.Range(1, 10).Select(level => level.ToString()).ToArray();

    private IBrush PriorityBrush(int priority)
    {
        bool dark = ActualThemeVariant == Avalonia.Styling.ThemeVariant.Dark;
        string[] colors = dark
            ? new[] { "#FF8A83", "#FFA279", "#FFBC75", "#E0CC7C", "#A6D59A", "#86D4CB", "#91C1FF", "#B0B7F0", "#C4B3DA", "#BBC2CD" }
            : new[] { "#A6202D", "#A13C20", "#944900", "#776000", "#3C7136", "#216F69", "#245BA6", "#5258A0", "#72538B", "#4C535C" };
        return new SolidColorBrush(Color.Parse(colors[Math.Clamp(priority, 1, 10) - 1]));
    }

    private Button CreatePriorityBadge(NoteEntry entry)
    {
        int priority = _organization?.GetPriority(entry) ?? 3;
        var color = PriorityBrush(priority);
        var label = new TextBlock { Text = PriorityTitles[priority - 1], FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = color, HorizontalAlignment = HorizontalAlignment.Center };
        var badge = new Button { IsEnabled = !_deleteInProgress, Content = label, Width = 30, Height = 26, MinHeight = 26, Padding = new Thickness(3, 1), Margin = new Thickness(0, 3, 7, 0), VerticalAlignment = VerticalAlignment.Top };
        badge.Classes.Add("modern"); badge.Classes.Add("priorityBadge");
        AutomationProperties.SetName(badge, "Priority " + priority + ": " + PriorityTitles[priority - 1] + ". Change priority for " + entry.Text);
        ToolTip.SetTip(badge, "Priority " + priority + " · 1 is highest · Click to change");
        var menu = new ContextMenu();
        for (int level = 1; level <= 10; level++)
        {
            int selected = level;
            var item = new MenuItem { Header = PriorityTitles[level - 1], Foreground = PriorityBrush(level) };
            item.Click += (_, _) => { menu.Close(); SaveOrganization(TodoOrganizationStore.SetPriority(_settings.ObsidianVaultFolder, entry, selected, _settings.NotesSubfolder)); };
            menu.Items.Add(item);
        }
        TrackTaskMenu(menu);
        badge.ContextMenu = menu;
        badge.Click += (_, _) => menu.Open(badge);
        return badge;
    }

    private void ToggleProgress(NoteEntry entry)
    {
        if (_closed || _deleteInProgress || entry.IsCompleted) return;
        CrashLogger.RecordUiAction("Toggle todo progress");
        var organization = TodoOrganizationStore.Load(_settings.ObsidianVaultFolder, _settings.NotesSubfolder);
        SaveOrganization(TodoOrganizationStore.SetInProgress(_settings.ObsidianVaultFolder, entry,
            !organization.IsInProgress(entry), _settings.NotesSubfolder));
    }

    private void SaveOrganization(NoteSyncResult result)
    {
        if (!result.Success) { ShowError(result.Message ?? "Could not save todo organization."); return; }
        ErrorMessage.IsVisible = false;
        _snapshot = null;
        ReloadTodos();
    }

    private Button CreateDragHandle(NoteEntry entry, Grid container)
    {
        var icon = new ShapePath { Data = Geometry.Parse("M2,2 L3,2 M7,2 L8,2 M2,7 L3,7 M7,7 L8,7 M2,12 L3,12 M7,12 L8,12"), StrokeThickness = 2, StrokeLineCap = PenLineCap.Round, Width = 9, Height = 14, Stretch = Stretch.Uniform };
        icon.Stroke = PriorityBrush(4);
        var handle = new Button { IsEnabled = !_deleteInProgress, Content = icon, Width = 18, Padding = new Thickness(3, 6), Margin = new Thickness(0, 5, 3, 0), Background = Brushes.Transparent, BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Top };
        handle.Classes.Add("todoDragHandle");
        AutomationProperties.SetName(handle, "Reorder " + entry.Text);
        ToolTip.SetTip(handle, "Drag to reorder · Up/Down also moves this task");
        handle.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (!e.GetCurrentPoint(handle).Properties.IsLeftButtonPressed) return;
            _dragPointer = e.Pointer; _draggedEntry = entry; _draggedRow = container; _dragOrigin = e.GetPosition(this);
            _reordering = false;
            e.Pointer.Capture(handle); e.Handled = true;
        }, RoutingStrategies.Tunnel);
        handle.AddHandler(PointerMovedEvent, (_, e) =>
        {
            if (_draggedEntry == null) return;
            var delta = e.GetPosition(this) - _dragOrigin;
            if (Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5 && !_reordering) return;
            _reordering = true; container.Opacity = .55;
            UpdateDropTarget(entry, container, e.GetPosition(TodoScrollViewer), e.GetPosition(TodoRows));
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
        handle.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (_draggedEntry != null)
                UpdateDropTarget(_draggedEntry, container, e.GetPosition(TodoScrollViewer), e.GetPosition(TodoRows));
            var source = _draggedEntry; var target = _dropEntry; bool after = _dropAfter; bool moved = _reordering;
            CancelDrag(); e.Pointer.Capture(null); e.Handled = true;
            if (moved && source != null && target != null) MoveTodo(source, target, after);
        }, RoutingStrategies.Tunnel);
        handle.PointerCaptureLost += (_, _) => CancelDrag();
        handle.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Up && e.Key != Key.Down) return;
            var group = TodoRows.Children.OfType<Grid>().Select(row => row.Tag).OfType<NoteEntry>().Where(item => item.IsCompleted == entry.IsCompleted).ToArray();
            int current = Array.IndexOf(group, entry), target = current + (e.Key == Key.Up ? -1 : 1);
            if (current >= 0 && target >= 0 && target < group.Length) MoveTodo(entry, group[target], e.Key == Key.Down);
            e.Handled = true;
        };
        container.Tag = entry;
        return handle;
    }

    private void UpdateDropTarget(NoteEntry source, Grid sourceRow, Point viewportPoint, Point rowPoint)
    {
        if (_dropRow != null) _dropRow.Background = Brushes.Transparent;
        _dropRow = null; _dropEntry = null;
        if (!new Rect(TodoScrollViewer.Bounds.Size).Contains(viewportPoint)) return;
        foreach (var candidate in TodoRows.Children.OfType<Grid>())
        {
            if (candidate.Tag is not NoteEntry target || target.IsCompleted != source.IsCompleted || candidate == sourceRow) continue;
            if (!candidate.Bounds.Contains(rowPoint)) continue;
            _dropRow = candidate; _dropEntry = target; _dropAfter = rowPoint.Y > candidate.Bounds.Center.Y;
            candidate.Background = new SolidColorBrush(Color.FromArgb(30, 85, 140, 190));
            break;
        }
    }

    private void MoveTodo(NoteEntry source, NoteEntry target, bool after)
    {
        try
        {
            var entries = ObsidianNotesSync.LoadAllNotes(_settings.ObsidianVaultFolder, _settings.NotesSubfolder).Where(item => item.Type == NoteType.Todo);
            var ordered = TodoOrganizationStore.Load(_settings.ObsidianVaultFolder, _settings.NotesSubfolder).Arrange(entries).ToList();
            string sourceKey = TodoOrganizationStore.GetKey(source), targetKey = TodoOrganizationStore.GetKey(target);
            if (ordered.Count(item => TodoOrganizationStore.GetKey(item) == sourceKey) != 1 || ordered.Count(item => TodoOrganizationStore.GetKey(item) == targetKey) != 1)
            { ShowError("These tasks changed. Refresh and try reordering again."); return; }
            var moving = ordered.Single(item => TodoOrganizationStore.GetKey(item) == sourceKey);
            ordered.Remove(moving);
            int index = ordered.FindIndex(item => TodoOrganizationStore.GetKey(item) == targetKey);
            ordered.Insert(index + (after ? 1 : 0), moving);
            var result = TodoOrganizationStore.SetManualOrder(_settings.ObsidianVaultFolder, ordered, _settings.NotesSubfolder);
            SaveOrganization(result);
            if (result.Success)
            {
                var movedRow = TodoRows.Children.OfType<Grid>().FirstOrDefault(row => row.Tag is NoteEntry item && TodoOrganizationStore.GetKey(item) == sourceKey);
                movedRow?.GetVisualDescendants().OfType<Button>().FirstOrDefault(button => button.Classes.Contains("todoDragHandle"))?.Focus();
            }
        }
        catch (Exception ex) { CrashLogger.LogRecoverable(ex, "TodoOverlayWindow.MoveTodo"); ShowError("Could not reorder todos. Check the notes folder and try again."); }
    }

    private void CancelDrag()
    {
        if (_draggedRow != null) _draggedRow.Opacity = 1;
        if (_dropRow != null) _dropRow.Background = Brushes.Transparent;
        _draggedEntry = null; _draggedRow = null; _dropRow = null; _dropEntry = null; _reordering = false;
        var pointer = _dragPointer; _dragPointer = null;
        pointer?.Capture(null);
    }

    private void CancelInteractions()
    {
        foreach (var row in TodoRows.GetVisualDescendants().OfType<HoldToCompleteCheckBox>()) row.CancelHold(resetKeyboard: true);
        CancelDrag();
    }

    private ContextMenu CreateTaskContextMenu(NoteEntry entry, bool linked, ContextMenu? menu = null)
    {
        menu ??= new ContextMenu();
        if (menu.Items.Count > 0) menu.Items.Add(new Separator());
        var edit = new MenuItem { Header = "Edit task" };
        AutomationProperties.SetName(edit, "Edit todo " + entry.Text);
        edit.Click += async (_, e) => { e.Handled = true; menu.Close(); await EditTaskAsync(entry); };
        menu.Items.Add(edit);
        string detailVault = _settings.ObsidianVaultFolder;
        string? detailSubfolder = _settings.NotesSubfolder;
        string detailAccount = DetailAccount();
        var add = new MenuItem { Header = "Add" };
        foreach (var kind in new[] { TodoDetailKind.Note, TodoDetailKind.Subtask })
        {
            var item = new MenuItem { Header = kind.ToString() };
            item.Click += async (_, e) =>
            {
                e.Handled = true; menu.Close();
                if (detailVault != _settings.ObsidianVaultFolder || detailSubfolder != _settings.NotesSubfolder || detailAccount != DetailAccount())
                { DetailConflict(); return; }
                await EditDetailAsync(entry, kind);
            };
            add.Items.Add(item);
        }
        menu.Items.Add(add);
        if (_settings.TelegramEnabled)
        {
            var link = new MenuItem { Header = linked ? "Relink" : "Link" };
            link.Click += (_, e) => { e.Handled = true; menu.Close(); OpenLinkEditor(entry); };
            menu.Items.Add(link);
        }
        var delete = new MenuItem { Header = "Delete" };
        if (Application.Current?.TryFindResource("DangerTextBrush", out var danger) == true && danger is IBrush brush) delete.Foreground = brush;
        AutomationProperties.SetName(delete, "Delete todo " + entry.Text);
        delete.Click += async (_, e) => { e.Handled = true; menu.Close(); await DeleteTodoAsync(entry); };
        menu.Items.Add(delete);
        TrackTaskMenu(menu);
        return menu;
    }

    private void TrackTaskMenu(ContextMenu menu)
    {
        menu.Opening += (_, e) =>
        {
            if (_closed || _deleteInProgress) { e.Cancel = true; return; }
            if (_openTaskMenu != menu) _openTaskMenu?.Close();
            CancelInteractions();
        };
        menu.Opened += (_, _) => _openTaskMenu = menu;
        menu.Closed += (_, _) =>
        {
            if (_openTaskMenu == menu) _openTaskMenu = null;
            Dispatcher.UIThread.Post(() => { if (!_closed) ReloadTodos(); });
        };
    }

    private async Task EditTaskAsync(NoteEntry entry)
    {
        if (_closed || _deleteInProgress) return;
        if (_taskEditor?.IsVisible == true) { _taskEditor.Activate(); return; }
        CancelInteractions();
        _taskEditor = new NoteEntryWindow(NoteType.Todo, _settings, entry);
        try { await _taskEditor.ShowDialog(this); }
        finally
        {
            _taskEditor = null;
            if (!_closed)
            {
                _snapshot = null; _nextSyncUtc = DateTime.MinValue;
                ReloadTodos(); _ = SynchronizeTelegramAsync();
            }
        }
    }

    private async Task DeleteTodoAsync(NoteEntry entry)
    {
        if (_closed || _deleteInProgress) return;
        _deleteInProgress = true;
        try
        {
            // Freeze the account and folder before presenting the confirmation.
            var settings = new AppSettings {
                TelegramEnabled = _settings.TelegramEnabled, TelegramBotToken = _settings.TelegramBotToken,
                TelegramChatId = _settings.TelegramChatId, TelegramTodosTopicId = _settings.TelegramTodosTopicId,
                ObsidianVaultFolder = _settings.ObsidianVaultFolder, NotesSubfolder = _settings.NotesSubfolder,
                HabiticaEnabled = _settings.HabiticaEnabled, HabiticaUserId = _settings.HabiticaUserId, HabiticaClientId = _settings.HabiticaClientId
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
                TelegramNotesSync.GetTodoVaultKey(settings) != TelegramNotesSync.GetTodoVaultKey(_settings) ||
                settings.HabiticaUserId != _settings.HabiticaUserId || settings.HabiticaClientId != _settings.HabiticaClientId ||
                settings.HabiticaEnabled != _settings.HabiticaEnabled)
            {
                ShowError("Your sync account or notes folder changed. Review the todo and choose Delete again.");
                return;
            }
            _snapshot = null;
            ReloadTodos();
            var result = await Task.Run(() => TelegramNotesSync.DeleteTodoEverywhereAsync(settings, entry));
            if (_closed) return;
            ErrorMessage.IsVisible = !result.Success || result.ErrorMessage != null;
            if (!result.Success) ShowError(result.ErrorMessage ?? "Could not finish deleting the todo. Try again.");
            else if (result.ErrorMessage != null) ShowError(result.ErrorMessage);
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
        Position = ClampToScreens(Position, Bounds.Size, Screens.All);
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
        => OverlayScreenConstraint.ClampToScreens(anchor, logicalSize, screens);
}
