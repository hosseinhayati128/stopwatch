using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class ControllerActiveHeaderChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "design/one-piece-v2/controller-active-header/implemented-native";
    private const string LongTitle = "Illustration and visual development for the autumn exhibition";

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
        foreach (int scale in new[] { 70, 90, 125 })
            foreach (bool pixel in new[] { false, true })
                Check(scale, pixel);
    }

    private static void Check(int scale, bool pixel)
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, NikaControllerPixel = pixel,
            UiScalePercent = scale, TimeFormat = 1, BlinkColon = false, FocusTrackingEnabled = false };
        DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Initialize", settings);
        var window = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden, null, [typeof(AppSettings)], null)!.Invoke([settings]);
        try
        {
            window.ShowActivated = window.ShowInTaskbar = false;
            window.Show();
            var manager = (TimerSessionManager)typeof(ControllerWindow).GetField("_timerManager", Hidden)!.GetValue(window)!;
            var refresh = typeof(ControllerWindow).GetMethod("UpdateActiveTimerDisplay", Hidden)!;
            var rail = typeof(ControllerWindow).GetMethod("RefreshTimerRail", Hidden)!;
            void Display()
            {
                rail.Invoke(window, null);
                refresh.Invoke(window, null);
                ClockOverlayChecks.Pump(60);
            }
            var header = window.FindControl<Grid>("WorkspaceHeader")!;
            var title = window.FindControl<TextBlock>("ActiveWorkspaceTitle")!;
            var compass = window.FindControl<NikaArtwork>("HeaderCompass")!;
            var mode = window.FindControl<TodoFrame>("ControllerModeTrack")!;
            var create = window.FindControl<Button>("WorkspaceNewTimerButton")!;
            var project = window.FindControl<Button>("WorkspaceProjectButton")!;
            Rect At(Control c) => ControllerSizingChecks.Bounds(c, window);
            Display();
            var anchor = At(create);
            foreach (string state in new[] { "short", "long", "empty" })
            {
                bool empty = state == "empty";
                if (empty) foreach (var timer in manager.Sessions.ToArray()) manager.Close(timer);
                else manager.Active!.Name = state == "short" ? "Illustration" : LongTitle;
                Display();
                Require(title.Text == (empty ? "No timers yet" : manager.Active!.Name), "Incorrect active heading.");
                Require(title.TextLayout.TextLines.Count == (state == "long" ? 2 : 1) && !title.TextLayout.TextLines.Any(l => l.HasCollapsed), "Heading was cut off.");
                Require(At(create) == anchor && !At(title).Intersects(At(create)), "New timer moved or overlapped the title.");
                Require(compass.IsEffectivelyVisible == (state == "short") && (!compass.IsEffectivelyVisible || !At(title).Intersects(At(compass))), "Compass competes with the title.");
                Require(create.IsEffectivelyEnabled && project.IsEffectivelyVisible == !empty && mode.IsEffectivelyEnabled == !empty, "Incorrect empty header commands.");
                Require(mode.GetVisualDescendants().OfType<RadioButton>().Count(r => r.IsChecked == true) == (empty ? 0 : 1), "Incorrect selected mode.");
                Require(create.GetVisualDescendants().OfType<TodoFrame>().Single().Pixel == pixel, "Incorrect header button shape.");
                string name = $"{(pixel ? "pixel" : "classic")}-{state}-{scale}";
                ControllerSizingChecks.Capture(window, At(header).Union(At(mode)).Inflate(5), name, Output);
                if (state == "long") ControllerSizingChecks.Capture(window, new Rect(window.ClientSize), name + "-controller", Output);

                if (state != "long")
                {
                    var button = empty ? create : project;
                    var hit = window.InputHitTest(At(button).Center) as Visual;
                    Require(hit == button || hit?.GetVisualAncestors().Contains(button) == true, "Header button cannot receive clicks.");
                    button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    ClockOverlayChecks.Pump(60);
                    var dialog = window.OwnedWindows.OfType<TimerNameWindow>().Single(w => w.IsVisible);
                    dialog.Close(false);
                    ClockOverlayChecks.Pump(45);
                    Require((manager.Active == null) == empty, "Cancel changed the active timer.");
                }
            }
            var restored = manager.Create();
            restored.Name = new string('W', 140);
            Display();
            Require(Equals(ToolTip.GetTip(title), restored.Name) && title.TextLayout.TextLines.Count <= 2 &&
                project.IsEffectivelyVisible && mode.IsEffectivelyEnabled, "Long name or commands not restored after the empty state.");
            restored.Name = "Art";
            Display();
            Require(compass.IsEffectivelyVisible && ToolTip.GetTip(title) == null, "Returning to a short name retained the long heading.");
            Console.WriteLine($"PASS {(pixel ? "Pixel" : "Classic")} {scale}% header: all three states, stable action and dialog routing.");
        }
        finally { window.Close(); DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown"); }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
