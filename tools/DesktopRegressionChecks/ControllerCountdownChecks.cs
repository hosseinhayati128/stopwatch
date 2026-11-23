using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class ControllerCountdownChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "design/one-piece-v2/controller-countdown/implemented-native";
    private static readonly DateTime SampleNow = new(2026, 11, 23, 23, 20, 0, DateTimeKind.Local);

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
        foreach (int scale in new[] { 70, 90, 125 })
            foreach (bool pixel in new[] { false, true })
                Verify(scale, pixel);
    }

    private static void Verify(int scale, bool pixel)
    {
        var settings = new AppSettings { ThemeMode = AppThemeCatalog.Nika, NikaControllerPixel = pixel,
            UiScalePercent = scale, BlinkColon = false, TimeFormat = 1, FocusTrackingEnabled = false };
        DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Initialize", settings);
        var window = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden, null, [typeof(AppSettings)], null)!.Invoke([settings]);
        T Find<T>(string name) where T : Control => window.FindControl<T>(name)!;
        object? Call(string method, params object[] args) => typeof(ControllerWindow).GetMethod(method, Hidden)!.Invoke(window, args);
        void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        void AtSampleTime() { Call("UpdateActiveTimerDisplayAt", SampleNow.ToUniversalTime()); ClockOverlayChecks.Pump(40); }
        var card = Find<TodoFrame>("CountdownCard");
        var presets = Find<WrapPanel>("CountdownPresets");
        var start = Find<Button>("StartStopButton");
        var clockMode = Find<RadioButton>("CountdownUntilRadio");
        var editor = Find<TextBox>("SmartCountdownInputBox");
        var error = Find<TextBlock>("CountdownInputError");
        var finish = Find<Border>("CountdownFinish");
        var timer = (TimerSession)typeof(ControllerWindow).GetProperty("CurrentTimer", Hidden)!.GetValue(window)!;
        void Capture(string state)
        {
            var rect = ControllerSizingChecks.Bounds(card, window);
            Need(new Rect(window.ClientSize).Inflate(1).Contains(rect), "Countdown card outside the controller.");
            foreach (var label in card.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible && !string.IsNullOrEmpty(t.Text)))
                Need(label.TextLayout.WidthIncludingTrailingWhitespace <= label.Bounds.Width + 2 &&
                    rect.Inflate(1).Contains(ControllerSizingChecks.Bounds(label, window)), $"Clipped label: {label.Text}");
            var name = $"{(pixel ? "pixel" : "classic")}-{state}-{scale}";
            ControllerSizingChecks.Capture(window, rect.Inflate(4), name, Output);
            if (state != "invalid") ControllerSizingChecks.Capture(window, new Rect(window.ClientSize), name + "-controller", Output);
        }
        try
        {
            window.ShowActivated = window.ShowInTaskbar = false;
            window.Show();
            Find<RadioButton>("CountdownModeRadio").IsChecked = true;
            var preset = presets.Children.OfType<Button>().Single(b => Equals(b.Tag, "25"));
            Click(preset);
            AtSampleTime();
            Need(card.Pixel == pixel && card.IsEffectivelyVisible && preset.Classes.Contains("selected") &&
                presets.Children.OfType<Button>().Count(b => b.Classes.Contains("selected")) == 1, "Preset selection or theme failed.");
            Need(Find<TextBlock>("CountdownTime").Text == "23:45" && Find<TextBlock>("CountdownDay").Text == "Today" &&
                Find<TextBlock>("TimeDisplay").Text == "00:25:00" && !error.IsVisible && start.IsEnabled, "Preset finish time failed.");
            Capture("preset");
            Click(start);
            Need(timer.IsRunning && timer.CountdownRemaining == TimeSpan.FromMinutes(25) && !presets.IsEnabled && !clockMode.IsEnabled, "Running countdown accepted edits or duration changed.");
            Click(start);
            timer.CountdownRemaining = TimeSpan.FromMinutes(7);
            Click(start);
            Need(timer.CountdownRemaining == TimeSpan.FromMinutes(7) && timer.CountdownDuration == TimeSpan.FromMinutes(25), "Resume reset the duration.");
            Click(start);
            clockMode.IsChecked = true;
            AtSampleTime();
            Need(editor.Text == "00:00" && !presets.IsVisible && finish.IsEffectivelyVisible &&
                Find<TextBlock>("CountdownDay").Text == "Tomorrow" && Find<TextBlock>("CountdownDetail").Text!.Contains("40 min") &&
                Find<TextBlock>("TimeDisplay").Text == "00:40:00", "Midnight did not resolve to the next day.");
            Capture("midnight");
            Need((bool)Call("PrepareCountdown", timer, SampleNow.ToUniversalTime())! && timer.ClockTarget == SampleNow.Date.AddDays(1), "Start target differs from the preview.");
            editor.Text = "23:75";
            AtSampleTime();
            Need(!start.IsEnabled && !finish.IsVisible && error.IsEffectivelyVisible && error.Text == "Minutes must be 00–59." &&
                editor.Text == "23:75" && timer.SmartInputText == "23:75", "Invalid feedback failed.");
            Need(error.Foreground is Avalonia.Media.ISolidColorBrush brush && brush.Color == Avalonia.Media.Color.Parse("#FFADA3"),
                "Validation text lost its contrast on navy.");
            Capture("invalid");
            Call("ToggleSessionStartPause", timer);
            Need(!timer.IsRunning, "Shortcut started invalid input.");
            editor.Text = "";
            AtSampleTime();
            Need(!start.IsEnabled && editor.Text == "", "The empty field silently reverted to midnight.");
            editor.Text = "23:55";
            AtSampleTime();
            Need(start.IsEnabled && !error.IsVisible && Find<TextBlock>("CountdownTime").Text == "23:55", "Correcting the input did not restore Start.");
            var rail = Find<ListBox>("TimerRailList");
            rail.SelectedIndex = 0; ClockOverlayChecks.Pump(25);
            Need(!card.IsEffectivelyVisible && start.IsEnabled, "Countdown state leaked into another timer.");
            rail.SelectedIndex = 2; ClockOverlayChecks.Pump(25);
            Need(editor.Text == "23:55" && start.IsEnabled, "Switching timers lost the clock draft.");
            Find<RadioButton>("CountdownDurationRadio").IsChecked = true;
            Find<NumericUpDown>("CountdownSecondsBox").Value = 30;
            AtSampleTime();
            Need(!preset.Classes.Contains("selected") && timer.CountdownRemaining == TimeSpan.FromSeconds(1530), "Editing seconds left a preset selected.");
            clockMode.IsChecked = true;
            Need(editor.Text == "23:55", "Returning to clock time lost the entered target.");
            settings.UseSmartCountdownInput = true;
            Find<RadioButton>("CountdownDurationRadio").IsChecked = true;
            editor.Text = "2m 30s";
            AtSampleTime();
            Need(start.IsEnabled && !error.IsVisible && Find<TextBlock>("TimeDisplay").Text == "00:02:30", "Smart duration was not previewed.");
            Click(start);
            Need(timer.IsRunning && timer.CountdownRemaining == TimeSpan.FromSeconds(150), "Smart duration did not start.");
            Console.WriteLine($"PASS {(pixel ? "Pixel" : "Classic")} {scale}%: preset, midnight, invalid and corrected input, resume, timer switching and smart duration.");
        }
        finally { window.Close(); DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown"); }
    }

    private static void Need(bool value, string message)
    {
        if (!value) throw new InvalidOperationException(message);
    }
}
