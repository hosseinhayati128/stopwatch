using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace StopwatchOverlay.Desktop.Platform;

/// <summary>Keeps floating windows in a monitor's usable area after moving or resizing.</summary>
internal sealed class OverlayScreenConstraint
{
    private readonly Window _window;
    private readonly DispatcherTimer _timer;
    private bool _closed;
    private bool _applying;

    internal static void Attach(Window window) => _ = new OverlayScreenConstraint(window);

    private OverlayScreenConstraint(Window window)
    {
        _window = window;
        // Debounce movement so crossing to another monitor is not blocked at
        // the old monitor's edge. Also handle native asynchronous Linux drags.
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
        _timer.Tick += (_, _) => Constrain();
        window.PositionChanged += (_, _) => Schedule();
        window.SizeChanged += (_, _) => Schedule();
        window.Opened += (_, _) => Schedule();
        window.PropertyChanged += (_, change) =>
        {
            if (change.Property == Visual.IsVisibleProperty && window.IsVisible) Schedule();
        };
        window.Closed += (_, _) => { _closed = true; _timer.Stop(); };
    }

    private void Schedule()
    {
        if (_closed || _applying) return;
        _timer.Stop();
        _timer.Start();
    }

    private void Constrain()
    {
        _timer.Stop();
        if (_closed || !_window.IsVisible) return;
        var position = ClampToScreens(_window.Position, _window.Bounds.Size, _window.Screens.All);
        if (position == _window.Position) return;
        _applying = true;
        try { _window.Position = position; }
        finally { _applying = false; }
    }

    internal static PixelPoint ClampToScreens(PixelPoint anchor, Size logicalSize, IReadOnlyList<Screen> screens)
    {
        // Select by full monitor bounds first, retaining taskbar/top-panel and
        // negative-coordinate monitor behavior of the existing todo overlay.
        var screen = screens.FirstOrDefault(screen => anchor.X >= screen.Bounds.X && anchor.X < screen.Bounds.Right &&
            anchor.Y >= screen.Bounds.Y && anchor.Y < screen.Bounds.Bottom) ??
            screens.OrderBy(screen => DistanceToScreen(anchor, screen.Bounds)).FirstOrDefault();
        if (screen == null) return anchor;
        var area = screen.WorkingArea;
        int width = (int)Math.Ceiling(logicalSize.Width * screen.Scaling);
        int height = (int)Math.Ceiling(logicalSize.Height * screen.Scaling);
        return new PixelPoint(Math.Clamp(anchor.X, area.X, Math.Max(area.X, area.Right - width)),
                              Math.Clamp(anchor.Y, area.Y, Math.Max(area.Y, area.Bottom - height)));
    }

    private static double DistanceToScreen(PixelPoint anchor, PixelRect bounds)
    {
        double dx = (double)anchor.X - Math.Clamp(anchor.X, bounds.X, bounds.Right);
        double dy = (double)anchor.Y - Math.Clamp(anchor.Y, bounds.Y, bounds.Bottom);
        return dx * dx + dy * dy;
    }
}
