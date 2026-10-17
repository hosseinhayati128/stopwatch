using System;
using System.IO;
using System.Linq;

namespace StopwatchOverlay.Platform.Linux;

/// <summary>Manages the current user's XDG login startup registration.</summary>
public sealed class LinuxStartupService : IStartupService
{
    private readonly string _desktopFilePath;
    private readonly string _executablePath;
    private readonly string _launcherPath;

    public LinuxStartupService(string? configHome = null, string? executablePath = null, string? launcherPath = null)
    {
        configHome ??= Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
        if (string.IsNullOrWhiteSpace(configHome) || !Path.IsPathRooted(configHome))
            configHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
        _desktopFilePath = Path.Combine(configHome, "autostart", "stopwatchoverlay.desktop");
        _executablePath = executablePath ?? Environment.ProcessPath ?? "stopwatch-overlay";
        string? dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (string.IsNullOrWhiteSpace(dataHome) || !Path.IsPathRooted(dataHome))
            dataHome = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
        _launcherPath = launcherPath ?? Path.Combine(dataHome, "applications", "stopwatch-overlay.desktop");
    }

    public bool IsSupported => OperatingSystem.IsLinux();

    public bool IsEnabled()
    {
        try
        {
            if (!File.Exists(_desktopFilePath)) return false;
            var lines = DesktopEntryLines(File.ReadAllText(_desktopFilePath));
            return !lines.Contains("Hidden=true", StringComparer.OrdinalIgnoreCase)
                && !lines.Contains("X-GNOME-Autostart-enabled=false", StringComparer.OrdinalIgnoreCase)
                && lines.Any(line => line.StartsWith("Exec=", StringComparison.Ordinal) && line.Length > 5);
        }
        catch { return false; }
    }

    public void SetEnabled(bool enabled)
    {
        try
        {
            if (!enabled)
            {
                if (File.Exists(_desktopFilePath)) File.Delete(_desktopFilePath);
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(_desktopFilePath)!);
            string command = QuoteArgument(_executablePath);
            // Use the installed app's launch command, retaining its configured Telegram proxy.
            if (File.Exists(_launcherPath))
            {
                var installedCommand = DesktopEntryLines(File.ReadAllText(_launcherPath))
                    .FirstOrDefault(line => line.StartsWith("Exec=", StringComparison.Ordinal));
                if (installedCommand != null && installedCommand.Contains(_executablePath, StringComparison.Ordinal))
                    command = installedCommand[5..];
            }
            string content = $"""
                [Desktop Entry]
                Type=Application
                Version=1.0
                Name=Stopwatch Overlay
                Comment=Transparent overlay stopwatch and project tracker
                Exec={command}
                Terminal=false
                Categories=Utility;Clock;
                StartupNotify=false
                X-GNOME-Autostart-enabled=true
                """;
            File.WriteAllText(_desktopFilePath, content + "\n");
        }
        catch (Exception ex) { CrashLogger.LogRecoverable(ex, "LinuxStartupService.SetEnabled"); }
    }

    private static string[] DesktopEntryLines(string content)
    {
        bool entry = false;
        return content.Split('\n').Select(line => line.Trim()).Where(line =>
        {
            if (line.StartsWith('[')) { entry = line == "[Desktop Entry]"; return false; }
            return entry && !line.StartsWith('#');
        }).ToArray();
    }

    private static string QuoteArgument(string value)
    {
        string escaped = value.Replace("%", "%%").Replace("\\", "\\\\").Replace("\"", "\\\"")
            .Replace("$", "\\$").Replace("`", "\\`");
        return "\"" + escaped.Replace("\\", "\\\\") + "\"";
    }
}
