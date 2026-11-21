using System.Reflection;
using Avalonia.Controls;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TodoSaveSyncPreview
{
    const string Output = "design/one-piece-v2/todo-save-sync/implemented";
    const string TaskTitle = "Review the print proof";
    static readonly string[] Tasks = ["Choose paper stock", "Check page numbers", "Match the cover colors",
        "Send final artwork", "Pack sample prints", "Archive the proofs", "File the color swatches", TaskTitle];
    enum Status { Current, Pending, Failed }

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
        var vault = Directory.CreateTempSubdirectory("stopwatch-todo-status-");
        var settings = new AppSettings {
            ThemeMode = AppThemeCatalog.Nika, TelegramEnabled = true, TelegramBotToken = "preview:local", TelegramChatId = "@print_studio", HabiticaEnabled = false,
            ObsidianVaultFolder = vault.FullName, TodoOverlayShowCompleted = true,
            TodoOverlayHasPosition = true, TodoOverlayLeft = 100, TodoOverlayTop = 80
        };
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(vault.FullName, "outbox.json"));
        var gate = (SemaphoreSlim)typeof(TelegramNotesSync).GetField("CompletionGate", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
        Require(gate.Wait(0), "Background Telegram sync is already active");
        TodoOverlayWindow? window = null;
        try
        {
            foreach (string task in Tasks)
                Require(ObsidianNotesSync.AppendEntry(vault.FullName, NoteType.Todo, task).Success, "Could not seed preview tasks");
            var entries = ObsidianNotesSync.LoadAllNotes(vault.FullName).ToDictionary(e => e.Text);
            for (int i = 0; i < Tasks.Length; i++)
                Require(TodoOrganizationStore.SetPriority(vault.FullName, entries[Tasks[i]], i + 1).Success, "Could not seed sample priorities");
            string vaultKey = TelegramNotesSync.GetTodoVaultKey(settings);
            for (int i = 0; i < Tasks.Length; i++)
                Require(TelegramOutboxStore.MarkSent(NoteType.Todo, Tasks[i], entries[Tasks[i]].Timestamp, 480+i,
                    settings.TelegramChatId, "preview", vaultKey), "Could not seed Telegram receipt");
            var receipt = TelegramOutboxStore.GetTrackedTodos("preview", vaultKey).Single(r => r.Text == TaskTitle);
            string taskFile = Directory.GetFiles(vault.FullName, "*.md", SearchOption.AllDirectories).Single();
            window = new TodoOverlayWindow(settings) { Topmost = false, ShowActivated = false };
            window.Show();
            window.ToggleExpanded();
            HoldToCompleteCheckBox TaskCheckBox(string title) => window.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>()
                .Single(r => ((NoteEntry)r.Tag!).Text == title).Children.OfType<HoldToCompleteCheckBox>().Single();
            foreach (int scale in new[] { 70, 90, 125 })
            {
                settings.UiScalePercent = scale;
                var footerHeights = new Dictionary<string, int>();
                foreach (string profile in new[] { "classic", "pixel" })
                {
                    settings.TodoAppearance.Profile = profile;
                    foreach (Status state in Enum.GetValues<Status>())
                    {
                        window.ApplySettings();
                        TaskCheckBox(TaskTitle).IsChecked = false;
                        ClockOverlayChecks.Pump(35);
                        Require(TelegramOutboxStore.AcknowledgeTodoState(receipt, false), "Cannot stage open receipt");
                        var attributes = File.GetAttributes(taskFile);
                        try
                        {
                            if (state == Status.Failed) File.SetAttributes(taskFile, attributes | FileAttributes.ReadOnly);
                            TaskCheckBox(TaskTitle).IsChecked = true;
                            ClockOverlayChecks.Pump(55);
                        }
                        finally { if (state == Status.Failed) File.SetAttributes(taskFile, attributes); }
                        Require(ObsidianNotesSync.LoadAllNotes(vault.FullName).Single(e => e.Text == TaskTitle).IsCompleted
                            == (state != Status.Failed), "Checkbox did not reflect the result of the Markdown write");
                        if (state == Status.Current)
                            Require(TelegramOutboxStore.AcknowledgeTodoState(receipt, true), "Cannot acknowledge Telegram completion");
                        window.ReloadTodos();
                        string path = Path.Combine(Output, $"{profile}-{state.ToString().ToLowerInvariant()}-{scale}.png");
                        footerHeights[path] = Capture(window, state, profile, scale, path);
                    }
                }
                TodoTaskMenuPreview.Comparison(Output, scale, ["current", "pending", "failed"],
                    ["Up to date", "Saved locally · Sync pending", "Local save error"], "messages", footerHeights);
            }
            var host = window.FindControl<ScrollViewer>("TodoSaveNotice")!;
            var hint = window.FindControl<TextBlock>("TelegramSyncStatus")!;
            string? Heading()
            {
                ClockOverlayChecks.Pump(40);
                return host.IsVisible ? host.GetVisualDescendants().OfType<TextBlock>().First().Text : null;
            }
            TaskCheckBox(TaskTitle).IsChecked = true;
            ClockOverlayChecks.Pump(60);
            Require(Heading() == "Saved locally", "Retry after a file becomes writable kept the old error");
            Require(TelegramOutboxStore.QueueTodoState(receipt, true) && TelegramOutboxStore.AcknowledgeTodoState(receipt, true),
                "Could not acknowledge the retried change");
            window.ReloadTodos();
            Require(Heading() == "Telegram sync is up to date", "Acknowledgement did not clear the pending notice");
            var currentFrame = host.Content;
            window.ReloadTodos();
            Require(ReferenceEquals(currentFrame, host.Content), "Idle refresh rebuilt the notice");

            string title = string.Join(" ", Enumerable.Repeat("Check the extended printer settings", 20));
            var created = ObsidianNotesSync.AppendEntry(vault.FullName, NoteType.Todo, title);
            Require(created.Success, "Could not seed a long unlinked todo");
            var entry = ObsidianNotesSync.LoadAllNotes(vault.FullName).Single(e => e.Text == title);
            TelegramOutboxStore.Enqueue(NoteType.Todo, title, entry.Timestamp, "another-bot", vaultKey);
            window.ReloadTodos();
            Require(!host.IsVisible && hint.IsVisible && hint.Text!.Contains("Link"), "Another bot's queue displaced linking guidance");
            var queued = TelegramOutboxStore.Enqueue(NoteType.Todo, title, entry.Timestamp, "preview", vaultKey);
            window.ReloadTodos();
            Require(Heading() == "Saved locally", "Queued first delivery was shown as an unlinked task");
            Require(TelegramOutboxStore.Remove(queued.Id), "Could not clear the sample queue");
            settings.TelegramEnabled = false;
            window.ApplySettings();
            Require(!host.IsVisible && !hint.IsVisible, "Disabled Telegram displayed a sync state");
            var originalAttributes = File.GetAttributes(taskFile);
            try
            {
                File.SetAttributes(taskFile, originalAttributes | FileAttributes.ReadOnly);
                TaskCheckBox(title).IsChecked = true;
                ClockOverlayChecks.Pump(70);
            }
            finally { File.SetAttributes(taskFile, originalAttributes); }
            Require(Heading() == "Couldn’t mark task complete" && host.Extent.Height > host.Viewport.Height &&
                !TaskCheckBox(title).IsChecked.GetValueOrDefault(), "Long local error lost its text or persisted a failed write");
            host.ScrollToEnd();
            ClockOverlayChecks.Pump(40);
            var detail = host.GetVisualDescendants().OfType<TextBlock>().Last();
            Require(detail.Text!.EndsWith("complete again.") && TodoDetailMenuPreview.BoundsOnDesktop(detail).Bottom <=
                TodoDetailMenuPreview.BoundsOnDesktop(host).Bottom + 1, "End of long error is unreachable");
        }
        finally { window?.Close(); ClockOverlayChecks.Pump(50); gate.Release(); TelegramOutboxStore.SetCustomFilePath(null); vault.Delete(true); }
    }

    static int Capture(TodoOverlayWindow window, Status state, string profile, int scale, string path)
    {
        var message = window.FindControl<ScrollViewer>("TodoSaveNotice")?.Content as TodoFrame
            ?? throw new InvalidOperationException($"Missing live save/sync notice at {profile}/{scale}/{state}");
        Require(message.IsEffectivelyVisible, $"Hidden save/sync notice at {profile}/{scale}/{state}");
        ClockOverlayChecks.Pump(60);
        var scroll = window.FindControl<ScrollViewer>("TodoScrollViewer")!;
        scroll.ScrollToEnd();
        ClockOverlayChecks.Pump(60);
        var button = window.FindControl<Button>("AddTodoButton")!;
        var footer = TodoDetailMenuPreview.BoundsOnDesktop(message);
        var action = TodoDetailMenuPreview.BoundsOnDesktop(button);
        var canvas = TodoDetailMenuPreview.BoundsOnDesktop(window);
        Require(footer.Bottom <= action.Top && canvas.Contains(action) && button.IsEffectivelyEnabled && button.IsEffectivelyVisible,
            $"Add todo is obstructed at {profile}/{scale}/{state}");
        var text = message.GetVisualDescendants().OfType<TextBlock>().ToArray();
        string[] expected = state switch {
            Status.Current => ["Telegram sync is up to date"],
            Status.Pending => ["Saved locally", "Telegram sync is pending. Stopwatch will retry automatically."],
            _ => ["Couldn’t mark task complete", $"The task file in your notes folder is read-only. Allow editing of the file, then mark “{TaskTitle}” complete again."]
        };
        Require(text.Select(t => t.Text).SequenceEqual(expected), $"Wrong notice at {profile}/{scale}/{state}: {string.Join(" / ", text.Select(t => t.Text))}");
        Require(message.Pixel == (profile == "pixel"), "Notice used the wrong profile");
        Require(text.All(t => footer.Inflate(1).Contains(TodoDetailMenuPreview.BoundsOnDesktop(t)) &&
            t.TextLayout.Height <= t.Bounds.Height + 1 && !t.TextLayout.TextLines.Any(line => line.HasCollapsed)),
            $"Clipped message at {profile}/{scale}/{state}");
        Require(state == Status.Current ? text.Single().TextLayout.TextLines.Count == 1 :
            state != Status.Failed || text.Last().TextLayout.TextLines.Count >= 3, "Unexpected message wrapping");
        var rows = window.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().ToArray();
        var row = rows.Single(r => ((NoteEntry)r.Tag!).Text == TaskTitle);
        var viewport = TodoDetailMenuPreview.BoundsOnDesktop(scroll);
        Require(viewport.Inflate(1).Contains(TodoDetailMenuPreview.BoundsOnDesktop(row)) &&
            rows.Count(r => viewport.Inflate(1).Contains(TodoDetailMenuPreview.BoundsOnDesktop(r))) >= 3 &&
            row.Children.OfType<HoldToCompleteCheckBox>().Single().IsChecked == (state != Status.Failed),
            $"Task state or visible list context is wrong at {profile}/{scale}/{state}");
        TodoReorderChecks.Capture(window, path);
        return (int)Math.Ceiling(canvas.Bottom - TodoDetailMenuPreview.BoundsOnDesktop(row).Top);
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
