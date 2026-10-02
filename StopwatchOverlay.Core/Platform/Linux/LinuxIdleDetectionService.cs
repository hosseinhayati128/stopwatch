using System;
using System.Diagnostics;

namespace StopwatchOverlay.Platform.Linux;

/// <summary>
/// Monitors system-wide user inactivity on Linux by querying xprintidle or desktop session idle monitors.
/// </summary>
public sealed class LinuxIdleDetectionService : IIdleDetectionService
{
    private bool _xprintidleChecked;
    private bool _hasXprintidle = true;

    public TimeSpan GetIdleTime()
    {
        if (!OperatingSystem.IsLinux() || !_hasXprintidle)
        {
            return TimeSpan.Zero;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "xprintidle",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(startInfo);
            if (process == null)
            {
                _hasXprintidle = false;
                return TimeSpan.Zero;
            }

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit(500);

            if (process.ExitCode == 0 && long.TryParse(output.Trim(), out long milliseconds))
            {
                _xprintidleChecked = true;
                return TimeSpan.FromMilliseconds(milliseconds);
            }
        }
        catch (Exception ex)
        {
            if (!_xprintidleChecked)
            {
                _hasXprintidle = false;
                _xprintidleChecked = true;
                CrashLogger.LogRecoverable(ex, "LinuxIdleDetectionService.GetIdleTime");
            }
        }

        return TimeSpan.Zero;
    }
}
