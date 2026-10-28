using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace StopwatchOverlay.Desktop.Controls;

public sealed class NikaTimePartsConverter : Avalonia.Data.Converters.IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
    {
        string text = value?.ToString() ?? "";
        int decimalIndex = text.LastIndexOf('.');
        return parameter?.ToString() == "fraction"
            ? decimalIndex >= 0 ? text[decimalIndex..] : ""
            : decimalIndex >= 0 ? text[..decimalIndex] : text;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
        => throw new NotSupportedException();
}

/// <summary>Decorative embedded artwork; never participates in input or timer state.</summary>
public sealed class NikaArtwork : Control
{
    public static readonly StyledProperty<string> PartProperty = AvaloniaProperty.Register<NikaArtwork, string>(nameof(Part), "Map");
    public static readonly StyledProperty<string> ChoiceProperty = AvaloniaProperty.Register<NikaArtwork, string>(nameof(Choice), "sun-warrior");
    private static readonly Dictionary<string, Bitmap> Images = new();
    public string Part { get => GetValue(PartProperty); set => SetValue(PartProperty, value); }
    public string Choice { get => GetValue(ChoiceProperty); set => SetValue(ChoiceProperty, value); }
    static NikaArtwork() => AffectsRender<NikaArtwork>(PartProperty, ChoiceProperty);
    public NikaArtwork() { IsHitTestVisible = false; Focusable = false; }

