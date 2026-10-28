using System.Reflection;
using Avalonia.Controls;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Views;

internal static class ControllerModeChecks
{
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
