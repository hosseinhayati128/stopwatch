using System;
using System.IO;

namespace StopwatchOverlay.Platform.Linux;

/// <summary>
/// Manages autostart on Linux desktop environments using the XDG Autostart specification (~/.config/autostart/*.desktop).
/// </summary>
public sealed class LinuxStartupService : IStartupService
{
    private const string DesktopFileName = "stopwatchoverlay.desktop";

    public bool IsSupported => OperatingSystem.IsLinux();

    private static string AutostartDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".config",
        "autostart");

    private static string DesktopFilePath => Path.Combine(AutostartDirectory, DesktopFileName);

    public bool IsEnabled()
    {
        try
        {
            return File.Exists(DesktopFilePath);
        }
        catch
        {
            return false;
        }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            if (enabled)
            {
                Directory.CreateDirectory(AutostartDirectory);
                string execPath = Environment.ProcessPath ?? "stopwatchoverlay";
                string content = $"""
                    [Desktop Entry]
                    Type=Application
                    Version=1.0
                    Name=Stopwatch Overlay
                    Comment=Transparent overlay stopwatch and project tracker
                    Exec="{execPath}"
                    Terminal=false
                    Categories=Utility;Clock;
                    StartupNotify=false
                    X-GNOME-Autostart-enabled=true
                    """;
                File.WriteAllText(DesktopFilePath, content);
            }
            else
            {
                if (File.Exists(DesktopFilePath))
                {
                    File.Delete(DesktopFilePath);
                }
            }
        }
        catch (Exception ex)
        {
            CrashLogger.LogRecoverable(ex, "LinuxStartupService.SetEnabled");
        }
    }
}
