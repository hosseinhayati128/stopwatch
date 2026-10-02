using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StopwatchOverlay;

public static class AppBackgroundManager
{
    private const int MaximumCachedImages = 12;
    private static readonly Dictionary<string, BitmapSource> ImageCache =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly LinkedList<string> ImageCacheOrder = new();

    private static string? _baseTheme;
    private static Brush? _baseBackground;
    private static BitmapSource? _currentPattern;
    private static double _currentStrength = AppBackgroundCatalog.DefaultPatternStrength;

    public static bool HasPattern => _currentPattern != null;

    internal static void InvalidateThemeBase()
    {
        _baseTheme = null;
        _baseBackground = null;
        _currentPattern = null;
    }

    public static bool Apply(AppSettings settings, out string? warning)
    {
        ArgumentNullException.ThrowIfNull(settings);
        warning = null;
        var application = Application.Current;
        if (application == null)
            return false;

        AppBackgroundCatalog.NormalizeSettings(settings);
        string theme = AppThemeCatalog.Normalize(settings.ThemeMode);
        if (_baseBackground == null || _baseTheme != theme)
        {
            _baseBackground = application.Resources["AppBackgroundBrush"] is Brush current
                ? current.Clone()
                : new SolidColorBrush(Colors.Transparent);
            _baseTheme = theme;
        }

        string requestedId = settings.PanelBackgroundId;
        AppBackgroundChoice choice = AppBackgroundCatalog.ResolveChoice(settings);
        if (choice.IsThemeDefault
            && !requestedId.Equals(
                AppBackgroundCatalog.ThemeDefault,
                StringComparison.OrdinalIgnoreCase))
        {
            warning = "The saved custom background is missing; Theme default is being used.";
        }
        _currentStrength = settings.PanelBackgroundStrength;
        _currentPattern = null;

        DrawingBrush next;
        if (choice.IsThemeDefault)
        {
            next = EnsureDrawingBrush(_baseBackground.Clone());
        }
        else
        {
            try
            {
                _currentPattern = LoadImage(choice);
                next = CreateTiledBrush(
                    _baseBackground.Clone(),
                    _currentPattern,
                    _currentStrength,
                    opacity: 1);
            }
            catch (Exception exception) when (
                AppBackgroundCatalog.IsExpectedImageBoundaryFailure(exception))
            {
                CrashLogger.LogRecoverable(exception, "BackgroundImageApply");
                warning = $"{choice.DisplayName} could not be loaded; Theme default is being used.";
                settings.PanelBackgroundId = AppBackgroundCatalog.ThemeDefault;
                next = EnsureDrawingBrush(_baseBackground.Clone());
            }
        }

        ApplyApplicationBrush(application, next);
        return warning == null;
    }

    public static Brush CreatePreviewBrush(
        AppBackgroundChoice choice,
        double strength)
    {
        if (choice.IsThemeDefault || !choice.IsAvailable)
            return _baseBackground?.Clone() ?? new SolidColorBrush(Colors.Transparent);

        try
        {
            BitmapSource image = LoadImage(choice);
            DrawingBrush brush = CreateTiledBrush(
                _baseBackground?.Clone() ?? new SolidColorBrush(Colors.Transparent),
                image,
                strength,
                opacity: 1);
            double previewScale = Math.Min(
                54d / brush.Viewbox.Width,
                38d / brush.Viewbox.Height);
            brush.Viewport = new Rect(
                0,
                0,
                Math.Max(4, brush.Viewbox.Width * previewScale),
                Math.Max(4, brush.Viewbox.Height * previewScale));
            brush.Freeze();
            return brush;
        }
        catch (Exception exception) when (
            AppBackgroundCatalog.IsExpectedImageBoundaryFailure(exception))
        {
            return _baseBackground?.Clone() ?? new SolidColorBrush(Colors.Transparent);
        }
    }

