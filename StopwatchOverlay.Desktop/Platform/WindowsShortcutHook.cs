using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace StopwatchOverlay.Desktop.Platform;

// Low-level keyboard hooks run on the installing thread. Keep that thread pumping
// while the controller is busy opening a window or saving its workspace.
internal sealed class WindowsShortcutHook : IDisposable
{
    private const int KeyboardLowLevel = 13, Quit = 0x12;
    private readonly HookProc _callback;
    private readonly Action<Exception> _onFailure;
    private Thread? _thread;
    private uint _threadId;

    internal WindowsShortcutHook(Func<uint, bool, uint, bool> keyChanged, Action<Exception> onFailure)
    {
        _onFailure = onFailure;
        _callback = (code, message, data) =>
        {
            if (code >= 0 && message is 0x100 or 0x101 or 0x104 or 0x105)
            {
                try
                {
                    uint vk = (uint)Marshal.ReadInt32(data);
                    bool down = message is 0x100 or 0x104;
                    if (keyChanged(vk, down, down ? ReadModifiers() : 0)) return 1;
                }
                catch (Exception ex) { _onFailure(ex); }
            }
            return CallNextHookEx(0, code, message, data);
        };
    }

    // Registration and disposal are serialized by the controller on its UI thread.
    internal void EnsureStarted()
    {
        if (_thread?.IsAlive == true) return;
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            nint handle = 0;
            try
            {
                _threadId = GetCurrentThreadId();
                PeekMessageW(out _, 0, 0, 0, 0); // Create the queue before disposal can post WM_QUIT.
                handle = SetWindowsHookExW(KeyboardLowLevel, _callback, GetModuleHandleW(0), 0);
                if (handle == 0) throw new Win32Exception(Marshal.GetLastWin32Error());
                ready.SetResult();
            }
            catch (Exception ex) { ready.SetException(ex); return; }
            try
            {
                // GetMessage dispatches the synchronous low-level hook notifications.
                int result;
                do { result = GetMessageW(out _, 0, 0, 0); } while (result > 0);
                if (result < 0) throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            catch (Exception ex) { _onFailure(ex); }
            finally { UnhookWindowsHookEx(handle); }
        }) { IsBackground = true, Name = "Stopwatch Windows shortcuts" };
        thread.Start();
        ready.Task.GetAwaiter().GetResult();
        _thread = thread;
    }

    public void Dispose()
    {
        if (_thread?.IsAlive == true && PostThreadMessageW(_threadId, Quit, 0, 0))
            _thread.Join(TimeSpan.FromSeconds(2));
    }

    private static uint ReadModifiers()
    {
        uint mods = 0;
        if (GetAsyncKeyState(0x11) < 0) mods |= Shortcut.MOD_CONTROL;
        if (GetAsyncKeyState(0x10) < 0) mods |= Shortcut.MOD_SHIFT;
        if (GetAsyncKeyState(0x12) < 0) mods |= Shortcut.MOD_ALT;
        if (GetAsyncKeyState(0x5B) < 0 || GetAsyncKeyState(0x5C) < 0)
            mods |= Shortcut.MOD_WIN;
        return mods;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Message
    {
        public nint Hwnd;
        public uint Id;
        public nuint WParam;
        public nint LParam;
        public uint Time;
        public int X, Y;
        public uint Private;
    }
    private delegate nint HookProc(int code, nint message, nint data);
    [DllImport("kernel32")] private static extern uint GetCurrentThreadId();
    [DllImport("kernel32", ExactSpelling = true)] private static extern nint GetModuleHandleW(nint name);
    [DllImport("user32", SetLastError = true)] private static extern nint SetWindowsHookExW(int id, HookProc callback, nint module, uint thread);
    [DllImport("user32")] private static extern nint CallNextHookEx(nint handle, int code, nint message, nint data);
    [DllImport("user32")] private static extern bool UnhookWindowsHookEx(nint handle);
    [DllImport("user32")] private static extern bool PeekMessageW(out Message msg, nint hwnd, uint min, uint max, uint remove);
    [DllImport("user32", SetLastError = true)] private static extern int GetMessageW(out Message msg, nint hwnd, uint min, uint max);
    [DllImport("user32")] private static extern bool PostThreadMessageW(uint thread, uint message, nuint wParam, nint lParam);
    [DllImport("user32")] private static extern short GetAsyncKeyState(int vk);
}
