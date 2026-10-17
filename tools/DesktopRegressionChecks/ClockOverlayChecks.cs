using System.Runtime.InteropServices;
using Avalonia.Threading;
using Avalonia.Controls;
using StopwatchOverlay;
using StopwatchOverlay.Desktop.Views;

internal static class ClockOverlayChecks
{
    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong(IntPtr handle, int index);
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);

    internal static void Run()
    {
        var overlay = new OverlayWindow();
        IntPtr foreground = OperatingSystem.IsWindows() ? GetForegroundWindow() : IntPtr.Zero;
        try
        {
            overlay.Show(); Pump(150);
            AssertTopmost(overlay, "first show");
            overlay.Hide(); overlay.Show(); Pump(150);
            AssertTopmost(overlay, "hide/show");
            if (OperatingSystem.IsWindows())
            {
                IntPtr handle = overlay.TryGetPlatformHandle()!.Handle;
                // Simulate native chrome/z-order updates demoting this test window.
                if (!SetWindowPos(handle, new IntPtr(-2), 0, 0, 0, 0, 0x13)) throw new Exception("Could not simulate native demotion.");
                Pump(1300);
                AssertTopmost(overlay, "native demotion");
                if (GetForegroundWindow() != foreground) throw new Exception("Clock stole foreground focus.");
            }
        }
        finally { overlay.Close(); }
    }

    internal static void RunTogether()
    {
        var clock = new OverlayWindow();
        var todos = new TodoOverlayWindow(new AppSettings());
        var notes = new NotesViewerWindow(new AppSettings());
        try
        {
            clock.Show(); todos.Show(); todos.ToggleExpanded(); notes.Show(); Pump(200);
            IntPtr foreground = OperatingSystem.IsWindows() ? GetForegroundWindow() : IntPtr.Zero;
            if (OperatingSystem.IsWindows())
                foreach (var window in new Window[] { clock, todos, notes })
                    SetWindowPos(window.TryGetPlatformHandle()!.Handle, new IntPtr(-2), 0, 0, 0, 0, 0x13);
            Pump(1300);
            foreach (var window in new Window[] { clock, todos, notes })
                AssertFloating(window, "all overlays recover together");
            if (OperatingSystem.IsWindows() && GetForegroundWindow() != foreground)
                throw new Exception("Topmost recovery stole focus from the notes window.");
            todos.Hide(); todos.Show(); todos.ToggleExpanded(); Pump(1300);
            AssertFloating(todos, "todo hide/show and collapse");
            AssertFloating(clock, "clock while notes are visible");
        }
        finally { notes.Close(); todos.Close(); clock.Close(); }
    }

    private static void AssertFloating(Window window, string scenario)
    {
        if (!window.Topmost || (OperatingSystem.IsWindows() && (GetWindowLong(window.TryGetPlatformHandle()!.Handle, -20) & 8) == 0))
            throw new Exception(window.Title + " is not topmost: " + scenario);
    }

    private static void AssertTopmost(OverlayWindow overlay, string scenario)
    {
        if (!overlay.Topmost) throw new Exception("Managed Topmost was cleared after " + scenario);
        if (!OperatingSystem.IsWindows()) return;
        int style = GetWindowLong(overlay.TryGetPlatformHandle()!.Handle, -20);
        if ((style & 8) == 0) throw new Exception("Native TOPMOST is missing after " + scenario);
        if ((style & 0x08000000) == 0) throw new Exception("Native NOACTIVATE is missing after " + scenario);
    }

    internal static void Pump(int milliseconds)
    {
        using var cancel = new CancellationTokenSource();
        DispatcherTimer.RunOnce(cancel.Cancel, TimeSpan.FromMilliseconds(milliseconds));
        Dispatcher.UIThread.MainLoop(cancel.Token);
    }
}
