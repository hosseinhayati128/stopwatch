using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TodoDetailHoverHintChecks
{
    const string Output = "design/one-piece-v2/todo-detail-hover-hints/implemented";

    internal static void Run() => TodoDetailMenuPreview.RunSamples(Output, "hints", Capture, CheckMultipleDetails);

    static void CheckMultipleDetails(TodoOverlayWindow overlay, AppSettings settings)
    {
        string task = TodoDetails.Append("Check the proofs", TodoDetailKind.Subtask, "Review alignment");
        task = TodoDetails.Append(task, TodoDetailKind.Note, "Use matte paper");
        task = TodoDetails.Append(task, TodoDetailKind.Note, "See [press settings](https://example.com/press) and [trim sizes](https://example.com/trim)");
        Require(ObsidianNotesSync.AppendEntry(settings.ObsidianVaultFolder, NoteType.Todo, task).Success, "Cannot seed mixed details");
        overlay.ReloadTodos();
        ClockOverlayChecks.Pump(50);
        var rows = overlay.FindControl<StackPanel>("TodoRows")!;
        var card = rows.Children.OfType<Grid>().Single(g => ((NoteEntry)g.Tag!).Text == task);
        card.GetVisualDescendants().OfType<Button>().Single(b => b.Name == "ToggleTodoDetails").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ClockOverlayChecks.Pump(60);
        var panel = rows.GetVisualDescendants().OfType<StackPanel>().Single(p => p.Name == "TodoDetailsPanel" && p.Children.Count == 3);
        var notes = panel.Children.OfType<Border>().Select(b => b.GetVisualDescendants().OfType<TextBlock>().Single(t => t.ContextMenu == b.ContextMenu)).ToArray();
        Require(AutomationProperties.GetHelpText(notes[0]) == "Right-click for note actions." &&
            AutomationProperties.GetHelpText(notes[1]) == "Click a link to open it. Right-click for note actions.", "Note actions did not reflect its links");
        Require(notes[1].ContextMenu!.Items.OfType<MenuItem>().Count(m => m.Classes.Contains("detailLink")) == 2, "Missing link actions");
        panel.GetVisualDescendants().OfType<HoldToCompleteCheckBox>().Single().IsChecked = true;
        ClockOverlayChecks.Pump(100);
        var completed = rows.GetVisualDescendants().OfType<HoldToCompleteCheckBox>().Single(c => c.Name == "SubtaskCheckBox");
        Require(completed.IsChecked == true && AutomationProperties.GetHelpText(completed) == "Hold for 1 second to reopen.", "Hint did not refresh after completion");
    }

    static void Capture(TodoOverlayWindow overlay, TodoFrame detail, Control child, string profile, string state, int scale)
    {
        var label = child.GetVisualDescendants().OfType<TextBlock>().Single(t => t.ContextMenu == child.ContextMenu);
        bool note = state == "linked-note";
        var owner = note ? (Control)label : child.GetVisualDescendants().OfType<HoldToCompleteCheckBox>().Single();
        var hint = ToolTip.GetTip(owner) as ToolTip ?? throw new InvalidOperationException($"Missing native hint on {profile}/{state}");
        string[] expected = note ? ["Click to open the link", "Right-click for note actions"]
            : [state == "open-subtask" ? "Hold for 1 second to complete" : "Hold for 1 second to reopen"];
        Require(expected.All(line => AutomationProperties.GetHelpText(owner)?.Contains(line) == true), "Accessible help is missing");
        if (note)
        {
            var lines = label.TextLayout.TextLines;
            int length = label.Inlines!.OfType<Run>().Sum(r => r.Text!.Length);
            Require(lines.Count > 1 && lines.All(l => !l.HasCollapsed) && lines[^1].FirstTextSourceIndex + lines[^1].Length >= length
                && label.TextLayout.Height <= label.Bounds.Height + 1, "Long link label clipped");
        }
        RunningClocksChecks.Pointer(overlay, label, RawPointerEventType.Move);
        try
        {
            var wait = System.Diagnostics.Stopwatch.StartNew();
            while (!ToolTip.GetIsOpen(owner) && wait.Elapsed < TimeSpan.FromSeconds(4)) ClockOverlayChecks.Pump(25);
            if (!ToolTip.GetIsOpen(owner))
            {
                var point = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), overlay)!.Value;
                var hit = overlay.InputHitTest(point) as Control;
                string PathOf(Control? control) => control == null ? "none" : string.Join("/", control.GetSelfAndVisualAncestors().OfType<Control>().Take(9).Select(c => c.Name ?? c.GetType().Name));
                throw new InvalidOperationException($"Hover did not open {profile}/{state}/{scale} at {point}: hit {PathOf(hit)}, target {PathOf(label)}; " +
                    $"label {label.IsPointerOver}/{label.IsHitTestVisible}, owner {owner.IsPointerOver}, attached {owner.IsAttachedToVisualTree()}, bounds {label.Bounds}");
            }
            ClockOverlayChecks.Pump(40);
            var frame = hint.GetVisualDescendants().OfType<TodoFrame>().Single();
            var bounds = TodoDetailMenuPreview.BoundsOnDesktop(frame);
            var anchor = TodoDetailMenuPreview.BoundsOnDesktop(owner);
            Require(frame.IsEffectivelyVisible && frame.Pixel == (profile == "pixel"), "Incorrect theme");
            Require(Math.Abs(bounds.Center.Y - anchor.Center.Y) <= 2 &&
                Math.Abs(bounds.Left - TodoDetailMenuPreview.BoundsOnDesktop(detail).Right - 7 * scale / 100d) <= 2, "Incorrect detail anchor");
            var text = frame.GetVisualDescendants().OfType<TextBlock>().ToArray();
            Require(text.Select(t => string.Concat(t.Inlines!.OfType<Run>().Select(r => r.Text))).SequenceEqual(expected), "Incorrect actions");
            Require(text.All(t => t.TextLayout.TextLines.Count == 1 && !t.TextLayout.TextLines[0].HasCollapsed &&
                t.TextLayout.Height <= t.Bounds.Height + 1 && bounds.Inflate(1).Contains(TodoDetailMenuPreview.BoundsOnDesktop(t))), "Clipped hint");
            TodoDetailMenuPreview.Capture(overlay, detail, frame, Path.Combine(Output, $"{profile}-{state}-{scale}.png"));
        }
        finally
        {
            RunningClocksChecks.Pointer(overlay, overlay.FindControl<Button>("AddTodoButton")!, RawPointerEventType.Move);
            ClockOverlayChecks.Pump(40);
        }
        Require(!ToolTip.GetIsOpen(owner), "Hint stayed open after leaving the detail");
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
