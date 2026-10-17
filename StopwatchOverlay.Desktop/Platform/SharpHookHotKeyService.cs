using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using SharpHook;
using SharpHook.Native;
using StopwatchOverlay.Platform;

namespace StopwatchOverlay.Desktop.Platform;

public sealed class SharpHookHotKeyService : IHotKeyService
{
    private readonly IGlobalHook _hook;
    private readonly ConcurrentDictionary<ShortcutAction, (uint Modifiers, uint VirtualKey)> _registered = new();
    private readonly ConcurrentDictionary<KeyCode, bool> _pressedKeys = new();
    private int _disposed;

    public event EventHandler<ShortcutAction>? HotKeyPressed;
    public event EventHandler<uint>? CommandKeyPressed;
    private volatile bool _isInCommandMode;
    public bool IsInCommandMode { get => _isInCommandMode; set => _isInCommandMode = value; }

    public SharpHookHotKeyService() : this(startHook: true) { }

    internal SharpHookHotKeyService(bool startHook)
    {
        // Windows command keys must be consumed synchronously so they do not type
        // into another app. Keep the existing asynchronous X11 hook on Linux.
        _hook = OperatingSystem.IsWindows() ? new SimpleGlobalHook() : new TaskPoolGlobalHook();
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;
        if (!startHook) return;
        _ = Task.Run(async () =>
        {
            try
            {
                await _hook.RunAsync();
            }
            catch (Exception ex)
            {
                CrashLogger.LogRecoverable(ex, "SharpHookHotKeyService.Run");
            }
        });
    }

    public bool RegisterHotKey(ShortcutAction action, uint modifiers, uint virtualKey)
    {
        if (virtualKey == 0)
            return false;

        _registered[action] = (modifiers, virtualKey);
        return true;
    }

    public void UnregisterHotKey(ShortcutAction action)
    {
        _registered.TryRemove(action, out _);
    }

    public void UnregisterAll()
    {
        _registered.Clear();
        IsInCommandMode = false;
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        uint vk = MapKeyCodeToVirtualKey(e.Data.KeyCode);
        if (vk == 0) return;
        if (!_pressedKeys.TryAdd(e.Data.KeyCode, false))
        {
            e.SuppressEvent = OperatingSystem.IsWindows() && _pressedKeys.TryGetValue(e.Data.KeyCode, out bool captured) && captured;
            return;
        }

        uint mods = 0;
        var mask = e.RawEvent.Mask;
        // These masks contain BOTH left and right bits. HasFlag would require
        // both physical modifier keys to be held at once.
        if ((mask & ModifierMask.Ctrl) != 0) mods |= Shortcut.MOD_CONTROL;
        if ((mask & ModifierMask.Alt) != 0) mods |= Shortcut.MOD_ALT;
        if ((mask & ModifierMask.Shift) != 0) mods |= Shortcut.MOD_SHIFT;
        if ((mask & ModifierMask.Meta) != 0) mods |= Shortcut.MOD_WIN;

        bool matchedHotKey = false;
        foreach (var (action, (regMods, regVk)) in _registered)
        {
            if (regVk == vk && regMods == mods)
            {
                // Arm capture before dispatching to the UI; otherwise a fast
                // follow-up key can arrive while the leader is still queued.
                if (action is ShortcutAction.CommandLeader or ShortcutAction.NoteCommandLeader)
                    IsInCommandMode = true;
                HotKeyPressed?.Invoke(this, action);
                matchedHotKey = true;
                break;
            }
        }

        if (!matchedHotKey && IsInCommandMode)
        {
            _pressedKeys[e.Data.KeyCode] = true;
            e.SuppressEvent = OperatingSystem.IsWindows();
            CommandKeyPressed?.Invoke(this, vk);
        }
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        if (_pressedKeys.TryRemove(e.Data.KeyCode, out bool captured))
            e.SuppressEvent = OperatingSystem.IsWindows() && captured;
    }

