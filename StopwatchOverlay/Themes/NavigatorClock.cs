using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace StopwatchOverlay.Themes;

/// <summary>Live clock with independently adjustable frame, map, metal, and text layers.</summary>
public sealed class NavigatorClock : FrameworkElement
{
    private static DependencyProperty VisualProperty<T>(string name, T value) => DependencyProperty.Register(
        name, typeof(T), typeof(NavigatorClock), new FrameworkPropertyMetadata(value,
            FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TextProperty = VisualProperty(nameof(Text), "00:00:00");
    public static readonly DependencyProperty ProjectNameProperty = VisualProperty(nameof(ProjectName), "");
    public static readonly DependencyProperty TextSizeProperty = VisualProperty(nameof(TextSize), 48d);
    public static readonly DependencyProperty TimerBrushProperty = VisualProperty<Brush>(nameof(TimerBrush), Brushes.Black);
    public static readonly DependencyProperty NameBrushProperty = VisualProperty<Brush>(nameof(NameBrush), Brushes.Black);
    public static readonly DependencyProperty TimerFontProperty = VisualProperty(nameof(TimerFont), new FontFamily("Consolas"));
    public static readonly DependencyProperty SurfaceOpacityProperty = VisualProperty(nameof(SurfaceOpacity), 1d);
    public static readonly DependencyProperty OutlineBrushProperty = VisualProperty<Brush>(nameof(OutlineBrush), Brushes.Black);
    public static readonly DependencyProperty OutlineWidthProperty = VisualProperty(nameof(OutlineWidth), 1d);
    public static readonly DependencyProperty RecVisibleProperty = VisualProperty(nameof(RecVisible), false);
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string ProjectName { get => (string)GetValue(ProjectNameProperty); set => SetValue(ProjectNameProperty, value); }
    public double TextSize { get => (double)GetValue(TextSizeProperty); set => SetValue(TextSizeProperty, value); }
    public Brush TimerBrush { get => (Brush)GetValue(TimerBrushProperty); set => SetValue(TimerBrushProperty, value); }
    public Brush NameBrush { get => (Brush)GetValue(NameBrushProperty); set => SetValue(NameBrushProperty, value); }
    public FontFamily TimerFont { get => (FontFamily)GetValue(TimerFontProperty); set => SetValue(TimerFontProperty, value); }
    public double SurfaceOpacity { get => (double)GetValue(SurfaceOpacityProperty); set => SetValue(SurfaceOpacityProperty, value); }
    public Brush OutlineBrush { get => (Brush)GetValue(OutlineBrushProperty); set => SetValue(OutlineBrushProperty, value); }
    public double OutlineWidth { get => (double)GetValue(OutlineWidthProperty); set => SetValue(OutlineWidthProperty, value); }
    public bool RecVisible { get => (bool)GetValue(RecVisibleProperty); set => SetValue(RecVisibleProperty, value); }

    private static BitmapImage Load(string name)
    {
        var image = new BitmapImage(new Uri($"pack://application:,,,/StopwatchOverlay;component/Assets/Pirate/navigator-{name}.png"));
        image.Freeze();
        return image;
    }
    private static readonly Lazy<BitmapImage> Map = new(() => Load("map"));
    // Trace the inner edge of the wooden frame in the source artwork coordinates.
    // The compass and diagonal ropes cross that edge and belong to the frame layer.
    private static Geometry PaperGeometry(double width, double height)
    {
        Geometry paper = new RectangleGeometry(new Rect(139, 151, 1274, 751));
        Geometry ornaments = new GeometryGroup
        {
            Children =
            {
                new EllipseGeometry(new Point(145, 148), 111, 115),
                Geometry.Parse("M1199,49 C1247,43 1260,65 1298,111 L1495,326 Q1510,352 1498,384 L1482,360 L1270,132 C1244,99 1220,82 1199,79Z"),
                Geometry.Parse("M1486,734 L1515,760 C1441,860 1331,951 1250,997 L1207,972 C1292,917 1410,817 1486,734Z")
            }
        };
        var result = new CombinedGeometry(GeometryCombineMode.Exclude, paper, ornaments)
        { Transform = new ScaleTransform(width / 1536, height / 1024) };
        return result;
    }
    private static readonly Lazy<ImageBrush> Plate = new(() =>
    {
        var brush = new ImageBrush(Load("plate")) { Viewbox = new Rect(0, .14, 1, .70), Stretch = Stretch.Fill };
        brush.Freeze();
        return brush;
    });
    private static readonly Geometry[] Segments = new[]
    {
        "M9,0H49L57,7 49,14H9L1,7Z", "M51,10L58,3V44L51,51 44,44V18Z",
        "M51,51L58,58V97L51,90 44,82V59Z", "M9,86H49L57,94 49,100H9L1,94Z",
        "M7,51L14,59V82L7,90 0,97V58Z", "M0,3L7,10 14,18V44L7,51 0,44Z",
        "M9,44H49L57,51 49,58H9L1,51Z"
    }.Select(path => { Geometry geometry = Geometry.Parse(path); geometry.Freeze(); return geometry; }).ToArray();
    private static readonly int[] DigitMasks = [0x3f, 0x06, 0x5b, 0x4f, 0x66, 0x6d, 0x7d, 0x07, 0x7f, 0x6f];
    private static readonly Brush Bevel = CreateBevel();
    private static Brush CreateBevel()
    {
        var brush = new SolidColorBrush(Color.FromRgb(239, 213, 151));
        brush.Freeze();
        return brush;
    }
    private double Em => double.IsFinite(TextSize) ? Math.Clamp(TextSize, 8, 210) : 48;
    private bool Digital => Text.All(c => char.IsAsciiDigit(c) || c is ':' or '.' or ',' or '-' or ' ')
        && (TimerFont.Source.Contains("Consolas", StringComparison.OrdinalIgnoreCase)
            || TimerFont.Source.Contains("Cascadia", StringComparison.OrdinalIgnoreCase)
            || TimerFont.Source.Contains("Barlow", StringComparison.OrdinalIgnoreCase));
    private static double Advance(char c) => c is ':' or ' ' ? 28 : c is '.' or ',' ? 22 : 70;
    private FormattedText Format(string text, FontFamily font, double size, Brush brush) => new(text,
        CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, new Typeface(font, FontStyles.Normal, FontWeights.Bold, FontStretches.Normal),
        size, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip);

    protected override Size MeasureOverride(Size availableSize)
    {
        double textWidth = Digital ? Text.Sum(Advance) * Em / 100 : Format(Text, TimerFont, Em, TimerBrush).WidthIncludingTrailingWhitespace;
        double width = Math.Max(Em * 8.6, (textWidth + Em * .15) / .53);
        return new Size(width, width * 2 / 3);
    }

    public Rect MetalBounds => new(ActualWidth * .155, ActualHeight * .352, ActualWidth * .705, ActualHeight * .264);
    public Rect MetalInsetBounds => new(MetalBounds.X + MetalBounds.Width * .117, MetalBounds.Y + MetalBounds.Height * .12,
        MetalBounds.Width * .766, MetalBounds.Height * .76);

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        if (ActualWidth <= 0 || ActualHeight <= 0) return;
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        Rect plate = MetalBounds;
        var inset = new RectangleGeometry(MetalInsetBounds, Em * .015, Em * .015);
        var parts = NavigatorVisual.GetOpaqueParts(this);
        double OpacityFor(NavigatorOpaqueParts part) => NavigatorVisual.LayerOpacity(parts, part, SurfaceOpacity);
        Geometry paper = PaperGeometry(ActualWidth, ActualHeight);
        Geometry frame = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(bounds), paper);
        foreach (var layer in new[] { (paper, NavigatorOpaqueParts.ClockMap), (frame, NavigatorOpaqueParts.ClockFrame) })
        {
            dc.PushOpacity(OpacityFor(layer.Item2));
            dc.PushClip(layer.Item1);
            dc.DrawImage(Map.Value, bounds);
            dc.Pop();
            dc.Pop();
        }
        dc.PushOpacity(OpacityFor(NavigatorOpaqueParts.MetalFill));
        dc.PushClip(inset);
        dc.DrawRectangle(Plate.Value, null, plate);
        dc.Pop();
        dc.Pop();

        var rim = new CombinedGeometry(GeometryCombineMode.Exclude, new RectangleGeometry(plate), inset);
        dc.PushOpacity(OpacityFor(NavigatorOpaqueParts.MetalBorder));
        dc.PushClip(rim);
        dc.DrawRectangle(Plate.Value, null, plate);
        dc.Pop();
        dc.Pop();
        dc.PushOpacity(OpacityFor(NavigatorOpaqueParts.TimerText));
        DrawTime(dc, plate);
        dc.Pop();
        if (!string.IsNullOrWhiteSpace(ProjectName))
        {
            dc.PushOpacity(OpacityFor(NavigatorOpaqueParts.ProjectName));
            FormattedText label = Format(ProjectName, new FontFamily("Times New Roman"), Em * .49, NameBrush);
            label.MaxTextWidth = ActualWidth * .77;
            label.MaxTextHeight = Em * .75;
            label.Trimming = TextTrimming.CharacterEllipsis;
            label.TextAlignment = TextAlignment.Center;
            dc.DrawText(label, new Point(ActualWidth * .125, ActualHeight * .655));
            dc.Pop();
        }
        if (RecVisible)
        {
            dc.DrawEllipse(Brushes.Firebrick, new Pen(Bevel, 1), new Point(ActualWidth * .875, ActualHeight * .31), Em * .07, Em * .07);
        }
    }

