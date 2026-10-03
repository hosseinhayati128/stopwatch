using System;

namespace StopwatchOverlay.Platform;

/// <summary>
/// Provides OS-level window styling for transparent floating overlays, click-through, and tool window semantics.
/// </summary>
public interface IWindowOverlayService
{
    /// <summary>
    /// Configures whether mouse and touch input passes through the window to windows beneath it.
    /// </summary>
    void SetClickThrough(IntPtr windowHandle, bool clickThrough);

    /// <summary>
    /// Configures whether the window stays on top of normal windows.
    /// </summary>
    void SetAlwaysOnTop(IntPtr windowHandle, bool topmost);

    /// <summary>
    /// Configures the window to not steal focus on activation and hide from task switchers (Alt+Tab).
    /// </summary>
    void SetNoActivateToolWindow(IntPtr windowHandle);

    /// <summary>
    /// Configures whether the window is excluded from screen capture (video recording / screenshots).
    /// </summary>
    void SetCaptureAffinity(IntPtr windowHandle, bool excludeFromCapture);
}
