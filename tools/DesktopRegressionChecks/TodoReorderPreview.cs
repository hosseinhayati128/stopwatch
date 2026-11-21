using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TodoReorderPreview
{
    const string Output = "design/one-piece-v2/todo-reorder/implemented";
    static readonly (string Title, int Rank)[] Tasks = [
        ("Pack sample prints", 4), ("Send corrected cover proofs and print notes to the bindery", 2),
        ("Choose paper stock", 5), ("Check page numbers", 3), ("Send final artwork", 1),
        ("File the color swatches", 7), ("Match the cover colors", 6), ("Archive the proofs", 8)
    ];

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
        var vault = Directory.CreateTempSubdirectory("stopwatch-drag-preview-");
        var settings = new AppSettings {
            ThemeMode = AppThemeCatalog.Nika, TelegramEnabled = false, HabiticaEnabled = false,
            ObsidianVaultFolder = vault.FullName, TodoOverlayHasPosition = true, TodoOverlayLeft = 100, TodoOverlayTop = 80
        };
        TodoOverlayWindow? window = null;
        try
        {
            foreach (var task in Tasks)
                Require(ObsidianNotesSync.AppendEntry(vault.FullName, NoteType.Todo, task.Title).Success, "Could not seed a task");
            var byTitle = ObsidianNotesSync.LoadAllNotes(vault.FullName).ToDictionary(t => t.Text);
            var order = Tasks.Select(task => byTitle[task.Title]).ToArray();
            foreach (var task in Tasks)
                Require(TodoOrganizationStore.SetPriority(vault.FullName, byTitle[task.Title], task.Rank).Success, "Could not seed a priority");
            Require(TodoOrganizationStore.SetSortMode(vault.FullName, "Manual").Success, "Could not set Manual mode");
            window = new TodoOverlayWindow(settings) { ShowActivated = false, Topmost = false };
            window.Show();
            window.ToggleExpanded();
            foreach (int scale in new[] { 70, 90, 125 })
            {
                settings.UiScalePercent = scale;
                foreach (string profile in new[] { "classic", "pixel" })
                {
                    settings.TodoAppearance.Profile = profile;
                    foreach (bool bottom in new[] { false, true })
                    {
                        Require(TodoOrganizationStore.SetManualOrder(vault.FullName, order).Success, "Could not reset the sample order");
                        window.ApplySettings();
                        CaptureDrag(window, settings, bottom, order);
                    }
                }
                TodoTaskMenuPreview.Comparison(Output, scale, ["between", "bottom"],
                    ["Moving between rows", "Moving a long task to the bottom"], "drag");
            }
        }
        finally { window?.Close(); vault.Delete(true); }
    }

    static void CaptureDrag(TodoOverlayWindow window, AppSettings settings, bool bottom, NoteEntry[] order)
    {
        var scroll = window.FindControl<ScrollViewer>("TodoScrollViewer")!;
        scroll.Offset = default;
        ClockOverlayChecks.Pump(60);
        var rows = window.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().ToArray();
        var source = rows[bottom ? 1 : 0];
        var target = bottom ? rows[^1] : rows[4];
        var handle = source.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("todoDragHandle"));
        var layer = window.FindControl<Canvas>("TodoDragLayer")!;
        var nativeLine = window.FindControl<Border>("TodoDropLine")!;
        var nativeCard = window.FindControl<Border>("TodoDragPreview")!;
        var pointer = new Pointer(944, PointerType.Mouse, true);
        bool pixel = settings.TodoAppearance.IsPixel;
        double zoom = settings.UiScalePercent / 100d;
        TodoReorderChecks.Press(handle, pointer, window);
        if (bottom)
        {
            var edge = scroll.TranslatePoint(new Point(135, scroll.Bounds.Height - 1), window)!.Value;
            TodoReorderChecks.Move(handle, pointer, window, edge);
            for (int i = 0; i < 35 && scroll.Offset.Y + scroll.Viewport.Height < scroll.Extent.Height - 1; i++)
                ClockOverlayChecks.Pump(30);
        }
        double y = target.TranslatePoint(new Point(0, bottom ? target.Bounds.Height - 1 : 2), layer)!.Value.Y;
        var point = new Point(layer.Bounds.Width * .58, Math.Clamp(y, 1, layer.Bounds.Height - 1));
        var drop = layer.TranslatePoint(point, window)!.Value;
        TodoReorderChecks.Move(handle, pointer, window, drop);
        ClockOverlayChecks.Pump(25);
        try
        {
            var frame = nativeCard.GetVisualDescendants().OfType<TodoFrame>().SingleOrDefault(f => f.IsEffectivelyVisible)
                ?? throw new InvalidOperationException("Dragging in Manual mode did not display the themed task card");
            var label = frame.GetVisualDescendants().OfType<TextBlock>().Single();
            Require(frame.Pixel == pixel && nativeLine.IsEffectivelyVisible && source.Opacity < 1 &&
                label.Text == ((NoteEntry)source.Tag!).Text, "Wrong drag style, marker, or source task");
            var caps = nativeLine.GetVisualDescendants().OfType<Border>()
                .Where(b => b.HorizontalAlignment is HorizontalAlignment.Left or HorizontalAlignment.Right).ToArray();
            Require(caps.Length == 2 && caps.All(c => (c.CornerRadius.TopLeft == 0) == pixel) &&
                Math.Abs(nativeLine.Bounds.Height - Math.Max(7, 8 * zoom)) <= 1, "Wrong marker ends or scale");
            double targetEdge = target.TranslatePoint(new Point(0, bottom ? target.Bounds.Height : 0), layer)!.Value.Y;
            double half = nativeLine.Bounds.Height / 2;
            Require(Math.Abs(nativeLine.Bounds.Center.Y - Math.Clamp(targetEdge, half, layer.Bounds.Height - half)) <= 2,
                "Insertion marker does not identify the destination");
            var targetLabel = TodoReorderChecks.Title(target);
            var targetText = new Rect(targetLabel.TranslatePoint(default, layer)!.Value, targetLabel.Bounds.Size);
            Require(new Rect(layer.Bounds.Size).Contains(nativeCard.Bounds) && !nativeLine.Bounds.Intersects(nativeCard.Bounds) &&
                !nativeCard.Bounds.Intersects(targetText), $"Drag preview obscures the destination or crosses the viewport edge at {zoom}");
            Require(label.TextLayout.TextLines.Count == (bottom ? 2 : 1) && !label.TextLayout.TextLines.Any(line => line.HasCollapsed),
                $"Unexpected task wrapping at {zoom}");
            string state = bottom ? "bottom" : "between";
            TodoReorderChecks.Capture(window, Path.Combine(Output, $"{settings.TodoAppearance.Profile}-{state}-{settings.UiScalePercent}.png"));
            TodoReorderChecks.Release(handle, pointer, window, drop);
            Require(pointer.Captured == null && !nativeCard.IsVisible && !nativeLine.IsVisible && source.Opacity == 1,
                "Releasing left drag feedback active");
            var expected = order.ToList();
            var entry = (NoteEntry)source.Tag!;
            expected.Remove(entry);
            expected.Insert(bottom ? expected.Count : expected.IndexOf((NoteEntry)target.Tag!), entry);
            Require(TodoOrganizationStore.Load(settings.ObsidianVaultFolder).Arrange(order).SequenceEqual(expected),
                "Releasing the drag did not save the indicated position");
        }
        finally { pointer.Capture(null); }
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
