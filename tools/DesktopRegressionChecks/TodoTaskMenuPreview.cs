using System.Globalization;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TodoTaskMenuPreview
{
    const string Output = "design/one-piece-v2/todo-task-menu/implemented";
    static readonly string[] Profiles = ["classic", "pixel"];
    static readonly string[] States = ["off", "link", "relink"];
    static readonly int[] Scales = [70, 90, 125];

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
        var vault = Directory.CreateTempSubdirectory("stopwatch-task-menu-");
        TelegramOutboxStore.SetCustomFilePath(Path.Combine(vault.FullName, "outbox.json"));
        var settings = new AppSettings {
            ThemeMode = AppThemeCatalog.Nika, HabiticaEnabled = false, TelegramBotToken = "",
            ObsidianVaultFolder = vault.FullName, TodoOverlayHasPosition = true, TodoOverlayLeft = 115, TodoOverlayTop = 100
        };
        const string unlinked = "Check [the print proof](https://example.com/proof)";
        const string linked = "Deliver the print proof";
        TodoOverlayWindow? overlay = null;
        try
        {
            foreach (string name in new[] { unlinked, linked })
                Require(ObsidianNotesSync.AppendEntry(vault.FullName, NoteType.Todo, name).Success, "Cannot seed task");
            var entry = ObsidianNotesSync.LoadAllNotes(vault.FullName).Single(n => n.Text == linked);
            Require(TelegramOutboxStore.MarkSent(NoteType.Todo, linked, entry.Timestamp, 42, "-123", "", TelegramNotesSync.GetTodoVaultKey(settings)), "Cannot seed link");
            overlay = new TodoOverlayWindow(settings) { ShowActivated = false, Topmost = false };
            overlay.Show();
            overlay.ToggleExpanded();
            foreach (int scale in Scales)
            {
                settings.UiScalePercent = scale;
                foreach (string profile in Profiles)
                foreach (string state in States)
                {
                    settings.TodoAppearance.Profile = profile;
                    settings.TelegramEnabled = state != "off";
                    overlay.ApplySettings();
                    ClockOverlayChecks.Pump(45);
                    var row = overlay.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>()
                        .Single(r => r.Tag is NoteEntry n && n.Text == (state == "relink" ? linked : unlinked));
                    var checkbox = row.GetVisualDescendants().OfType<HoldToCompleteCheckBox>().Single();
                    var text = checkbox.GetVisualDescendants().OfType<TextBlock>().Single(t => t.ContextMenu != null);
                    Require(text.ContextMenu!.Items.OfType<MenuItem>().Any(i => Equals(i.Header, "Open the print proof")) == (state != "relink"),
                        "Task text lost its Markdown link command");
                    Control target = state == "relink" ? text : state == "link" ? checkbox : row;
                    var menu = target.ContextMenu!;
                    target.RaiseEvent(new ContextRequestedEventArgs { RoutedEvent = Control.ContextRequestedEvent });
                    ClockOverlayChecks.Pump(50);
                    try
                    {
                        var commands = menu.Items.OfType<MenuItem>().ToArray();
                        string[] expected = state switch {
                            "off" => ["Edit task", "Add", "Delete"], "link" => ["Edit task", "Add", "Link", "Delete"],
                            _ => ["Edit task", "Add", "Relink", "Delete"]
                        };
                        Require(menu.IsOpen && commands.Select(i => i.Header).SequenceEqual(expected), $"Wrong task menu for {state}");
                        Require(menu.Items[^2] is Separator, "Delete is missing its divider");
                        var add = commands[1];
                        var children = add.Items.OfType<MenuItem>().ToArray();
                        Require(children.Select(i => i.Header).SequenceEqual(new[] { "Note", "Subtask" }), "Wrong Add commands");
                        add.IsSubMenuOpen = true;
                        ClockOverlayChecks.Pump(70);
                        var root = add.GetVisualAncestors().OfType<TodoFrame>().FirstOrDefault() ??
                            throw new InvalidOperationException($"Task frame missing at {profile}/{scale}/{state}");
                        var sub = children[0].GetVisualAncestors().OfType<TodoFrame>().First();
                        Require(add.IsSubMenuOpen && root != sub && root.Pixel == (profile == "pixel") && sub.Pixel == root.Pixel &&
                            children.All(i => i.IsEffectivelyVisible), "Wrong submenu frame");
                        Capture(root, sub, add, scale / 100d, FileName(profile, state, scale));
                        if (scale == 90)
                        {
                            add.IsSubMenuOpen = false;
                            add.Focus();
                            KeyDown(add, Avalonia.Input.Key.Right);
                            Require(add.IsSubMenuOpen, "Right arrow failed to reopen Add");
                            KeyDown(children[0], Avalonia.Input.Key.Left);
                            Require(!add.IsSubMenuOpen && menu.IsOpen, "Left arrow failed to return to task menu");
                            if (state == "off")
                            {
                                KeyDown(add, Avalonia.Input.Key.Escape);
                                Require(!menu.IsOpen, "Escape failed to dismiss task menu");
                            }
                            else
                            {
                                commands[2].RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                                var panel = overlay.FindControl<Border>("TelegramLinkPanel")!;
                                var taskName = string.Concat(FormattedTodoText.Parse(((NoteEntry)row.Tag!).Text).Select(p => p.Text));
                                Require(!menu.IsOpen && panel.IsVisible && panel.GetVisualDescendants().OfType<TextBlock>()
                                    .Any(t => t.Name == "TaskName" && t.IsEffectivelyVisible && t.Text == taskName),
                                    "Telegram action failed to open the correct task");
                                panel.GetVisualDescendants().OfType<Button>().Single(b => b.IsEffectivelyVisible && Equals(b.Content, "Cancel")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                            }
                        }
                    }
                    finally { menu.Close(); }
                }
                Comparison(Output, scale, States, ["Telegram off", "Telegram on · Unlinked task", "Telegram on · Linked task"]);
            }
            VerifyMenuActions(overlay, settings, unlinked);
        }
        finally
        {
            overlay?.Close();
            TelegramOutboxStore.SetCustomFilePath(null);
            vault.Delete(true);
        }
    }

    static void VerifyMenuActions(TodoOverlayWindow overlay, AppSettings settings, string markdown)
    {
        settings.TodoAppearance.Current.Background = "midnight-chart";
        settings.TelegramEnabled = false;
        overlay.ApplySettings();
        var screen = overlay.Screens.ScreenFromVisual(overlay)!;
        overlay.Position = new PixelPoint(screen.WorkingArea.Right - (int)Math.Ceiling(overlay.Width * screen.Scaling), screen.WorkingArea.Y + 30);
        ClockOverlayChecks.Pump(55);
        foreach (string action in new[] { "Note", "Subtask", "Edit task", "Delete" })
        {
            var row = overlay.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().Single(r => ((NoteEntry)r.Tag!).Text == markdown);
            var text = row.GetVisualDescendants().OfType<TextBlock>().Single(t => t.ContextMenu?.Items.OfType<MenuItem>().Any(i => Equals(i.Header, "Open the print proof")) == true);
            var menu = text.ContextMenu!;
            // Anchor the menu against the screen edge to exercise the submenu's native flip.
            menu.Placement = PlacementMode.BottomEdgeAlignedRight;
            menu.PlacementTarget = row;
            menu.Open(text);
            ClockOverlayChecks.Pump(50);
            var add = menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, "Add"));
            var root = add.GetVisualAncestors().OfType<TodoFrame>().First();
            Require(root.Background is ISolidColorBrush paper && paper.Color == Color.Parse("#082D43") &&
                add.Foreground is ISolidColorBrush ink && ink.Color == Color.Parse("#FFF0D3"), "Midnight menu lost its readable palette");
            MenuItem command;
            if (action is "Note" or "Subtask")
            {
                add.Focus();
                KeyDown(add, Avalonia.Input.Key.Right);
                command = add.Items.OfType<MenuItem>().Single(i => Equals(i.Header, action));
                var sub = command.GetVisualAncestors().OfType<TodoFrame>().First();
                Require(sub.PointToScreen(default).X < root.PointToScreen(default).X &&
                    screen.WorkingArea.Contains(sub.PointToScreen(new Point(sub.Bounds.Width - 1, sub.Bounds.Height - 1))),
                    "Add did not flip on screen near the right edge");
            }
            else command = menu.Items.OfType<MenuItem>().Single(i => Equals(i.Header, action));
            command.Focus();
            KeyDown(command, Avalonia.Input.Key.Enter);
            var dialog = overlay.OwnedWindows.Single(w => w.IsVisible);
            Require(!menu.IsOpen, "Command left its menu open");
            if (action == "Delete")
            {
                Require(dialog is ConfirmationDialogWindow, "Delete missed its confirmation");
                dialog.FindControl<Button>("CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            }
            else
            {
                Require(dialog is NoteEntryWindow && dialog.Title == (action == "Edit task" ? "Edit Todo" : "Add " + action.ToLowerInvariant()), "Wrong editor for " + action);
                dialog.Close();
            }
            ClockOverlayChecks.Pump(60);
        }
        Require(ObsidianNotesSync.LoadAllNotes(settings.ObsidianVaultFolder).Count == 2, "Cancel changed fixture tasks");
    }

    static void KeyDown(Control item, Avalonia.Input.Key key)
    {
        item.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        ClockOverlayChecks.Pump(25);
    }

    static void Capture(TodoFrame root, TodoFrame sub, MenuItem add, double zoom, string path)
    {
        var scaling = TopLevel.GetTopLevel(root)!.RenderScaling;
        var origin = root.PointToScreen(default);
        Rect Surface(Control control)
        {
            var start = control.PointToScreen(default) - origin;
            var end = control.PointToScreen(new Point(control.Bounds.Width, control.Bounds.Height)) - origin;
            return new Rect(new Point(start.X / scaling, start.Y / scaling), new Point(end.X / scaling, end.Y / scaling));
        }
        var parent = Surface(root);
        var submenu = Surface(sub);
        var row = Surface(add);
        Require(Math.Abs(parent.Width - 182 * zoom) <= 2 && Math.Abs(submenu.Width - 137 * zoom) <= 2,
            $"Unexpected size at {zoom}: {parent}, {submenu}");
        Require(Math.Abs(submenu.Y - row.Y) <= 2 && submenu.X >= parent.Right - 2 && submenu.X <= parent.Right + 8,
            $"Submenu missed the Add row: {parent}, {row}, {submenu}");
        var union = parent.Union(submenu);
        const int margin = 6;
        using var capture = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(union.Width) + 2 * margin,
            (int)Math.Ceiling(union.Height) + 2 * margin), new Vector(96, 96));
        using (var dc = capture.CreateDrawingContext())
        {
            foreach (var (frame, bounds) in new[] { (root, parent), (sub, submenu) })
                dc.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(new Rect(bounds.Size), RelativeUnit.Absolute) },
                    null, bounds.Translate(new Vector(margin, margin)));
        }
        capture.Save(path);
    }

    internal static void Comparison(string directory, int scale, string[] states, string[] labels, string fileStem = "menus")
    {
        const int gutter = 24, top = 52, labelHeight = 28;
        string Sample(string profile, string state) => Path.Combine(directory, $"{profile}-{state}-{scale}.png");
        var sizes = states.Select(state => Profiles.Select(profile => {
            using var image = new Bitmap(Sample(profile, state));
            return image.PixelSize;
        }).ToArray()).ToArray();
        int column = sizes.SelectMany(row => row).Max(size => size.Width) + gutter;
        int height = top + sizes.Sum(row => row.Max(s => s.Height) + labelHeight + gutter);
        using var board = new RenderTargetBitmap(new PixelSize(column * 2 + gutter, height), new Vector(96,96));
        using (var dc = board.CreateDrawingContext())
        {
            dc.DrawRectangle(Brush.Parse("#1B2932"), null, new Rect(board.Size));
            void Label(string label, int x, int y, double size, IBrush ink) => dc.DrawText(new FormattedText(label, CultureInfo.InvariantCulture,
                FlowDirection.LeftToRight, new Typeface(TodoVisuals.BodyFont), size, ink), new Point(x, y));
            for (int p = 0; p < Profiles.Length; p++)
            {
                int x = gutter + p * column;
                Label((p == 0 ? "Classic" : "Pixel") + $"  ·  {scale}%", x + 6, 15, 21, TodoVisuals.Ivory);
                int y = top;
                for (int i = 0; i < states.Length; i++)
                {
                    Label(labels[i], x + 6, y + 2, 15, Brush.Parse("#CAD6D6"));
                    using var image = new Bitmap(Sample(Profiles[p], states[i]));
                    dc.DrawImage(image, new Rect(new Point(x, y + labelHeight), image.Size));
                    y += sizes[i].Max(s => s.Height) + labelHeight + gutter;
                }
            }
        }
        board.Save(Path.Combine(directory, $"{fileStem}-{scale}.png"));
    }

    static string FileName(string profile, string state, int scale) => Path.Combine(Output, $"{profile}-{state}-{scale}.png");
    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
