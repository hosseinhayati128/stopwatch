using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class ControllerFooterChecks
{
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private const string Output = "design/one-piece-v2/controller-footer/implemented-native";

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
        var w = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden, null, [typeof(AppSettings)], null)!.Invoke([settings]);
        try
        {
            w.ShowActivated = w.ShowInTaskbar = false;
            w.Show();
            var manager = (TimerSessionManager)typeof(ControllerWindow).GetField("_timerManager", Hidden)!.GetValue(w)!;
            var timer = manager.Active!;
            var footer = w.GetVisualDescendants().OfType<Border>().Single(b => b.Classes.Contains("nikaFooter"));
            var refresh = typeof(ControllerWindow).GetMethod("UpdateActiveTimerDisplay", Hidden)!;
            var rail = typeof(ControllerWindow).GetMethod("RefreshTimerRail", Hidden)!;
            void Expect(params string[] labels)
            {
                rail.Invoke(w, null);
                refresh.Invoke(w, null);
                ClockOverlayChecks.Pump(55);
                var texts = footer.GetVisualDescendants().OfType<TextBlock>().Where(t => t.IsEffectivelyVisible).ToArray();
                if (!texts.Select(t => t.Text).SequenceEqual(labels))
                    throw new Exception($"Expected [{string.Join(", ", labels)}], saw [{string.Join(", ", texts.Select(t => t.Text))}].");
                var strip = ControllerSizingChecks.Bounds(footer, w);
                double right = strip.Left;
                foreach (var t in texts)
                {
                    var bounds = ControllerSizingChecks.Bounds(t, w);
                    if (bounds.Left < right - 1 || !strip.Inflate(1).Contains(bounds) || bounds.Width <= 0 ||
                        t.TextLayout.TextLines.Count != 1 || t.TextLayout.TextLines.Any(l => l.HasCollapsed))
                        throw new Exception("Overlapping or clipped footer label: " + t.Text);
                    right = bounds.Right;
                }
                foreach (var cap in footer.GetVisualDescendants().OfType<TodoFrame>().Where(c => c.IsEffectivelyVisible))
                    if (cap.Pixel != pixel) throw new Exception("Keycap shape does not match the controller style.");
            }
            void Capture(string state)
            {
                string name = $"{(pixel ? "pixel" : "classic")}-{state}-{scale}";
                ControllerSizingChecks.Capture(w, ControllerSizingChecks.Bounds(footer, w), name, Output);
                if (state == "paused") ControllerSizingChecks.Capture(w, new Rect(w.ClientSize), name + "-controller", Output);
            }
            timer.RestoreElapsed(TimeSpan.FromSeconds(2667), true);
            timer.IsRunning = true;
            Expect("Running", "Win+F2", "then", "Space", "Pause", "R", "Reset");
            Capture("running");
            timer.IsRunning = false;
            timer.RestoreElapsed(TimeSpan.FromSeconds(2667), false);
            settings.CommandModeKeys[ShortcutAction.StartStop] = 'Y';
            Expect("Paused", "Win+F2", "then", "Y", "Resume", "R", "Reset");
            Capture("paused");
            timer.LastNonClockMode = 0;
            timer.Mode = 1;
            Expect("Live clock", "Win+F2", "then", "C", "Switch to stopwatch", "O", "Hide overlay");
            Capture("clock");
            var overrides = (Dictionary<Guid, bool>)typeof(ControllerWindow).GetField("_overlayVisibilityOverrides", Hidden)!.GetValue(w)!;
            overrides[timer.Id] = false;
            settings.LeaderShortcut = new Shortcut(Shortcut.MOD_CONTROL | Shortcut.MOD_SHIFT, 0x7B);
            timer.LastNonClockMode = 2;
            Expect("Live clock", "Ctrl+Shift+F12", "then", "C", "Switch to countdown", "O", "Show overlay");
            timer.LastNonClockMode = 3;
            Expect("Live clock", "Ctrl+Shift+F12", "then", "C", "Switch to timecode", "O", "Show overlay");
            settings.CommandModeKeys[ShortcutAction.ToggleClock] = 0;
            Expect("Live clock", "Ctrl+Shift+F12", "then", "O", "Show overlay");
            settings.CommandModeKeys[ShortcutAction.ToggleOverlay] = 0;
            Expect("Live clock");
            settings.CommandModeKeys = AppSettings.DefaultCommandModeKeys();
            settings.LeaderShortcut = new Shortcut(0, 0);
            Expect("Live clock");
            settings.LeaderShortcut = AppSettings.DefaultLeaderShortcut();
            timer.Mode = 0;
            timer.ResetElapsed();
            Expect("Ready", "Win+F2", "then", "Space", "Start", "R", "Reset");
            foreach (var existing in manager.Sessions.ToArray()) manager.Close(existing);
            Expect("No timer");
            Console.WriteLine($"PASS {(pixel ? "Pixel" : "Classic")} at {scale}%");
        }
        finally { w.Close(); DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown"); }
    }
}
