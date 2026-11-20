using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;
using StopwatchOverlay.Habitica;
using DialogKey = Avalonia.Input.Key;

internal static class TodoDetailEditorNativeChecks
{
    const string Output = "design/one-piece-v2/todo-detail-editor/implemented";

    internal static void Run()
    {
        var temp = Directory.CreateTempSubdirectory("stopwatch-detail-editor-");
        Directory.CreateDirectory(Output);
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, TelegramEnabled = false, HabiticaEnabled = false };
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(temp.FullName, "outbox.json"));
        try
        {
            DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
            DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Initialize", settings);
            DeleteTodoNativeChecks.Desktop("DesktopTypography", "Initialize", settings.Typography);
            foreach (var sample in TodoDetailEditorPreview.Samples)
            {
                string title = (sample.Edit ? "Edit " : "Add ") + (sample.Note ? "note" : "subtask");
                settings.ObsidianVaultFolder = temp.CreateSubdirectory(title).FullName;
                VerifyMenus(settings, sample, title);
            }
        }
        finally
        {
            DeleteTodoNativeChecks.Desktop("DesktopTypography", "Shutdown");
            DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown");
            TelegramOutboxStore.SetCustomFilePath(null);
            temp.Delete(true);
        }
    }

    static void VerifyMenus(AppSettings settings, (bool Note, bool Edit, string Parent, string Text) sample, string title)
    {
        string stem = Path.Combine(Output, title.ToLowerInvariant().Replace(' ', '-'));
        string parent = "**" + sample.Parent + "**";
        const string sibling = "Keep measurements";
        string markdown = TodoDetails.Append(parent, TodoDetailKind.Subtask, sibling);
        if (sample.Note && sample.Edit) markdown = TodoDetails.Append(markdown, TodoDetailKind.Note, sample.Text);
        string vault = settings.ObsidianVaultFolder;
        NoteEntry Read() => ObsidianNotesSync.LoadAllNotes(vault).Single();
        Require(ObsidianNotesSync.AppendEntry(vault, NoteType.Todo, markdown).Success, "Cannot seed parent");
        var initial = Read();
        var sync = HabiticaIntegration.Current;
        var child = new HabiticaSubtask(Guid.NewGuid(), sample.Text, true);
        var children = !sample.Note && sample.Edit ? new[] { child } : Array.Empty<HabiticaSubtask>();
        Require(sync.SetChildren(settings, initial, children).SyncWarning == null, "Cannot seed stored children");
        var overlay = new TodoOverlayWindow(settings) { ShowActivated = false, Topmost = false };
        try
        {
            overlay.Show(); overlay.ToggleExpanded(); ClockOverlayChecks.Pump(80);
            NoteEntryWindow Open(bool edit, bool note, string? target = null)
            {
                Grid Row() => overlay.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().Single();
                var row = Row();
                MenuItem command;
                if (edit)
                {
                    if (!row.GetVisualDescendants().Any(c => c.Name == "TodoDetailsPanel"))
                    {
                        row.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ToggleTodoDetails")
                            .RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        ClockOverlayChecks.Pump(50);
                    }
                    var entry = Read();
                    var items = TodoDetails.Parse(entry.Text).Items.Select(d => d.Text).Concat(sync.GetDetails(settings, entry).Children.Select(d => d.Text)).ToList();
                    var panel = Row().GetVisualDescendants().OfType<StackPanel>().Single(s => s.Name == "TodoDetailsPanel");
                    var element = panel.Children[items.IndexOf(target!)];
                    var menu = element.ContextMenu ?? element.GetVisualDescendants().OfType<Control>().First(c => c.ContextMenu != null).ContextMenu!;
                    command = menu.Items.OfType<MenuItem>().Single(m => Equals(m.Header, note ? "Edit note" : "Edit subtask"));
                }
                else
                {
                    var add = row.ContextMenu!.Items.OfType<MenuItem>().Single(m => Equals(m.Header, "Add"));
                    command = add.Items.OfType<MenuItem>().Single(m => Equals(m.Header, note ? "Note" : "Subtask"));
                }
                command.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); ClockOverlayChecks.Pump(100);
                return overlay.OwnedWindows.OfType<NoteEntryWindow>().Single();
            }
            string updated = (sample.Text.Length == 0 ? "Sketch layout" : sample.Text) + "\nFinal proof";
            foreach (int scale in new[] { 70, 90, 125 })
            {
                settings.UiScalePercent = scale;
                DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Apply", settings, true);
                var window = Open(sample.Edit, sample.Note, sample.Text);
                var input = window.FindControl<TextBox>("NoteInputBox")!;
                var save = window.FindControl<Button>("SaveButton")!;
                var cancel = window.FindControl<Button>("CancelButton")!;
                var error = window.FindControl<TextBlock>("SaveError")!;
                Require(window.Title == title && input.IsFocused && input.AcceptsReturn && input.Text == (sample.Edit ? sample.Text : ""), "Wrong editor, focus or draft");
                if (!sample.Edit)
                {
                    input.Text = "  "; ClockOverlayChecks.Pump(20); Press(input, DialogKey.Enter, KeyModifiers.Control);
                    Require(!save.IsEnabled && window.IsVisible && Read() == initial, "Blank draft saved");
                    input.Text = sample.Text; ClockOverlayChecks.Pump(35);
                }
                var parentLabel = window.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == sample.Parent);
                Require(parentLabel.TextLayout.Height <= parentLabel.Bounds.Height + 1 && input.FlowDirection == AutoTextDirection.Detect(sample.Text), "Parent clipped or incorrect text direction");
                Require(AutomationProperties.GetName(save) == (sample.Edit ? "Save changes" : title) && save.IsEnabled == (sample.Text.Length != 0), "Wrong action or enabled state");
                Require(Math.Abs(window.ClientSize.Width - 520 * scale / 100d) <= 1, $"Unexpected editor width {window.ClientSize.Width} at {scale}%");
                var inputBottom = input.TranslatePoint(new Point(0, input.Bounds.Height), window)!.Value.Y;
                Require(inputBottom < save.TranslatePoint(default, window)!.Value.Y && save.TranslatePoint(new Point(save.Bounds.Width, save.Bounds.Height), window)!.Value.Y <= window.ClientSize.Height, "Footer clipped or overlapping input");
                DeleteTodoNativeChecks.Capture(window, scale, $"{stem}-{scale}.png");

                input.Text = updated; ClockOverlayChecks.Pump(20);
                if (scale == 70) Press(input, DialogKey.Escape);
                else if (scale == 90) cancel.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                else
                {
                    input.CaretIndex = updated.Length;
                    Press(input, DialogKey.Enter);
                    Require(window.IsVisible && input.Text!.EndsWith('\n'), "Enter did not insert a newline");
                    input.Text = updated;
                    string subfolder = settings.NotesSubfolder;
                    settings.NotesSubfolder = "Other";
                    Press(input, DialogKey.Enter, KeyModifiers.Control);
                    Require(window.IsVisible && error.IsVisible && input.Text == updated && save.IsEnabled && Read() == initial, "Failed save lost its draft or changed the parent");
                    settings.NotesSubfolder = subfolder;
                    if (sample.Edit) save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    else Press(input, DialogKey.Enter, KeyModifiers.Control);
                }
                ClockOverlayChecks.Pump(60);
                Require(!window.IsVisible, "Editor did not close");
                if (scale != 125) Require(Read() == initial && sync.GetDetails(settings, Read()).Children.SequenceEqual(children), "Cancellation changed stored details");
                Console.WriteLine($"PASS {title} {scale}% through Todo menu");
            }
            var final = Read();
            var doc = TodoDetails.Parse(final.Text);
            var stored = sync.GetDetails(settings, final).Children;
            Require(doc.Body == parent && final.Timestamp == initial.Timestamp && !final.IsCompleted &&
                doc.Items.Single(d => d.Kind == TodoDetailKind.Subtask).Text == sibling, "Parent or sibling changed");
            if (sample.Note) Require(doc.Items.Single(d => d.Kind == TodoDetailKind.Note).Text == updated && stored.Count == 0, "Attached note failed to persist once");
            else Require(final.Text == initial.Text && stored.Single().Text == updated && stored[0].Completed == sample.Edit &&
                (!sample.Edit || stored[0].Id == child.Id), "Incorrect subtask content, completion or identity");
            DeleteTodoPreview.Comparison(stem);

            if (!sample.Edit && !sample.Note)
            {
                settings.TodoAppearance.Profile = "pixel"; settings.TodoAppearance.Pixel.Background = "midnight-chart";
                foreach (string theme in new[] { AppThemeCatalog.Nika, AppThemeCatalog.Daylight })
                {
                    settings.ThemeMode = theme;
                    DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", theme);
                    var window = Open(true, false, sibling);
                    var input = window.FindControl<TextBox>("NoteInputBox")!;
                    Require(input.Text == sibling && window.GetVisualDescendants().OfType<TodoFrame>().Any(f => f.Pixel) == (theme == AppThemeCatalog.Nika), "Markdown subtask or theme mismatch");
                    if (theme == AppThemeCatalog.Nika)
                    {
                        settings.UiScalePercent = 70;
                        DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Apply", settings, true); ClockOverlayChecks.Pump(80);
                        Require(Math.Abs(window.ClientSize.Width - 364) <= 1, "Open editor did not resize after page-scale change");
                    }
                    input.Text = "Revised checklist";
                    if (theme == AppThemeCatalog.Nika) window.FindControl<Button>("CloseButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    else Press(input, DialogKey.Enter, KeyModifiers.Control);
                    ClockOverlayChecks.Pump(50);
                    Require(!window.IsVisible, "Alternate theme editor did not close");
                }
                Require(TodoDetails.Parse(Read().Text).Items.Single().Text == "Revised checklist" &&
                    sync.GetDetails(settings, Read()).Children.SequenceEqual(stored), "Markdown edit changed a metadata sibling");
                settings.TodoAppearance.Profile = "classic"; settings.ThemeMode = AppThemeCatalog.Nika;
                DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
            }
        }
        finally { overlay.Close(); }
    }

    static void Press(Control control, DialogKey key, KeyModifiers modifiers = KeyModifiers.None)
    {
        control.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key, KeyModifiers = modifiers });
        ClockOverlayChecks.Pump(25);
    }

    static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
