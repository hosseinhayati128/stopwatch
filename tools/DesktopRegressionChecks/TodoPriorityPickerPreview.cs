using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TodoPriorityPickerPreview
{
    const string Output = "design/one-piece-v2/todo-priority-picker/implemented";
    static readonly int[] Priorities = [1, 3, 10];

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
        var vault = Directory.CreateTempSubdirectory("stopwatch-priority-picker-");
        var settings = new AppSettings {
            ThemeMode = AppThemeCatalog.Nika, ObsidianVaultFolder = vault.FullName,
            TelegramEnabled = false, HabiticaEnabled = false,
            TodoOverlayHasPosition = true, TodoOverlayLeft = 100, TodoOverlayTop = 80
        };
        TodoOverlayWindow? overlay = null;
        try
        {
            Require(ObsidianNotesSync.AppendEntry(vault.FullName, NoteType.Todo, "Review the print proof").Success, "Cannot seed preview task");
            var task = ObsidianNotesSync.LoadAllNotes(vault.FullName).Single();
            overlay = new TodoOverlayWindow(settings) { ShowActivated = false, Topmost = false };
            overlay.Show();
            overlay.ToggleExpanded();
            foreach (int scale in new[] { 70, 90, 125 })
            {
                settings.UiScalePercent = scale;
                foreach (string profile in new[] { "classic", "pixel" })
                foreach (int priority in Priorities)
                {
                    settings.TodoAppearance.Profile = profile;
                    Require(TodoOrganizationStore.SetPriority(vault.FullName, task, priority).Success, "Cannot set preview priority");
                    overlay.ApplySettings();
                    ClockOverlayChecks.Pump(55);
                    var card = overlay.FindControl<StackPanel>("TodoRows")!.Children.Single();
                    var badge = PriorityBadge(card);
                    Require(badge.Content is TextBlock t && t.Text == priority.ToString(), "Task badge has the wrong rank");
                    var menu = badge.ContextMenu!;
                    var options = menu.Items.OfType<MenuItem>().ToArray();
                    Require(options.Length == 10, "Missing priority choices");
                    for (int index = 0; index < options.Length; index++)
                    {
                        var option = options[index];
                        int level = index + 1;
                        var marker = (Button)option.Icon!;
                        Require(marker.Content is TextBlock number && number.Text == level.ToString(), "Marker order changed");
                    }
                    badge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    try
                    {
                        ClockOverlayChecks.Pump(75);
                        var frame = menu.GetVisualDescendants().OfType<TodoFrame>().SingleOrDefault(f => f.Name == "PickerFrame")
                            ?? throw new InvalidOperationException("Opening the task badge did not show its two-row picker");
                        Require(menu.IsOpen && frame.Pixel == settings.TodoAppearance.IsPixel, "Wrong popup profile");
                        CheckLayout(badge, frame, options, scale, priority);
                        TodoDetailMenuPreview.Capture(overlay, card, frame, Path.Combine(Output, $"{profile}-p{priority}-{scale}.png"));
                        if (scale == 90) CheckSelection(overlay, task, vault.FullName, menu, options, priority);
                    }
                    finally { menu.Close(); }
                }
                TodoTaskMenuPreview.Comparison(Output, scale, ["p1", "p3", "p10"],
                    ["Priority 1 selected", "Priority 3 selected", "Priority 10 selected"]);
            }
            settings.ThemeMode = AppThemeCatalog.Daylight;
            DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
            overlay.ApplySettings();
            ClockOverlayChecks.Pump(40);
            var legacyBadge = PriorityBadge(overlay.FindControl<StackPanel>("TodoRows")!.Children.Single());
            legacyBadge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var legacy = legacyBadge.ContextMenu!;
            ClockOverlayChecks.Pump(40);
            Require(legacy.IsOpen && legacy.Items.Count == 10 && !legacy.GetVisualDescendants().OfType<TodoFrame>().Any(), "Daylight lost its standard menu");
            ((MenuItem)legacy.Items[3]!).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Require(!legacy.IsOpen && TodoOrganizationStore.Load(vault.FullName).GetPriority(task) == 4, "Daylight could not save a priority");
        }
        finally { overlay?.Close(); vault.Delete(true); }
    }

    static void CheckSelection(TodoOverlayWindow overlay, NoteEntry task, string vault, ContextMenu menu, MenuItem[] options, int priority)
    {
        int neighbor = priority > 5 ? priority - 5 : priority + 5;
        Require(menu.SelectedItem == options[priority - 1], "Picker did not open on the saved rank");
        KeyDown(options[priority - 1], priority > 5 ? Avalonia.Input.Key.Up : Avalonia.Input.Key.Down);
        Require(menu.SelectedItem == options[neighbor - 1], $"Vertical navigation from {priority} reached {menu.SelectedIndex + 1}, expected {neighbor}");
        int next = neighbor % 5 == 0 ? neighbor - 1 : neighbor + 1;
        KeyDown(options[neighbor - 1], next > neighbor ? Avalonia.Input.Key.Right : Avalonia.Input.Key.Left);
        Require(menu.SelectedItem == options[next - 1], $"Horizontal navigation from {neighbor} reached {menu.SelectedIndex + 1}, expected {next}");
        KeyDown(options[next - 1], Avalonia.Input.Key.Escape);
        Require(!menu.IsOpen && TodoOrganizationStore.Load(vault).GetPriority(task) == priority, "Escape changed the saved priority");
        var rows = overlay.FindControl<StackPanel>("TodoRows")!;
        var badge = PriorityBadge(rows.Children.Single());
        badge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ClockOverlayChecks.Pump(30);
        menu = badge.ContextMenu!;
        var target = (MenuItem)menu.Items[next - 1]!;
        if (priority == 3)
        {
            var root = TopLevel.GetTopLevel(target)!;
            var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), root)!.Value;
            var hit = (Control)root.InputHitTest(point)!;
            var pointer = new Pointer(75, PointerType.Mouse, true);
            hit.RaiseEvent(new PointerPressedEventArgs(hit, pointer, root, point, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
            hit.RaiseEvent(new PointerReleasedEventArgs(hit, pointer, root, point, 1,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        }
        else
        {
            target.Focus();
            KeyDown(target, Avalonia.Input.Key.Enter);
        }
        ClockOverlayChecks.Pump(45);
        Require(!menu.IsOpen && TodoOrganizationStore.Load(vault).GetPriority(task) == next, "The chosen priority was not saved");
        var updated = PriorityBadge(rows.Children.Single());
        Require(updated.Content is TextBlock text && text.Text == next.ToString(), "Task badge did not refresh after selection");
        updated.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ClockOverlayChecks.Pump(30);
        var reopened = updated.ContextMenu!;
        Require(reopened.Items.OfType<MenuItem>().Single(i => i.IsChecked) == reopened.Items[next - 1], "Reopened picker had the wrong check");
        reopened.Close();
    }

    static Button PriorityBadge(Control card) => card.GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains("priorityBadge"));

    static void KeyDown(Control item, Avalonia.Input.Key key)
    {
        item.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        ClockOverlayChecks.Pump(25);
    }

    static void CheckLayout(Button badge, TodoFrame frame, MenuItem[] options, int scale, int priority)
    {
        var anchor = TodoDetailMenuPreview.BoundsOnDesktop(badge);
        var popup = TodoDetailMenuPreview.BoundsOnDesktop(frame);
        Require(Math.Abs(popup.X - anchor.X) <= 2 && Math.Abs(popup.Y - anchor.Bottom - 4) <= 2 &&
            Math.Abs(popup.Width - 252 * scale / 100d) <= 2, $"Picker missed the badge at {scale}%: {anchor}, {popup}");
        var cells = options.Select(o => TodoDetailMenuPreview.BoundsOnDesktop(
            o.GetVisualDescendants().OfType<Border>().Single(b => b.Name == "ChoiceTile"))).ToArray();
        for (int i = 0; i < options.Length; i++)
        {
            var cell = cells[i];
            var check = options[i].GetVisualDescendants().OfType<Border>().Single(b => b.Name == "SelectionCheck");
            Require(check.IsEffectivelyVisible == (i + 1 == priority), "Wrong selected tile");
            Require(popup.Contains(cell.TopLeft) && popup.Contains(cell.BottomRight) && Math.Abs(cell.Y - cells[i < 5 ? 0 : 5].Y) <= 1 &&
                (i % 5 == 0 || cell.Left >= cells[i - 1].Right - 1) &&
                (i < 5 || (cell.Top >= cells[i - 5].Bottom - 1 && Math.Abs(cell.X - cells[i - 5].X) <= 1)),
                $"Invalid two-row layout for rank {i + 1} at {scale}%: {cell} in {popup}");
        }
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
