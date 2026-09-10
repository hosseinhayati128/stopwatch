using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Threading;

namespace StopwatchOverlay
{
    /// <summary>
    /// Manages the temporary two-stage keyboard command mode activated by the leader shortcut (default Win+F2).
    /// Installs a scoped WH_KEYBOARD_LL hook while active to capture the next command key without stealing focus.
    /// </summary>
    public sealed class ShortcutCommandMode : IDisposable
    {
        public const string GuidanceLine1 = "Timer command: Space Start/Stop · R Reset · O Overlay · L Lap · W Controller";
        public const string GuidanceLine2 = "C Clock · N New · T Next · X Close · P Project · D Dashboard";
        public const string GuidanceStatusText = "Timer command: Space Start/Stop · R Reset · O Overlay · L Lap · W Controller · C Clock · N New · T Next · X Close · P Project · D Dashboard";

        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_KEYUP = 0x0101;
        private const int WM_SYSKEYDOWN = 0x0104;
        private const int WM_SYSKEYUP = 0x0105;

        // Virtual key codes for fixed commands
        public const uint VK_SPACE = 0x20;
        public const uint VK_ESCAPE = 0x1B;
        public const uint VK_KEY_R = 0x52;
        public const uint VK_KEY_O = 0x4F;
        public const uint VK_KEY_L = 0x4C;
        public const uint VK_KEY_C = 0x43;
        public const uint VK_KEY_N = 0x4E;
        public const uint VK_KEY_T = 0x54;
        public const uint VK_KEY_X = 0x58;
        public const uint VK_KEY_P = 0x50;
        public const uint VK_KEY_D = 0x44;
        public const uint VK_KEY_W = 0x57;

        // Modifier virtual keys
        public const uint VK_SHIFT = 0x10;
        public const uint VK_CONTROL = 0x11;
        public const uint VK_MENU = 0x12; // Alt
        public const uint VK_CAPITAL = 0x14; // CapsLock
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

        private static readonly Dictionary<uint, ShortcutAction> ActionMap = new()
        {
            [VK_SPACE] = ShortcutAction.StartStop,
            [VK_KEY_R] = ShortcutAction.Reset,
            [VK_KEY_O] = ShortcutAction.ToggleOverlay,
            [VK_KEY_L] = ShortcutAction.Lap,
            [VK_KEY_C] = ShortcutAction.ToggleClock,
            [VK_KEY_N] = ShortcutAction.NewTimer,
            [VK_KEY_T] = ShortcutAction.NextTimer,
            [VK_KEY_X] = ShortcutAction.CloseTimer,
            [VK_KEY_P] = ShortcutAction.RenameTimer,
            [VK_KEY_D] = ShortcutAction.OpenDashboard,
            [VK_KEY_W] = ShortcutAction.OpenController,
        };

        private readonly Dispatcher _dispatcher;
        private readonly LowLevelKeyboardProc _hookProc; // Strongly referenced delegate
        private IntPtr _hookId = IntPtr.Zero;
        private readonly DispatcherTimer _timeoutTimer;
        private readonly DispatcherTimer _cleanupSafetyTimer;

        private bool _isActive;
        private bool _actionExecuted;
        private uint _suppressedKeyUpVk;
        private uint _leaderVk;
        private bool _disposed;

        public event Action<ShortcutAction>? ActionTriggered;
        public event Action? Cancelled;
        public event Action? TimedOut;
        public event Action? UnknownCommand;
        public event Action? ModeStarted;

        public bool IsActive => _isActive;

        public ShortcutCommandMode(Dispatcher dispatcher, uint leaderVk = 0x71u /* VK_F2 */)
        {
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _leaderVk = leaderVk;
            _hookProc = HookCallback;

            _timeoutTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _timeoutTimer.Tick += OnTimeoutTick;

            _cleanupSafetyTimer = new DispatcherTimer(DispatcherPriority.Normal, _dispatcher)
            {
                Interval = TimeSpan.FromMilliseconds(800)
            };
            _cleanupSafetyTimer.Tick += OnCleanupSafetyTick;
        }

        public void SetLeaderVirtualKey(uint leaderVk)
        {
            _leaderVk = leaderVk;
        }

        public static bool TryGetAction(uint virtualKey, out ShortcutAction action)
        {
            // Normalize lowercase ASCII to uppercase virtual keys if passed
            if (virtualKey is >= 0x61 and <= 0x7A)
            {
                virtualKey -= 0x20;
            }
            return ActionMap.TryGetValue(virtualKey, out action);
        }

        public static bool TryGetAction(char keyChar, out ShortcutAction action)
        {
            char upper = char.ToUpperInvariant(keyChar);
            if (upper == ' ')
            {
                action = ShortcutAction.StartStop;
                return true;
            }
            return TryGetAction((uint)upper, out action);
        }

