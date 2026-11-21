using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Raw;
using InputKey = Avalonia.Input.Key;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;
using ShapePath = Avalonia.Controls.Shapes.Path;

internal static class TodoSortDropdownPreview
{
    const string Output = "design/one-piece-v2/todo-sort-dropdown/implemented";
    // This is the saved manual order. Each rank is distinct so both views are easy to compare.
    static readonly (string Text, int Rank)[] Tasks = [
        ("Sketch cover options", 4), ("Choose paper stock", 6), ("Review the print proof", 1),
        ("Pack sample prints", 8), ("Send final artwork", 2), ("File the color swatches", 10),
        ("Check page numbers", 3)
    ];

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
        var vault = Directory.CreateTempSubdirectory("stopwatch-sort-preview-");
        var settings = new AppSettings {
            ThemeMode = AppThemeCatalog.Nika, TelegramEnabled = false, HabiticaEnabled = false,
            ObsidianVaultFolder = vault.FullName, TodoOverlayHasPosition = true, TodoOverlayLeft = 100, TodoOverlayTop = 80
        };
        TodoOverlayWindow? overlay = null;
        try
        {
            foreach (var task in Tasks)
                Require(ObsidianNotesSync.AppendEntry(vault.FullName, NoteType.Todo, task.Text).Success, "Cannot seed sample task");
            var entries = ObsidianNotesSync.LoadAllNotes(vault.FullName).ToDictionary(e => e.Text);
            foreach (var task in Tasks)
                Require(TodoOrganizationStore.SetPriority(vault.FullName, entries[task.Text], task.Rank).Success, "Cannot seed priority");
            Require(TodoOrganizationStore.SetManualOrder(vault.FullName, Tasks.Select(t => entries[t.Text]).ToArray()).Success, "Cannot seed manual order");
            overlay = new TodoOverlayWindow(settings) { ShowActivated = false, Topmost = false };
            overlay.Show();
            overlay.ToggleExpanded();
            var combo = overlay.FindControl<ComboBox>("TodoSortCombo")!;
            foreach (int scale in new[] { 70, 90, 125 })
            {
                settings.UiScalePercent = scale;
                foreach (string profile in new[] { "classic", "pixel" })
                {
                    settings.TodoAppearance.Profile = profile;
                    overlay.ApplySettings();
                    ClockOverlayChecks.Pump(50);
                    foreach (string mode in new[] { "Priority", "Manual" })
                    {
                        if (!Equals(combo.SelectedItem, mode))
                        {
                            Click(combo);
                            Click(combo.GetRealizedContainers().OfType<ComboBoxItem>().Single(i => Equals(i.Content, mode)));
                            Require(!combo.IsDropDownOpen && TodoOrganizationStore.Load(vault.FullName).SortMode == mode,
                                $"Clicking {mode} did not close the dropdown and save the selection");
                        }
                        Click(combo);
                        try
                        {
                            ClockOverlayChecks.Pump(100);
                            var options = combo.GetRealizedContainers().OfType<ComboBoxItem>().ToArray();
                            Require(options.Length == 2 && options.Select(i => i.Content).SequenceEqual(new[] { "Priority", "Manual" }), "Missing sort choices");
                            var frame = options[0].GetVisualAncestors().OfType<TodoFrame>().SingleOrDefault(f => f.Name == "SortMenuFrame")
                                ?? throw new InvalidOperationException("Opening the Todo toolbar did not show the designed Sort dropdown");
                            var popup = TodoDetailMenuPreview.BoundsOnDesktop(frame);
                            var anchor = TodoDetailMenuPreview.BoundsOnDesktop(combo);
                            Require(frame.Pixel == settings.TodoAppearance.IsPixel && Math.Abs(popup.X - anchor.X) <= 2 &&
                                Math.Abs(popup.Y - anchor.Bottom - 4) <= 2 && Math.Abs(popup.Width - 206 * scale / 100d) <= 2,
                                $"Wrong popup style, anchor or scale at {profile}/{scale}/{mode}: {anchor}, {popup}");
                            foreach (var option in options)
                            {
                                bool selected = Equals(option.Content, mode);
                                var descendants = option.GetVisualDescendants().ToArray();
                                var hint = descendants.OfType<TextBlock>().Single(t => t.Name == "SortHint");
                                Require(option.IsSelected == selected && descendants.OfType<ShapePath>().Single(p => p.Name == "ActiveCheck").IsEffectivelyVisible == selected &&
                                    hint.Text == (Equals(option.Content, "Manual") ? "Drag tasks to reorder" : "Highest priority first") &&
                                    popup.Contains(TodoDetailMenuPreview.BoundsOnDesktop(hint).BottomRight), "Incorrect selection or clipped description");
                            }
                            var rows = overlay.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().ToArray();
                            var expected = mode == "Manual" ? Tasks : Tasks.OrderBy(t => t.Rank).ToArray();
                            Require(rows.Select(r => ((NoteEntry)r.Tag!).Text).SequenceEqual(expected.Select(t => t.Text)), "Sample task order is incorrect");
                            Require(rows.All(r => r.GetVisualDescendants().OfType<Button>().Any(b => b.Classes.Contains("todoDragHandle")) == (mode == "Manual")), "Wrong drag handles");
                            var viewport = TodoDetailMenuPreview.BoundsOnDesktop(overlay.FindControl<ScrollViewer>("TodoScrollViewer")!);
                            Require(rows.Select(TodoDetailMenuPreview.BoundsOnDesktop).Count(r => r.Top >= popup.Bottom && r.Bottom <= viewport.Bottom + 1) >= 3,
                                "Fewer than three task rows remain fully visible below the dropdown");
                            TodoDetailMenuPreview.Capture(overlay, overlay.FindControl<Border>("TodoOutline")!, frame,
                                Path.Combine(Output, $"{profile}-{mode.ToLowerInvariant()}-{scale}.png"));
                        }
                        finally { combo.IsDropDownOpen = false; }
                    }
                }
                TodoTaskMenuPreview.Comparison(Output, scale, ["priority", "manual"], ["Priority selected", "Manual selected"]);
            }

            overlay.Close();
            overlay = new TodoOverlayWindow(settings) { ShowActivated = false, Topmost = false };
            overlay.Show();
            overlay.ToggleExpanded();
            combo = overlay.FindControl<ComboBox>("TodoSortCombo")!;
            Require(Equals(combo.SelectedItem, "Manual"), "Reopening the overlay lost the saved sort mode");
            settings.TodoAppearance.Pixel.Toolbar = "classic-brass";
            settings.TodoAppearance.Pixel.Background = "midnight-chart";
            overlay.ApplySettings();
            ClockOverlayChecks.Pump(80);
            Require(combo.Focus(NavigationMethod.Tab), $"Tab cannot focus Sort, focusable={combo.Focusable}, enabled={combo.IsEffectivelyEnabled}");
            KeyDown(combo, InputKey.F4);
            var optionsOnReopen = combo.GetRealizedContainers().OfType<ComboBoxItem>().ToArray();
            Require(combo.IsDropDownOpen && optionsOnReopen.Length == 2, "Keyboard could not open the Sort dropdown");
            Require(optionsOnReopen[1].IsFocused,
                $"Keyboard did not open on Manual, focusable={optionsOnReopen[1].Focusable}, focus={overlay.FocusManager?.GetFocusedElement()}");
            var reopenedFrame = optionsOnReopen[0].GetVisualAncestors().OfType<TodoFrame>().Single(f => f.Name == "SortMenuFrame");
            Require(!reopenedFrame.Pixel && reopenedFrame.Background?.ToString() == TodoVisuals.Paper(settings.TodoAppearance.Pixel).ToString(),
                "Dropdown ignored the custom Classic toolbar or Midnight Chart palette");
            KeyDown(optionsOnReopen[1], InputKey.Up);
            var highlighted = (Control?)overlay.FocusManager?.GetFocusedElement() ?? combo;
            KeyDown(highlighted, InputKey.Escape);
            Require(!combo.IsDropDownOpen && overlay.IsExpanded && Equals(combo.SelectedItem, "Manual") &&
                TodoOrganizationStore.Load(vault.FullName).SortMode == "Manual",
                $"Escape from {highlighted} left open={combo.IsDropDownOpen}, expanded={overlay.IsExpanded}, selected={combo.SelectedItem}, saved={TodoOrganizationStore.Load(vault.FullName).SortMode}");
            KeyDown(combo, InputKey.F4);
            var manual = (ComboBoxItem)combo.ContainerFromIndex(1)!;
            KeyDown(manual, InputKey.Up);
            KeyDown((Control?)overlay.FocusManager?.GetFocusedElement() ?? combo, InputKey.Enter);
            Require(!combo.IsDropDownOpen && Equals(combo.SelectedItem, "Priority") && TodoOrganizationStore.Load(vault.FullName).SortMode == "Priority",
                "Keyboard selection did not save Priority");

            settings.ThemeMode = AppThemeCatalog.Daylight;
            DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", settings.ThemeMode);
            overlay.ApplySettings();
            ClockOverlayChecks.Pump(80);
            Click(combo);
            var legacyOption = combo.ContainerFromIndex(1) as ComboBoxItem
                ?? throw new InvalidOperationException("Daylight did not show the Manual option");
            Require(combo.IsDropDownOpen && !legacyOption.GetVisualAncestors().OfType<TodoFrame>().Any(f => f.Name == "SortMenuFrame"),
                "Daylight kept the Nika dropdown frame");
            Click(legacyOption);
            Require(TodoOrganizationStore.Load(vault.FullName).SortMode == "Manual", "Daylight could not save the sort mode");
        }
        finally { overlay?.Close(); vault.Delete(true); }
    }

    static void Click(Control target)
    {
        var root = TopLevel.GetTopLevel(target)!;
        var point = target.TranslatePoint(new Point(target.Bounds.Width / 2, target.Bounds.Height / 2), root)!.Value;
        var pointer = new Pointer(75, PointerType.Mouse, true);
        target.RaiseEvent(new PointerPressedEventArgs(target, pointer, root, point, 0,
            new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
        target.RaiseEvent(new PointerReleasedEventArgs(target, pointer, root, point, 1,
            new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
        ClockOverlayChecks.Pump(50);
    }

    static void KeyDown(Control target, InputKey key)
    {
        target.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = key });
        ClockOverlayChecks.Pump(30);
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
