using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.VisualTree;
using StopwatchOverlay;
using StopwatchOverlay.Desktop;
using StopwatchOverlay.Desktop.Views;
using StopwatchOverlay.Desktop.Controls;

internal static class ThemeAndBoundsChecks
{
    internal static void TodoAddAlignment()
    {
        var applyTheme = typeof(App).Assembly.GetType("StopwatchOverlay.Desktop.ThemeResources")!
            .GetMethod("ApplyApplication", BindingFlags.Static | BindingFlags.NonPublic)!;
        foreach (var theme in new[] { AppThemeCatalog.Pirate, AppThemeCatalog.Daylight, AppThemeCatalog.Midnight })
        {
            applyTheme.Invoke(null, new object[] { theme });
            string vault = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "stopwatch-layout-" + Guid.NewGuid().ToString("N"));
            ObsidianNotesSync.AppendEntry(vault, NoteType.Todo, "First visible task", timestamp: new DateTime(2026, 10, 18, 10, 0, 0));
            ObsidianNotesSync.AppendEntry(vault, NoteType.Todo, "Second visible task", timestamp: new DateTime(2026, 10, 18, 10, 1, 0));
            var window = new TodoOverlayWindow(new AppSettings { ThemeMode = theme, ObsidianVaultFolder = vault });
            try
            {
                window.Show(); window.ToggleExpanded();
                var button = window.FindControl<Button>("AddTodoButton")!;
                var row = (Panel)button.Content!;
                var icon = row.Children.OfType<Avalonia.Controls.Shapes.Path>().Single();
                var label = row.Children.OfType<TextBlock>().Single();
                foreach (double fontSize in new[] { 14d, 20d, 28d })
                {
                    button.FontSize = fontSize;
                    ClockOverlayChecks.Pump(150); window.UpdateLayout();
                    for (int pass = 0; pass < 2; pass++)
                    {
                        var completed = window.FindControl<CheckBox>("ShowCompletedToggle")!;
                        if (completed.Bounds.Width < 70 || completed.Bounds.Height > 40)
                            throw new Exception($"{theme}: Show completed is constrained to {completed.Bounds.Size} after expansion.");
                        var completedLabel = completed.GetVisualDescendants().OfType<TextBlock>().First(t => t.Text == "Show completed");
                        if (completedLabel.Bounds.Width < completedLabel.TextLayout.Width - 1 || completedLabel.Bounds.Height > 30)
                            throw new Exception($"{theme}: Show completed text is clipped: {completedLabel.Bounds.Size}, text width {completedLabel.TextLayout.Width}.");
                        var tasks = window.FindControl<StackPanel>("TodoRows")!.GetVisualDescendants().OfType<HoldToCompleteCheckBox>().ToArray();
                        if (tasks.Length != 2 || tasks.Any(task => task.Content is not TextBlock text || text.Bounds.Width < 50 || text.TextLayout.Width < 50))
                            throw new Exception(theme + ": Existing task labels are missing or clipped after expansion.");
                        if (theme == AppThemeCatalog.Pirate && fontSize == 14 && pass == 0 &&
                            Environment.GetEnvironmentVariable("STOPWATCH_LAYOUT_SNAPSHOT") is string snapshotPath)
                        {
                            using var bitmap = new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(600, 750), new Vector(144, 144));
                            bitmap.Render(window); bitmap.Save(snapshotPath);
                        }
                        var iconOrigin = icon.TranslatePoint(default, button)!.Value;
                        var labelOrigin = label.TranslatePoint(default, button)!.Value;
                        double labelCenter = labelOrigin.Y + label.Bounds.Height / 2;
                        if (Math.Abs(iconOrigin.Y + icon.Bounds.Height / 2 - labelCenter) > 1 ||
                            Math.Abs(labelCenter - button.Bounds.Height / 2) > 1)
                            throw new Exception($"{theme}, font {fontSize}: Add todo is not vertically centered.");
                        double left = Math.Min(iconOrigin.X, labelOrigin.X);
                        double right = Math.Max(iconOrigin.X + icon.Bounds.Width, labelOrigin.X + label.Bounds.Width);
                        if (Math.Abs((left + right - button.Bounds.Width) / 2) > 1)
                            throw new Exception($"{theme}, font {fontSize}: Add todo is not horizontally centered.");
                        window.ToggleExpanded(); ClockOverlayChecks.Pump(50);
                        window.ToggleExpanded(); ClockOverlayChecks.Pump(150); window.UpdateLayout();
                    }
                }
                bool requested = false;
                window.AddTodoRequested += () => requested = true;
                button.RaiseEvent(new Avalonia.Interactivity.RoutedEventArgs(Button.ClickEvent));
                if (!requested) throw new Exception("Centered Add todo button no longer opens the entry flow.");
            }
            finally { window.Close(); System.IO.Directory.Delete(vault, recursive: true); }
        }
    }

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
