using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace StopwatchOverlay.Desktop.Controls;

/// <summary>A native stepped outline, independent of the artwork's resolution.</summary>
public sealed class TodoFrame : Decorator
{
    public static readonly StyledProperty<IBrush?> BackgroundProperty = Border.BackgroundProperty.AddOwner<TodoFrame>();
    public static readonly StyledProperty<IBrush?> BorderBrushProperty = Border.BorderBrushProperty.AddOwner<TodoFrame>();
    public static readonly StyledProperty<Thickness> BorderThicknessProperty = Border.BorderThicknessProperty.AddOwner<TodoFrame>();
    public static readonly StyledProperty<CornerRadius> CornerRadiusProperty = Border.CornerRadiusProperty.AddOwner<TodoFrame>();
    public static readonly StyledProperty<IBrush?> InnerBorderBrushProperty = AvaloniaProperty.Register<TodoFrame,IBrush?>(nameof(InnerBorderBrush));
    public IBrush? InnerBorderBrush { get => GetValue(InnerBorderBrushProperty); set => SetValue(InnerBorderBrushProperty,value); }
    public IBrush? Background { get => GetValue(BackgroundProperty); set => SetValue(BackgroundProperty,value); }
    public IBrush? BorderBrush { get => GetValue(BorderBrushProperty); set => SetValue(BorderBrushProperty,value); }
    public Thickness BorderThickness { get => GetValue(BorderThicknessProperty); set => SetValue(BorderThicknessProperty,value); }
    public CornerRadius CornerRadius { get => GetValue(CornerRadiusProperty); set => SetValue(CornerRadiusProperty,value); }
    public static readonly StyledProperty<bool> PixelProperty = AvaloniaProperty.Register<TodoFrame, bool>(nameof(Pixel));
    public bool Pixel { get => GetValue(PixelProperty); set => SetValue(PixelProperty, value); }
    static TodoFrame()
    {
        AffectsRender<TodoFrame>(PixelProperty,BackgroundProperty,BorderBrushProperty,BorderThicknessProperty,CornerRadiusProperty,InnerBorderBrushProperty);
        AffectsMeasure<TodoFrame>(BorderThicknessProperty);
    }
    private Thickness Insets => new(Padding.Left+BorderThickness.Left,Padding.Top+BorderThickness.Top,
        Padding.Right+BorderThickness.Right,Padding.Bottom+BorderThickness.Bottom);
    protected override Size MeasureOverride(Size availableSize)
    {
        var inset=Insets;
        Child?.Measure(availableSize.Deflate(inset));
        return (Child?.DesiredSize??default).Inflate(inset);
    }
    protected override Size ArrangeOverride(Size finalSize)
    {
        Child?.Arrange(new Rect(finalSize).Deflate(Insets));
        if(ClipToBounds)Clip=Pixel?Outline(new Rect(finalSize)):new RectangleGeometry(new Rect(finalSize),CornerRadius.TopLeft,CornerRadius.TopLeft);
        return finalSize;
    }
    public override void Render(DrawingContext context)
    {
        if (!Pixel)
        {
            var r = new Rect(Bounds.Size).Deflate(Math.Max(.5,BorderThickness.Left/2));
            if (r.Width <= 0 || r.Height <= 0) return;
            bool uniform = BorderThickness.Left == BorderThickness.Top && BorderThickness.Top == BorderThickness.Right && BorderThickness.Right == BorderThickness.Bottom;
            var pen = BorderBrush == null || !uniform || BorderThickness.Left == 0 ? null : new Pen(BorderBrush,BorderThickness.Left);
            context.DrawRectangle(Background,pen,r,CornerRadius.TopLeft,CornerRadius.TopLeft);
            if(InnerBorderBrush!=null&&r.Width>6&&r.Height>6)
                context.DrawRectangle(null,new Pen(InnerBorderBrush,.75),r.Deflate(2),Math.Max(0,CornerRadius.TopLeft-2),Math.Max(0,CornerRadius.TopLeft-2));
            if (!uniform && BorderBrush != null)
            {
                if(BorderThickness.Left>0) context.DrawLine(new Pen(BorderBrush,BorderThickness.Left),r.TopLeft,r.BottomLeft);
                if(BorderThickness.Top>0) context.DrawLine(new Pen(BorderBrush,BorderThickness.Top),r.TopLeft,r.TopRight);
                if(BorderThickness.Right>0) context.DrawLine(new Pen(BorderBrush,BorderThickness.Right),r.TopRight,r.BottomRight);
                if(BorderThickness.Bottom>0) context.DrawLine(new Pen(BorderBrush,BorderThickness.Bottom),r.BottomLeft,r.BottomRight);
            }
            return;
        }
        var rect = new Rect(Bounds.Size).Deflate(Math.Max(.5, BorderThickness.Left / 2));
        if (rect.Width <= 0 || rect.Height <= 0) return;
        context.DrawGeometry(Background, BorderBrush == null ? null : new Pen(BorderBrush, Math.Max(1, BorderThickness.Left)), Outline(rect));
        if(InnerBorderBrush!=null&&rect.Width>6&&rect.Height>6)
            context.DrawGeometry(null,new Pen(InnerBorderBrush,.75),Outline(rect.Deflate(2)));
    }

    internal static StreamGeometry Outline(Rect r)
    {
        double s = Math.Min(4, Math.Min(r.Width, r.Height) / 12);
        var geometry = new StreamGeometry();
        using var c = geometry.Open();
        c.BeginFigure(new Point(r.Left + 2*s, r.Top), true);
        foreach (var p in new[] { new Point(r.Right-2*s,r.Top), new(r.Right-2*s,r.Top+s), new(r.Right-s,r.Top+s),
            new(r.Right-s,r.Top+2*s), new(r.Right,r.Top+2*s), new(r.Right,r.Bottom-2*s), new(r.Right-s,r.Bottom-2*s),
            new(r.Right-s,r.Bottom-s), new(r.Right-2*s,r.Bottom-s), new(r.Right-2*s,r.Bottom), new(r.Left+2*s,r.Bottom),
            new(r.Left+2*s,r.Bottom-s), new(r.Left+s,r.Bottom-s), new(r.Left+s,r.Bottom-2*s), new(r.Left,r.Bottom-2*s),
            new(r.Left,r.Top+2*s), new(r.Left+s,r.Top+2*s), new(r.Left+s,r.Top+s), new(r.Left+2*s,r.Top+s) }) c.LineTo(p);
        c.EndFigure(true);
        return geometry;
    }
}