    public static Brush CreateOverlaySurfaceBrush(Color baseColor, double opacity)
    {
        double clampedOpacity = Math.Clamp(opacity, 0, 1);
        if (_currentPattern == null)
        {
            return new SolidColorBrush(Color.FromArgb(
                (byte)Math.Round(clampedOpacity * 255),
                baseColor.R,
                baseColor.G,
                baseColor.B));
        }

        DrawingBrush brush = CreateTiledBrush(
            new SolidColorBrush(Color.FromRgb(baseColor.R, baseColor.G, baseColor.B)),
            _currentPattern,
            _currentStrength,
            clampedOpacity);
        brush.Freeze();
        return brush;
    }

    public static void ClearImageCache()
    {
        ImageCache.Clear();
        ImageCacheOrder.Clear();
    }

    private static BitmapSource LoadImage(AppBackgroundChoice choice)
    {
        string key = choice.ResourceUri ?? choice.FilePath
            ?? throw new InvalidOperationException("The background has no image source.");
        if (ImageCache.TryGetValue(key, out BitmapSource? cached))
        {
            ImageCacheOrder.Remove(key);
            ImageCacheOrder.AddLast(key);
            return cached;
        }

        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
        if (choice.FilePath != null)
        {
            using var stream = new FileStream(
                choice.FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read);
            BitmapDecoder metadata = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.DelayCreation,
                BitmapCacheOption.None);
            BitmapFrame frame = metadata.Frames[0];
            if (frame.PixelWidth >= frame.PixelHeight)
                image.DecodePixelWidth = 768;
            else
                image.DecodePixelHeight = 768;
        }
        else
        {
            // Bound decoded preset size while preserving the source aspect ratio.
            image.DecodePixelHeight = 768;
        }
        image.UriSource = new Uri(key, UriKind.RelativeOrAbsolute);
        image.EndInit();
        image.Freeze();

        while (ImageCacheOrder.Count >= MaximumCachedImages)
        {
            string oldest = ImageCacheOrder.First!.Value;
            ImageCacheOrder.RemoveFirst();
            ImageCache.Remove(oldest);
        }
        ImageCache[key] = image;
        ImageCacheOrder.AddLast(key);
        return image;
    }

    private static DrawingBrush CreateTiledBrush(
        Brush baseBrush,
        BitmapSource image,
        double strength,
        double opacity)
    {
        (double width, double height) = CalculateTileSize(image.PixelWidth, image.PixelHeight);
        var bounds = new Rect(0, 0, width, height);
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(
            baseBrush,
            null,
            new RectangleGeometry(bounds)));

        var imageLayer = new DrawingGroup
        {
            Opacity = Math.Clamp(strength, 0, 100) / 100d
        };
        imageLayer.Children.Add(new ImageDrawing(image, bounds));
        drawing.Children.Add(imageLayer);

        return new DrawingBrush(drawing)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = bounds,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = bounds,
            Stretch = Stretch.Fill,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top,
            Opacity = opacity
        };
    }

    private static (double Width, double Height) CalculateTileSize(int pixelWidth, int pixelHeight)
    {
        double width = Math.Max(1, pixelWidth);
        double height = Math.Max(1, pixelHeight);
        double scale = Math.Min(360d / width, 520d / height);
        return (Math.Max(96, width * scale), Math.Max(96, height * scale));
    }

    private static DrawingBrush EnsureDrawingBrush(Brush brush)
    {
        if (brush is DrawingBrush drawingBrush)
            return drawingBrush;

        var bounds = new Rect(0, 0, 1, 1);
        return new DrawingBrush(new GeometryDrawing(
            brush,
            null,
            new RectangleGeometry(bounds)))
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = bounds,
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = bounds
        };
    }

    private static void ApplyApplicationBrush(Application application, DrawingBrush next)
    {
        if (application.Resources["AppBackgroundBrush"] is DrawingBrush current
            && !current.IsFrozen)
        {
            current.Drawing = next.Drawing?.Clone();
            current.TileMode = next.TileMode;
            current.ViewportUnits = next.ViewportUnits;
            current.Viewport = next.Viewport;
            current.ViewboxUnits = next.ViewboxUnits;
            current.Viewbox = next.Viewbox;
            current.Stretch = next.Stretch;
            current.AlignmentX = next.AlignmentX;
            current.AlignmentY = next.AlignmentY;
            current.Opacity = next.Opacity;
        }
        else
        {
            application.Resources["AppBackgroundBrush"] = next;
        }
    }
}
