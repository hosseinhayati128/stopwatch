using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TodoHoverHintsPreview
{
    const string Output = "design/one-piece-v2/todo-hover-hints/implemented";
    static readonly string[] Titles = ["Check paper stock", "Review the print proof", "Send the final artwork"];
    static readonly string[] States = ["open", "in-progress", "completed"];

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        var vault = Directory.CreateTempSubdirectory("stopwatch-hover-hints-");
        var settings = new AppSettings {
            ThemeMode = AppThemeCatalog.Nika, ObsidianVaultFolder = vault.FullName, TodoOverlayShowCompleted = true,
            TelegramEnabled = false, HabiticaEnabled = false, TodoOverlayHasPosition = true, TodoOverlayLeft = 90, TodoOverlayTop = 70
        };
        TodoOverlayWindow? overlay = null;
        try
        {
            DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
            foreach (string title in Titles)
                Require(ObsidianNotesSync.AppendEntry(vault.FullName, NoteType.Todo, title).Success, "Could not seed task");
            var entries = ObsidianNotesSync.LoadAllNotes(vault.FullName).ToDictionary(e => e.Text);
            var due = DateTime.Today.AddDays(1).AddHours(9).AddMinutes(30);
            for (int i = 0; i < Titles.Length; i++)
                Require(TodoOrganizationStore.SaveTodo(vault.FullName, entries[Titles[i]], Titles[i],
                    new[] { 3, 2, 6 }[i], i == 1 ? due : null).Success, "Could not seed task metadata");
            Require(TodoOrganizationStore.SetInProgress(vault.FullName, entries[Titles[1]], true).Success &&
                ObsidianNotesSync.SetTodoCompleted(vault.FullName, entries[Titles[2]], true).Success, "Could not seed task states");
            var organization = TodoOrganizationStore.Load(vault.FullName);
            overlay = new TodoOverlayWindow(settings) { ShowActivated = false, Topmost = false };
            overlay.Show();
            overlay.ToggleExpanded();
            foreach (int scale in new[] { 70, 90, 125 })
            {
                settings.UiScalePercent = scale;
                foreach (string profile in new[] { "classic", "pixel" })
                {
                    settings.TodoAppearance.Profile = profile;
                    overlay.ApplySettings();
                    ClockOverlayChecks.Pump(60);
                    for (int i = 0; i < States.Length; i++)
                    {
                        var card = overlay.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>()
                            .Single(row => ((NoteEntry)row.Tag!).Text == Titles[i]);
                        var task = (NoteEntry)card.Tag!;
                        bool completed = task.IsCompleted, inProgress = organization.IsInProgress(task);
                        DateTime? deadline = organization.GetDueAt(task);
                        Require(card.Children.OfType<HoldToCompleteCheckBox>().Single().IsChecked == (i == 2) &&
                            inProgress == (i == 1) && deadline.HasValue == inProgress, "Incorrect task context");
                        string? date = deadline is DateTime at ? TodoDeadlines.Label(at, TodoDeadlines.GetState(task, at, DateTime.Now)) : null;
                        Capture(overlay, card, completed, inProgress, date, profile, scale, States[i]);
                    }
                }
                TodoTaskMenuPreview.Comparison(Output, scale, States, ["Open task", "In progress · With deadline", "Completed task"], "hints");
            }
            CheckChanges(overlay, settings, entries[Titles[1]]);
        }
        finally { overlay?.Close(); vault.Delete(true); }
    }

    static void CheckChanges(TodoOverlayWindow overlay, AppSettings settings, NoteEntry task)
    {
        Grid Card() => overlay.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().Single(r => ((NoteEntry)r.Tag!).Text == task.Text);
        settings.UiScalePercent = 90;
        typeof(TodoOverlayWindow).GetMethod("ApplyTaskMenuScale", System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance)!.Invoke(overlay, null);
        var area = overlay.Screens.ScreenFromVisual(overlay)!.WorkingArea;
        overlay.Position = new PixelPoint(area.Right - (int)Math.Ceiling(overlay.Bounds.Width * overlay.RenderScaling), overlay.Position.Y);
        ClockOverlayChecks.Pump(60);
        var currentDue = TodoOrganizationStore.Load(settings.ObsidianVaultFolder).GetDueAt(task)!.Value;
        Capture(overlay, Card(), false, true, "Due · " + currentDue.ToString("ddd, dd MMM yyyy · HH:mm"), "pixel", 90, null, left: true);
        var overdue = DateTime.Today.AddDays(-1).AddHours(11);
        Require(TodoOrganizationStore.SaveTodo(settings.ObsidianVaultFolder, task, task.Text, 2, overdue).Success &&
            TodoOrganizationStore.SetInProgress(settings.ObsidianVaultFolder, task, false).Success, "Could not update the test deadline/status");
        overlay.ReloadTodos();
        ClockOverlayChecks.Pump(50);
        Capture(overlay, Card(), false, false, "Overdue · " + overdue.ToString("ddd, dd MMM yyyy · HH:mm"), "pixel", 90, null, left: true);
        Card().Children.OfType<HoldToCompleteCheckBox>().Single().IsChecked = true;
        ClockOverlayChecks.Pump(100);
        Capture(overlay, Card(), true, false, null, "pixel", 90, null, left: true);
        Require(TodoOrganizationStore.SetSortMode(settings.ObsidianVaultFolder, "Manual").Success, "Could not switch sort mode");
        overlay.ReloadTodos();
        ClockOverlayChecks.Pump(50);
        Capture(overlay, Card(), true, false, null, "pixel", 90, null, left: true, manual: true);
        Require(ToolTip.GetTip(Card().Children.OfType<Button>().Single(b => b.Classes.Contains("todoDragHandle"))) is string,
            "Manual drag handle lost its reorder hint");
        settings.ThemeMode = AppThemeCatalog.Daylight;
        DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
        overlay.ApplySettings();
        Require(ToolTip.GetTip(Card().Children.OfType<HoldToCompleteCheckBox>().Single()) is string legacy &&
            legacy.Contains("mark incomplete"), "Daylight lost its task tooltip");
    }

    static void Capture(Window overlay, Grid card, bool completed, bool inProgress, string? date, string profile, int scale,
        string? state, bool left = false, bool manual = false)
    {
        var checkbox = card.Children.OfType<HoldToCompleteCheckBox>().Single();
        var tip = ToolTip.GetTip(checkbox) as ToolTip
            ?? throw new InvalidOperationException($"Task row is missing its native {profile} hover hint");
        var title = TodoReorderChecks.Title(card);
        RunningClocksChecks.Pointer(overlay, title, RawPointerEventType.Move);
        try
        {
            var wait = System.Diagnostics.Stopwatch.StartNew();
            while (!ToolTip.GetIsOpen(checkbox) && wait.Elapsed < TimeSpan.FromSeconds(4))
                ClockOverlayChecks.Pump(30);
            ClockOverlayChecks.Pump(40);
            var point = title.TranslatePoint(new Point(title.Bounds.Width / 2, title.Bounds.Height / 2), overlay)!.Value;
            Require(ToolTip.GetIsOpen(checkbox), $"Hover over the task title did not open its hint ({profile}/{state}); " +
                $"hit {overlay.InputHitTest(point)}, pointer over {title.IsPointerOver}/{checkbox.IsPointerOver}, " +
                $"attached {checkbox.IsAttachedToVisualTree()}, delay {ToolTip.GetShowDelay(checkbox)}, point {point}, row {card.Bounds}");
            var frame = tip.GetVisualDescendants().OfType<TodoFrame>().Single();
            Require(frame.Pixel == (profile == "pixel") && frame.IsEffectivelyVisible, "Incorrect hint profile");
            var row = TodoDetailMenuPreview.BoundsOnDesktop(card);
            var popup = TodoDetailMenuPreview.BoundsOnDesktop(frame);
            double gap = left ? row.Left - popup.Right : popup.Left - row.Right;
            Require(Math.Abs(gap - 7 * scale / 100d) <= 2 &&
                Math.Abs(popup.Center.Y - row.Center.Y) <= 2, $"Hint missed its row at {profile}/{state}/{scale}: {row} vs {popup}");
            var lines = frame.GetVisualDescendants().OfType<TextBlock>().ToArray();
            string hold = manual ? "Hold checkbox for 1 second" : "Hold for 1 second";
            var expected = new List<string> { hold + (completed ? " to reopen" : " to complete") };
            if (!completed) expected.Add(inProgress ? "Double-click to clear in progress" : "Double-click to mark in progress");
            if (date != null) expected.Add(date);
            var copy = lines.Select(t => string.Concat(t.Inlines!.OfType<Run>().Select(run => run.Text))).ToArray();
            Require(copy.SequenceEqual(expected), $"Wrong hint for {profile}/{state}: {string.Join(" / ", copy)}");
            Require(lines.All(t => popup.Inflate(1).Contains(TodoDetailMenuPreview.BoundsOnDesktop(t)) &&
                t.TextLayout.Height <= t.Bounds.Height + 1 && t.TextLayout.TextLines.Count == 1 &&
                !t.TextLayout.TextLines[0].HasCollapsed), $"Sample hint wrapped or clipped at {profile}/{state}/{scale}");
            Require(Math.Abs(lines[0].FontSize - Math.Max(12, 14 * scale / 100d)) < .01, "Hint retained a stale application scale");
            if (state != null)
                TodoDetailMenuPreview.Capture(overlay, card, frame, Path.Combine(Output, $"{profile}-{state}-{scale}.png"));
        }
        finally
        {
            RunningClocksChecks.Pointer(overlay, overlay.FindControl<Button>("AddTodoButton")!, RawPointerEventType.Move);
            ClockOverlayChecks.Pump(40);
        }
        Require(!ToolTip.GetIsOpen(checkbox), "Hint stayed open after leaving the task");
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
