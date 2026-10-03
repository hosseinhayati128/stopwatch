using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Media.Imaging;
using StopwatchOverlay.Desktop.Platform;
using StopwatchOverlay.Platform;
using StopwatchOverlay.Platform.Linux;
using StopwatchOverlay.Platform.Windows;

namespace StopwatchOverlay.Desktop;

internal static class DesktopPlatformInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        Shortcut.KeyNameFormatter = FormatVirtualKey;

        CrashLogger.ThemeProvider = () => AppThemeCatalog.Midnight;
        CrashLogger.WindowSnapshotProvider = GetOpenWindowSnapshot;

        AppBackgroundCatalog.ImageDimensionReader = ReadImageDimensions;
        AppBackgroundCatalog.ImageValidator = ValidateImage;

        if (OperatingSystem.IsWindows())
        {
            PlatformServices.IdleDetection = new WindowsIdleDetectionService();
            PlatformServices.Startup = new WindowsStartupService();
            PlatformServices.WindowOverlay = new WindowsOverlayService();
            PlatformServices.SingleInstance = new WindowsSingleInstanceService();
        }
        else if (OperatingSystem.IsLinux())
        {
            PlatformServices.IdleDetection = new LinuxIdleDetectionService();
            PlatformServices.Startup = new LinuxStartupService();
            PlatformServices.SingleInstance = new LinuxSingleInstanceService();
        }

        PlatformServices.HotKey = new SharpHookHotKeyService();
    }

    private static string FormatVirtualKey(uint vk) => vk switch
    {
        >= 0x41 and <= 0x5A => ((char)vk).ToString(),
        >= 0x30 and <= 0x39 => ((char)vk).ToString(),
        >= 0x70 and <= 0x87 => $"F{vk - 0x70 + 1}",
        0x20 => "Space",
        0x1B => "Escape",
        0x09 => "Tab",
        0x0D => "Enter",
        0x08 => "Backspace",
        0x2D => "Insert",
        0x2E => "Delete",
        0x24 => "Home",
        0x23 => "End",
        0x21 => "PageUp",
        0x22 => "PageDown",
        0x25 => "Left",
        0x26 => "Up",
        0x27 => "Right",
        0x28 => "Down",
        _ => $"Key_{vk:X2}"
    };

    private static string? GetOpenWindowSnapshot()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        string[] windowTypes = desktop.Windows
            .Select(w => w.GetType().Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        return windowTypes.Length == 0 ? "None" : string.Join(",", windowTypes);
    }

    private static (int Width, int Height) ReadImageDimensions(Stream stream, string extension)
    {
        using var bmp = new Bitmap(stream);
        return (bmp.PixelSize.Width, bmp.PixelSize.Height);
    }

    private static (int Width, int Height) ValidateImage(Stream stream, string extension)
    {
        using var bmp = new Bitmap(stream);
        return (bmp.PixelSize.Width, bmp.PixelSize.Height);
    }
}
