using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using StopwatchOverlay.Platform;

namespace StopwatchOverlay.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsOverlayService : IWindowOverlayService
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TRANSPARENT = 0x00000020;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_NOACTIVATE = 0x08000000;

    private static readonly IntPtr HWND_TOPMOST = new(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
    private static extern int GetWindowLong32(IntPtr hwnd, int index);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern int SetWindowLong32(IntPtr hwnd, int index, int newStyle);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

    public void SetClickThrough(IntPtr windowHandle, bool clickThrough)
    {
        if (windowHandle == IntPtr.Zero) return;
        try
        {
            int extendedStyle = GetWindowLong32(windowHandle, GWL_EXSTYLE);
            extendedStyle = clickThrough
                ? extendedStyle | WS_EX_TRANSPARENT
                : extendedStyle & ~WS_EX_TRANSPARENT;
            SetWindowLong32(windowHandle, GWL_EXSTYLE, extendedStyle);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "WindowsOverlayService.SetClickThrough");
        }
    }

    public void SetAlwaysOnTop(IntPtr windowHandle, bool topmost)
    {
        if (windowHandle == IntPtr.Zero) return;
        try
        {
            SetWindowPos(windowHandle, topmost ? HWND_TOPMOST : new IntPtr(-2), 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "WindowsOverlayService.SetAlwaysOnTop");
        }
    }

    public void SetNoActivateToolWindow(IntPtr windowHandle)
    {
        if (windowHandle == IntPtr.Zero) return;
        try
        {
            int extendedStyle = GetWindowLong32(windowHandle, GWL_EXSTYLE);
            extendedStyle |= WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW;
            SetWindowLong32(windowHandle, GWL_EXSTYLE, extendedStyle);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "WindowsOverlayService.SetNoActivateToolWindow");
        }
    }

    private const uint WDA_NONE = 0x00000000;
    private const uint WDA_EXCLUDEFROMCAPTURE = 0x00000011;

    [DllImport("user32.dll")]
    private static extern bool SetWindowDisplayAffinity(IntPtr hWnd, uint dwAffinity);

    public void SetCaptureAffinity(IntPtr windowHandle, bool excludeFromCapture)
    {
        if (windowHandle == IntPtr.Zero) return;
        try
        {
            SetWindowDisplayAffinity(windowHandle, excludeFromCapture ? WDA_EXCLUDEFROMCAPTURE : WDA_NONE);
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "WindowsOverlayService.SetCaptureAffinity");
        }
    }
}