    public override void Render(DrawingContext context)
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0) return;
        if (Part == "Dial")
        {
            DrawCircularDials(context);
            return;
        }
        if (Part == "Header")
        {
            DrawHeader(context);
            return;
        }
        string file = Part switch
        {
            "Navy" => "navy-map.png",
            "Map" => NikaThemeCatalog.GetBackgroundAsset(Choice),
            "LogPose" => "log-pose-reference.png",
            "Crest" => "controller-art-reference.png",
            _ => Choice == "sun-warrior" ? "warrior-reference-flat.png" : "mural-atlas.png"
        };
        var bitmap = Load(file);
        var source = new Rect(bitmap.Size);
        if (Part == "Crest") source = ReferenceRegion(bitmap, new Rect(52, 31, 93, 75));
        if (file == "mural-atlas.png")
        {
            if (NikaThemeCatalog.GetArtworkRegion(Choice) is { } crop)
                source = new Rect(crop.X * bitmap.Size.Width, crop.Y * bitmap.Size.Height,
                    crop.Width * bitmap.Size.Width, crop.Height * bitmap.Size.Height);
        }
        Rect destination = new(Bounds.Size);
        if (Part is "Map" or "Navy")
            source = CoverSource(source, Bounds.Size, Part == "Map" ? 1 : .5);
        else
        {
            double scale = Math.Min(Bounds.Width / source.Width, Bounds.Height / source.Height);
            destination = new Rect((Bounds.Width - source.Width * scale) / 2, (Bounds.Height - source.Height * scale) / 2, source.Width * scale, source.Height * scale);
        }
        context.DrawImage(bitmap, source, destination);
        if (Part == "Navy")
            context.FillRectangle(new SolidColorBrush(Color.Parse("#16032437")), new Rect(Bounds.Size));
        if (Part == "Map" && file == "map.png")
            context.FillRectangle(new SolidColorBrush(Color.Parse("#0C80582A")), new Rect(Bounds.Size));
        if (Part == "Map" && Choice == "amber-atlas")
            context.FillRectangle(new SolidColorBrush(Color.Parse("#30A96A10")), new Rect(Bounds.Size));
    }

    private static Bitmap Load(string file)
    {
        if (!Images.TryGetValue(file, out var bitmap))
            Images[file] = bitmap = new Bitmap(AssetLoader.Open(new Uri($"avares://StopwatchOverlay.Desktop/Assets/Nika/{file}")));
        return bitmap;
    }

    // Crop the source rather than changing the illustration's aspect ratio.
    internal static Rect CoverSource(Rect source, Size target, double horizontal = .5)
    {
        double scale = Math.Max(target.Width / source.Width, target.Height / source.Height);
        double width = target.Width / scale, height = target.Height / scale;
        return new Rect(source.X + (source.Width - width) * horizontal,
            source.Y + (source.Height - height) / 2, width, height);
    }

    private static Rect ReferenceRegion(Bitmap bitmap, Rect region)
        => new(region.X / 1536 * bitmap.Size.Width, region.Y / 1024 * bitmap.Size.Height,
            region.Width / 1536 * bitmap.Size.Width, region.Height / 1024 * bitmap.Size.Height);

    private void DrawHeader(DrawingContext context)
    {
        var texture = Load("navy-map.png");
        context.DrawImage(texture, CoverSource(new Rect(texture.Size), Bounds.Size), new Rect(Bounds.Size));
        context.FillRectangle(Brush.Parse("#30002235"), new Rect(Bounds.Size));
        var reference = Load("controller-art-reference.png");
        double scale = Bounds.Height / 96;
        var fade = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, .5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, .5, RelativeUnit.Relative),
            GradientStops = new GradientStops { new(Colors.Transparent, 0), new(Colors.White, .12), new(Colors.White, .88), new(Colors.Transparent, 1) }
        };
        void Patch(Rect region, double x, double y)
        {
            var destination = new Rect(x, y * scale, region.Width * scale, region.Height * scale);
            using (context.PushOpacityMask(fade, destination))
                context.DrawImage(reference, ReferenceRegion(reference, region), destination);
        }
        // These regions contain only cartography/islands, never reference text.
        Patch(new Rect(575, 20, 220, 94), Bounds.Width * .37, 0);
        Patch(new Rect(700, 58, 190, 56), Bounds.Width * .46, 38);
        Patch(new Rect(810, 84, 694, 30), Bounds.Width - 694 * scale, 64);
        Patch(new Rect(1374, 58, 139, 56), Bounds.Width - 139 * scale, 38);
    }

    internal static (Point Center, double Radius, double Angle)[] CircularDials(Size size)
    {
        double radius = size.Height * .60;
        double inset = size.Width * .065;
        return [ (new Point(inset + radius, size.Height / 2), radius, Math.PI),
            (new Point(size.Width - inset - radius, size.Height / 2), radius, 0) ];
    }

    private void DrawCircularDials(DrawingContext context)
    {
        var fine = new Pen(Brush.Parse("#B68B4D"), .85);
        var major = new Pen(Brush.Parse("#AB7C3D"), 2.4);
        foreach (var dial in CircularDials(Bounds.Size))
        {
            Point At(double angle, double inset = 0) => new(dial.Center.X + (dial.Radius - inset) * Math.Cos(angle),
                dial.Center.Y + (dial.Radius - inset) * Math.Sin(angle));
            var line = new StreamGeometry();
            using (var drawing = line.Open())
            {
                drawing.BeginFigure(At(dial.Angle - 1), false);
                drawing.ArcTo(At(dial.Angle + 1), new Size(dial.Radius, dial.Radius), 0, false, SweepDirection.Clockwise);
                drawing.EndFigure(false);
            }
            context.DrawGeometry(null, fine, line);
            for (int tick = -20; tick <= 20; tick++)
                context.DrawLine(tick % 10 == 0 ? major : fine,
                    At(dial.Angle + tick * .05, 4), At(dial.Angle + tick * .05, tick % 10 == 0 ? 19 : tick % 5 == 0 ? 14 : 10));
            var point = At(dial.Angle);
            context.DrawLine(fine, new Point(point.X - 13, point.Y), new Point(point.X + 13, point.Y));
            context.DrawLine(fine, new Point(point.X, point.Y - 13), new Point(point.X, point.Y + 13));
            var star = new StreamGeometry();
            using (var drawing = star.Open())
            {
                drawing.BeginFigure(new Point(point.X, point.Y - 7), false);
                drawing.LineTo(new Point(point.X + 5, point.Y));
                drawing.LineTo(new Point(point.X, point.Y + 7));
                drawing.LineTo(new Point(point.X - 5, point.Y));
                drawing.EndFigure(true);
            }
            context.DrawGeometry(null, fine, star);
        }
    }
}
