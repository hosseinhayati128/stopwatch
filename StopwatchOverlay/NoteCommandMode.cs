using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace StopwatchOverlay;

public enum NoteCommandAction
{
    AddTodo = 1,
    AddNote = 2,
    AddReminder = 3,
    ViewNotes = 4
}

/// <summary>
/// Manages the temporary keyboard command mode activated by the note leader shortcut (default Win+F3).
/// Installs a scoped WH_KEYBOARD_LL hook while active to capture the next note command key without stealing focus.
/// </summary>
public sealed class NoteCommandMode : IDisposable
{
    public const string GuidanceStatusText = "Note command: 1 Todo · 2 Quick Note · 3 Reminder · 4 View Notes · Esc Cancel";

    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_KEYUP = 0x0101;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const int WM_SYSKEYUP = 0x0105;

    public const uint VK_ESCAPE = 0x1B;
    public const uint VK_KEY_1 = 0x31;
    public const uint VK_KEY_2 = 0x32;
    public const uint VK_KEY_3 = 0x33;
    public const uint VK_KEY_4 = 0x34;
    public const uint VK_NUMPAD1 = 0x61;
    public const uint VK_NUMPAD2 = 0x62;
    public const uint VK_NUMPAD3 = 0x63;
    public const uint VK_NUMPAD4 = 0x64;

    public const uint VK_KEY_T = 0x54;
    public const uint VK_KEY_N = 0x4E;
    public const uint VK_KEY_R = 0x52;
    public const uint VK_KEY_V = 0x56;
    public const uint VK_KEY_O = 0x4F;

