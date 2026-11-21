using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TodoTelegramLinkPreview
{
    const string Output = "design/one-piece-v2/todo-telegram-link/implemented";
    const string InvalidReference = "https://t.me/print_studio/proof";
    const string LinkError = "The link must end with its message ID.";
    static readonly string[] States = ["link", "relink", "invalid"];

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        var vault = Directory.CreateTempSubdirectory("stopwatch-todo-link-");
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(vault.FullName, "outbox.json"));
        // Exercise real local bindings while keeping the background HTTP worker idle.
        var syncGate = (SemaphoreSlim)typeof(TelegramNotesSync).GetField("CompletionGate", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        Require(syncGate.Wait(0), "Unexpected background sync");
        var settings = new AppSettings {
            ThemeMode = AppThemeCatalog.Nika, ObsidianVaultFolder = vault.FullName,
            TelegramEnabled = true, TelegramBotToken = "preview:local", TelegramChatId = "@print_studio", HabiticaEnabled = false,
            TodoOverlayHasPosition = true, TodoOverlayLeft = 100, TodoOverlayTop = 70
        };
        try
        {
            DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
            foreach (string title in new[] { "Review the poster proof", "Send the final poster to print" })
                Require(ObsidianNotesSync.AppendEntry(vault.FullName, NoteType.Todo, title).Success, "Cannot seed task");
            var linked = ObsidianNotesSync.LoadAllNotes(vault.FullName).Single(n => n.Text.StartsWith("Send"));
            string vaultKey = TelegramNotesSync.GetTodoVaultKey(settings);
            Require(TelegramOutboxStore.MarkSent(NoteType.Todo, linked.Text, linked.Timestamp, 482, "@print_studio", "preview", vaultKey), "Cannot seed current link");
            foreach (int scale in new[] { 70, 90, 125 })
            {
                foreach (string profile in new[] { "classic", "pixel" })
                {
                    settings.TodoAppearance.Profile = profile;
                    foreach (string state in States)
                    {
                        settings.UiScalePercent = scale;
                        Render(settings, state);
                    }
                }
                TodoTaskMenuPreview.Comparison(Output, scale, States, ["First-time linking", "Replacing a link", "Invalid input"]);
            }
            Require(TelegramOutboxStore.GetTrackedTodos("preview", vaultKey).Single().MessageId == 482, "Cancel or invalid text altered a binding");
            foreach (string profile in new[] { "classic", "pixel" })
            {
                settings.TodoAppearance.Profile = profile;
                CheckSave(settings, profile == "pixel");
            }
            CheckSettingsAndLegacy(settings, linked);
        }
        finally { syncGate.Release(); TelegramOutboxStore.SetCustomFilePath(null); vault.Delete(true); }
    }

    static Border Open(TodoOverlayWindow overlay, bool replacing, string task)
    {
        var row = overlay.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().Single(r => ((NoteEntry)r.Tag!).Text == task);
        var menu = row.ContextMenu!;
        menu.Open(row);
        menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, replacing ? "Relink" : "Link"))
            .RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        ClockOverlayChecks.Pump(70);
        var host = overlay.FindControl<Border>("TelegramLinkPanel")!;
        Require(host.IsVisible && host.GetVisualDescendants().OfType<UserControl>().Any(c => c.IsEffectivelyVisible), "Task menu did not open the new Telegram editor");
        return host;
    }

    static T Field<T>(Control host, string name) where T : Control =>
        host.GetVisualDescendants().OfType<T>().Single(c => c.Name == name);

    static void Render(AppSettings settings, string state)
    {
        var overlay = new TodoOverlayWindow(settings) { ShowActivated = false, Topmost = false };
        try
        {
            overlay.Show();
            overlay.ToggleExpanded();
            ClockOverlayChecks.Pump(55);
            bool replacing = state != "link", invalid = state == "invalid";
            string task = replacing ? "Send the final poster to print" : "Review the poster proof";
            var form = Open(overlay, replacing, task);
            var input = Field<TextBox>(form, "Reference");
            var hint = Field<TextBlock>(form, "Hint");
            var save = Field<Button>(form, "SaveAction");
            Require(Field<TextBlock>(form, "Title").Text == (replacing ? "Relink Telegram message" : "Link Telegram message")
                && Field<TextBlock>(form, "TaskName").Text == task && Field<TodoFrame>(form, "TaskFrame").Pixel == settings.TodoAppearance.IsPixel
                && Field<Grid>(form, "BindingRow").IsVisible == replacing && (!replacing || Field<TextBlock>(form, "CurrentLink").Text == "@print_studio · #482")
                && Equals(save.Content, replacing ? "Replace link" : "Link message") && !save.IsEnabled && input.IsFocused, "Incorrect initial link state");
            input.Text = invalid ? InvalidReference : replacing ? "486" : "";
            input.CaretIndex = input.Text.Length;
            ClockOverlayChecks.Pump(50);
            Require(save.IsEnabled == (replacing && !invalid) && (hint.Text == LinkError) == invalid, "Validation did not track the draft");
            double zoom = settings.UiScalePercent / 100d;
            var field = Bounds(input);
            var message = Bounds(hint);
            var action = Bounds(save);
            Require(Math.Abs(overlay.ClientSize.Width - 400 * zoom) < 1.5 && field.Width > 330 * zoom &&
                message.Top >= field.Bottom - 1 && action.Top >= message.Bottom && action.Right <= overlay.ClientSize.Width - 4 * zoom &&
                action.Bottom <= overlay.ClientSize.Height - 5 * zoom && action.Height >= 28 * zoom,
                $"Clipped {settings.TodoAppearance.Profile}/{state}/{zoom}: {overlay.ClientSize}, {field}, {message}, {action}");
            var captureSize = PixelSize.FromSize(overlay.ClientSize, 1);
            var scale = settings.UiScalePercent;
            // Render through the app's scale control at full layout size to avoid Win32 bitmap viewport culling.
            settings.UiScalePercent = 100;
            typeof(TodoOverlayWindow).GetMethod("ApplyTaskMenuScale", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(overlay, null);
            ClockOverlayChecks.Pump(75);
            Require(input.Text == (invalid ? InvalidReference : replacing ? "486" : "") && input.IsEffectivelyVisible &&
                Math.Abs(overlay.ClientSize.Width - 400) <= 1, "Scaling discarded the editor or draft");
            using var bitmap = new RenderTargetBitmap(captureSize, new Vector(96 * zoom, 96 * zoom));
            bitmap.Render(overlay);
            bitmap.Save(Path.Combine(Output, $"{settings.TodoAppearance.Profile}-{state}-{scale}.png"));
            Field<Button>(form, "CancelAction").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            CheckList(overlay);
            Rect Bounds(Control c) => new(c.TranslatePoint(default, overlay)!.Value, c.TranslatePoint(new Point(c.Bounds.Width, c.Bounds.Height), overlay)!.Value);
        }
        finally { overlay.Close(); }
    }

    static void CheckSave(AppSettings settings, bool replacing)
    {
        var overlay = new TodoOverlayWindow(settings) { ShowActivated = false, Topmost = false };
        const string task = "Review the poster proof";
        try
        {
            overlay.Show(); overlay.ToggleExpanded(); ClockOverlayChecks.Pump(55);
            var form = Open(overlay, replacing, task);
            var input = Field<TextBox>(form, "Reference");
            var save = Field<Button>(form, "SaveAction");
            var hint = Field<TextBlock>(form, "Hint");
            input.Text = "482"; ClockOverlayChecks.Pump(20);
            save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(form.IsVisible && input.Text == "482" && hint.Text!.Contains("already linked"), "Binding collision did not preserve the draft");
            input.Text = replacing ? "https://t.me/print_studio/499" : "486";
            ClockOverlayChecks.Pump(20);
            Require(save.IsEnabled && !hint.Text!.Contains("already linked"), "Editing did not clear the error");
            if (replacing) input.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
            else save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            CheckList(overlay);
            var records = TelegramOutboxStore.GetTrackedTodos("preview", TelegramNotesSync.GetTodoVaultKey(settings));
            Require(records.Count == 2 && records.Single(r => r.Text == task).MessageId == (replacing ? 499 : 486), "Wrong persisted binding");
            form = Open(overlay, true, task);
            Require(Field<TextBlock>(form, "CurrentLink").Text == $"@print_studio · #{(replacing ? 499 : 486)}", "Reopened editor has stale link");
            Field<TextBox>(form, "Reference").RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Escape });
            CheckList(overlay);
        }
        finally { overlay.Close(); }
    }

    static void CheckList(TodoOverlayWindow overlay)
    {
        ClockOverlayChecks.Pump(50);
        Require(overlay.IsExpanded && !overlay.FindControl<Border>("TelegramLinkPanel")!.IsVisible &&
            overlay.FindControl<ScrollViewer>("TodoScrollViewer")!.IsEffectivelyVisible &&
            Math.Abs(overlay.ClientSize.Width - 400) <= 1 && Math.Abs(overlay.ClientSize.Height - 560) <= 1, "Todo list did not regain its original size");
    }

    static void CheckSettingsAndLegacy(AppSettings settings, NoteEntry entry)
    {
        Require(TodoOrganizationStore.SetPriority(settings.ObsidianVaultFolder, entry, 10).Success, "Cannot set custom rank");
        var overlay = new TodoOverlayWindow(settings) { ShowActivated = false, Topmost = false };
        try
        {
            overlay.Show(); overlay.ToggleExpanded(); ClockOverlayChecks.Pump(55);
            var form = Open(overlay, true, entry.Text);
            Require(Field<Button>(form, "Rank").Content is TextBlock { Text: "10" }, "Editor lost the task's priority");
            var input = Field<TextBox>(form, "Reference");
            input.Text = "507";
            settings.TodoAppearance.Profile = "classic";
            overlay.ApplySettings(); ClockOverlayChecks.Pump(45);
            Require(form.IsVisible && input.Text == "507" && !Field<TodoFrame>(form, "TaskFrame").Pixel, "Appearance change lost the draft");
            settings.TelegramChatId = "@another_chat";
            overlay.ApplySettings();
            CheckList(overlay);
            settings.ThemeMode = AppThemeCatalog.Daylight;
            DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
            overlay.ApplySettings(); ClockOverlayChecks.Pump(35);
            var row = overlay.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().Single(r => ((NoteEntry)r.Tag!).Text == entry.Text);
            var menu = row.ContextMenu!;
            menu.Open(row);
            menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Relink")).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            var legacy = overlay.FindControl<TextBox>("TelegramMessageLink")!;
            Require(legacy.IsEffectivelyVisible && !Field<TextBox>(form, "Reference").IsEffectivelyVisible, "Daylight lost its existing link form");
        }
        finally { overlay.Close(); }
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
