using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class ControllerTimerActionsChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "design/one-piece-v2/controller-timer-actions/implemented-native";

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
        foreach (int scale in new[] { 70, 90, 125 })
            foreach (bool pixel in new[] { false, true })
                CheckController(scale, pixel);
    }

    private static void CheckController(int scale, bool pixel)
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, NikaControllerPixel = pixel,
            UiScalePercent = scale, TimeFormat = 1, BlinkColon = false, FocusTrackingEnabled = false };
        DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Initialize", settings);
        var w = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden, null, [typeof(AppSettings)], null)!.Invoke([settings]);
        try
        {
            w.ShowActivated = false;
            w.ShowInTaskbar = false;
            w.WindowStartupLocation = WindowStartupLocation.Manual;
            w.Show();
            var manager = (TimerSessionManager)typeof(ControllerWindow).GetField("_timerManager", Hidden)!.GetValue(w)!;
            var timers = manager.Sessions;
            timers[0].Name = "Reading";
            timers[0].RestoreElapsed(TimeSpan.FromSeconds(1092), false);
            timers[0].IsRunning = true;
            timers[1].Name = "Illustration";
            timers[1].RestoreElapsed(TimeSpan.FromSeconds(2678), false);
            timers[2].Name = "Research";
            timers[2].RestoreElapsed(TimeSpan.FromSeconds(1763), false);
            timers[3].Name = "Programming";
            timers[4].Name = "Navigation";
            timers[4].Mode = 1;
            timers[4].IsSeparated = true;
            var rail = w.FindControl<ListBox>("TimerRailList")!;
            var work = w.Screens.ScreenFromVisual(w)!.WorkingArea;
            var visible = typeof(ControllerWindow).GetField("_overlaysVisible", Hidden)!;
            foreach (var (state, index) in new[] { ("running", 0), ("paused", 1), ("clock", 4) })
            {
                bool clock = state == "clock";
                w.Position = new PixelPoint(work.X + 32, work.Y + 24);
                visible.SetValue(w, state != "paused");
                typeof(ControllerWindow).GetMethod("RefreshTimerRail", Hidden)!.Invoke(w, null);
                rail.ScrollIntoView(index);
                ClockOverlayChecks.Pump(65);
                var row = (ListBoxItem)rail.ContainerFromIndex(index)!;
                var trigger = row.GetVisualDescendants().OfType<Button>().Single();
                if (clock)
                {
                    int bottom = trigger.PointToScreen(new Point(0, trigger.Bounds.Height)).Y;
                    int clearance = (int)Math.Ceiling(36 * scale / 100.0 * w.RenderScaling);
                    w.Position = new PixelPoint(w.Position.X, w.Position.Y + work.Bottom - bottom - clearance);
                    ClockOverlayChecks.Pump(65);
                }
                var menu = Open(trigger);
                try
                {
                    var labels = menu.Items.OfType<MenuItem>().Select(a => a.Header as string).ToArray();
                    string[] expected = clock ? ["Hide timer overlays", "Combine overlays", "Edit timer…", "Set project…", "Close timer…"] :
                        [state == "running" ? "Pause" : "Resume", "Add lap", "Reset", state == "running" ? "Hide timer overlays" : "Show timer overlays",
                         "Separate overlay", "Edit timer…", "Set project…", "Close timer…"];
                    Require(labels.SequenceEqual(expected), $"Incorrect {state} commands: {string.Join(", ",labels)}");
                    Require(manager.Active == timers[index], "The row menu opened for the wrong timer.");
                    var surface = menu.GetVisualDescendants().OfType<TodoFrame>().First();
                    Require(surface.Pixel == pixel, "Wrong menu theme.");
                    var bounds = TodoDetailMenuPreview.BoundsOnDesktop(surface);
                    var button = TodoDetailMenuPreview.BoundsOnDesktop(trigger);
                    var screen = work.ToRect(w.RenderScaling);
                    Require(screen.Inflate(1).Contains(bounds) && (clock
                        ? bounds.Top < button.Top - 20 && bounds.Bottom <= button.Bottom + 2
                        : Math.Abs(bounds.Top - button.Top) < 2 && bounds.Bottom > button.Bottom), "Menu was misplaced: " + bounds);
                    foreach (var item in menu.Items.OfType<MenuItem>())
                    {
                        var label = item.GetVisualDescendants().OfType<TextBlock>().Single();
                        Require(label.TextLayout.WidthIncludingTrailingWhitespace <= label.Bounds.Width + .5 &&
                            screen.Contains(TodoDetailMenuPreview.BoundsOnDesktop(label)), "Clipped menu label: " + item.Header);
                    }
                    var options = menu.Items.OfType<MenuItem>().ToArray();
                    var selectedRow = options[0].GetVisualDescendants().OfType<TodoFrame>().Single();
                    var closeText = options[^1].GetVisualDescendants().OfType<TextBlock>().Single();
                    Require((selectedRow.Background as ISolidColorBrush)?.Color == Color.Parse("#204B58") &&
                        (closeText.Foreground as ISolidColorBrush)?.Color == Color.Parse("#F3A69A"),
                        "Selected and destructive actions did not use the approved menu colors.");
                    Capture(w, row, surface, screen, clock, $"{(pixel ? "pixel" : "classic")}-{state}-{scale}");
                    if (scale == 90)
                    {
                        if (clock)
                        {
                            Invoke(menu, "Combine overlays");
                            Require(!timers[index].IsSeparated, "Clock did not rejoin the combined overlay.");
                            Invoke(Open(trigger), "Separate overlay");
                            Require(timers[index].IsSeparated, "Clock did not separate.");
                            Invoke(Open(trigger), "Hide timer overlays");
                            Require(Equals(visible.GetValue(w), false), "Visibility command lost its global scope.");
                            Invoke(Open(trigger), "Show timer overlays");
                            foreach (var (label, type) in new[] { ("Edit timer…", typeof(TimerEditorWindow)), ("Set project…", typeof(TimerNameWindow)),
                                        ("Close timer…", typeof(ConfirmationDialogWindow)) })
                            {
                                Invoke(Open(trigger), label);
                                var dialog = w.OwnedWindows.Single(d => d.IsVisible && d.GetType() == type);
                                dialog.FindControl<Button>("CancelButton")!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                                ClockOverlayChecks.Pump(45);
                                Require(manager.Sessions.Contains(timers[index]) && manager.Active == timers[index], "Cancel lost the target timer.");
                            }
                        }
                        else if (state == "running")
                        {
                            Invoke(menu, "Pause");
                            Require(!timers[index].IsRunning, "Pause left its target running.");
                            Invoke(Open(trigger), "Resume");
                            Require(timers[index].IsRunning, "Resume did not restart its target.");
                        }
                        else
                        {
                            int laps = timers[index].LapTimes.Count;
                            Invoke(menu, "Add lap");
                            Require(timers[index].LapTimes.Count == laps + 1 && timers[0].IsRunning, "Lap affected the wrong timer.");
                            Invoke(Open(trigger), "Reset");
                            Require(timers[index].LapTimes.Count == 0 && !timers[index].HasAccumulatedTime, "Reset failed.");
                            var ready = Open(trigger);
                            Require(Equals(((MenuItem)ready.Items[0]!).Header, "Start"), "Reset timer did not offer Start.");
                            var selected = (MenuItem)ready.Items[0]!;
                            selected.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Escape });
                            ClockOverlayChecks.Pump(40);
                            Require(!ready.IsOpen && !timers[index].IsRunning, "Escape ran a command.");
                            var stale = Open(trigger);
                            var oldCommand = (MenuItem)stale.Items[0]!;
                            rail.SelectedIndex = 0;
                            ClockOverlayChecks.Pump(40);
                            oldCommand.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                            Require(!stale.IsOpen && timers[0].IsRunning && !timers[index].IsRunning, "A stale row menu acted on another timer.");
                        }
                    }
                }
                finally { menu.Close(); }
            }
            Console.WriteLine($"PASS {(pixel ? "Pixel" : "Classic")} {scale}%: stopwatch states and bottom-edge clock.");
        }
        finally { w.Close(); DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown"); }
    }

    private static ContextMenu Open(Button button)
    {
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        ClockOverlayChecks.Pump(75);
        Require(button.ContextMenu is { IsOpen: true }, "Timer actions did not open.");
        return button.ContextMenu!;
    }

    private static void Invoke(ContextMenu menu, string label)
    {
        var item = menu.Items.OfType<MenuItem>().Single(a => Equals(a.Header, label));
        item.Focus();
        item.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
        ClockOverlayChecks.Pump(55);
        Require(!menu.IsOpen, "Command did not dismiss the menu: " + label);
    }

    private static void Capture(ControllerWindow w, Control row, TodoFrame surface, Rect screen, bool clock, string name)
    {
        var menu = TodoDetailMenuPreview.BoundsOnDesktop(surface);
        var area = TodoDetailMenuPreview.BoundsOnDesktop(row).Union(menu).Inflate(8);
        if (clock) area = area.WithHeight(screen.Bottom - area.Top);
        area = area.Intersect(screen);
        var offset = new Vector(-Math.Floor(area.X), -Math.Floor(area.Y));
        var root = w.FindControl<Grid>("ControllerFrame")!;
        using var image = new RenderTargetBitmap(new PixelSize((int)(Math.Ceiling(area.Right) + offset.X),
            (int)(Math.Ceiling(area.Bottom) + offset.Y)), new Vector(96, 96));
        using (var dc = image.CreateDrawingContext())
        {
            dc.DrawRectangle(Brush.Parse("#102631"), null, new Rect(image.Size));
            dc.DrawRectangle(new VisualBrush(root) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(root.Bounds, RelativeUnit.Absolute) },
                null, TodoDetailMenuPreview.BoundsOnDesktop(root).Translate(offset));
            dc.DrawRectangle(new VisualBrush(surface) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(new Rect(menu.Size), RelativeUnit.Absolute) },
                null, menu.Translate(offset));
        }
        image.Save(Path.Combine(Output, name + ".png"));
    }

    private static void Require(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
}