        public static bool TryGetAction(Key key, out ShortcutAction action)
        {
            uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
            return TryGetAction(vk, out action);
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

        /// <summary>
        /// Enters command mode: registers the low-level hook and starts the 2-second timeout.
        /// If already in command mode, restarts the 2-second timeout.
        /// </summary>
        public void Enter()
        {
            if (_disposed) return;

            if (_isActive)
            {
                RestartTimeout();
                return;
            }

            _isActive = true;
            _actionExecuted = false;
            _suppressedKeyUpVk = 0;

            InstallHook();
            _timeoutTimer.Stop();
            _timeoutTimer.Start();

            ModeStarted?.Invoke();
        }

        /// <summary>
        /// Restarts the 2-second timeout window.
        /// </summary>
        public void RestartTimeout()
        {
            if (!_isActive || _disposed) return;

            _timeoutTimer.Stop();
            _timeoutTimer.Start();
            ModeStarted?.Invoke();
        }

        /// <summary>
        /// Explicitly cancels command mode.
        /// </summary>
        public void Cancel()
        {
            if (!_isActive && _hookId == IntPtr.Zero) return;

            _isActive = false;
            _timeoutTimer.Stop();
            _cleanupSafetyTimer.Stop();
            UninstallHook();

            Cancelled?.Invoke();
        }

        private void OnTimeoutTick(object? sender, EventArgs e)
        {
            _timeoutTimer.Stop();
            if (!_isActive) return;

            _isActive = false;
            UninstallHook();
            TimedOut?.Invoke();
        }

        private void OnCleanupSafetyTick(object? sender, EventArgs e)
        {
            _cleanupSafetyTimer.Stop();
            _suppressedKeyUpVk = 0;
            UninstallHook();
        }

        private void InstallHook()
        {
            if (_hookId != IntPtr.Zero) return;

            try
            {
                IntPtr hMod = GetModuleHandle(null);
                _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _hookProc, hMod, 0);
            }
            catch
            {
                _hookId = IntPtr.Zero;
                _isActive = false;
                _timeoutTimer.Stop();
            }
        }

        private void UninstallHook()
        {
            if (_hookId != IntPtr.Zero)
            {
                IntPtr currentHook = _hookId;
                _hookId = IntPtr.Zero;
                UnhookWindowsHookEx(currentHook);
            }
        }

        private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode < 0 || _disposed)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            int msg = wParam.ToInt32();
            bool isKeyDown = msg is WM_KEYDOWN or WM_SYSKEYDOWN;
            bool isKeyUp = msg is WM_KEYUP or WM_SYSKEYUP;

            if (!isKeyDown && !isKeyUp)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            KBDLLHOOKSTRUCT kbd;
            try
            {
                kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);
            }
            catch
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            uint vk = kbd.vkCode;

            // Check if this is the matching key-up for an already consumed command key
            if (_suppressedKeyUpVk != 0 && vk == _suppressedKeyUpVk)
            {
                if (isKeyUp)
                {
                    _suppressedKeyUpVk = 0;
                    _cleanupSafetyTimer.Stop();
                    UninstallHook();
                }
                return (IntPtr)1; // Suppress matching key up and repeated key downs
            }

            // Outside active command mode, do not process
            if (!_isActive)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            // Modifier-only key presses must not select a command and pass through cleanly
            if (IsModifierKey(vk))
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            // If leader key (e.g. F2) is pressed again while in command mode, restart the timeout
            if (_leaderVk != 0 && vk == _leaderVk)
            {
                if (isKeyDown)
                {
                    _dispatcher.BeginInvoke(new Action(RestartTimeout));
                }
                // Suppress leader key so it doesn't leak into the active app
                return (IntPtr)1;
            }

            // Only process actions on KeyDown
            if (!isKeyDown)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            // Auto-repeat guard: if an action has already fired during this window, suppress and do not re-execute
            if (_actionExecuted)
            {
                return (IntPtr)1;
            }

            // Escape cancels command mode without action
            if (IsEscape(vk))
            {
                _isActive = false;
                _actionExecuted = true;
                _timeoutTimer.Stop();
                _suppressedKeyUpVk = vk;
                _cleanupSafetyTimer.Start();

                _dispatcher.BeginInvoke(new Action(() => Cancelled?.Invoke()));
                return (IntPtr)1; // Suppress Escape key down
            }

            // Check if key maps to a recognized action
            if (TryGetAction(vk, out ShortcutAction action))
            {
                _isActive = false;
                _actionExecuted = true;
                _timeoutTimer.Stop();
                _suppressedKeyUpVk = vk;
                _cleanupSafetyTimer.Start();

                _dispatcher.BeginInvoke(new Action(() => ActionTriggered?.Invoke(action)));
                return (IntPtr)1; // Suppress command key down
            }

            // Unsupported key: leave command mode, report unknown command, and let the key pass through
            _isActive = false;
            _timeoutTimer.Stop();
            UninstallHook();

            _dispatcher.BeginInvoke(new Action(() => UnknownCommand?.Invoke()));
            return CallNextHookEx(_hookId, nCode, wParam, lParam);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            _isActive = false;
            _timeoutTimer.Stop();
            _cleanupSafetyTimer.Stop();
            UninstallHook();
        }
    }
}
