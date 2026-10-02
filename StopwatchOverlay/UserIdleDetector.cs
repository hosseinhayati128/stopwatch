using System;
using System.Runtime.InteropServices;

namespace StopwatchOverlay;

/// <summary>
/// Monitors system-wide user inactivity by querying Windows input timing.
/// </summary>
public static class UserIdleDetector
{

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

        return Platform.PlatformServices.IdleDetection.GetIdleTime();
    }
}
