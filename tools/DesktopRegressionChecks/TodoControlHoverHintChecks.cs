using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class TodoControlHoverHintChecks
{
    const string Output = "design/one-piece-v2/todo-control-hover-hints/implemented";
    const string Collapsed = "launcher-collapsed", Expanded = "launcher-expanded", Priority = "priority", Manual = "manual";

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        var vault = Directory.CreateTempSubdirectory("stopwatch-control-hints-");
        try
        {
            Require(ObsidianNotesSync.AppendEntry(vault.FullName, NoteType.Todo, "Review the print proof").Success, "Cannot create sample task");
            var task = ObsidianNotesSync.LoadAllNotes(vault.FullName).Single();
            DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
            foreach (int scale in new[] { 70, 90, 125 })
            {
                foreach (string profile in new[] { "classic", "pixel" })
                    CheckControls(vault.FullName, task, scale, profile);
                TodoTaskMenuPreview.Comparison(Output, scale, [Collapsed, Expanded, Priority, Manual],
                    ["Collapsed launcher · right screen edge", "Expanded launcher · left screen edge", "Priority badge", "Manual drag handle"], "controls");
            }
        }
        finally { vault.Delete(true); }
    }

    static void CheckControls(string vault, NoteEntry task, int scale, string profile)
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = scale,
            TelegramEnabled = false, HabiticaEnabled = false, ObsidianVaultFolder = vault,
            TodoOverlayHasPosition = true, TodoOverlayTop = 90, TodoOverlayLeft = 80 };
        settings.TodoAppearance.Profile = profile;
        Require(TodoOrganizationStore.SetPriority(vault, task, 3).Success && TodoOrganizationStore.SetSortMode(vault, "Priority").Success, "Cannot prepare priority sort");
        var overlay = new TodoOverlayWindow(settings) { ShowActivated = false, Topmost = false };
        try
        {
            overlay.Show();
            ClockOverlayChecks.Pump(75);
            var screen = overlay.Screens.ScreenFromVisual(overlay)!.WorkingArea;
            var launcher = overlay.FindControl<Button>("LauncherButton")!;
            var header = overlay.FindControl<Grid>("OverlayHeader")!;
            var y = screen.Y + 100;
            void MoveTo(int x, int top) { overlay.Position = new PixelPoint(x, top); ClockOverlayChecks.Pump(65); }
            MoveTo(screen.Right - (int)Math.Ceiling(overlay.Bounds.Width * overlay.RenderScaling) - 2, y);
            CheckHover(overlay, launcher, launcher, profile, scale, Collapsed, ["Click to expand todos", "Drag to move the overlay"]);
            overlay.ToggleExpanded();
            ClockOverlayChecks.Pump(75);
            MoveTo(screen.X + 2, screen.Y + (int)(180 * overlay.RenderScaling));
            string[] collapse = ["Click to collapse todos", "Drag to move the overlay"];
            CheckHover(overlay, launcher, header, profile, scale, Expanded, collapse);
            MoveTo(screen.X + 2, screen.Y + 2);
            CheckHover(overlay, launcher, header, profile, scale, Expanded, collapse, nearTop: true);
            MoveTo(screen.X + screen.Width / 2 - (int)(190 * overlay.RenderScaling), y);
            Grid Card() => overlay.FindControl<StackPanel>("TodoRows")!.Children.OfType<Grid>().Single();
            Button ButtonWith(string cls) => Card().GetVisualDescendants().OfType<Button>().Single(b => b.Classes.Contains(cls));
            CheckHover(overlay, ButtonWith("priorityBadge"), Card(), profile, scale, Priority, ["Priority 3 · 1 is highest", "Click to change priority"]);
            Require(TodoOrganizationStore.SetSortMode(vault, "Manual").Success, "Cannot set Manual sort");
            overlay.ReloadTodos();
            ClockOverlayChecks.Pump(65);
            CheckHover(overlay, ButtonWith("todoDragHandle"), Card(), profile, scale, Manual,
                ["Drag handle or title to reorder", "Hold near a list edge to scroll", "Focus the handle, then press ↑ or ↓"]);

            Require(TodoOrganizationStore.SetPriority(vault, task, 10).Success, "Cannot change priority");
            overlay.ReloadTodos();
            ClockOverlayChecks.Pump(50);
            Require(AutomationProperties.GetHelpText(ButtonWith("priorityBadge"))?.Contains("Priority 10 · 1 is highest") == true, "Stale priority hint");
            settings.ThemeMode = AppThemeCatalog.Daylight;
            overlay.ApplySettings();
            Require(ToolTip.GetTip(launcher) is string legacy && legacy.Contains("collapse") &&
                ToolTip.GetPlacement(launcher) != PlacementMode.Custom, "Launcher did not restore its standard tooltip");
            overlay.ToggleExpanded();
            Require(ToolTip.GetTip(launcher) is string closed && closed.Contains("expand"), "Collapsed launcher retained expanded wording");
            settings.ThemeMode = AppThemeCatalog.Nika;
            overlay.ApplySettings();
            Require(ToolTip.GetTip(launcher) is ToolTip && AutomationProperties.GetHelpText(launcher)?.Contains("expand") == true, "Themed launcher did not return");
        }
        finally { overlay.Close(); }
    }

    static void CheckHover(TodoOverlayWindow overlay, Control owner, Control context, string profile, int scale,
        string state, string[] expected, bool nearTop = false)
    {
        var tip = ToolTip.GetTip(owner) as ToolTip ?? throw new InvalidOperationException($"Missing native {state} hint at {profile}/{scale}");
        Require(expected.All(line => AutomationProperties.GetHelpText(owner)?.Contains(line) == true), "Missing accessible instructions");
        var away = overlay.FindControl<Button>("AddTodoButton")!;
        if (overlay.IsExpanded) { RunningClocksChecks.Pointer(overlay, away, RawPointerEventType.Move); ClockOverlayChecks.Pump(50); }
        RunningClocksChecks.Pointer(overlay, owner, RawPointerEventType.Move);
        var wait = System.Diagnostics.Stopwatch.StartNew();
        while (!ToolTip.GetIsOpen(owner) && wait.Elapsed < TimeSpan.FromSeconds(4)) ClockOverlayChecks.Pump(25);
        Require(ToolTip.GetIsOpen(owner), $"Pointer did not open {state}/{profile}/{scale}");
        ClockOverlayChecks.Pump(45);
        var frame = tip.GetVisualDescendants().OfType<TodoFrame>().Single();
        var button = TodoDetailMenuPreview.BoundsOnDesktop(owner);
        var row = TodoDetailMenuPreview.BoundsOnDesktop(context);
        var popup = TodoDetailMenuPreview.BoundsOnDesktop(frame);
        var screen = overlay.Screens.ScreenFromVisual(overlay)!.WorkingArea;
        double dpi = overlay.RenderScaling, gap = 7 * scale / 100d;
        var work = new Rect(screen.X / dpi, screen.Y / dpi, screen.Width / dpi, screen.Height / dpi);
        Require(work.Inflate(1).Contains(popup) && frame.Pixel == (profile == "pixel"), "Off-screen or wrong profile");
        bool aligned = state switch
        {
            Collapsed or Manual => Math.Abs(button.Left - popup.Right - gap) < 2 && Math.Abs(button.Center.Y - popup.Center.Y) < 2,
            Expanded when !nearTop => Math.Abs(button.Left - popup.Left) < 2 && Math.Abs(button.Top - popup.Bottom - gap) < 2,
            _ => Math.Abs(button.Left - popup.Left) < 2 && Math.Abs(row.Bottom + gap - popup.Top) < 2
        };
        Require(aligned, $"Misplaced {profile}/{state}/{scale}: {button}, {row}, {popup}");
        if (state == Collapsed) Require(work.Right - button.Right < 20, "Launcher did not reach right edge");
        if (state == Expanded) Require(button.Left - work.Left < 20, "Launcher did not reach left edge");
        var text = frame.GetVisualDescendants().OfType<TextBlock>().ToArray();
        Require(text.Select(t => string.Concat(t.Inlines!.OfType<Run>().Select(r => r.Text))).SequenceEqual(expected), "Incorrect hint instructions");
        Require(text.All(t => t.TextLayout.TextLines.Count == 1 && !t.TextLayout.TextLines[0].HasCollapsed &&
            t.TextLayout.Height <= t.Bounds.Height + 1 && popup.Inflate(1).Contains(TodoDetailMenuPreview.BoundsOnDesktop(t))), "Clipped hint");
        if (!nearTop) SaveCapture(overlay, frame, state, row, popup, work, Path.Combine(Output, $"{profile}-{state}-{scale}.png"));
        if (overlay.IsExpanded)
        {
            RunningClocksChecks.Pointer(overlay, away, RawPointerEventType.Move);
            ClockOverlayChecks.Pump(40);
            Require(!ToolTip.GetIsOpen(owner), "Hint stayed open after leaving the control");
        }
    }

    static void SaveCapture(Window overlay, TodoFrame frame, string state, Rect row, Rect popup, Rect work, string path)
    {
        var crop = row.Union(popup).Inflate(6);
        if (state == Manual) crop = crop.Union(TodoDetailMenuPreview.BoundsOnDesktop(overlay.FindControl<Grid>("TodoOptionsRow")!).Inflate(4));
        if (state == Collapsed) crop = new Rect(crop.Left, crop.Top, work.Right - crop.Left + 2, crop.Height);
        if (state == Expanded) crop = new Rect(work.Left - 2, crop.Top, crop.Right - work.Left + 2, crop.Height);
        var window = TodoDetailMenuPreview.BoundsOnDesktop(overlay);
        var visible = window.Intersect(crop);
        var shift = new Vector(-crop.X, -crop.Y);
        using var backdrop = new RenderTargetBitmap(PixelSize.FromSize(overlay.ClientSize, 1), new Vector(96, 96));
        backdrop.Render(overlay);
        using var image = new RenderTargetBitmap(PixelSize.FromSize(crop.Size, 1), new Vector(96, 96));
        using (var dc = image.CreateDrawingContext())
        {
            dc.DrawImage(backdrop, visible.Translate(new Vector(-window.X, -window.Y)), visible.Translate(shift));
            dc.DrawRectangle(new VisualBrush(frame) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(new Rect(popup.Size), RelativeUnit.Absolute) }, null, popup.Translate(shift));
            if (state == Collapsed || state == Expanded)
            {
                double edge = (state == Collapsed ? work.Right : work.Left) - crop.X;
                dc.DrawLine(new Pen(Brush.Parse("#81959B"), 1), new Point(edge, 0), new Point(edge, crop.Height));
            }
        }
        image.Save(path);
    }

    static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
}
