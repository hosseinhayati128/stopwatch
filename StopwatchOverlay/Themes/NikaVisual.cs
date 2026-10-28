using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls;
using System.Windows.Data;
using System.Globalization;
using System.Collections;
using System.Linq;
using System.Text.RegularExpressions;

namespace StopwatchOverlay.Themes;

/// <summary>Inherited controller-only theme scope; choice resources update without touching timer state.</summary>
public static class NikaVisual
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(NikaVisual), new FrameworkPropertyMetadata(false,
            FrameworkPropertyMetadataOptions.Inherits | FrameworkPropertyMetadataOptions.AffectsMeasure, OnEnabledChanged));
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);

    // The controller gets its own palette. Every other Nika window retains the
    // established one piece resources, and theme switching removes this scope.
    private static void OnEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not Window window || window.GetType().Name is not ("ControllerWindow" or "Window")) return;
        var dictionaries = window.Resources.MergedDictionaries;
        var local = dictionaries.FirstOrDefault(d => d.Source?.OriginalString.EndsWith("/Nika.xaml", StringComparison.Ordinal) == true);
        if ((bool)e.NewValue && local == null)
            dictionaries.Add(new ResourceDictionary { Source = new Uri("pack://application:,,,/StopwatchOverlay;component/Themes/Nika.xaml") });
        else if (!(bool)e.NewValue && local != null) dictionaries.Remove(local);
    }

    public static readonly DependencyProperty CompactProperty = DependencyProperty.RegisterAttached(
        "Compact", typeof(bool), typeof(NikaVisual), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));
    public static bool GetCompact(DependencyObject target) => (bool)target.GetValue(CompactProperty);
    public static void SetCompact(DependencyObject target, bool value) => target.SetValue(CompactProperty, value);

    public static void ApplyChoices(AppSettings settings)
    {
        var app = Application.Current;
        if (app == null) return;
        string typography = NikaThemeCatalog.NormalizeTypography(settings.NikaTypography);
        app.Resources["NikaHeadingFont"] = typography switch
        {
            "typewriter-log" => new FontFamily("Courier New"),
            "chartmakers-script" => Bundled("Pirate", "Almendra"),
            "navigator-precision" => Bundled("Acanthus", "Inter"),
            _ => new FontFamily("Times New Roman")
        };
        app.Resources["NikaDigitFont"] = typography switch
        {
            "typewriter-log" => Bundled("Acanthus", "Cascadia Mono"),
            "chartmakers-script" => Bundled("Pirate", "Almendra"),
            "navigator-precision" => Bundled("Acanthus", "Inter"),
            _ => Bundled("Nika", "Roboto Condensed Black")
        };
        string background = NikaThemeCatalog.NormalizeBackground(settings.NikaBackground);
        app.Resources["NikaMapBrush"] = CreateMap(background);
        var navy = new DrawingGroup();
        navy.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(0, 31, 49)), null, new RectangleGeometry(new Rect(0, 0, 1000, 1000))));
        navy.Children.Add(new GeometryDrawing(new ImageBrush(Image("navy-map.png")) { Stretch = Stretch.UniformToFill, Opacity = .38 }, null, new RectangleGeometry(new Rect(0, 0, 1000, 1000))));
        app.Resources["NikaNavyBrush"] = new DrawingBrush(navy) { Stretch = Stretch.Fill };
        app.Resources["NikaMuralBrush"] = CreateMural(NikaThemeCatalog.NormalizeArtwork(settings.NikaSidebarArtwork));
        app.Resources["NikaMuralOpacity"] = NikaThemeCatalog.NormalizeArtwork(settings.NikaSidebarArtwork) == "sun-warrior" ? 1d : .4d;
    }

    private static Brush CreateMap(string background)
    {
        var image = Image(NikaThemeCatalog.GetBackgroundAsset(background));
        if (background is not ("original-map" or "amber-atlas"))
            return new ImageBrush(image)
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Right,
                AlignmentY = AlignmentY.Center
            };
        var mapImage = new ImageBrush(image) { Stretch = Stretch.Fill,
            Opacity = background == "amber-atlas" ? .58 : 1 };
        var drawing = new DrawingGroup();
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromRgb(216, 180, 115)), null,
            new RectangleGeometry(new Rect(0, 0, 1000, 1000))));
        drawing.Children.Add(new GeometryDrawing(mapImage, null, new RectangleGeometry(new Rect(0, 0, 1000, 1000))));
        drawing.Children.Add(new GeometryDrawing(new SolidColorBrush(Color.FromArgb(39, 215, 190, 137)), null, new RectangleGeometry(new Rect(0, 0, 1000, 1000))));
        return new DrawingBrush(drawing) { Stretch = Stretch.Fill };
    }

    private static FontFamily Bundled(string folder, string family) => new(
        new Uri("pack://application:,,,/StopwatchOverlay;component/"), $"./Assets/Fonts/{folder}/#{family}");
    private static BitmapImage Image(string name) => new(new Uri($"pack://application:,,,/StopwatchOverlay;component/Assets/Nika/{name}"));
    private static Brush CreateMural(string id)
    {
        var region = NikaThemeCatalog.GetArtworkRegion(id);
        if (region == null) return new ImageBrush(new CroppedBitmap(Image("approved-art-atlas.png"), new Int32Rect(23, 550, 350, 338)))
        {
            // Preserve the approved mural itself; this rectangle contains only sidebar artwork.
            Stretch = Stretch.Uniform
        };
        var crop = region.Value;
        var atlas = Image("mural-atlas.png");
        return new ImageBrush(new CroppedBitmap(atlas, new Int32Rect(
            (int)(crop.X * atlas.PixelWidth), (int)(crop.Y * atlas.PixelHeight),
            (int)(crop.Width * atlas.PixelWidth), (int)(crop.Height * atlas.PixelHeight))))
        {
            Stretch = Stretch.Uniform
        };
    }
}

