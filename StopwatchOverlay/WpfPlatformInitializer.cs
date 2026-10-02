using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StopwatchOverlay;

internal static class WpfPlatformInitializer
{
    [ModuleInitializer]
    internal static void Initialize()
    {
        AppUiScale.Applier = ApplyUiScale;
        Shortcut.KeyNameFormatter = vk => KeyInterop.KeyFromVirtualKey((int)vk).ToString();

        CrashLogger.ThemeProvider = () => AppThemeManager.CurrentTheme;
        CrashLogger.WindowSnapshotProvider = GetOpenWindowSnapshot;

        AppBackgroundCatalog.ImageDimensionReader = ReadImageDimensions;
        AppBackgroundCatalog.ImageValidator = ValidateImage;

        Platform.PlatformServices.IdleDetection = new Platform.Windows.WindowsIdleDetectionService();
        Platform.PlatformServices.Startup = new Platform.Windows.WindowsStartupService();
        Platform.PlatformServices.WindowOverlay = new Platform.Windows.WindowsOverlayService();
        Platform.PlatformServices.SingleInstance = new Platform.Windows.WindowsSingleInstanceService();
    }

    private static void ApplyUiScale(double percent)
    {
        if (Application.Current is not { } app) return;
        double factor = AppUiScale.Normalize(percent) / 100;
        if (app.Resources["ApplicationScaleTransform"] is ScaleTransform current
            && current.ScaleX == factor && current.ScaleY == factor) return;
        var transform = new ScaleTransform(factor, factor);
        transform.Freeze();
        app.Resources["ApplicationScaleTransform"] = transform;
    }

    private static string? GetOpenWindowSnapshot()
    {
        Application? application = Application.Current;
        if (application == null || !application.Dispatcher.CheckAccess())
            return null;

        string[] windowTypes = application.Windows
            .OfType<Window>()
            .Where(window => window.IsLoaded)
            .Select(window => window.GetType().Name)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        return windowTypes.Length == 0 ? "None" : string.Join(",", windowTypes);
    }

    private static (int Width, int Height) ReadImageDimensions(Stream stream, string extension)
    {
        BitmapDecoder decoder = CreateDecoder(stream, extension, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        BitmapFrame frame = decoder.Frames[0];
        return (frame.PixelWidth, frame.PixelHeight);
    }

    private static (int Width, int Height) ValidateImage(Stream stream, string extension)
    {
        BitmapDecoder metadataDecoder = CreateDecoder(stream, extension, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None);
        BitmapFrame frame = metadataDecoder.Frames[0];
        int width = frame.PixelWidth;
        int height = frame.PixelHeight;

        stream.Position = 0;
        BitmapDecoder validationDecoder = CreateDecoder(stream, extension, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
        _ = validationDecoder.Frames[0].PixelWidth;
        return (width, height);
    }

    private static BitmapDecoder CreateDecoder(
        Stream stream,
        string extension,
        BitmapCreateOptions createOptions,
        BitmapCacheOption cacheOption) => extension switch
        {
            ".jpg" or ".jpeg" => new JpegBitmapDecoder(stream, createOptions, cacheOption),
            ".png" => new PngBitmapDecoder(stream, createOptions, cacheOption),
            ".bmp" => new BmpBitmapDecoder(stream, createOptions, cacheOption),
            _ => throw new NotSupportedException("Unsupported image type.")
        };
}
