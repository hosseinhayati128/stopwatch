using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;
using StopwatchOverlay.Platform;

internal static class ControllerNavigationChecks
{
    private const string Output = "design/one-piece-v2/controller-navigation/implemented-native";
    private const BindingFlags Hidden = BindingFlags.NonPublic | BindingFlags.Instance;
    private static readonly string[] Labels = ["Records & Analytics", "Settings", "Shortcuts"];
    private static readonly string[] Stems = ["records", "settings", "shortcuts"];

    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        DeleteTodoNativeChecks.Desktop("ThemeResources", "ApplyApplication", AppThemeCatalog.Nika);
        var original = PlatformServices.HotKey;
        // Use the core no-op service while opening and dismissing the shortcuts dialog.
        PlatformServices.HotKey = null!;
        try
        {
            foreach (int scale in new[] { 70, 90, 125 })
                foreach (bool pixel in new[] { false, true }) Check(scale, pixel);
        }
        finally { PlatformServices.HotKey = original; }
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
            ClockOverlayChecks.Pump(80);
            var header = w.FindControl<Border>("NikaHeader")!;
            var timers = w.FindControl<Button>("NavyTimersButton")!;
            var tray = w.FindControl<TodoFrame>("NavyNavigation") ?? throw new Exception("Grouped Navy navigation is missing.");
            var buttons = tray.GetVisualDescendants().OfType<Button>().ToArray();
            Require(buttons.Select(b => b.Content as string).SequenceEqual(Labels), "Unexpected navigation order or labels.");
            Rect At(Control c) => ControllerSizingChecks.Bounds(c, w);
            var bounds = At(tray);
            var title = header.GetVisualDescendants().OfType<TextBlock>().Single(t => t.Classes.Contains("nikaHeaderTitle"));
            Require(At(title).Right <= bounds.Left+1 && bounds.Right+2 <= At(timers).Left &&
                title.TextLayout.WidthIncludingTrailingWhitespace <= title.Bounds.Width+1, "Header elements overlap or title is clipped.");
            string theme = pixel ? "pixel" : "classic";
            void State(string state, int index)
            {
                timers.Focus();
                foreach (var b in buttons) ((IPseudoClasses)b.Classes).Set(":pointerover", false);
                if (state == "hover") ((IPseudoClasses)buttons[index].Classes).Set(":pointerover", true);
                if (state == "focus") Require(buttons[index].Focus(NavigationMethod.Tab), "Keyboard focus failed.");
                ClockOverlayChecks.Pump(55);
                for (int i = 0; i < buttons.Length; i++)
                {
                    var b = buttons[i];
                    var r = At(b);
                    var hit = w.InputHitTest(r.Center) as Visual;
                    var label = b.GetVisualDescendants().OfType<TextBlock>().Single();
                    var plate = b.GetVisualDescendants().OfType<TodoFrame>().Single();
                    var icon = b.GetVisualDescendants().OfType<NikaActionIcon>().Single();
                    string fill = i != index ? "#0D2D3D" : state == "hover" ? "#235466" : "#153B4E";
                    Require(plate.Pixel == pixel && icon.Pixel == pixel && label.Text == Labels[i] &&
                        bounds.Inflate(1).Contains(r) && (hit == b || hit?.GetVisualAncestors().Contains(b) == true) &&
                        label.TextLayout.WidthIncludingTrailingWhitespace <= label.Bounds.Width+1 &&
                        label.TextLayout.TextLines.Count == 1, "Wrong style, hit target or label for " + Labels[i]);
                    Require((plate.Background as ISolidColorBrush)?.Color == Color.Parse(fill), "Wrong navigation fill.");
                    if (state == "focus" && i == index)
                        Require(b.IsFocused && (plate.BorderBrush as ISolidColorBrush)?.Color == Color.Parse("#F1C76D"), "Focus ring missing.");
                }
                string name = index < 0 ? state : state + "-" + Stems[index];
                ControllerSizingChecks.Capture(w, bounds.Inflate(6), $"{theme}-{name}-{scale}", Output);
                Console.WriteLine($"PASS {theme} {scale}% {name}");
            }
            State("idle", -1);
            ControllerSizingChecks.Capture(w, At(header), $"{theme}-header-{scale}", Output);
            for (int i = 0; i < buttons.Length; i++)
            {
                State("hover", i);
                State("focus", i);
            }
            if (scale == 90)
            {
                Type[] targets = [typeof(ProjectDashboardWindow), typeof(SettingsWindow), typeof(ShortcutsWindow)];
                var opened = new List<Window>();
                using var watch = Window.WindowOpenedEvent.AddClassHandler<Window>((window, _) => opened.Add(window));
                for (int i = 0; i < buttons.Length; i++)
                {
                    buttons[i].Focus(NavigationMethod.Tab);
                    buttons[i].RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Avalonia.Input.Key.Enter });
                    ClockOverlayChecks.Pump(80);
                    var target = opened.Single(window => window.IsVisible && window.GetType() == targets[i]);
                    target.Close();
                    ClockOverlayChecks.Pump(70);
                    Require(!target.IsVisible, "Destination did not close.");
                }
            }
            var updateStatus = typeof(ControllerWindow).GetMethod("OnShortcutRegistrationStatusChanged", Hidden)!;
            const string unavailable = "ERROR Could not register shortcuts.";
            updateStatus.Invoke(w, [null, unavailable]);
            ClockOverlayChecks.Pump(45);
            Require(Equals(buttons[2].Content, "Shortcuts unavailable") && Equals(ToolTip.GetTip(buttons[2]), unavailable) &&
                At(tray).Right + 2 <= At(timers).Left, "Navy navigation lost the shortcut registration status or overlapped Timers.");
            updateStatus.Invoke(w, [null, "Shortcuts are available."]);
            ClockOverlayChecks.Pump(45);
            Require(Equals(buttons[2].Content, Labels[2]), "Shortcuts label did not recover.");
        }
        finally { w.Close(); DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown"); }
    }

    private static void Require(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }
}