    private void DrawTime(DrawingContext dc, Rect plate)
    {
        if (!Digital)
        {
            var text = Format(Text, TimerFont, Em, TimerBrush);
            Geometry geometry = text.BuildGeometry(new Point(plate.X + (plate.Width - text.Width) / 2, plate.Y + (plate.Height - text.Height) / 2));
            dc.DrawGeometry(TimerBrush, new Pen(OutlineBrush, Math.Clamp(OutlineWidth, 0, 8)), geometry);
            return;
        }
        double width = Text.Sum(Advance) * Em / 100;
        dc.PushTransform(new TranslateTransform(plate.X + (plate.Width - width) / 2, plate.Y + (plate.Height - Em) / 2));
        dc.PushTransform(new ScaleTransform(Em / 100, Em / 100));
        double x = 0;
        var pen = new Pen(Bevel, 1.4);
        var outline = new Pen(OutlineBrush, Math.Clamp(OutlineWidth, 0, 8) * 100 / Em + pen.Thickness);
        void DrawSegment(Geometry geometry)
        {
            if (OutlineWidth > 0) dc.DrawGeometry(null, outline, geometry);
            dc.DrawGeometry(TimerBrush, pen, geometry);
        }
        void DrawDot(Point center, double radius)
        {
            if (OutlineWidth > 0) dc.DrawEllipse(null, outline, center, radius, radius);
            dc.DrawEllipse(TimerBrush, pen, center, radius, radius);
        }
        foreach (char c in Text)
        {
            dc.PushTransform(new TranslateTransform(x, 0));
            if (char.IsAsciiDigit(c) || c == '-')
            {
                int mask = c == '-' ? 0x40 : DigitMasks[c - '0'];
                for (int segment = 0; segment < 7; segment++)
                    if ((mask & (1 << segment)) != 0) DrawSegment(Segments[segment]);
                if (c == '0') DrawSegment(Geometry.Parse("M41,17L45,22 18,84 13,89 13,82Z"));
            }
            else if (c == ':')
            {
                DrawDot(new Point(10, 30), 7);
                DrawDot(new Point(10, 72), 7);
            }
            else if (c is '.' or ',') DrawDot(new Point(8, 93), 6);
            dc.Pop();
            x += Advance(c);
        }
        dc.Pop();
        dc.Pop();
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new ClockPeer(this);
    private sealed class ClockPeer(NavigatorClock owner) : FrameworkElementAutomationPeer(owner)
    {
        protected override string GetClassNameCore() => nameof(NavigatorClock);
        protected override string GetNameCore() => $"{owner.ProjectName} {owner.Text}".Trim();
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    }
}