/// <summary>Formats the same persisted lap strings into responsive, real table columns.</summary>
public sealed class NikaLapFieldConverter : IMultiValueConverter
{
    private static readonly Regex FullElapsed = new(@"^\d{2,}:\d{2}:\d{2}(?:\.\d+)?$", RegexOptions.Compiled);
    private static readonly Regex PreviewColumns = new(@"^\s*\d+\s+(?<split>\S+)\s+(?<total>\S+)\s*$", RegexOptions.Compiled);
    private static string Total(string text)
    {
        var columns = PreviewColumns.Match(text);
        if (columns.Success) return columns.Groups["total"].Value;
        int colon = text.IndexOf(':');
        return text.StartsWith("Lap ", StringComparison.OrdinalIgnoreCase) && colon >= 0 ? text[(colon + 1)..].Trim() : text.Trim();
    }
    private static bool TryElapsed(string text, out TimeSpan elapsed)
    {
        elapsed = default;
        if (!FullElapsed.IsMatch(text)) return false;
        var parts = text.Split(':');
        if (!long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out long hours)
            || !long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out long minutes)
            || minutes is < 0 or >= 60
            || !decimal.TryParse(parts[2], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out decimal seconds)
            || seconds is < 0 or >= 60) return false;
        decimal ticks = ((decimal)hours * 3600 + minutes * 60 + seconds) * TimeSpan.TicksPerSecond;
        if (ticks < 0 || ticks > long.MaxValue) return false;
        elapsed = TimeSpan.FromTicks((long)ticks);
        return true;
    }
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        string text = values.FirstOrDefault()?.ToString() ?? "";
        int column = int.TryParse(parameter?.ToString(), out int field) ? field : 0;
        if (column == 0) return int.TryParse(Regex.Match(text, @"\d+").Value, out int index) ? index.ToString("00") : "—";
        string total = Total(text);
        if (column == 2) return total;
        var columns = PreviewColumns.Match(text);
        if (columns.Success) return TryElapsed(columns.Groups["split"].Value, out _) ? columns.Groups["split"].Value : "—";
        if (values.ElementAtOrDefault(1) is IEnumerable source)
        {
            var rows = source.Cast<object>().Select(v => v.ToString() ?? "").ToArray();
            int position = Array.IndexOf(rows, text);
            if (position >= 0 && TryElapsed(total, out var current))
            {
                TimeSpan previous = TimeSpan.Zero;
                if (position + 1 < rows.Length && !TryElapsed(Total(rows[position + 1]), out previous)) return "—";
                var split = current - previous;
                if (split < TimeSpan.Zero) return "—";
                string fraction = total.Contains('.') ? $".{split.Milliseconds / 100}" : "";
                return $"{(long)split.TotalHours:00}:{split.Minutes:00}:{split.Seconds:00}{fraction}";
            }
        }
        return "—";
    }
    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Scale markings are vector artwork, independent from the live timer glyphs.</summary>
