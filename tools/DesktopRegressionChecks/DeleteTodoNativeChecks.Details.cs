using Avalonia;
using Avalonia.Controls;
using DialogKey = Avalonia.Input.Key;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;
using StopwatchOverlay.Habitica;

internal static partial class DeleteTodoNativeChecks
{
    internal static void RunDetails()
    {
        const string output = "design/one-piece-v2/delete-todo-detail/implemented";
        const string parent = "**Finish** illustration", sketch = "Sketch layout", checklist = "Keep paper checklist";
        const string note = DeleteTodoPreview.IllustrationNote;
        Directory.CreateDirectory(output);
        var vault = Directory.CreateTempSubdirectory("stopwatch-detail-");
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, ObsidianVaultFolder = vault.FullName, HabiticaEnabled = false, TelegramEnabled = false };
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(vault.FullName, "outbox.json"));
        Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
        Desktop("DesktopPageAppearance", "Initialize", settings);
        Desktop("DesktopTypography", "Initialize", settings.Typography);
        TodoOverlayWindow? overlay = null;
        try
        {
            NoteEntry Read() => ObsidianNotesSync.LoadAllNotes(vault.FullName).Single();
            string markdown = TodoDetails.Append(TodoDetails.Append(parent, TodoDetailKind.Note, note), TodoDetailKind.Subtask, checklist);
            Require(ObsidianNotesSync.AppendEntry(vault.FullName, NoteType.Todo, markdown).Success, "Cannot seed parent");
            var sketchItem = new HabiticaSubtask(Guid.NewGuid(), sketch, false);
            var palette = new HabiticaSubtask(Guid.NewGuid(), "Keep palette", true);
            var initial = Read();
            var sync = HabiticaIntegration.Current;
            Require(sync.SetChildren(settings, initial, [sketchItem, palette]).SyncWarning == null, "Cannot seed metadata subtasks");
            overlay = new TodoOverlayWindow(settings) { Topmost = false, ShowActivated = false };
            overlay.Show();
            overlay.ToggleExpanded();
            ClockOverlayChecks.Pump(80);

            ConfirmationDialogWindow OpenItem(string value)
            {
                var entry = Read();
                var row = overlay.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().Single();
                if (!row.GetVisualDescendants().Any(c => c.Name == "TodoDetailsPanel"))
                {
                    row.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ToggleTodoDetails")
                        .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    ClockOverlayChecks.Pump(40);
                }
                var panel = overlay.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "TodoDetailsPanel");
                var texts = TodoDetails.Parse(entry.Text).Items.Select(d => d.Text)
                    .Concat(sync.GetDetails(settings, entry).Children.Select(c => c.Text)).ToList();
                var item = panel.Children[texts.IndexOf(value)];
                var menu = item.ContextMenu ?? item.GetVisualDescendants().OfType<Control>().First(c => c.ContextMenu != null).ContextMenu!;
                menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Delete")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                ClockOverlayChecks.Pump(80);
                return overlay.OwnedWindows.OfType<ConfirmationDialogWindow>().Single();
            }
            void Click(ConfirmationDialogWindow dialog, string name)
            {
                dialog.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ClockOverlayChecks.Pump(50);
            }
            foreach (int scale in new[] { 70, 90, 125 })
            {
                settings.UiScalePercent = scale;
                Desktop("DesktopPageAppearance", "Apply", settings, true);
                foreach (string value in new[] { sketch, note })
                {
                    var dialog = OpenItem(value);
                    string kind = value == note ? "note" : "subtask";
                    var labels = dialog.GetVisualDescendants().OfType<TextBlock>().ToArray();
                    Require(labels.Any(t => t.Text == "Delete " + kind + "?") && labels.Any(t => t.Text == value)
                        && labels.Any(t => t.Text == "Finish illustration"), "Missing heading, selected item or parent");
                    var cancel = dialog.FindControl<Button>("CancelButton")!;
                    var delete = dialog.FindControl<Button>("ConfirmButton")!;
                    Require(cancel.IsFocused && ((TextBlock)delete.Content!).Text == "Delete " + kind, "Wrong focus or delete action");
                    Require(Math.Abs(dialog.ClientSize.Width - 480 * scale / 100d) <= 1 && labels.All(t => t.TextLayout.Height <= t.Bounds.Height + 1),
                        "Wrong dimensions or clipped text");
                    var area = dialog.Screens.ScreenFromWindow(dialog)!.WorkingArea;
                    var bottom = dialog.Position.Y + dialog.ClientSize.Height * dialog.RenderScaling;
                    Require(dialog.Position.Y >= area.Y && bottom <= area.Bottom + 1, $"Window outside working area ({bottom} > {area.Bottom})");
                    Require(dialog.GetVisualDescendants().OfType<ScrollViewer>().All(s => s.Extent.Height <= s.Viewport.Height + 1), "Sample requires scrolling");
                    var effect = labels.Single(t => t.Text == "The task and its other details will stay.");
                    Require(effect.TranslatePoint(new Point(0, effect.Bounds.Height), dialog)!.Value.Y < delete.TranslatePoint(default, dialog)!.Value.Y &&
                        delete.TranslatePoint(new Point(0, delete.Bounds.Height), dialog)!.Value.Y <= dialog.ClientSize.Height, "Actions overlap or exceed the window");
                    Capture(dialog, scale, Path.Combine(output, $"delete-{kind}-{scale}.png"));
                    if (scale == 70) Click(dialog, "CancelButton");
                    else if (scale == 90) { delete.Focus(); Press(delete, DialogKey.Escape); }
                    else Press(cancel, DialogKey.Enter);
                    Require(!dialog.IsVisible && !dialog.Result && Read() == initial &&
                        sync.GetDetails(settings, Read()).Children.SequenceEqual(new[] { sketchItem, palette }), "Cancellation changed the task or children");
                    Console.WriteLine($"PASS {kind} at {scale}% through overlay menu: layout, focus and cancellation");
                }
            }
            foreach (string kind in new[] { "note", "subtask" }) DeleteTodoPreview.Comparison(Path.Combine(output, "delete-" + kind));

            var stale = OpenItem(note);
            string subfolder = settings.NotesSubfolder;
            settings.NotesSubfolder = "Other";
            Click(stale, "ConfirmButton");
            settings.NotesSubfolder = subfolder;
            overlay.ReloadTodos();
            Require(Read() == initial, "A changed folder allowed deletion");
            Click(OpenItem(note), "ConfirmButton");
            var current = Read();
            var doc = TodoDetails.Parse(current.Text);
            Require(doc.Body == parent && doc.Items.Single().Text == checklist && !current.IsCompleted &&
                current.Timestamp == initial.Timestamp && sync.GetDetails(settings, current).Children.SequenceEqual(new[] { sketchItem, palette }), "Note deletion changed a sibling or parent");
            var confirmed = OpenItem(sketch);
            var confirmButton = confirmed.FindControl<Button>("ConfirmButton")!;
            confirmButton.Focus();
            Press(confirmButton, DialogKey.Enter);
            Require(confirmed.Result && Read() == current && sync.GetDetails(settings, current).Children.SequenceEqual(new[] { palette }), "Metadata subtask deletion affected its parent or sibling");
            Click(OpenItem(checklist), "ConfirmButton");
            Require(Read().Text == parent && sync.GetDetails(settings, Read()).Children.SequenceEqual(new[] { palette }), "Markdown subtask deletion removed unrelated content");
            Console.WriteLine("PASS confirmed note, Markdown subtask and metadata subtask deletions; retained parent, sibling identity and completion; stale folder rejected");

            string rtl = string.Join(" ", Enumerable.Repeat("یادداشت برای فردا", 75));
            settings.TodoAppearance.Profile = "pixel";
            settings.TodoAppearance.Pixel.Background = "midnight-chart";
            var configure = typeof(ConfirmationDialogWindow).GetMethod("ConfigureDeleteTodoDetail", Private)!;
            foreach (string theme in new[] { AppThemeCatalog.Nika, AppThemeCatalog.Daylight })
            {
                settings.ThemeMode = theme;
                Desktop("ThemeResources", "ApplyApplication", theme);
                var dialog = new ConfirmationDialogWindow { ShowActivated = false };
                try
                {
                    configure.Invoke(dialog, ["والد", new TodoDetail(TodoDetailKind.Note, rtl, false, 0, rtl.Length, rtl), settings]);
                    dialog.Show(); ClockOverlayChecks.Pump(80);
                    if (theme == AppThemeCatalog.Nika)
                    {
                        Require(dialog.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == rtl).FlowDirection == Avalonia.Media.FlowDirection.RightToLeft
                            && dialog.GetVisualDescendants().OfType<TodoFrame>().Any(f => f.Pixel)
                            && dialog.GetVisualDescendants().OfType<ScrollViewer>().Any(s => s.Extent.Height > s.Viewport.Height + 1), "Long RTL or pixel styling failed");
                    }
                    else Require(dialog.FindControl<TextBlock>("MessageText")!.Text!.Contains(rtl) &&
                        dialog.FindControl<TextBlock>("MessageText")!.Text!.Contains("والد"), "Fallback lost the selected item or parent");
                    var cancel = dialog.FindControl<Button>("CancelButton")!;
                    Require(cancel.IsFocused, "Initial focus bypassed Cancel");
                    Press(cancel, DialogKey.Escape);
                    Require(!dialog.IsVisible && !dialog.Result, "Escape did not cancel");
                }
                finally { dialog.Close(); }
            }
        }
        finally
        {
            overlay?.Close();
            Desktop("DesktopTypography", "Shutdown");
            Desktop("DesktopPageAppearance", "Shutdown");
            TelegramOutboxStore.SetCustomFilePath(null);
            vault.Delete(true);
        }
    }
}
