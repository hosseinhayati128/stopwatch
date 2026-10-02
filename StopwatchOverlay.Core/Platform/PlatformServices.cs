using System;

namespace StopwatchOverlay.Platform;

/// <summary>
/// Provides access to the registered platform-specific services (hotkeys, idle detection, overlay windows, autostart).
/// </summary>
public static class PlatformServices
{
    private static IIdleDetectionService _idleDetection = new NullIdleDetectionService();
    private static IStartupService _startup = new NullStartupService();
    private static IWindowOverlayService _windowOverlay = new NullWindowOverlayService();
    private static IHotKeyService _hotKey = new NullHotKeyService();
    private static ISingleInstanceService _singleInstance = new NullSingleInstanceService();

    public static IIdleDetectionService IdleDetection
    {
        get => _idleDetection;
        set => _idleDetection = value ?? new NullIdleDetectionService();
    }

    public static IStartupService Startup
    {
        get => _startup;
        set => _startup = value ?? new NullStartupService();
    }

    public static IWindowOverlayService WindowOverlay
    {
        get => _windowOverlay;
        set => _windowOverlay = value ?? new NullWindowOverlayService();
    }

    public static IHotKeyService HotKey
    {
        get => _hotKey;
        set => _hotKey = value ?? new NullHotKeyService();
    }

    public static ISingleInstanceService SingleInstance
    {
        get => _singleInstance;
        set => _singleInstance = value ?? new NullSingleInstanceService();
    }

    private sealed class NullIdleDetectionService : IIdleDetectionService
    {
        public TimeSpan GetIdleTime() => TimeSpan.Zero;
    }

    private sealed class NullStartupService : IStartupService
    {
        public bool IsSupported => false;
        public bool IsEnabled() => false;
        public void SetEnabled(bool enabled) { }
    }

    private sealed class NullWindowOverlayService : IWindowOverlayService
    {
        public void SetClickThrough(IntPtr windowHandle, bool clickThrough) { }
        public void SetAlwaysOnTop(IntPtr windowHandle, bool topmost) { }
        public void SetNoActivateToolWindow(IntPtr windowHandle) { }
    }

    private sealed class NullHotKeyService : IHotKeyService
    {
        public bool RegisterHotKey(ShortcutAction action, uint modifiers, uint virtualKey) => false;
        public void UnregisterHotKey(ShortcutAction action) { }
        public void UnregisterAll() { }
        public event EventHandler<ShortcutAction>? HotKeyPressed { add { } remove { } }
        public void Dispose() { }
    }

    private sealed class NullSingleInstanceService : ISingleInstanceService
    {
        public bool TryAcquireSingleInstance(Action onShowExistingRequested) => true;
        public void SignalExistingInstance() { }
        public void Dispose() { }
    }
}