public sealed class NikaNavigationArc : FrameworkElement
{
    protected override void OnRender(DrawingContext dc)
    {
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var brush = new SolidColorBrush(Color.FromArgb(185, 165, 115, 45));
        var thin = new Pen(brush, .8);
        double radiusX = ActualWidth / 2, radiusY = ActualHeight * .88;
        var center = new Point(radiusX, ActualHeight * .57);
        foreach (int sign in new[] { -1, 1 })
        {
            Point PointAt(double angle, double inset = 0) => new(center.X + sign * (radiusX - inset) * Math.Cos(angle), center.Y + (radiusY - inset) * Math.Sin(angle));
            var arc = new StreamGeometry();
            using (var path = arc.Open())
            {
                path.BeginFigure(PointAt(-.69), false, false);
                for (int i = 1; i <= 90; i++) path.LineTo(PointAt(-.69 + i * 1.23 / 90), true, false);
            }
            dc.DrawGeometry(null, thin, arc);
            for (int i = 0; i <= 46; i++)
            {
                double angle = -.69 + i * 1.23 / 46;
                bool major = i % 8 == 0;
                dc.DrawLine(new Pen(brush, major ? 3.5 : .8), PointAt(angle, 4), PointAt(angle, major ? 17 : 10));
            }
            var diamond = new StreamGeometry();
            using (var path = diamond.Open())
            {
                double x = center.X + sign * radiusX;
                path.BeginFigure(new Point(x, center.Y - 13), true, true);
                path.PolyLineTo([new Point(x + 4, center.Y - 3), new Point(x + 13, center.Y), new Point(x + 4, center.Y + 3), new Point(x, center.Y + 13), new Point(x - 4, center.Y + 3), new Point(x - 13, center.Y), new Point(x - 4, center.Y - 3)], true, true);
            }
            dc.DrawGeometry(null, thin, diamond);
        }
    }
}

/// <summary>Small navigation symbols are drawn from the actual timer's project name.</summary>
public sealed class NikaRailIcon : FrameworkElement
{
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(NikaRailIcon), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsRender));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    protected override void OnRender(DrawingContext dc)
    {
        var gold = new SolidColorBrush(Color.FromRgb(247, 206, 137)); var pen = new Pen(gold, 1.5);
        string name = Label.ToLowerInvariant();
        dc.PushTransform(new ScaleTransform(ActualWidth / 36, ActualHeight / 36));
        if (name.Contains("nav"))
        {
            dc.DrawEllipse(null, pen, new Point(18, 19), 14, 14);
            dc.DrawGeometry(gold, null, Geometry.Parse("M12,27 L24,9 20,23Z"));
            dc.DrawEllipse(gold, null, new Point(21, 2), 1.6, 1.6);
        }
        else
        {
            string path = name.Contains("read") ? "M18,32 L18,7 C12,2 5,4 2,8 L2,31 C7,27 13,28 18,32 M18,7 C24,2 31,4 34,8 L34,31 C28,27 23,28 18,32"
                : name.Contains("illustr") || name.Contains("paint") || name.Contains("draw") ? "M4,33 C11,34 14,30 16,24 L10,19 C7,23 7,27 4,33 M11,19 L29,2 34,0 30,8 17,25Z"
                : name.Contains("program") || name.Contains("code") ? "M12,8 L3,18 12,28 M24,8 L33,18 24,28 M21,4 L15,32"
                : "M6,2 L25,2 31,8 31,34 6,34Z M24,2 L24,10 31,10 M10,14 L25,14 M10,20 L25,20 M10,26 L21,26";
            dc.DrawGeometry(null, pen, Geometry.Parse(path));
        }
        dc.Pop();
    }
}

/// <summary>Clip only the themed controller frame, including the header corners.</summary>
public sealed class NikaFrameBorder : Border
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        if (Child != null) Child.Clip = NikaVisual.GetEnabled(this)
            ? new RectangleGeometry(new Rect(new Point(), Child.RenderSize), 8, 8) : null;
        return result;
    }
}
