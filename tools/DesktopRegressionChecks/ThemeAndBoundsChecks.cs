using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Views;

internal static class ThemeAndBoundsChecks
{
    internal static void CloseDialogTheme()
    {
        typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!
            .GetMethod("ApplyApplication", BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, new object[] { AppThemeCatalog.Pirate });
        var dialog = new CloseActionDialogWindow { ShowActivated = false };
        dialog.Classes.Add("pirate");
        try
        {
            dialog.Show(); ClockOverlayChecks.Pump(150);
            foreach (string name in new[] { "MinimizeButton", "ExitButton" })
            {
                var button = dialog.FindControl<Button>(name)!;
                button.ApplyTemplate();
                if (!button.GetVisualDescendants().OfType<Border>().Any(border => border.Name == "Plate"))
                    throw new Exception(name + " does not use the One Piece brass button template.");
                if (button.Bounds.Right > dialog.ClientSize.Width) throw new Exception(name + " overflows the dialog.");
            }
        }
        finally { dialog.Close(); }
    }

    internal static void ClockBounds()
    {
        var overlay = new OverlayWindow();
        try
        {
            overlay.Show(); ClockOverlayChecks.Pump(150);
            var right = overlay.Screens.All.OrderBy(screen => screen.Bounds.Right).Last();
            overlay.Position = new PixelPoint(right.WorkingArea.Right + 100, right.WorkingArea.Y + 60);
            ClockOverlayChecks.Pump(450);
            if (overlay.Position.X + Math.Ceiling(overlay.Bounds.Width * right.Scaling) > right.WorkingArea.Right)
                throw new Exception("Clock can remain beyond the right screen edge.");
            var left = overlay.Screens.All.OrderBy(screen => screen.Bounds.X).First();
            overlay.Position = new PixelPoint(left.WorkingArea.X - 100, left.WorkingArea.Y + 60);
            ClockOverlayChecks.Pump(450);
            if (overlay.Position.X < left.WorkingArea.X) throw new Exception("Clock can remain beyond the left screen edge.");
        }
        finally { overlay.Close(); }
    }

    internal static void MonitorBounds()
    {
        var clamp = typeof(TodoOverlayWindow).GetMethod("ClampToScreens", BindingFlags.NonPublic | BindingFlags.Static)!;
        var primary = new Screen(1, new PixelRect(0, 0, 1920, 1080), new PixelRect(0, 32, 1920, 1048), true);
        var left = new Screen(1, new PixelRect(-1280, 0, 1280, 1024), new PixelRect(-1280, 32, 1280, 992), false);
        var right = new Screen(1.5, new PixelRect(1920, 0, 1920, 1080), new PixelRect(1920, 32, 1920, 1048), false);
        var screens = new[] { primary, left, right };
        var cases = new[] {
            (new PixelPoint(1920, 0), new PixelPoint(1920, 32)),
            (new PixelPoint(3752, 0), new PixelPoint(3300, 32)),
            (new PixelPoint(-1280, 0), new PixelPoint(-1280, 32)),
            (new PixelPoint(-1500, 80), new PixelPoint(-1280, 80)),
            (new PixelPoint(4200, 80), new PixelPoint(3300, 80)),
            (new PixelPoint(100, 1000), new PixelPoint(100, 650))
        };
        foreach (var (position, expected) in cases)
        {
            var result = (PixelPoint)clamp.Invoke(null, new object[] { position, new Size(360, 430), screens })!;
            if (result != expected) throw new Exception($"Monitor clamp for {position} returned {result}, expected {expected}.");
        }
    }
}
