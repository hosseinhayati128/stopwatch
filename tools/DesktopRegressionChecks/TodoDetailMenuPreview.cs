using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TodoDetailMenuPreview
{
    const string Output = "design/one-piece-v2/todo-detail-menu/implemented";
    const string LinkLabel = "Print studio checklist for fold marks, bleed and final trim sizes";
    static readonly string[] States = ["open-subtask", "completed-subtask", "linked-note"];

    internal static void Run() => RunSamples(Output, "menus", Render);

    internal static void RunSamples(string output, string fileStem, Action<TodoOverlayWindow, TodoFrame, Control, string, string, int> render,
        Action<TodoOverlayWindow, AppSettings>? afterSamples = null)
    {
        Directory.CreateDirectory(output);
        var vault = Directory.CreateTempSubdirectory("stopwatch-detail-menu-");
        var settings = new AppSettings {
            ThemeMode = AppThemeCatalog.Nika, TelegramEnabled = false, HabiticaEnabled = false,
            ObsidianVaultFolder = vault.FullName, TodoOverlayHasPosition = true, TodoOverlayLeft = 100, TodoOverlayTop = 80
        };
        TodoOverlayWindow? overlay = null;
        try
        {
            DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
            foreach (string state in States)
            {
                string markdown = state == "linked-note"
                    ? TodoDetails.Append("Prepare the print proof", TodoDetailKind.Note, $"[{LinkLabel}](https://example.com/print-guide)")
                    : TodoDetails.Append("Prepare the print proof", TodoDetailKind.Subtask, "Check the trim marks");
                if (state == "completed-subtask")
                    markdown = TodoDetails.SetCompleted(markdown, TodoDetails.Parse(markdown).Items.Single(), true);
                Require(ObsidianNotesSync.AppendEntry(vault.CreateSubdirectory(state).FullName, NoteType.Todo, markdown).Success, "Cannot seed preview");
            }
            overlay = new TodoOverlayWindow(settings) { ShowActivated = false, Topmost = false };
            overlay.Show();
            overlay.ToggleExpanded();
            foreach (int scale in new[] { 70, 90, 125 })
            {
                settings.UiScalePercent = scale;
                foreach (string profile in new[] { "classic", "pixel" })
                foreach (string state in States)
                {
                    settings.TodoAppearance.Profile = profile;
                    settings.ObsidianVaultFolder = Path.Combine(vault.FullName, state);
                    overlay.ApplySettings();
                    ClockOverlayChecks.Pump(50);
                    var list = overlay.FindControl<StackPanel>("TodoRows")!;
                    Control Card() => list.Children.Single();
                    if (!Card().GetVisualDescendants().Any(v => v.Name == "TodoDetailsFrame"))
                    {
                        Card().GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ToggleTodoDetails")
                            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        ClockOverlayChecks.Pump(45);
                    }
                    var detail = Card().GetVisualDescendants().OfType<TodoFrame>().Single(f => f.Name == "TodoDetailsFrame");
                    var child = ((StackPanel)detail.Child!).Children.Single();
                    if (state != "linked-note")
                        Require(child.GetVisualDescendants().OfType<HoldToCompleteCheckBox>().Single().IsChecked == (state == "completed-subtask"), "Wrong checkbox state");
                    render(overlay, detail, child, profile, state, scale);
                }
                TodoTaskMenuPreview.Comparison(output, scale, States, ["Open subtask", "Completed subtask", "Attached note · long link"], fileStem);
            }
            afterSamples?.Invoke(overlay, settings);
        }
        finally { overlay?.Close(); vault.Delete(true); }
    }

    static void Render(TodoOverlayWindow overlay, TodoFrame detail, Control child, string profile, string state, int scale)
    {
        var menu = child.ContextMenu!;
        var actions = menu.Items.OfType<MenuItem>().ToArray();
        bool note = state == "linked-note";
        string[] expected = note ? ["Edit note", "Open " + LinkLabel, "Delete"] : ["Edit subtask", "Delete"];
        Require(actions.Select(i => i.Header).SequenceEqual(expected), "Unexpected detail commands");
        Require(menu.Items[^2] is Separator, "Missing divider before Delete");
        var edit = actions[0];
        menu.Placement = PlacementMode.BottomEdgeAlignedLeft;
        menu.VerticalOffset = 5;
        menu.Open(child);
        try
        {
            ClockOverlayChecks.Pump(55);
            var surface = edit.GetVisualAncestors().OfType<TodoFrame>().First();
            Require(surface.Pixel == (profile == "pixel") && menu.IsOpen, "Incorrect popup profile");
            if (note)
            {
                var label = actions[1].GetVisualDescendants().OfType<TextBlock>().Single(t => Equals(t.Text, actions[1].Header));
                var lines = label.TextLayout.TextLines;
                Require(lines.Count > 1 && lines[^1].FirstTextSourceIndex + lines[^1].Length >= label.Text!.Length &&
                    label.TextLayout.Height <= label.Bounds.Height + 1 && !lines.Any(l => l.HasCollapsed),
                    $"Incomplete link layout: {lines.Count} lines, end {lines[^1].FirstTextSourceIndex + lines[^1].Length}/{label.Text!.Length}, " +
                    $"text {label.TextLayout.Height}, bounds {label.Bounds}");
            }
            var popupBounds = BoundsOnDesktop(surface);
            Require(Math.Abs(popupBounds.Width - (note ? 292 : 182) * scale / 100d) <= 2, "Incorrect menu scale");
            Capture(overlay, detail, surface, Path.Combine(Output, $"{profile}-{state}-{scale}.png"));
        }
        finally { menu.Close(); }
        if (scale == 90)
        {
            var target = child.GetVisualDescendants().OfType<TextBlock>().Single(t => t.ContextMenu == menu);
            target.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
            ClockOverlayChecks.Pump(45);
            Require(menu.IsOpen, "Right-click on the detail text did not open its menu");
            bool delete = profile == "pixel";
            var command = delete ? actions[^1] : edit;
            command.Focus();
            command.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
            ClockOverlayChecks.Pump(75);
            var dialog = overlay.OwnedWindows.Single(w => w.IsVisible);
            Require(!menu.IsOpen, "Detail menu remained open after selection");
            if (delete)
            {
                Require(dialog is ConfirmationDialogWindow, "Delete bypassed the detail confirmation");
                dialog.FindControl<Button>("CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            else
            {
                Require(dialog is NoteEntryWindow && dialog.Title == (note ? "Edit note" : "Edit subtask"), "Edit opened the wrong editor");
                dialog.Close();
            }
            ClockOverlayChecks.Pump(55);
        }
    }

    internal static Rect BoundsOnDesktop(Control control)
    {
        double dpi = TopLevel.GetTopLevel(control)!.RenderScaling;
        return new Rect(control.PointToScreen(default).ToPoint(dpi),
            control.PointToScreen(new Point(control.Bounds.Width, control.Bounds.Height)).ToPoint(dpi));
    }

    internal static void Capture(Window overlay, Control rowContext, TodoFrame menu, string path)
    {
        var window = BoundsOnDesktop(overlay);
        var row = BoundsOnDesktop(rowContext).Inflate(2);
        var popup = BoundsOnDesktop(menu);
        var area = row.Union(popup).Inflate(6);
        var shift = new Vector(-area.X, -area.Y);
        using var backdrop = new RenderTargetBitmap(PixelSize.FromSize(overlay.ClientSize, 1), new Vector(96, 96));
        backdrop.Render(overlay);
        using var capture = new RenderTargetBitmap(PixelSize.FromSize(area.Size, 1), new Vector(96, 96));
        using (var dc = capture.CreateDrawingContext())
        {
            dc.DrawImage(backdrop, row.Translate(new Vector(-window.X, -window.Y)), row.Translate(shift));
            dc.DrawRectangle(new VisualBrush(menu) {
                Stretch = Stretch.Fill, SourceRect = new RelativeRect(new Rect(popup.Size), RelativeUnit.Absolute)
            }, null, popup.Translate(shift));
        }
        capture.Save(path);
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
