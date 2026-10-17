using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using StopwatchOverlay.Platform;

namespace StopwatchOverlay.Desktop.Platform;

/// <summary>Repairs stale native topmost state without raising healthy overlays or taking focus.</summary>
internal sealed class WindowsTopmostGuard
{
    private readonly Window _window;
    private readonly DispatcherTimer _timer;
    private bool _closed;

    internal static void Attach(Window window)
    {
        if (OperatingSystem.IsWindows()) _ = new WindowsTopmostGuard(window);
    }

    private WindowsTopmostGuard(Window window)
    {
        _window = window;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => Refresh();
        window.Opened += OnShown;
        window.Deactivated += OnShown;
        window.PropertyChanged += OnPropertyChanged;
        window.Closed += OnClosed;
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs change)
    {
        if (change.Property != Visual.IsVisibleProperty) return;
        if (_window.IsVisible) OnShown(sender, EventArgs.Empty);
        else _timer.Stop();
    }

    private void OnShown(object? sender, EventArgs e) => Dispatcher.UIThread.Post(() =>
    {
        if (_closed || !_window.IsVisible) return;
        Refresh();
        _timer.Start();
    }, DispatcherPriority.Background);

    private void Refresh()
    {
        if (_closed || !_window.IsVisible) return;
        var handle = _window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        // The Windows service checks TOPMOST first and uses SWP_NOACTIVATE.
        // Never toggle Topmost off: multiple floating windows must coexist.
        PlatformServices.WindowOverlay.SetAlwaysOnTop(handle, true);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _timer.Stop();
        _window.Opened -= OnShown;
        _window.Deactivated -= OnShown;
        _window.PropertyChanged -= OnPropertyChanged;
        _window.Closed -= OnClosed;
    }
}