    // Modifier virtual keys
    public const uint VK_SHIFT = 0x10;
    public const uint VK_CONTROL = 0x11;
    public const uint VK_MENU = 0x12;
    public const uint VK_CAPITAL = 0x14;
    public const uint VK_LWIN = 0x5B;
    public const uint VK_RWIN = 0x5C;
    public const uint VK_LSHIFT = 0xA0;
    public const uint VK_RSHIFT = 0xA1;
    public const uint VK_LCONTROL = 0xA2;
    public const uint VK_RCONTROL = 0xA3;
    public const uint VK_LMENU = 0xA4;
    public const uint VK_RMENU = 0xA5;

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public UIntPtr dwExtraInfo;
    }

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    private static readonly Dictionary<uint, NoteCommandAction> ActionMap = new()
    {
        [VK_KEY_1] = NoteCommandAction.AddTodo,
        [VK_NUMPAD1] = NoteCommandAction.AddTodo,
        [VK_KEY_T] = NoteCommandAction.AddTodo,

        [VK_KEY_2] = NoteCommandAction.AddNote,
        [VK_NUMPAD2] = NoteCommandAction.AddNote,
        [VK_KEY_N] = NoteCommandAction.AddNote,

        [VK_KEY_3] = NoteCommandAction.AddReminder,
        [VK_NUMPAD3] = NoteCommandAction.AddReminder,
        [VK_KEY_R] = NoteCommandAction.AddReminder,

        [VK_KEY_4] = NoteCommandAction.ViewNotes,
        [VK_NUMPAD4] = NoteCommandAction.ViewNotes,
        [VK_KEY_V] = NoteCommandAction.ViewNotes,
        [VK_KEY_O] = NoteCommandAction.ViewNotes,
    };

    public static readonly TimeSpan InitialTimeout = TimeSpan.FromSeconds(5);

    private readonly Dispatcher _dispatcher;
    private readonly LowLevelKeyboardProc _hookProc;
    private IntPtr _hookId = IntPtr.Zero;
    private readonly DispatcherTimer _timeoutTimer;

    private bool _isActive;
    private uint _suppressedKeyUpVk;
    private uint _leaderVk;
    private bool _disposed;

    public event Action<NoteCommandAction>? ActionTriggered;
    public event Action? Cancelled;
    public event Action? TimedOut;
    public event Action? UnknownCommand;
    public event Action? ModeStarted;

    public bool IsActive => _isActive;

    public NoteCommandMode(Dispatcher dispatcher, uint leaderVk = 0x72u /* VK_F3 */)
    {
        _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        _leaderVk = leaderVk;
        _hookProc = HookCallback;

        _timeoutTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
        {
            Interval = InitialTimeout
        };
        _timeoutTimer.Tick += OnTimeoutTick;
    }

    public void SetLeaderVirtualKey(uint leaderVk)
    {
        _leaderVk = leaderVk;
    }

    public static bool TryGetAction(uint virtualKey, out NoteCommandAction action)
    {
        if (virtualKey is >= 0x61 and <= 0x7A)
        {
            virtualKey -= 0x20;
        }
        return ActionMap.TryGetValue(virtualKey, out action);
    }

    public static bool IsEscape(uint virtualKey) => virtualKey == VK_ESCAPE;

    public static bool IsModifierKey(uint virtualKey)
    {
        return virtualKey is VK_SHIFT or VK_CONTROL or VK_MENU or VK_CAPITAL
            or VK_LWIN or VK_RWIN
            or VK_LSHIFT or VK_RSHIFT
            or VK_LCONTROL or VK_RCONTROL
            or VK_LMENU or VK_RMENU;
    }

    public void Enter()
    {
        if (_disposed) return;

        if (_isActive)
        {
            RestartTimeout();
            return;
        }

        _suppressedKeyUpVk = _leaderVk;
        _isActive = true;
        InstallHook();

        _timeoutTimer.Stop();
        _timeoutTimer.Interval = InitialTimeout;
        _timeoutTimer.Start();

        ModeStarted?.Invoke();
    }

    public void RestartTimeout()
    {
        if (!_isActive || _disposed) return;
        _timeoutTimer.Stop();
        _timeoutTimer.Interval = InitialTimeout;
        _timeoutTimer.Start();
    }

    public void Exit()
    {
        if (!_isActive) return;

        _isActive = false;
        _timeoutTimer.Stop();
        UninstallHook();
        _suppressedKeyUpVk = 0;
    }

    private void InstallHook()
    {
        if (_hookId != IntPtr.Zero) return;

        using var process = System.Diagnostics.Process.GetCurrentProcess();
        using var module = process.MainModule;
        IntPtr hMod = GetModuleHandle(module?.ModuleName);
        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, hMod, 0);

        if (_hookId == IntPtr.Zero)
        {
            int err = Marshal.GetLastWin32Error();
            CrashLogger.LogRecoverable(
                new InvalidOperationException($"SetWindowsHookEx failed in NoteCommandMode with error {err}"),
                "NoteCommandMode.InstallHook");
        }
    }

    private void UninstallHook()
    {
        if (_hookId == IntPtr.Zero) return;

        UnhookWindowsHookEx(_hookId);
        _hookId = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode < 0 || !_isActive)
        {
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        int msg = wParam.ToInt32();

        if (msg is WM_KEYUP or WM_SYSKEYUP)
        {
            var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            if (kbd.vkCode == _suppressedKeyUpVk)
            {
                _suppressedKeyUpVk = 0;
                return (IntPtr)1;
            }
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        if (msg is WM_KEYDOWN or WM_SYSKEYDOWN)
        {
            var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            uint vk = kbd.vkCode;

            if (IsModifierKey(vk))
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            if (vk == _leaderVk)
            {
                _suppressedKeyUpVk = vk;
                _dispatcher.BeginInvoke(new Action(RestartTimeout));
                return (IntPtr)1;
            }

            if (IsEscape(vk))
            {
                _suppressedKeyUpVk = vk;
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    Exit();
                    Cancelled?.Invoke();
                }));
                return (IntPtr)1;
            }

            if (TryGetAction(vk, out var action))
            {
                _suppressedKeyUpVk = vk;
                _dispatcher.BeginInvoke(new Action(() =>
                {
                    Exit();
                    ActionTriggered?.Invoke(action);
                }));
                return (IntPtr)1;
            }

            // Unknown key - cancel command mode and consume key
            _suppressedKeyUpVk = vk;
            _dispatcher.BeginInvoke(new Action(() =>
            {
                Exit();
                UnknownCommand?.Invoke();
            }));
            return (IntPtr)1;
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private void OnTimeoutTick(object? sender, EventArgs e)
    {
        Exit();
        TimedOut?.Invoke();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Exit();
    }
}
