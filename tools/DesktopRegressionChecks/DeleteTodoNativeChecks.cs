using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static partial class DeleteTodoNativeChecks
{
    const string Output = "design/one-piece-v2/delete-todo/implemented";
    const string Short = "Review the dashboard sketches.";
    const string Long = "Review the dashboard sketches and the full accessibility checklist before tomorrow’s planning session. Bring the notes about keyboard navigation, smaller screens, timer switching, and the remaining reminder improvements.";
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    static void Require(bool ok, string message)
    {
        if (!ok) throw new Exception(message);
    }

    internal static void Desktop(string type, string method, params object[] args) =>
        typeof(ConfirmationDialogWindow).Assembly.GetType("StopwatchOverlay.Desktop." + type)!
            .GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);

    static ConfirmationDialogWindow Open(AppSettings settings, string task, bool linked)
    {
        var dialog = new ConfirmationDialogWindow { ShowActivated = false };
        var configure = dialog.GetType().GetMethod("ConfigureDeleteTodo", Private)
            ?? throw new Exception("Missing Delete Todo presentation");
        configure.Invoke(dialog, [task, linked, settings]);
        dialog.Show();
        ClockOverlayChecks.Pump(80);
        return dialog;
    }

    static void Press(Control target, Key key)
    {
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = key });
        ClockOverlayChecks.Pump(25);
    }

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, TelegramEnabled = false, HabiticaEnabled = false };
        Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
        Desktop("DesktopPageAppearance", "Initialize", settings);
        Desktop("DesktopTypography", "Initialize", settings.Typography);
        try
        {
            foreach (int scale in new[] { 70, 90, 125 })
            {
                settings.UiScalePercent = scale;
                Desktop("DesktopPageAppearance", "Apply", settings, true);
                foreach (bool linked in new[] { true, false })
                foreach (string task in new[] { Short, Long })
                {
                    var window = Open(settings, task, linked);
                    try
                    {
                        var labels = window.GetVisualDescendants().OfType<TextBlock>().ToArray();
                        Require(labels.Any(t => t.Text == task), "Task missing");
                        Require(labels.Any(t => t.Text == (linked ? "Linked message" : "No linked message")), "Incorrect Telegram state");
                        Require(labels.Any(t => t.Text == (linked ? "Deletes its Telegram message and cancels unsent copies." : "Cancels any unsent Telegram copy.")), "Incorrect deletion effect");
                        Require(labels.All(t => t.TextLayout.Height <= t.Bounds.Height + 1), "Clipped text");
                        Require(window.GetVisualDescendants().OfType<ScrollViewer>().All(s => s.Extent.Height <= s.Viewport.Height + 1), "Sample needs scrolling");
                        var cancel = window.FindControl<Button>("CancelButton")!;
                        var delete = window.FindControl<Button>("ConfirmButton")!;
                        Require(cancel.IsFocused, "Cancel lacks initial focus");
                        Require(Math.Abs(window.ClientSize.Width - 540 * scale / 100d) < 1.1 &&
                            Math.Abs(window.ClientSize.Height - (task == Long ? 500 : 430) * scale / 100d) < 1.1, "Incorrect size");
                        string state = (task == Long ? "long-" : "") + (linked ? "linked" : "unlinked");
                        Capture(window, scale, Path.Combine(Output, $"delete-todo-{state}-{scale}.png"));
                        if (task == Short) Press(cancel, Avalonia.Input.Key.Enter);
                        else { delete.Focus(); Press(delete, linked ? Avalonia.Input.Key.Escape : Avalonia.Input.Key.Enter); }
                        bool expected = task == Long && !linked;
                        Require(!window.IsVisible && window.Result == expected, "Wrong keyboard result");
                        delete.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                        Require(window.Result == expected, "Stale click changed result");
                        Console.WriteLine($"PASS Delete Todo {state} {scale}%");
                    }
                    finally { window.Close(); }
                }
            }
            OverlayDeletion(settings);
            settings.TodoAppearance.Profile = "pixel";
            settings.TodoAppearance.Pixel.Background = "midnight-chart";
            settings.TodoAppearance.Pixel.EngravedHeader = false;
            string rtl = string.Join(" ", Enumerable.Repeat("یادداشت برای فردا", 75));
            var pixel = Open(settings, rtl, true);
            try
            {
                Require(pixel.GetVisualDescendants().OfType<TodoFrame>().Any(f => f.Pixel), "Pixel frame missing");
                Require(!pixel.GetVisualDescendants().OfType<TodoArtwork>().Any(a => a.Kind == "Header"), "Engraving setting ignored");
                Require(pixel.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Text == rtl).FlowDirection == FlowDirection.RightToLeft, "Incorrect task direction");
                Require(pixel.GetVisualDescendants().OfType<ScrollViewer>().Any(s => s.Extent.Height > s.Viewport.Height + 1), "Unbounded long task");
            }
            finally { pixel.Close(); }
            settings.ThemeMode = AppThemeCatalog.Daylight;
            Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
            var legacy = Open(settings, Short, false);
            try
            {
                Require(legacy.FindControl<TextBlock>("MessageText")!.Text!.Contains("No linked message"), "Legacy dialog lost Telegram state");
                Require(legacy.FindControl<Button>("CancelButton")!.IsFocused, "Legacy initial focus unsafe");
                Press(legacy, Avalonia.Input.Key.Escape);
                Require(!legacy.IsVisible && !legacy.Result, "Legacy escape confirmed");
            }
            finally { legacy.Close(); }
        }
        finally
        {
            Desktop("DesktopTypography", "Shutdown");
            Desktop("DesktopPageAppearance", "Shutdown");
        }
    }

    internal static void Capture(Window window, int scale, string path)
    {
        var root = (Control)((LayoutTransformControl)window.Content!).Child!;
        using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(window.ClientSize.Width), (int)Math.Ceiling(window.ClientSize.Height)), new Vector(96, 96));
        if (scale >= 100) bitmap.Render(window);
        else
        {
            using var dc = bitmap.CreateDrawingContext();
            dc.DrawRectangle(new VisualBrush(root) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(root.Bounds, RelativeUnit.Absolute) }, null, new Rect(root.Bounds.Size));
        }
        bitmap.Save(path);
    }

    static void OverlayDeletion(AppSettings settings)
    {
        var directory = Directory.CreateTempSubdirectory("stopwatch-delete-todo-");
        settings.ObsidianVaultFolder = directory.FullName;
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(directory.FullName, "outbox.json"));
        TodoOverlayWindow? overlay = null;
        try
        {
            foreach (string name in new[] { "Keep neighbor", "Linked sample", "Unsent sample" })
                Require(ObsidianNotesSync.AppendEntry(directory.FullName, NoteType.Todo, name).Success, "Cannot seed task");
            var entries = ObsidianNotesSync.LoadAllNotes(directory.FullName);
            var linked = entries.Single(e => e.Text == "Linked sample");
            var unsent = entries.Single(e => e.Text == "Unsent sample");
            var vault = TelegramNotesSync.GetTodoVaultKey(settings);
            settings.TelegramBotToken = "123:fixture";
            TelegramOutboxStore.MarkSent(NoteType.Todo, linked.Text, linked.Timestamp, 42, "-123", "123", vault);
            TelegramOutboxStore.Enqueue(NoteType.Todo, unsent.Text, unsent.Timestamp, "123", vault, "-123");
            overlay = new TodoOverlayWindow(settings) { ShowActivated = false, Topmost = false };
            overlay.Show();
            overlay.ToggleExpanded();
            ClockOverlayChecks.Pump(80);
            void Choose(NoteEntry entry, bool confirm, Action? whileOpen = null)
            {
                var row = overlay.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().Single(r => r.Tag is NoteEntry n && n.Text == entry.Text);
                row.ContextMenu!.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Delete"))
                    .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                ClockOverlayChecks.Pump(60);
                var dialog = overlay.OwnedWindows.OfType<ConfirmationDialogWindow>().Single();
                var labels = dialog.GetVisualDescendants().OfType<TextBlock>().Select(t => t.Text).ToArray();
                Require(labels.Contains(entry.Text) && labels.Contains(entry == linked ? "Linked message" : "No linked message"), "Task menu opened the wrong confirmation");
                whileOpen?.Invoke();
                dialog.FindControl<Button>(confirm ? "ConfirmButton" : "CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                var busy = typeof(TodoOverlayWindow).GetField("_deleteInProgress", Private)!;
                for (int i = 0; i < 100 && (bool)busy.GetValue(overlay)!; i++) ClockOverlayChecks.Pump(30);
                Require(!(bool)busy.GetValue(overlay)!, "Deletion did not finish");
            }
            Choose(unsent, false);
            Require(ObsidianNotesSync.LoadAllNotes(directory.FullName).Count == 3 && TelegramOutboxStore.GetAll().Count == 1, "Cancel modified data");
            Choose(linked, true);
            Require(ObsidianNotesSync.LoadAllNotes(directory.FullName).Count == 3, "Offline linked deletion removed the task");
            Choose(linked, true, () => TelegramOutboxStore.MarkSent(NoteType.Todo, linked.Text, linked.Timestamp, 43, "-123", "123", vault));
            Require(overlay.FindControl<TextBlock>("ErrorMessage")!.Text!.Contains("choose Delete again"), "Changed linked message used stale confirmation");
            Choose(unsent, true);
            var kept = ObsidianNotesSync.LoadAllNotes(directory.FullName);
            Require(kept.Count == 2 && kept.Any(e => e.Text == "Keep neighbor") && kept.Any(e => e.Text == linked.Text)
                && TelegramOutboxStore.GetAll().Count == 0, "Confirmed unsent deletion changed the wrong task or left a queued copy");
            var neighbor = kept.Single(e => e.Text == "Keep neighbor");
            var queued = TelegramOutboxStore.Enqueue(NoteType.Todo, neighbor.Text, neighbor.Timestamp, "123", vault, "-123");
            Choose(neighbor, true, () => TelegramOutboxStore.Update(queued with { DeliveredMessageId = 44 }));
            Require(overlay.FindControl<TextBlock>("ErrorMessage")!.Text!.Contains("choose Delete again")
                && ObsidianNotesSync.LoadAllNotes(directory.FullName).Count == 2
                && TelegramOutboxStore.GetAll().Count == 1 && TelegramNotesSync.HasTodoMessageLink(settings, neighbor),
                "Newly delivered Telegram message was not retained for a fresh confirmation");
            Console.WriteLine("PASS Todo overlay menu, cancellation, offline linked protection and confirmed unsent deletion");
        }
        finally
        {
            overlay?.Close();
            TelegramOutboxStore.SetCustomFilePath(null);
            directory.Delete(true);
        }
    }
}
