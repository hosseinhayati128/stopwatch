using System.Diagnostics;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using StopwatchOverlay.Desktop.Controls;
using ShapePath = Avalonia.Controls.Shapes.Path;

namespace StopwatchOverlay.Desktop.Views;

public partial class TodoOverlayWindow
{
    private readonly DispatcherTimer _reorderScrollTimer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    private NoteEntry? _draggedEntry;
    private Grid? _draggedRow;
    private NoteEntry? _dropEntry;
    private Point _dragOrigin, _dragPosition;
    private bool _reordering, _dropAfter, _dragTitleDoubleClick;
    private IPointer? _dragPointer;
    private string? _dragVault, _dragSubfolder;
    private long _lastDragTick;

    private Button CreateDragHandle(NoteEntry entry, Grid container)
    {
        var icon = new ShapePath { Data = Geometry.Parse("M2,2 L3,2 M7,2 L8,2 M2,7 L3,7 M7,7 L8,7 M2,12 L3,12 M7,12 L8,12"),
            StrokeThickness = 2, StrokeLineCap = PenLineCap.Round, Width = 9, Height = 14, Stretch = Stretch.Uniform, Stroke = PriorityBrush(4) };
        var handle = new Button { IsEnabled = !_deleteInProgress, Content = icon, Width = 26, Height = 38,
            Padding = new Thickness(8, 6), Margin = new Thickness(0, 2, 2, 0), Background = Brushes.Transparent,
            BorderThickness = new Thickness(0), VerticalAlignment = VerticalAlignment.Top, Cursor = new Cursor(StandardCursorType.SizeAll) };
        handle.Classes.Add("todoDragHandle");
        AutomationProperties.SetName(handle, "Reorder " + entry.Text);
        ToolTip.SetTip(handle, "Drag this handle or the task title to reorder · Hold near an edge to scroll · Up/Down also moves this task");
        handle.AddHandler(PointerPressedEvent, (_, e) => BeginCardDrag(entry, container, e, false), RoutingStrategies.Tunnel);
        handle.KeyDown += (_, e) =>
        {
            if (e.Key != Key.Up && e.Key != Key.Down) return;
            CancelDrag();
            var group = TodoRows.Children.OfType<Grid>().Select(row => row.Tag).OfType<NoteEntry>().Where(item => item.IsCompleted == entry.IsCompleted).ToArray();
            int current = Array.IndexOf(group, entry), target = current + (e.Key == Key.Up ? -1 : 1);
            if (current >= 0 && target >= 0 && target < group.Length) MoveTodo(entry, group[target], e.Key == Key.Down);
            e.Handled = true;
        };
        return handle;
    }

