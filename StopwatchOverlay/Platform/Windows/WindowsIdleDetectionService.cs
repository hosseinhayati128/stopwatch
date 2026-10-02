using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using StopwatchOverlay.Platform;

namespace StopwatchOverlay.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsIdleDetectionService : IIdleDetectionService
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    public TimeSpan GetIdleTime()
    {
        try
        {
            var lii = new LASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<LASTINPUTINFO>() };
            if (GetLastInputInfo(ref lii))
            {
                uint currentTick = (uint)Environment.TickCount;
                uint idleTicks = unchecked(currentTick - lii.dwTime);
                return TimeSpan.FromMilliseconds(idleTicks);
            }
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "WindowsIdleDetectionService.GetIdleTime");
        }

        return TimeSpan.Zero;
    }
}
