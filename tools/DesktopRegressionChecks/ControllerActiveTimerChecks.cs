using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class ControllerActiveTimerChecks
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private const string Output = "design/one-piece-v2/controller-active-timer/implemented-native";

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
        foreach (int scale in new[] { 70, 90, 125 })
        {
            var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, UiScalePercent = scale, TimeFormat = 1, BlinkColon = false, FocusTrackingEnabled = false };
            DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Initialize", settings);
            var window = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden, null, [typeof(AppSettings)], null)!.Invoke([settings]);
            var editor = new SettingsWindow(settings);
            try
            {
                window.ShowActivated = false;
                window.ShowInTaskbar = false;
                window.Show();
                var timer = (TimerSession)typeof(ControllerWindow).GetProperty("CurrentTimer", Hidden)!.GetValue(window)!;
                var originalLaps = timer.LapTimes.ToArray();
                var picker = editor.FindControl<ComboBox>("ControllerStyleCombo")!;
                var changes = new List<SettingsChangeKind>();
                Action<SettingsChangeKind> changed = change =>
                {
                    changes.Add(change);
                    if (change == SettingsChangeKind.ControllerStyle) Invoke("ApplyActiveTimerStyle");
                };
                typeof(SettingsWindow).GetEvent("SettingsChanged", Hidden)!.GetAddMethod(true)!.Invoke(editor, [changed]);
                string overlay = settings.OverlayTheme;
                Require(picker.SelectedIndex == 0 && !settings.NikaControllerPixel, "Existing settings must default to Classic Navy.");
                foreach (bool pixel in new[] { false, true })
                {
                    changes.Clear();
                    picker.SelectedIndex = pixel ? 1 : 0;
                    if (pixel) Require(changes.SequenceEqual([SettingsChangeKind.ControllerStyle]), $"Expected a controller style change, received {string.Join(", ",changes)}.");
                    Require(settings.NikaControllerPixel == pixel && settings.OverlayTheme == overlay, "Style choice did not stay local to the controller.");
                    string path = Path.Combine(Path.GetTempPath(), $"controller-style-{Guid.NewGuid():N}.json");
                    try { Require(SettingsStore.Save(settings, path) && SettingsStore.Load(path).NikaControllerPixel == pixel, "Style did not round-trip through the settings store."); }
                    finally { File.Delete(path); }
                    timer.Mode = 0;
                    timer.LapTimes.Clear();
                    foreach (var lap in originalLaps) timer.LapTimes.Add(lap);
                    timer.RestoreElapsed(TimeSpan.FromSeconds(2678), false);
                    timer.IsRunning = true;
                    Invoke("RefreshTimerRail");
                    Refresh();
                    var card = window.FindControl<TodoFrame>("ActiveTimerCard")!;
                    var digitView = window.FindControl<ControllerPixelDigits>("PixelTimeDisplay")!;
                    Require(card.Pixel == pixel && digitView.IsEffectivelyVisible == pixel && window.FindControl<TextBlock>("NikaTimeDisplay")!.IsEffectivelyVisible != pixel, "Wrong visible clock face.");
                    Require(digitView.Text == "00:44:38" && State() == "Running" && Label("StartStopButton") == "Pause", "Incorrect running timer.");
                    foreach (var button in window.FindControl<PirateActionPanel>("ControllerActions")!.Children.OfType<Button>())
                    {
                        var rect = new Rect(button.TranslatePoint(default, window)!.Value, button.TranslatePoint(new Point(button.Bounds.Width,button.Bounds.Height),window)!.Value);
                        Require(button.IsEffectivelyVisible && rect.Width > 0 && new Rect(window.ClientSize).Contains(rect), $"Clipped action {button.Name} at {scale}%.");
                        Require(button.GetVisualDescendants().OfType<NikaActionIcon>().Single().Pixel == pixel, "Incorrect action icons.");
                    }
                    ControllerSizingChecks.Capture(window, $"{(pixel ? "pixel" : "classic")}-running-{scale}", Output);
                    int laps = timer.LapTimes.Count;
                    Click("LapButton");
                    Require(timer.LapTimes.Count == laps + 1, "Add lap did not record a lap.");
                    Click("StartStopButton");
                    Require(!timer.IsRunning && State() == "Paused" && Label("StartStopButton") == "Resume", "Pause did not expose Resume.");
                    Click("StartStopButton");
                    Require(timer.IsRunning && State() == "Running", "Resume did not restart the timer.");
                    Click("ResetButton");
                    Require(!timer.IsRunning && timer.LapTimes.Count == 0 && State() == "Ready" && Label("StartStopButton") == "Start", "Reset did not restore Ready.");
                    timer.Mode = 1;
                    Refresh();
                    Require(State() == "Live" && Caption() == "Local time" && !window.FindControl<Button>("LapButton")!.IsEffectivelyVisible
                        && !window.FindControl<Button>("StartStopButton")!.IsEffectivelyVisible && !window.FindControl<Button>("ResetButton")!.IsEffectivelyVisible
                        && !window.FindControl<Border>("ControllerLaps")!.IsEffectivelyVisible && window.FindControl<Button>("EditButton")!.IsEffectivelyVisible, "Clock actions or labels are incorrect.");
                    Click("ToggleOverlayButton");
                    Require(Label("ToggleOverlayButton") == "Show overlay", "Hide overlay action did not update.");
                    Click("ToggleOverlayButton");
                    timer.Mode = 2;
                    Refresh();
                    Require(Caption() == "Time remaining" && window.FindControl<Button>("StartStopButton")!.IsEffectivelyVisible, "Countdown controls were not restored.");
                    timer.CountdownInitialized = true;
                    timer.CountdownRemaining = TimeSpan.Zero;
                    Refresh();
                    Require(State() == "Finished" && Label("StartStopButton") == "Start", "An expired countdown incorrectly offered to resume.");
                    timer.Mode = 3;
                    Refresh();
                    Require(Caption() == "Timecode", "Timecode caption is incorrect.");
                }
                Console.WriteLine($"PASS Classic and original Pixel at {scale}%, all four modes and transport actions.");
            }
            finally { editor.Close(); window.Close(); DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown"); }

            void Invoke(string method) => typeof(ControllerWindow).GetMethod(method, Hidden)!.Invoke(window, null);
            void Refresh() { Invoke("UpdateActiveTimerDisplay"); ClockOverlayChecks.Pump(80); }
            string? State() => window.FindControl<TextBlock>("ActiveTimerStatus")!.Text;
            string? Caption() => window.FindControl<TextBlock>("ActiveTimerCaption")!.Text;
            string? Label(string name) => window.FindControl<Button>(name)!.Content as string;
            void Click(string name) => window.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        }
    }

    private static void Require(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
