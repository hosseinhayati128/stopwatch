using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class ControllerModeChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "design/one-piece-v2/controller-mode-selector/implemented-native";

    internal static void NavySelector()
    {
        Directory.CreateDirectory(Output);
        DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
        foreach (int scale in new[] { 70, 90, 125 })
            foreach (bool pixel in new[] { false, true })
                CheckNavySelector(scale, pixel);
    }

    private static void CheckNavySelector(int scale, bool pixel)
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, NikaControllerPixel = pixel, UiScalePercent = scale,
            TimeFormat = 1, BlinkColon = false, FocusTrackingEnabled = false };
        DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Initialize", settings);
        var window = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden, null, [typeof(AppSettings)], null)!.Invoke([settings]);
        var sampleUtc = new DateTime(2026, 11, 23, 23, 20, 0, DateTimeKind.Local).ToUniversalTime();
        var display = typeof(ControllerWindow).GetMethod("UpdateActiveTimerDisplayAt", Hidden)!;
        var click = typeof(RadioButton).GetMethod("OnClick", Hidden)!;
        try
        {
            window.ShowInTaskbar = false;
            window.Show();
            window.Activate();
            ClockOverlayChecks.Pump(80);
            var timer = (TimerSession)typeof(ControllerWindow).GetProperty("CurrentTimer", Hidden)!.GetValue(window)!;
            var radios = new[] { "Stopwatch", "Clock", "Countdown", "Timecode" }
                .Select(name => window.FindControl<RadioButton>(name + "ModeRadio")!).ToArray();
            var track = window.FindControl<TodoFrame>("ControllerModeTrack")!;
            var setup = window.FindControl<TodoFrame>("CountdownCard")!;
            var bounds = ControllerSizingChecks.Bounds(track, window);
            Require(track.Pixel == pixel && new Rect(window.ClientSize).Contains(bounds), "Incorrect track style or bounds.");
            foreach (int mode in new[] { 0, 1, 3, 2 })
            {
                window.FindControl<Button>("WorkspaceNewTimerButton")!.Focus();
                click.Invoke(radios[mode], null);
                if (mode == 2)
                {
                    var preset = window.FindControl<WrapPanel>("CountdownPresets")!.Children.OfType<Button>().Single(b => Equals(b.Tag, "25"));
                    preset.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    Require(radios[1].Focus(NavigationMethod.Tab), "Keyboard focus could not reach Clock.");
                }
                display.Invoke(window, [sampleUtc]);
                ClockOverlayChecks.Pump(45);
                Require(timer.Mode == mode && setup.IsEffectivelyVisible == (mode == 2), "The selector and workspace disagree.");
                foreach (var (radio, index) in radios.Select((button, i) => (button, i)))
                {
                    var plate = radio.GetVisualDescendants().OfType<TodoFrame>().Single();
                    var label = plate.GetVisualDescendants().OfType<TextBlock>().Single();
                    var icon = plate.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single();
                    Require(plate.Pixel == pixel && icon.Data != null && icon.StrokeThickness == (pixel ? 2 : 1.8), "Incorrect mode icon or frame.");
                    Require(radio.IsChecked == (index == mode) && HasColor(plate.Background, "#EDCB83") == (index == mode), "Incorrect selected marker.");
                    Require(bounds.Contains(ControllerSizingChecks.Bounds(radio, window)) &&
                        label.TextLayout.Width <= label.Bounds.Width + 1, "Clipped mode button.");
                    if (mode == 2 && index == 1)
                        Require(radio.IsFocused && HasColor(plate.BorderBrush, "#9FE4D3"), "Focus on Clock failed to display its own outline.");
                }
                var name = $"{(pixel ? "pixel" : "classic")}-{radios[mode].Content!.ToString()!.ToLowerInvariant()}-{scale}";
                ControllerSizingChecks.Capture(window, bounds.Inflate(7), name, Output);
                if (mode == 2)
                {
                    var region = bounds.Union(ControllerSizingChecks.Bounds(setup, window));
                    ControllerSizingChecks.Capture(window, region.Inflate(5), name + "-setup", Output);
                    ControllerSizingChecks.Capture(window, new Rect(window.ClientSize), name + "-controller", Output);
                }
            }
            radios[1].RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Space });
            radios[1].RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyUpEvent, Key = Avalonia.Input.Key.Space });
            ClockOverlayChecks.Pump(30);
            Require(timer.Mode == 1 && radios[1].IsChecked == true && !setup.IsEffectivelyVisible, "Space did not activate the focused Clock.");
            var selected = radios[1].GetVisualDescendants().OfType<TodoFrame>().Single();
            Require(HasColor(selected.BorderBrush, "#654316"), "Selected keyboard focus is indistinct.");
            settings.NikaControllerPixel = !pixel;
            typeof(ControllerWindow).GetMethod("ApplyActiveTimerStyle", Hidden)!.Invoke(window, null);
            ClockOverlayChecks.Pump(30);
            Require(track.Pixel == !pixel && selected.Pixel == !pixel && timer.Mode == 1, "Changing styles lost the current mode or its shape.");
            Console.WriteLine($"PASS {(pixel ? "Pixel" : "Classic")} {scale}%: four modes, selection, focus, Space activation and live style change.");
        }
        finally { window.Close(); DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown"); }
    }

    private static bool HasColor(IBrush? brush, string color) => brush is ISolidColorBrush solid && solid.Color == Color.Parse(color);
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }

    internal static void Run()
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        var constructor = typeof(ControllerWindow).GetConstructor(hidden, null, new[] { typeof(AppSettings) }, null)!;
        var refresh = typeof(ControllerWindow).GetMethod("UpdateActiveTimerDisplay", hidden)!;
        var current = typeof(ControllerWindow).GetProperty("CurrentTimer", hidden)!;
        var click = typeof(RadioButton).GetMethod("OnClick", hidden)!;
        foreach (string theme in new[] { AppThemeCatalog.Nika, AppThemeCatalog.Pirate, AppThemeCatalog.Midnight })
        {
            var controller = (ControllerWindow)constructor.Invoke(new object[] { new AppSettings { ThemeMode = theme } });
            controller.ShowActivated = false;
            controller.ShowInTaskbar = false;
            try
            {
                controller.Show(); ClockOverlayChecks.Pump(100);
                var radios = new[] { "StopwatchModeRadio", "ClockModeRadio", "CountdownModeRadio", "TimecodeModeRadio" }
                    .Select(name => controller.FindControl<RadioButton>(name)!).ToArray();
                var timer = (TimerSession)current.GetValue(controller)!;
                foreach (int mode in new[] { 1, 2, 3, 0, 3, 2, 1, 0 })
                {
                    int previousMode = timer.Mode;
                    click.Invoke(radios[mode], null);
                    for (int tick = 0; tick < 3; tick++) refresh.Invoke(controller, null);
                    if (timer.Mode != mode || radios.Where((radio, index) => radio.IsChecked != (index == mode)).Any())
                        throw new Exception($"{theme}: clicking {radios[mode].Content} reverted to mode {timer.Mode} during refresh.");
                    if (controller.FindControl<Border>("CountdownPanel")!.IsVisible != (mode == 2))
                        throw new Exception($"{theme}: countdown options do not follow mode {mode}.");
                    if (mode == 1 && previousMode != 1 && timer.LastNonClockMode != previousMode)
                        throw new Exception($"{theme}: entering Clock forgot the previous timer mode.");
                }
                // Loading an existing timer's mode must only update its controls.
                foreach (int mode in new[] { 3, 1, 2, 0 })
                {
                    timer.Mode = mode;
                    int rememberedMode = timer.LastNonClockMode;
                    refresh.Invoke(controller, null);
                    if (timer.Mode != mode || radios[mode].IsChecked != true || timer.LastNonClockMode != rememberedMode)
                        throw new Exception($"{theme}: displaying stored mode {mode} changed the timer's mode.");
                }
            }
            finally { controller.Close(); }
        }
    }
}
