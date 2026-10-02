using System;

namespace StopwatchOverlay.Platform;

/// <summary>
/// Provides cross-platform user idle / inactivity detection.
/// </summary>
public interface IIdleDetectionService
{
    /// <summary>
    /// Gets the duration of continuous user inactivity across keyboard and mouse.
    /// </summary>
    TimeSpan GetIdleTime();

    /// <summary>
    /// Determines whether the user has been idle for at least the specified threshold.
    /// </summary>
    bool IsUserIdle(TimeSpan threshold) => GetIdleTime() >= threshold;
}
