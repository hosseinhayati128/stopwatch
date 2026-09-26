using System;
using System.Runtime.InteropServices;

namespace StopwatchOverlay;

/// <summary>
/// Monitors system-wide user inactivity by querying Windows input timing.
/// </summary>
public static class UserIdleDetector
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("user32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    /// <summary>
    /// Test hook allowing deterministic unit tests to simulate idle durations.
    /// When null, native Windows user input timing is queried.
    /// </summary>
    public static Func<TimeSpan>? CustomIdleTimeProvider { get; set; }

    /// <summary>
    /// Gets the duration of continuous user inactivity across keyboard and mouse.
    /// </summary>
    public static TimeSpan GetIdleTime()
    {
        if (CustomIdleTimeProvider != null)
        {
            return CustomIdleTimeProvider();
        }

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
            CrashLogger.LogRecoverable(ex, "UserIdleDetector.GetIdleTime");
        }

        return TimeSpan.Zero;
    }
}