    private void AttachCardDrag(Grid container, HoldToCompleteCheckBox checkbox, TextBlock title, NoteEntry entry)
    {
        if (_organization?.SortMode != "Manual") return;
        container.Background = Brushes.Transparent;
        title.Cursor = new Cursor(StandardCursorType.SizeAll);
        // Rich-text link handlers run first and mark their presses handled. Other title
        // presses start dragging before bubbling into the hold-to-complete checkbox.
        title.AddHandler(PointerPressedEvent, (_, e) => BeginCardDrag(entry, container, e, true));
        container.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (ReferenceEquals(e.Source, container) && e.GetPosition(container).Y <= checkbox.Bounds.Bottom)
                BeginCardDrag(entry, container, e, false);
        });
        container.AddHandler(PointerMovedEvent, (_, e) =>
        {
            if (_draggedRow != container || _dragPointer != e.Pointer) return;
            _dragPosition = e.GetPosition(this);
            var delta = _dragPosition - _dragOrigin;
            if (!_reordering && Math.Abs(delta.X) < 5 && Math.Abs(delta.Y) < 5) return;
            if (!_reordering)
            {
                _reordering = true;
                container.Opacity = .45;
                TodoDraggedTitle.Text = string.Concat(FormattedTodoText.Parse(TodoDetails.Parse(entry.Text).Body).Select(part => part.Text));
                TodoDragPreview.IsVisible = true;
                _lastDragTick = Stopwatch.GetTimestamp();
                _reorderScrollTimer.Start();
            }
            UpdateCardDropTarget();
            e.Handled = true;
        }, RoutingStrategies.Tunnel);
        container.AddHandler(PointerReleasedEvent, (_, e) =>
        {
            if (_draggedRow != container || _dragPointer != e.Pointer || e.InitialPressMouseButton != MouseButton.Left) return;
            _dragPosition = e.GetPosition(this);
            if (_reordering) UpdateCardDropTarget();
            var source = _draggedEntry;
            var target = _dropEntry;
            bool after = _dropAfter, moved = _reordering, doubleClick = _dragTitleDoubleClick;
            bool contextCurrent = DragContextCurrent();
            var point = this.TranslatePoint(_dragPosition, TodoScrollViewer);
            bool inside = point.HasValue && new Rect(TodoScrollViewer.Bounds.Size).Contains(point.Value);
            CancelDrag();
            e.Handled = true;
            if (!contextCurrent || !inside || source == null) return;
            if (moved && target != null) MoveTodo(source, target, after);
            else if (!moved && doubleClick && !source.IsCompleted) ToggleProgress(source);
        }, RoutingStrategies.Tunnel);
        container.PointerCaptureLost += (_, e) =>
        {
            if (_draggedRow == container && ReferenceEquals(e.Source, container)) CancelDrag();
        };
    }

    private void BeginCardDrag(NoteEntry entry, Grid container, PointerPressedEventArgs e, bool fromTitle)
    {
        if (e.Handled || _closed || _deleteInProgress || _organization?.SortMode != "Manual" ||
            !e.GetCurrentPoint(container).Properties.IsLeftButtonPressed) return;
        CancelDrag();
        foreach (var check in container.GetVisualDescendants().OfType<HoldToCompleteCheckBox>()) check.CancelHold(true);
        _draggedEntry = entry;
        _draggedRow = container;
        _dragPointer = e.Pointer;
        _dragOrigin = _dragPosition = e.GetPosition(this);
        _dragVault = _settings.ObsidianVaultFolder;
        _dragSubfolder = _settings.NotesSubfolder;
        _dragTitleDoubleClick = fromTitle && e.ClickCount == 2;
        e.Pointer.Capture(container);
        e.Handled = true;
    }

    private bool DragContextCurrent() => !_closed && IsExpanded && IsVisible &&
        _organization?.SortMode == "Manual" && _dragVault == _settings.ObsidianVaultFolder && _dragSubfolder == _settings.NotesSubfolder;

    private void AutoScrollCardDrag()
    {
        if (!_reordering || _draggedRow == null || _dragPointer?.Captured != _draggedRow || !DragContextCurrent())
        { CancelDrag(); return; }
        long now = Stopwatch.GetTimestamp();
        double seconds = Math.Min(.05, (now - _lastDragTick) / (double)Stopwatch.Frequency);
        _lastDragTick = now;
        var point = this.TranslatePoint(_dragPosition, TodoScrollViewer);
        if (!point.HasValue) return;
        var size = TodoScrollViewer.Bounds.Size;
        const double edge = 48;
        double speed = 0;
        if (point.Value.X >= 0 && point.Value.X <= size.Width && point.Value.Y >= -edge && point.Value.Y <= size.Height + edge)
        {
            double zone = Math.Min(edge, size.Height / 3);
            double proximity = point.Value.Y < zone ? -Math.Clamp((zone - point.Value.Y) / zone, 0, 1)
                : point.Value.Y > size.Height - zone ? Math.Clamp((point.Value.Y - size.Height + zone) / zone, 0, 1) : 0;
            speed = Math.Sign(proximity) * 700 * proximity * proximity;
        }
        double maximum = Math.Max(0, TodoScrollViewer.Extent.Height - TodoScrollViewer.Viewport.Height);
        double offset = Math.Clamp(TodoScrollViewer.Offset.Y + speed * seconds, 0, maximum);
        if (offset != TodoScrollViewer.Offset.Y)
        {
            TodoScrollViewer.Offset = new Vector(TodoScrollViewer.Offset.X, offset);
            TodoScrollViewer.UpdateLayout();
        }
        UpdateCardDropTarget();
    }

    private void UpdateCardDropTarget()
    {
        _dropEntry = null;
        TodoDropLine.IsVisible = false;
        if (_draggedEntry == null || _draggedRow == null) return;
        var viewportPoint = this.TranslatePoint(_dragPosition, TodoScrollViewer);
        var rowPoint = this.TranslatePoint(_dragPosition, TodoRows);
        if (!viewportPoint.HasValue || !rowPoint.HasValue) return;
        var size = TodoScrollViewer.Bounds.Size;
        TodoDragPreview.IsVisible = new Rect(-24, -48, size.Width + 48, size.Height + 96).Contains(viewportPoint.Value);
        Canvas.SetLeft(TodoDragPreview, Math.Clamp(viewportPoint.Value.X + 12, 0, Math.Max(0, size.Width - 240)));
        Canvas.SetTop(TodoDragPreview, Math.Clamp(viewportPoint.Value.Y + 14, 0, Math.Max(0, size.Height - 44)));
        if (viewportPoint.Value.X < 0 || viewportPoint.Value.X > size.Width || viewportPoint.Value.Y < -48 || viewportPoint.Value.Y > size.Height + 48) return;
        var candidates = TodoRows.Children.OfType<Grid>().Where(row => row != _draggedRow &&
            row.Tag is NoteEntry target && target.IsCompleted == _draggedEntry.IsCompleted).ToArray();
        if (candidates.Length == 0) return;
        var targetRow = candidates.FirstOrDefault(row => rowPoint.Value.Y < row.Bounds.Center.Y);
        _dropAfter = targetRow == null;
        targetRow ??= candidates[^1];
        _dropEntry = (NoteEntry)targetRow.Tag!;
        var position = targetRow.TranslatePoint(new Point(0, _dropAfter ? targetRow.Bounds.Height : 0), TodoDragLayer);
        if (!position.HasValue || position.Value.Y < -3 || position.Value.Y > size.Height + 3) return;
        TodoDropLine.Width = Math.Max(0, size.Width - 6);
        Canvas.SetLeft(TodoDropLine, 2);
        Canvas.SetTop(TodoDropLine, Math.Clamp(position.Value.Y - 1.5, 0, Math.Max(0, size.Height - 3)));
        TodoDropLine.IsVisible = true;
    }

    private void CancelDrag()
    {
        _reorderScrollTimer.Stop();
        if (_draggedRow != null) _draggedRow.Opacity = 1;
        _draggedEntry = null; _draggedRow = null; _dropEntry = null;
        _reordering = false; _dragTitleDoubleClick = false;
        _dragVault = _dragSubfolder = null;
        TodoDropLine.IsVisible = TodoDragPreview.IsVisible = false;
        var pointer = _dragPointer; _dragPointer = null;
        pointer?.Capture(null);
    }
}
