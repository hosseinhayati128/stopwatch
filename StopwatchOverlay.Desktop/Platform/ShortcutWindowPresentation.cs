using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace StopwatchOverlay.Desktop.Platform;

internal static class ShortcutWindowPresentation
{
    private const int Topmost = -1, NotTopmost = -2;
    private const uint NoSize = 0x0001, NoMove = 0x0002, NoActivate = 0x0010, NoOwnerZOrder = 0x0200;
    private const uint RaiseFlags = NoSize | NoMove | NoActivate | NoOwnerZOrder;
    private static readonly ConditionalWeakTable<Window, TemporaryRaise> RaisedWindows = new();

    internal static void BringForward(Window window, bool activate = true)
    {
        // A modal owner cannot take focus. Present its visible child instead.
        while (true)
        {
            Window? child = null;
            foreach (var owned in window.OwnedWindows)
                if (owned.IsVisible) child = owned;
            if (child == null) break;
            window = child;
        }

        if (!window.IsVisible) return;
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        if (activate) window.Activate();
        if (!OperatingSystem.IsWindows() || window.TryGetPlatformHandle()?.Handle is not { } handle || handle == 0)
            return;

        // Windows can refuse foreground activation when another process has the input queue.
        // Raise explicitly so the user can see and click the requested window in that case.
        if (SetWindowPos(handle, Topmost, 0, 0, 0, 0, RaiseFlags) && !window.Topmost)
            RaisedWindows.GetValue(window, static target => new TemporaryRaise(target)).WaitForActivation(handle);
    }

    private sealed class TemporaryRaise
    {
        private readonly Window _window;
        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(4) };

        internal TemporaryRaise(Window window)
        {
            _window = window;
            _timer.Tick += Release;
            window.Activated += OnActivated;
            window.Deactivated += Release;
            window.Closed += Release;
            window.PropertyChanged += WindowChanged;
        }

        internal void WaitForActivation(nint handle)
        {
            _timer.Stop();
            // Give the user time to click if Windows declined activation. Once focused,
            // keep the window in front until they switch away or dismiss it.
            if (GetForegroundWindow() != handle) _timer.Start();
        }

        private void OnActivated(object? sender, EventArgs e) => _timer.Stop();

        private void WindowChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if ((e.Property == Visual.IsVisibleProperty && !_window.IsVisible)
                || (e.Property == Window.WindowStateProperty && _window.WindowState == WindowState.Minimized))
                Release(sender, EventArgs.Empty);
        }

        private void Release(object? sender, EventArgs e)
        {
            _timer.Stop();
            _window.Activated -= OnActivated;
            _window.Deactivated -= Release;
            _window.Closed -= Release;
            _window.PropertyChanged -= WindowChanged;
            RaisedWindows.Remove(_window);
            if (!_window.Topmost && _window.TryGetPlatformHandle()?.Handle is { } handle && handle != 0)
                SetWindowPos(handle, NotTopmost, 0, 0, 0, 0, RaiseFlags);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(nint handle, nint insertAfter, int x, int y, int width, int height, uint flags);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
}
