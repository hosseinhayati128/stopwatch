using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using static ControllerSizingChecks;

using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Controls;
using StopwatchOverlay.Desktop.Views;

internal static class ControllerLapTimesChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Output = "design/one-piece-v2/controller-lap-times/implemented-native";

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
        var window = (ControllerWindow)typeof(ControllerWindow).GetConstructor(Hidden, null, [typeof(AppSettings)], null)!.Invoke([settings]);
        try
        {
            window.ShowActivated = window.ShowInTaskbar = false;
            window.Show();
            var timer = (TimerSession)typeof(ControllerWindow).GetProperty("CurrentTimer", Hidden)!.GetValue(window)!;
            var table = window.FindControl<ItemsControl>("NikaLapTable")!;
            var card = window.FindControl<TodoFrame>("ControllerLapCard")!;
            var columns = window.FindControl<Grid>("LapColumns")!;
            var empty = window.FindControl<StackPanel>("LapEmptyState")!;
            void Press(string name) => window.FindControl<Button>(name)!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Press("ResetButton");
            int total = 0;
            for (int count = 0; count <= 24; count++)
            {
                if (count > 0)
                {
                    total += count switch { 1 => 1303, 2 => 1306, 3 => 1302, _ => 2868 + (count - 1) * 97 % 670 };
                    timer.RestoreElapsed(TimeSpan.FromMilliseconds(total * 100L), false);
                    Press("LapButton");
                }
                if (count != 0 && count != 3 && count != 24) continue;
                ClockOverlayChecks.Pump(65);
                Need(card.Pixel == pixel && table.ItemCount == Math.Min(count,4) && empty.IsEffectivelyVisible == (count == 0) &&
                    columns.IsEffectivelyVisible == (count > 0), "Incorrect lap state or theme.");
                if (count > 0)
                {
                    var scroll = window.FindControl<ScrollViewer>("LapScroll")!;
                    var first = (NikaLapRow)table.Items[0]!;
                    Need(first.IsNewest && (count == 3 ? first.Split == "00:02:10.2" && first.Total == "00:06:31.1" :
                        first.Total == "01:57:32.0"), "Add lap lost fractional time.");
                    var rows = table.GetVisualDescendants().OfType<TodoFrame>().Where(r => r.Classes.Contains("lapRow")).ToList();
                    var digits = rows[0].GetVisualDescendants().OfType<ControllerPixelDigits>().Count(d => d.IsEffectivelyVisible);
                    Need(digits == (pixel ? 2 : 0), "Wrong digit style.");
                    var values = rows[0].GetVisualDescendants().OfType<Grid>().Where(g => g.Classes.Contains("lapValue")).ToArray();
                    for (int index = 0; index < 2; index++)
                        Need(Math.Abs(Bounds(columns.Children[index+1],window).Right - Bounds(values[index],window).Right) <= 1.5, "Header and values misaligned.");
                    if(count == 24)
                    {
                        double y = Bounds(columns,window).Top;
                        scroll.ScrollToEnd(); ClockOverlayChecks.Pump(70);
                        Need(table.GetVisualDescendants().OfType<TodoFrame>().Any(c => c.DataContext is NikaLapRow { Number: "01" }), "Earliest lap unreachable.");
                        scroll.Offset = new Vector(0,210); ClockOverlayChecks.Pump(70);
                        var oldItems = table.ItemsSource;
                        typeof(ControllerWindow).GetMethod("UpdateActiveTimerDisplay",Hidden)!.Invoke(window,null);
                        Need(Math.Abs(scroll.Offset.Y - 210) < 1 && Bounds(columns,window).Top == y && ReferenceEquals(oldItems,table.ItemsSource),
                            "Timer refresh moved the lap history or its header.");
                    }
                }
                string state = count == 0 ? "empty" : count == 3 ? "three-laps" : "long-session";
                string name = $"{(pixel ? "pixel" : "classic")}-{state}-{scale}";
                var area = Bounds(card,window);
                Need(new Rect(window.ClientSize).Inflate(1).Contains(area),"Lap card outside controller.");
                Capture(window,area.Inflate(5),name,Output);
                if(count == 3) Capture(window,new Rect(window.ClientSize),name+"-controller",Output);
            }
            var rail = window.FindControl<ListBox>("TimerRailList")!;
            rail.SelectedIndex = 0; ClockOverlayChecks.Pump(45);
            Need(empty.IsEffectivelyVisible, "Switching timers retained the previous laps.");
            rail.SelectedIndex = 2; ClockOverlayChecks.Pump(45);
            Need(table.ItemCount == 4 && window.FindControl<ScrollViewer>("LapScroll")!.Offset.Y == 0, "Restored timer did not start at its newest lap.");
            timer.LapTimes[0] = "Lap 24: unknown";
            typeof(ControllerWindow).GetMethod("UpdateActiveTimerDisplay",Hidden)!.Invoke(window,null);
            ClockOverlayChecks.Pump(45);
            Need(table.GetVisualDescendants().OfType<TextBlock>().Any(t => t.IsEffectivelyVisible && t.Text == "Lap 24: unknown"), "Unreadable legacy lap.");
            Press("ResetButton"); ClockOverlayChecks.Pump(30);
            Need(empty.IsEffectivelyVisible && !columns.IsEffectivelyVisible, "Reset did not restore the empty panel.");
            Console.WriteLine($"PASS {(pixel ? "Pixel" : "Classic")} {scale}%: Add lap, empty, fractional splits, 24-lap scrolling, switch, legacy text, reset.");
        }
        finally { window.Close(); DeleteTodoNativeChecks.Desktop("DesktopPageAppearance", "Shutdown"); }
    }

    private static void Need(bool ok, string message) { if(!ok) throw new InvalidOperationException(message); }
}
