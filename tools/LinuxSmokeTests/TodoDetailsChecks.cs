using StopwatchOverlay;
using StopwatchOverlay.Habitica;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using System.Reflection;
using StopwatchOverlay.Desktop.Views;
using StopwatchOverlay.Desktop.Controls;

internal static class TodoDetailsChecks
{
    public static void Run(string root)
    {
        string body = "Parent\n\n- ordinary **list**";
        string text = TodoDetails.Append(body, TodoDetailKind.Subtask, "First step\nMore detail");
        text = TodoDetails.Append(text, TodoDetailKind.Note, "Remember **this**\n\nSecond paragraph");
        var parsed = TodoDetails.Parse(text);
        if (parsed.Body != body || parsed.Items.Count != 2 || parsed.Items[0].Text != "First step\nMore detail" || parsed.Items[1].Text != "Remember **this**\n\nSecond paragraph") throw new Exception("Child detail parsing lost content.");
        string completed = TodoDetails.SetCompleted(text, parsed.Items[0], true);
        if (!TodoDetails.Parse(completed).Items[0].IsCompleted || TodoDetails.SetCompleted(completed, TodoDetails.Parse(completed).Items[0], false) != text) throw new Exception("Subtask checkbox round trip failed.");
        if (TodoDetails.Replace(text, parsed.Items[1], null).TrimEnd() != TodoDetails.Append(body, TodoDetailKind.Subtask, "First step\nMore detail")) throw new Exception("Removing a note changed its sibling.");
        string html = FormattedTodoText.ToTelegramHtml(completed);
        if (!html.Contains("☑ <s>First step\nMore detail</s>") || !html.Contains("<b>this</b>")) throw new Exception("Telegram lost child formatting or completion.");
        if (FormattedTodoText.ToTelegramHtml(text).Contains("<s>")) throw new Exception("Unchecked child stayed struck through.");
        string vault = Path.Combine(root, "todo-details");
        var timestamp = new DateTime(2026, 10, 19, 10, 0, 0);
        ObsidianNotesSync.AppendEntry(vault, NoteType.Todo, body, timestamp: timestamp);
        var parent = ObsidianNotesSync.LoadAllNotes(vault).Single();
        TodoOrganizationStore.SaveTodo(vault, parent, text, 2, timestamp.AddDays(1));
        var reloaded = ObsidianNotesSync.LoadAllNotes(vault).Single();
        if (reloaded.Text != text || reloaded.IsCompleted || TodoDetails.Parse(reloaded.Text).Items.Count != 2) throw new Exception("Children did not stay inside their parent Markdown block.");
        if (!TodoOrganizationStore.SaveTodo(vault, reloaded, completed, 2, timestamp.AddDays(1)).Success) throw new Exception("Could not check child.");
        if (TodoOrganizationStore.SaveTodo(vault, reloaded, text + "stale", 3, null).Success) throw new Exception("Stale child edit overwrote parent.");
        var final = ObsidianNotesSync.LoadAllNotes(vault).Single();
        var organization = TodoOrganizationStore.Load(vault);
        if (final.IsCompleted || organization.GetPriority(final) != 2 || organization.GetDueAt(final) != timestamp.AddDays(1)) throw new Exception("Checking child changed parent metadata.");
        Console.WriteLine("PASS attached notes and subtasks round trip, completion, stale edits and metadata");
    }
    public static void Ui(string root)
    {
        string vault = Path.Combine(root, "todo-details-ui"); Directory.CreateDirectory(vault);
        ObsidianNotesSync.AppendEntry(vault, NoteType.Todo, "Parent task");
        var settings = new AppSettings { ObsidianVaultFolder = vault, TodoOverlayShowCompleted = true };
        var overlay = new TodoOverlayWindow(settings); overlay.Show(); overlay.ToggleExpanded(); Pump();
        var rows = overlay.FindControl<StackPanel>("TodoRows")!;
        foreach (string header in new[] { "Note", "Subtask" })
        {
            var row = rows.Children.OfType<Grid>().Single();
            var menu = row.ContextMenu!;
            var add = menu.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Add");
            if (!add.Items.OfType<MenuItem>().Select(item => item.Header).SequenceEqual(new[] { "Note", "Subtask" })) throw new Exception("Add submenu has incorrect choices.");
            add.Items.OfType<MenuItem>().Single(item => (string?)item.Header == header).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump();
            var editor = (NoteEntryWindow)typeof(TodoOverlayWindow).GetField("_taskEditor", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(overlay)!;
            if (editor.Title != "Add " + header.ToLowerInvariant() || editor.FindControl<StackPanel>("TodoDetailsPanel")!.IsVisible) throw new Exception("Detail editor has incorrect mode.");
            editor.FindControl<TextBox>("NoteInputBox")!.Text = header + " **detail**";
            editor.FindControl<TextBox>("NoteInputBox")!.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = KeyModifiers.Control }); Pump();
            if (editor.IsVisible) throw new Exception("Ctrl+Enter did not save attached detail.");
        }
        var savedParent = ObsidianNotesSync.LoadAllNotes(vault).Single();
        var originalChildren = HabiticaIntegration.Current.GetDetails(settings, savedParent).Children.ToArray();
        var noteDetail = TodoDetails.Parse(savedParent.Text).Items.Single(item => item.Kind == TodoDetailKind.Note);
        var saveDetail = typeof(TodoOverlayWindow).GetMethod("SaveDetail", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var saved = (NoteSyncResult)saveDetail.Invoke(overlay, new object[] { savedParent, TodoDetails.Replace(savedParent.Text, noteDetail, "Updated note") })!;
        Pump();
        var updatedParent = ObsidianNotesSync.LoadAllNotes(vault).Single();
        if (!saved.Success || !HabiticaIntegration.Current.GetDetails(settings, updatedParent).Children.SequenceEqual(originalChildren))
            throw new Exception("Editing attached note lost Habitica checklist metadata.");
        var staleResult = (NoteSyncResult)saveDetail.Invoke(overlay, new object[] { savedParent, "Stale parent" })!;
        Pump();
        if (staleResult.Success || ObsidianNotesSync.LoadAllNotes(vault).Single().Text != updatedParent.Text)
            throw new Exception("A stale attached-note snapshot overwrote the task.");
        if (rows.GetVisualDescendants().Any(control => control.Name == "TodoDetailsPanel")) throw new Exception("Details are not collapsed by default.");
        rows.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "ToggleTodoDetails").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        var child = rows.GetVisualDescendants().OfType<HoldToCompleteCheckBox>().Single(box => box.Name == "SubtaskCheckBox");
        child.IsChecked = true; Pump();
        var parent = ObsidianNotesSync.LoadAllNotes(vault).Single();
        if (parent.IsCompleted || !HabiticaIntegration.Current.GetDetails(settings, parent).Children.Single().Completed) throw new Exception("Completing child changed parent or was not saved.");
        if (!rows.GetVisualDescendants().Any(control => control.Name == "TodoDetailsPanel")) throw new Exception("Checking child collapsed its parent.");
        child = rows.GetVisualDescendants().OfType<HoldToCompleteCheckBox>().Single(box => box.Name == "SubtaskCheckBox");
        child.ContextMenu!.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Edit subtask").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump();
        var editChild = (NoteEntryWindow)typeof(TodoOverlayWindow).GetField("_taskEditor", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(overlay)!;
        if (editChild.FindControl<TextBox>("NoteInputBox")!.Text != "Subtask **detail**") throw new Exception("Child editor was not prefilled.");
        editChild.Close(); Pump();
        child.ContextMenu!.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Delete").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump();
        var confirmation = (ConfirmationDialogWindow)typeof(TodoOverlayWindow).GetField("_deleteDialog", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(overlay)!;
        if (confirmation == null || !confirmation.IsVisible) throw new Exception("Detail deletion did not ask for confirmation.");
        string alternate = Path.Combine(root, "todo-details-alternate"); Directory.CreateDirectory(alternate);
        var original = ObsidianNotesSync.LoadAllNotes(vault).Single();
        ObsidianNotesSync.AppendEntry(alternate, NoteType.Todo, original.Text, timestamp: original.Timestamp);
        settings.ObsidianVaultFolder = alternate;
        confirmation.FindControl<Button>("ConfirmButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        if (ObsidianNotesSync.LoadAllNotes(alternate).Single().Text != original.Text || ObsidianNotesSync.LoadAllNotes(vault).Single().Text != original.Text) throw new Exception("Changed notes folder redirected detail deletion.");
        settings.ObsidianVaultFolder = vault; overlay.ReloadTodos(); Pump();
        child = rows.GetVisualDescendants().OfType<HoldToCompleteCheckBox>().Single(box => box.Name == "SubtaskCheckBox");
        child.ContextMenu!.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Delete").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump();
        confirmation = (ConfirmationDialogWindow)typeof(TodoOverlayWindow).GetField("_deleteDialog", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(overlay)!;
        confirmation.FindControl<Button>("ConfirmButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Pump();
        if (HabiticaIntegration.Current.GetDetails(settings, ObsidianNotesSync.LoadAllNotes(vault).Single()).Children.Count != 0) throw new Exception("Confirmed detail deletion did not save.");
        // Metadata failure must keep new child content in its editor for a safe retry.
        var finalRow = rows.Children.OfType<Grid>().Single();
        finalRow.ContextMenu!.Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Add")
            .Items.OfType<MenuItem>().Single(item => (string?)item.Header == "Subtask").RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent)); Pump();
        var retryEditor = (NoteEntryWindow)typeof(TodoOverlayWindow).GetField("_taskEditor", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(overlay)!;
        retryEditor.FindControl<TextBox>("NoteInputBox")!.Text = "Retained child";
        string metadataPath = Path.Combine(vault, "Notes", ".stopwatch-habitica.json");
        string metadata = File.ReadAllText(metadataPath);
        File.WriteAllText(metadataPath, "invalid metadata");
        retryEditor.FindControl<TextBox>("NoteInputBox")!.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = KeyModifiers.Control }); Pump();
        if (!retryEditor.IsVisible || retryEditor.FindControl<TextBlock>("SaveError")!.IsVisible != true)
            throw new Exception("Unpersisted child was reported as saved and its editor closed.");
        File.WriteAllText(metadataPath, metadata);
        retryEditor.FindControl<TextBox>("NoteInputBox")!.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Enter, KeyModifiers = KeyModifiers.Control }); Pump();
        var retriedChildren = HabiticaIntegration.Current.GetDetails(settings, ObsidianNotesSync.LoadAllNotes(vault).Single()).Children;
        if (retryEditor.IsVisible || retriedChildren.Count != 1 || retriedChildren.Single().Text != "Retained child")
            throw new Exception("Retry failed to save exactly one retained child.");
        overlay.Close();
        Console.WriteLine("PASS Add submenu, child editors, collapsed details, child completion and edit");
    }
    private static void Pump()
    {
        using var cancellation = new CancellationTokenSource();
        DispatcherTimer.RunOnce(cancellation.Cancel, TimeSpan.FromMilliseconds(180));
        Dispatcher.UIThread.MainLoop(cancellation.Token); Dispatcher.UIThread.RunJobs();
    }

}