    private static uint MapKeyCodeToVirtualKey(KeyCode code) => code switch
    {
        KeyCode.VcSpace => 0x20,
        KeyCode.VcEscape => 0x1B,
        KeyCode.VcTab => 0x09,
        KeyCode.VcEnter => 0x0D,
        KeyCode.VcBackspace => 0x08,
        KeyCode.VcInsert => 0x2D,
        KeyCode.VcDelete => 0x2E,
        KeyCode.VcHome => 0x24,
        KeyCode.VcEnd => 0x23,
        KeyCode.VcPageUp => 0x21,
        KeyCode.VcPageDown => 0x22,
        KeyCode.VcLeft => 0x25,
        KeyCode.VcUp => 0x26,
        KeyCode.VcRight => 0x27,
        KeyCode.VcDown => 0x28,
        KeyCode.VcF1 => 0x70,
        KeyCode.VcF2 => 0x71,
        KeyCode.VcF3 => 0x72,
        KeyCode.VcF4 => 0x73,
        KeyCode.VcF5 => 0x74,
        KeyCode.VcF6 => 0x75,
        KeyCode.VcF7 => 0x76,
        KeyCode.VcF8 => 0x77,
        KeyCode.VcF9 => 0x78,
        KeyCode.VcF10 => 0x79,
        KeyCode.VcF11 => 0x7A,
        KeyCode.VcF12 => 0x7B,
        KeyCode.VcF13 => 0x7C,
        KeyCode.VcF14 => 0x7D,
        KeyCode.VcF15 => 0x7E,
        KeyCode.VcF16 => 0x7F,
        KeyCode.VcF17 => 0x80,
        KeyCode.VcF18 => 0x81,
        KeyCode.VcF19 => 0x82,
        KeyCode.VcF20 => 0x83,
        KeyCode.VcF21 => 0x84,
        KeyCode.VcF22 => 0x85,
        KeyCode.VcF23 => 0x86,
        KeyCode.VcF24 => 0x87,
        KeyCode.VcA => 0x41,
        KeyCode.VcB => 0x42,
        KeyCode.VcC => 0x43,
        KeyCode.VcD => 0x44,
        KeyCode.VcE => 0x45,
        KeyCode.VcF => 0x46,
        KeyCode.VcG => 0x47,
        KeyCode.VcH => 0x48,
        KeyCode.VcI => 0x49,
        KeyCode.VcJ => 0x4A,
        KeyCode.VcK => 0x4B,
        KeyCode.VcL => 0x4C,
        KeyCode.VcM => 0x4D,
        KeyCode.VcN => 0x4E,
        KeyCode.VcO => 0x4F,
        KeyCode.VcP => 0x50,
        KeyCode.VcQ => 0x51,
        KeyCode.VcR => 0x52,
        KeyCode.VcS => 0x53,
        KeyCode.VcT => 0x54,
        KeyCode.VcU => 0x55,
        KeyCode.VcV => 0x56,
        KeyCode.VcW => 0x57,
        KeyCode.VcX => 0x58,
        KeyCode.VcY => 0x59,
        KeyCode.VcZ => 0x5A,
        KeyCode.Vc0 => 0x30,
        KeyCode.Vc1 => 0x31,
        KeyCode.Vc2 => 0x32,
        KeyCode.Vc3 => 0x33,
        KeyCode.Vc4 => 0x34,
        KeyCode.Vc5 => 0x35,
        KeyCode.Vc6 => 0x36,
        KeyCode.Vc7 => 0x37,
        KeyCode.Vc8 => 0x38,
        KeyCode.Vc9 => 0x39,
        KeyCode.VcNumPad0 => 0x60,
        KeyCode.VcNumPad1 => 0x61,
        KeyCode.VcNumPad2 => 0x62,
        KeyCode.VcNumPad3 => 0x63,
        KeyCode.VcNumPad4 => 0x64,
        KeyCode.VcNumPad5 => 0x65,
        KeyCode.VcNumPad6 => 0x66,
        KeyCode.VcNumPad7 => 0x67,
        KeyCode.VcNumPad8 => 0x68,
        KeyCode.VcNumPad9 => 0x69,
        KeyCode.VcNumPadEnter => 0x0D,
        _ => 0
    };

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
        _registered.Clear();
        _pressedKeys.Clear();
        _hook.Dispose();
    }
}
