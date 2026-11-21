using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;
using StopwatchOverlay.Habitica;

internal static class TodoEditorPreview
{
    const string Output = "design/one-piece-v2/todo-editor/implemented";
    const string TaskText = "Prepare the winter exhibition poster for print.\nReview the title lettering at full size and send the final proof to the studio before the afternoon pickup.";
    static readonly DateTime Due = new(2026, 11, 27, 16, 30, 0);

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        var temp = Directory.CreateTempSubdirectory("stopwatch-todo-editor-");
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, HabiticaEnabled = false, TelegramEnabled = false };
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(temp.FullName, "outbox.json"));
        try
        {
            DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
            DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Initialize", settings);
            DeleteTodoNativeChecks.Desktop("DesktopTypography", "Initialize", settings.Typography);
            foreach (var profile in new[] { "classic", "pixel" })
            {
                settings.TodoAppearance.Profile = profile;
                foreach (bool edit in new[] { false, true })
                {
                    string stem = Path.Combine(Output, profile + (edit ? "-edit" : "-add"));
                    foreach (int percent in new[] { 70, 90, 125 })
                    {
                        settings.ObsidianVaultFolder = temp.CreateSubdirectory(Path.GetFileName(stem) + percent).FullName;
                        settings.UiScalePercent = percent;
                        DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Apply", settings, true);
                        Render(settings, edit, percent, stem);
                    }
                    DeleteTodoPreview.Comparison(stem);
                }
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

    static void Render(AppSettings settings, bool edit, int percent, string stem)
    {
        var vault = settings.ObsidianVaultFolder;
        var sync = HabiticaIntegration.Current;
        const string neighbor = "Keep this task";
        NoteEntry[] Entries() => ObsidianNotesSync.LoadAllNotes(vault).ToArray();
        Require(ObsidianNotesSync.AppendEntry(vault, NoteType.Todo, neighbor).Success && TodoOrganizationStore.SetSortMode(vault, "Manual").Success, "Cannot seed vault");
        NoteEntry? entry = null;
        HabiticaSubtask[] children = [new(Guid.NewGuid(), "Check trim size", true), new(Guid.NewGuid(), "Export print PDF", false)];
        if (edit)
        {
            Require(ObsidianNotesSync.AppendEntry(vault, NoteType.Todo, TaskText).Success, "Cannot seed task");
            entry = Entries().Single(e => e.Text == TaskText);
            Require(TodoOrganizationStore.SaveTodo(vault, entry, TaskText, 2, Due, insertAtTop:false).Success &&
                sync.SetChildren(settings, entry, children).SyncWarning == null, "Cannot seed task details");
        }
        var window = new NoteEntryWindow(NoteType.Todo, settings, entry) { Topmost = false, ShowActivated = false };
        try
        {
            T Get<T>(string name) where T : Control => window.FindControl<T>(name)!;
            window.Show(); ClockOverlayChecks.Pump(100);
            var input = Get<TextBox>("NoteInputBox");
            var save = Get<Button>("SaveButton");
            var scroll = Get<ScrollViewer>("EditorScroll");
            var fields = Get<StackPanel>("DueFields");
            var date = Get<DatePicker>("TodoDueDate");
            var time = Get<TimePicker>("TodoDueTime");
            var due = Get<CheckBox>("DueEnabledToggle");
            var host = Get<ContentControl>("SubtaskEditorHost");
            var checklist = (HabiticaSubtaskEditor)host.Content!;
            var toggle = window.GetVisualDescendants().OfType<ToggleButton>().Single(c => c.Name == "TodoChecklistToggle");
            Require((input.Text ?? "") == (edit ? TaskText : "") && input.IsFocused && save.IsEnabled == edit, "Wrong draft, focus or save state");
            Require(fields.IsVisible == edit && Get<Grid>("TodoPlacementRow").IsVisible != edit && !host.IsVisible, "Wrong fields");
            var priority = Get<ComboBox>("TodoPriorityCombo");
            Require(priority.SelectedIndex == (edit ? 1 : 2) && (!edit || (date.SelectedDate?.Date == Due.Date && time.SelectedTime == Due.TimeOfDay &&
                checklist.GetSubtasks().SequenceEqual(children) && toggle.Content!.ToString()!.Contains("(2)"))), "Stored details missing");
            double scale = percent / 100d;
            Require(Math.Abs(window.ClientSize.Width - 530 * scale) <= 1 && scroll.Viewport.Height + 1 >= scroll.Extent.Height, "Initial size or scroll clipping");
            if (edit)
            {
                var segments = date.GetVisualDescendants().OfType<TextBlock>().Where(t => !string.IsNullOrEmpty(t.Text)).ToArray();
                Require(segments.Length >= 3 && segments.All(t => t.TranslatePoint(default, date)!.Value.X >= -1 &&
                    t.TranslatePoint(new Point(t.Bounds.Width, 0), date)!.Value.X <= date.Bounds.Width + 1), "Date clipped");
            }
            DeleteTodoNativeChecks.Capture(window, percent, $"{stem}-{percent}.png");
            toggle.IsChecked = true; ClockOverlayChecks.Pump(35);
            Require(host.IsVisible, "Checklist did not open");
            window.SizeToContent = SizeToContent.Manual; window.Height = 330 * scale; ClockOverlayChecks.Pump(80);
            Require(scroll.Extent.Height > scroll.Viewport.Height && save.TranslatePoint(default, window)!.Value.Y >=
                scroll.TranslatePoint(new Point(0, scroll.Bounds.Height), window)!.Value.Y - 1 &&
                save.TranslatePoint(new Point(0, save.Bounds.Height), window)!.Value.Y <= window.ClientSize.Height + 1, "Footer moved outside viewport");
            input.Text = "Revised print task"; priority.SelectedIndex = 4;
            if (edit) Get<Button>("ClearDueButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            else
            {
                if (percent != 90) Get<Button>("TodoPlacementButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                due.IsChecked = true; date.SelectedDate = new DateTimeOffset(Due.Date); time.SelectedTime = Due.TimeOfDay;
            }
            if (percent == 70)
            {
                input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Escape });
                Require(!window.IsVisible && Entries().Length == (edit ? 2 : 1) && (!edit || Entries().Contains(entry!)), "Cancel changed task");
            }
            else
            {
                var add = window.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "AddSubtask");
                add.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); toggle.IsChecked = false;
                save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); ClockOverlayChecks.Pump(25);
                Require(window.IsVisible && host.IsVisible &&
                    checklist.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Name == "SubtaskError" && t.IsVisible) &&
                    checklist.Count == (edit ? 3 : 1), "Hidden blank checklist row did not reopen for correction");
                checklist.GetVisualDescendants().OfType<TextBox>().Last().Text = "Send proof";
                toggle.IsChecked = false;
                if (percent == 90) input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter, KeyModifiers = KeyModifiers.Control });
                else save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                ClockOverlayChecks.Pump(60);
                var entries = Entries(); var updated = entries.Single(e => e.Text == input.Text);
                var organization = TodoOrganizationStore.Load(vault);
                Require(!window.IsVisible && entries.Length == 2 && entries.Any(e => e.Text == neighbor) && organization.GetPriority(updated) == 5 &&
                    organization.GetDueAt(updated) == (edit ? (DateTime?)null : Due), "Saved wrong task, priority or deadline");
                var saved = sync.GetDetails(settings, updated).Children;
                Require(saved.Last().Text == "Send proof" && saved.Count == (edit ? 3 : 1) && (!edit || saved.Take(2).SequenceEqual(children)), "Checklist lost data");
                Require(organization.Arrange(entries)[edit || percent == 125 ? 1 : 0] == updated, "Manual order changed");
            }
            Console.WriteLine($"PASS {Path.GetFileName(stem)} at {percent}%");
        }
        finally { window.Close(); }
    }

    static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
