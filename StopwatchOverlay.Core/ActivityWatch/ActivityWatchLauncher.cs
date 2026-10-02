using System;
using System.Diagnostics;
using System.IO;

namespace StopwatchOverlay.ActivityWatch;

public static class ActivityWatchLauncher
{
    public static bool TryFindExecutable(out string exePath)
    {
        exePath = "";

        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

        string[] candidates =
        [
            Path.Combine(localAppData, "Programs", "ActivityWatch", "aw-qt.exe"),
            Path.Combine(localAppData, "activitywatch", "aw-qt.exe"),
            Path.Combine(programFiles, "ActivityWatch", "aw-qt.exe"),
            Path.Combine(programFilesX86, "ActivityWatch", "aw-qt.exe")
        ];

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                exePath = candidate;
                return true;
            }
        }

        return false;
    }

    public static bool TryLaunch()
    {
        if (TryFindExecutable(out string exePath))
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = exePath,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(exePath)
                });
                return true;
            }
            catch (Exception ex)
            {
                CrashLogger.LogRecoverable(ex, "ActivityWatchLauncher.TryLaunch");
                return false;
            }
        }

        return false;
    }
}
