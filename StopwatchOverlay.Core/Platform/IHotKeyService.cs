using System;

namespace StopwatchOverlay.Platform;

/// <summary>
/// Manages system-wide global hotkeys and modal chord sequences.
/// </summary>
public interface IHotKeyService : IDisposable
{
    /// <summary>
    /// Registers a global hotkey for the specified action.
    /// </summary>
    bool RegisterHotKey(ShortcutAction action, uint modifiers, uint virtualKey);

    /// <summary>
    /// Unregisters the global hotkey for the specified action.
    /// </summary>
    void UnregisterHotKey(ShortcutAction action);

    /// <summary>
    /// Unregisters all registered global hotkeys.
    /// </summary>
    void UnregisterAll();

    /// <summary>
    /// Event fired when a registered global hotkey is pressed.
    /// </summary>
    event EventHandler<ShortcutAction>? HotKeyPressed;
}
