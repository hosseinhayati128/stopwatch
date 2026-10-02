namespace StopwatchOverlay.Platform;

/// <summary>
/// Manages system sign-in autostart registration (Windows Run key, Linux .desktop autostart).
/// </summary>
public interface IStartupService
{
    /// <summary>
    /// Gets whether autostart registration is supported on the current platform.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>
    /// Checks whether autostart is currently enabled for this application.
    /// </summary>
    bool IsEnabled();

    /// <summary>
    /// Enables or disables autostart for the current user.
    /// </summary>
    void SetEnabled(bool enabled);
}
