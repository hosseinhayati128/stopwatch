using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

public sealed class TimerRailMark : Control
{
    public static readonly StyledProperty<string> SymbolProperty = AvaloniaProperty.Register<TimerRailMark, string>(nameof(Symbol), "Ready");
    public static readonly StyledProperty<bool> PixelProperty = AvaloniaProperty.Register<TimerRailMark, bool>(nameof(Pixel));
    public static readonly StyledProperty<IBrush> InkProperty = AvaloniaProperty.Register<TimerRailMark, IBrush>(nameof(Ink), Brushes.White);
    public string Symbol { get => GetValue(SymbolProperty); set => SetValue(SymbolProperty, value); }
    public bool Pixel { get => GetValue(PixelProperty); set => SetValue(PixelProperty, value); }
    public IBrush Ink { get => GetValue(InkProperty); set => SetValue(InkProperty, value); }
    private static readonly Geometry Play = Geometry.Parse("M3,1.5 L14,8 L3,14.5 Z");
    private static readonly Geometry PixelPlay = Geometry.Parse("M3,1H6V3H9V5H12V7H15V9H12V11H9V13H6V15H3Z");
    private static readonly Geometry ClockOutline = Geometry.Parse("M5,1H11V3H14V5H15V11H13V14H11V15H5V13H2V11H1V5H3V3H5Z");
    private static readonly Geometry Hands = Geometry.Parse("M8,4V8H11");
    private static readonly Geometry Checkmark = Geometry.Parse("M2,8 L6,12 L14,4");

    static TimerRailMark() => AffectsRender<TimerRailMark>(SymbolProperty, PixelProperty, InkProperty);
    public override void Render(DrawingContext dc)
    {
        using var transform = dc.PushTransform(Matrix.CreateScale(Bounds.Width / 16, Bounds.Height / 16));
        var pen = new Pen(Ink, 1.5);
        switch (Symbol)
        {
            case "More":
                for (int x = 2; x <= 14; x += 6)
                    if (Pixel) dc.DrawRectangle(Ink, null, new Rect(x - 1.45, 6.55, 2.9, 2.9));
                    else dc.DrawEllipse(Ink, null, new Point(x, 8), 1.5, 1.5);
                break;
            case "Running": dc.DrawGeometry(Ink, null, Pixel ? PixelPlay : Play); break;
            case "Paused":
                double radius = Pixel ? 0 : .8;
                dc.DrawRectangle(Ink, null, new Rect(3, 2.5, 3.4, 11), radius, radius);
                dc.DrawRectangle(Ink, null, new Rect(9.6, 2.5, 3.4, 11), radius, radius);
                break;
            case "Clock":
                if (Pixel) dc.DrawGeometry(null, pen, ClockOutline);
                else dc.DrawEllipse(null, pen, new Point(8, 8), 6.5, 6.5);
                dc.DrawGeometry(null, pen, Hands);
                break;
            case "Finished": dc.DrawGeometry(null, pen, Checkmark); break;
            default:
                if (Pixel) dc.DrawRectangle(null, pen, new Rect(3.5, 3.5, 9, 9));
                else dc.DrawEllipse(null, pen, new Point(8, 8), 4.8, 4.8);
                break;
        }
    }
}
