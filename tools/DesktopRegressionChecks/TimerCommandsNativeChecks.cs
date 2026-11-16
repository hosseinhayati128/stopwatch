using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Views;

internal static class TimerCommandsNativeChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    const string Output = "design/one-piece-v2/shortcuts/timer-commands-implemented";
    internal static void Run()
    {
        Directory.CreateDirectory(Output);
        foreach (int percent in new[] { 70, 90, 125 }) Render(percent, false, AppThemeCatalog.Nika);
        Render(90, true, AppThemeCatalog.Nika);
        Render(90, false, AppThemeCatalog.Midnight);
        Render(90, false, AppThemeCatalog.Daylight);
    }

    static void Render(int percent, bool custom, string theme)
    {
        var settings = new AppSettings { ThemeMode = theme, UiScalePercent = percent };
        if (custom)
        {
            settings.CommandModeKeys[ShortcutAction.StartStop] = 0x78;
            settings.CommandModeKeys[ShortcutAction.ToggleClock] = 0x79;
            settings.CommandModeKeys[ShortcutAction.Reset] = 0;
            settings.LeaderShortcut = new Shortcut(Shortcut.MOD_CONTROL, 0x77);
            settings.Typography.Global.SizePercent = 125;
        }
        Desktop("ThemeResources", "ApplyApplication", theme);
        Desktop("DesktopPageAppearance", "Initialize", settings);
        Desktop("DesktopTypography", "Initialize", settings.Typography);
        var w = (Window)Activator.CreateInstance(typeof(ShortcutCommandHintWindow), Hidden, null,
            [settings.CommandModeKeys, settings, custom ? "پروژه برنامه نویسی — a very long project name for the compact popup" : "Programming"], null)!;
        w.ShowActivated = false;
        string suffix = custom ? "custom" : theme == AppThemeCatalog.Nika ? percent.ToString() : theme.ToLowerInvariant();
        try
        {
            w.Show(); ClockOverlayChecks.Pump(100);
            Capture("quick-" + suffix); CheckLayout(false);
            int top = w.Position.Y;
            Require(w.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "3 more · All commands"), "Initial footer count is wrong.");
            for (int press = 2; press <= 4; press++)
            {
                bool expanded = (bool)typeof(ShortcutCommandHintWindow).GetMethod("AdvanceExpansion", Hidden)!.Invoke(w, null)!;
                ClockOverlayChecks.Pump(75);
                Require(expanded == (press == 4), "Full list must appear on the fourth total press.");
                Require(w.Position.Y == top, "Expansion moved the top edge.");
                CheckLayout(expanded);
                if (!expanded)
                {
                    Require(w.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == $"{4 - press} more · All commands"), "Remaining-press footer is stale.");
                    if (percent == 90 && theme == AppThemeCatalog.Nika && !custom) Capture($"quick-press{press}-90");
                }
            }
            Capture("expanded-" + suffix);
            Console.WriteLine($"PASS actual Timer Commands {suffix}: quick/full coverage, layout, screen bounds and capture");
        }
        finally
        {
            w.Close();
            Desktop("DesktopTypography", "Shutdown");
            Desktop("DesktopPageAppearance", "Shutdown");
        }

        Border Frame() => w.GetVisualDescendants().OfType<Border>().Single(c => c.Name == "TimerCommandFrame");
        void CheckLayout(bool expanded)
        {
            var frame = Frame();
            var rows = w.GetVisualDescendants().OfType<Grid>().Where(c => c.Name?.StartsWith("TimerCommandRow") == true).ToArray();
            Require(rows.Length == (expanded ? 21 : 6) - (custom ? 1 : 0), "Assigned command coverage changed.");
            if (!expanded)
            {
                Require(rows.Any(row => row.Name == "TimerCommandRowToggleClock") && rows.All(row => row.Name != "TimerCommandRowEditTimer"),
                    "Compact hint must use Clock mode instead of Edit timer.");
                var clockRow = rows.Single(row => row.Name == "TimerCommandRowToggleClock");
                Require(clockRow.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == (custom ? "F10" : "C")), "Clock hint ignores its mapping.");
            }
            Require(frame.TranslatePoint(default, w) is { } origin && Math.Abs(origin.X) < 1 && Math.Abs(origin.Y) < 1,
                "Frame is offset inside the native window.");
            Require(Math.Abs(w.ClientSize.Width - frame.Bounds.Width * percent / 100d) <= 1 &&
                Math.Abs(w.ClientSize.Height - frame.Bounds.Height * percent / 100d) <= 1, "Window and panel sizes disagree.");
            var scroll = w.GetVisualDescendants().OfType<ScrollViewer>().Single();
            Require(scroll.Extent.Height <= scroll.Viewport.Height + 1, $"Default desktop unnecessarily clips command rows: extent {scroll.Extent}, viewport {scroll.Viewport}, frame {frame.Bounds}, client {w.ClientSize}, scale {percent}, expanded {expanded}.");
            foreach (var row in rows)
            {
                Require(row.Bounds.Width > 150 && row.Bounds.Height >= row.DesiredSize.Height - 1, "Command row clipped.");
                var corner = row.TranslatePoint(new Point(row.Bounds.Width, row.Bounds.Height), frame)!.Value;
                Require(corner.X <= frame.Bounds.Width - 12 && corner.Y <= frame.Bounds.Height - 24, "Command exceeds the frame/footer.");
            }
            var screen = w.Screens.ScreenFromVisual(w)!;
            var area = screen.WorkingArea;
            Require(w.Position.X >= area.X && w.Position.Y >= area.Y &&
                w.Position.X + w.ClientSize.Width * screen.Scaling <= area.Right + 1 &&
                w.Position.Y + w.ClientSize.Height * screen.Scaling <= area.Bottom + 1, "Popup exceeds the working area.");
            if (custom) Require(w.GetVisualDescendants().OfType<TextBlock>().Any(t => t.Text == "Ctrl + F8"), "Configured leader missing.");
        }
        void Capture(string name)
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize((int)Math.Ceiling(w.ClientSize.Width), (int)Math.Ceiling(w.ClientSize.Height)), new Vector(96,96));
            if (percent >= 100) bitmap.Render(w);
            else
            {
                var surface = Frame();
                using var context = bitmap.CreateDrawingContext();
                context.DrawRectangle(new VisualBrush(surface) { Stretch = Stretch.Fill, SourceRect = new RelativeRect(surface.Bounds, RelativeUnit.Absolute) }, null, new Rect(surface.Bounds.Size));
            }
            bitmap.Save(Path.Combine(Output, name + ".png"));
        }
    }

    static void Desktop(string type, string method, params object[] args) => typeof(ControllerWindow).Assembly
        .GetType("StopwatchOverlay.Desktop." + type)!.GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, args);
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
