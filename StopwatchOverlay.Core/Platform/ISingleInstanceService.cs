using System;

namespace StopwatchOverlay.Platform;

/// <summary>
/// Ensures only one instance of the application runs per user session and handles signaling existing instances.
/// </summary>
public interface ISingleInstanceService : IDisposable
{
    /// <summary>
    /// Attempts to acquire the single-instance lock.
    /// Returns true if this is the first/only instance, false if another instance is already running.
    /// </summary>
    /// <param name="onShowExistingRequested">Callback invoked on this primary instance when a second instance starts and requests the window be shown.</param>
    bool TryAcquireSingleInstance(Action onShowExistingRequested);

    /// <summary>
    /// Signals the already-running primary instance to display itself.
    /// </summary>
    void SignalExistingInstance();
}
