using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using StopwatchOverlay.Platform;

namespace StopwatchOverlay.Desktop.Platform;

/// <summary>Wayland shortcuts registered through the user's desktop portal.</summary>
public sealed class PortalHotKeyService : IHotKeyService
{
    private readonly object _gate = new();
    private readonly Dictionary<ShortcutAction, (uint Modifiers, uint Key)> _registered = new();
    private readonly Timer _registrationTimer;
    private Process? _helper;
    private bool _disposed;

    public event EventHandler<ShortcutAction>? HotKeyPressed;
    public event EventHandler<uint>? CommandKeyPressed { add { } remove { } }
    public event EventHandler<string>? RegistrationStatusChanged;
    public string RegistrationStatus { get; private set; } = "Not registered";
    public bool IsInCommandMode { get; set; }

    public PortalHotKeyService()
    {
        _registrationTimer = new Timer(_ => StartHelper(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public bool RegisterHotKey(ShortcutAction action, uint modifiers, uint virtualKey)
    {
        if (FormatTrigger(modifiers, virtualKey) is null) return false;
        lock (_gate)
        {
            if (_disposed) return false;
            _registered[action] = (modifiers, virtualKey);
            _registrationTimer.Change(250, Timeout.Infinite);
        }
        return true;
    }

    public void UnregisterHotKey(ShortcutAction action)
    {
        lock (_gate)
        {
            _registered.Remove(action);
            if (!_disposed) _registrationTimer.Change(250, Timeout.Infinite);
        }
    }

    public void UnregisterAll()
    {
        lock (_gate)
        {
            _registered.Clear();
            _registrationTimer.Change(Timeout.Infinite, Timeout.Infinite);
            StopHelper();
        }
    }

    private void StartHelper()
    {
        lock (_gate)
        {
            if (_disposed) return;
            StopHelper();
            if (_registered.Count == 0) return;
            try
            {
                using var script = Assembly.GetExecutingAssembly().GetManifestResourceStream(
                    "StopwatchOverlay.Desktop.Platform.portal_hotkeys.py")
                    ?? throw new IOException("Portal helper resource is missing.");
                using var reader = new StreamReader(script);
                var start = new ProcessStartInfo("/usr/bin/python3")
                {
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                start.ArgumentList.Add("-u");
                start.ArgumentList.Add("-c");
                start.ArgumentList.Add(reader.ReadToEnd());
                start.ArgumentList.Add(JsonSerializer.Serialize(_registered.Select(item => new
                {
                    id = item.Key.ToString(),
                    description = item.Key == ShortcutAction.CommandLeader ? "Timer command menu" :
                        item.Key == ShortcutAction.NoteCommandLeader ? "Quick notes menu" :
                        item.Key == ShortcutAction.OpenController ? "Open Stopwatch controller" : "Show active timer",
                    trigger = FormatTrigger(item.Value.Modifiers, item.Value.Key)
                })));
                var process = new Process { StartInfo = start, EnableRaisingEvents = true };
                process.OutputDataReceived += (_, e) =>
                {
                    if (e.Data is null) return;
                    if (e.Data.StartsWith("HOTKEY ") && Enum.TryParse<ShortcutAction>(e.Data[7..], out var action))
                        HotKeyPressed?.Invoke(this, action);
                    else if (e.Data.StartsWith("ERROR "))
                        ReportStatus(e.Data);
                    else if (e.Data.StartsWith("READY "))
                        {
                        using var bindings = JsonDocument.Parse(e.Data[6..]);
                        ReportStatus("Ubuntu global shortcuts:\n" + string.Join("\n", bindings.RootElement.EnumerateArray().Select(binding =>
                            binding[1].GetProperty("description").GetString() + ": " + binding[1].GetProperty("trigger_description").GetString())));
                    }
                };
                process.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data)) ReportStatus("ERROR " + e.Data);
                };
                _helper = process;
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                ReportStatus("Waiting for desktop shortcut permission");
            }
            catch (Exception ex)
            {
                ReportStatus("ERROR " + ex.Message);
                CrashLogger.LogRecoverable(ex, "PortalHotKeyService.Start");
            }
        }
    }

    private void ReportStatus(string status)
    {
        Console.WriteLine("[Shortcuts] " + status);
        RegistrationStatus = status;
        RegistrationStatusChanged?.Invoke(this, status);
        if (status.StartsWith("ERROR "))
            CrashLogger.LogRecoverable(new InvalidOperationException(status), "PortalHotKeyService.Register");
    }

    internal static string? FormatTrigger(uint modifiers, uint key)
    {
        string? name = key switch
        {
            >= 0x70 and <= 0x87 => "F" + (key - 0x70 + 1),
            >= 0x41 and <= 0x5A => ((char)key).ToString(),
            >= 0x30 and <= 0x39 => ((char)key).ToString(),
            0x20 => "space", 0x1B => "Escape", 0x09 => "Tab", 0x0D => "Return",
            0x08 => "BackSpace", 0x2D => "Insert", 0x2E => "Delete", 0x24 => "Home",
            0x23 => "End", 0x21 => "Page_Up", 0x22 => "Page_Down", 0x25 => "Left",
            0x26 => "Up", 0x27 => "Right", 0x28 => "Down", _ => null
        };
        if (name is null) return null;
        return ((modifiers & Shortcut.MOD_CONTROL) != 0 ? "CTRL+" : "")
             + ((modifiers & Shortcut.MOD_ALT) != 0 ? "ALT+" : "")
             + ((modifiers & Shortcut.MOD_SHIFT) != 0 ? "SHIFT+" : "")
             + ((modifiers & Shortcut.MOD_WIN) != 0 ? "LOGO+" : "") + name;
    }

    private void StopHelper()
    {
        if (_helper is not { } process) return;
        _helper = null;
        try
        {
            process.StandardInput.Close();
            if (!process.WaitForExit(1000)) process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException) { }
        process.Dispose();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _registrationTimer.Dispose();
            StopHelper();
        }
    }
}
