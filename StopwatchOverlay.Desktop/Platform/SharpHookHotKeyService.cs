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
    private readonly TaskPoolGlobalHook _hook;
    private readonly ConcurrentDictionary<ShortcutAction, (uint Modifiers, uint VirtualKey)> _registered = new();
    private readonly CancellationTokenSource _cts = new();

    public event EventHandler<ShortcutAction>? HotKeyPressed;
    public event EventHandler<uint>? CommandKeyPressed;
    public bool IsInCommandMode { get; set; }

    public SharpHookHotKeyService()
    {
        _hook = new TaskPoolGlobalHook();
        _hook.KeyPressed += OnKeyPressed;
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
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        uint vk = MapKeyCodeToVirtualKey(e.Data.KeyCode);
        if (vk == 0) return;

        uint mods = 0;
        var mask = e.RawEvent.Mask;
        if (mask.HasFlag(ModifierMask.Ctrl)) mods |= Shortcut.MOD_CONTROL;
        if (mask.HasFlag(ModifierMask.Alt)) mods |= Shortcut.MOD_ALT;
        if (mask.HasFlag(ModifierMask.Shift)) mods |= Shortcut.MOD_SHIFT;
        if (mask.HasFlag(ModifierMask.Meta)) mods |= Shortcut.MOD_WIN;

        bool matchedHotKey = false;
        foreach (var (action, (regMods, regVk)) in _registered)
        {
            if (regVk == vk && regMods == mods)
            {
                HotKeyPressed?.Invoke(this, action);
                matchedHotKey = true;
                break;
            }
        }

        if (!matchedHotKey && IsInCommandMode)
        {
            CommandKeyPressed?.Invoke(this, vk);
        }
    }

    private static uint MapKeyCodeToVirtualKey(KeyCode code) => code switch
    {
        KeyCode.VcSpace => 0x20,
        KeyCode.VcEscape => 0x1B,
        KeyCode.VcTab => 0x09,
        KeyCode.VcEnter => 0x0D,
        KeyCode.VcBackspace => 0x08,
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
        _ => 0
    };

    public void Dispose()
    {
        _registered.Clear();
        _cts.Cancel();
        _hook.Dispose();
        _cts.Dispose();
    }
}
